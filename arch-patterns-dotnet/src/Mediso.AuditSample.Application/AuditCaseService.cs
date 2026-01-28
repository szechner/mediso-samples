using System.IO.Compression;
using System.Text.Json;
using Mediso.AuditSample.Application.Models;
using Mediso.AuditSample.Domain.Crypto;
using Mediso.AuditSample.Domain.Services;

namespace Mediso.AuditSample.Application;

public sealed class AuditCaseService
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly IPgAuditCaseQueries _q;

    public AuditCaseService(IPgAuditCaseQueries q) => _q = q;

    public async Task<AuditCaseSnapshot> GetSnapshotAsync(
        Guid correlationId,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        int? take,
        bool includePayload,
        CancellationToken ct)
    {
        var rows = await _q.GetCaseJoinedAsync(correlationId, fromUtc, toUtc, take, ct);

        // group by record
        var byRecord = rows.GroupBy(r => r.RecordId).ToList();
        var records = new List<AuditCaseRecordCoverage>(byRecord.Count);

        foreach (var g in byRecord)
        {
            var first = g.First();

            var payload = includePayload ? first.PayloadJson : "{}";

            var rec = new AuditCaseRecord(
                first.RecordId,
                first.EventId,
                first.CorrelationId,
                first.Source,
                first.EventType,
                first.OccurredAtUtc,
                payload,
                first.PayloadSha256
            );

            var links = g
                .Where(x => x.BatchId.HasValue && x.LeafIndex.HasValue && x.LeafSha256 != null && x.MerkleRootSha256 != null)
                .Select(x => new AuditCaseBatchLink(
                    x.BatchId!.Value,
                    x.LeafIndex!.Value,
                    x.LeafSha256!,
                    x.MerkleRootSha256!,
                    x.TxSignature,
                    x.Chain,
                    x.Network,
                    x.VerifiedAtUtc,
                    x.Commitment,
                    x.Slot,
                    x.BlockTimeUtc,
                    x.AnchorerPubkey
                ))
                .ToList();

            records.Add(new AuditCaseRecordCoverage(rec, links));
        }

        return new AuditCaseSnapshot(correlationId, fromUtc, toUtc, records);
    }

    public async Task<(AuditCaseVerifyResult Result, Dictionary<long, Merkle.MerkleProof> Proofs)> VerifyAsync(
        Guid correlationId,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        int? take,
        CancellationToken ct)
    {
        // include payload so we can export full evidence, but the cryptographic check uses payloadSha256 (not payloadJson)
        var snap = await GetSnapshotAsync(correlationId, fromUtc, toUtc, take, includePayload: true, ct);

        var problems = new List<string>();
        var proofs = new Dictionary<long, Merkle.MerkleProof>();

        // prepare per batch leaves
        var batchIds = snap.Records
            .SelectMany(r => r.Links.Select(l => l.BatchId))
            .Distinct()
            .ToList();

        var leavesByBatch = new Dictionary<Guid, IReadOnlyList<string>>(batchIds.Count);

        foreach (var bid in batchIds)
        {
            var leaves = await _q.GetBatchLeavesAsync(bid, ct);
            leavesByBatch[bid] = leaves.Select(x => x.LeafHex).ToList();
        }

        int total = snap.Records.Count;
        int anchored = 0;

        foreach (var rc in snap.Records)
        {
            if (rc.Links.Count == 0)
            {
                problems.Add($"Record {rc.Record.RecordId}: not included in any batch (inconclusive).");
                continue;
            }

            // anchored (strong) = any link has verified anchor metadata (worker already proved finalized+memo)
            var hasVerifiedAnchor = rc.Links.Any(l => l.VerifiedAtUtc.HasValue);
            if (hasVerifiedAnchor) anchored++;

            // if there is txSignature but not verified yet -> pending
            var hasPendingAnchor = rc.Links.Any(l => !string.IsNullOrWhiteSpace(l.TxSignature) && !l.VerifiedAtUtc.HasValue);
            if (hasPendingAnchor)
                problems.Add($"Record {rc.Record.RecordId}: anchor tx present but not verified yet (inconclusive).");

            foreach (var link in rc.Links)
            {
                if (!leavesByBatch.TryGetValue(link.BatchId, out var leafHexes))
                {
                    problems.Add($"Record {rc.Record.RecordId}: missing leaves for batch {link.BatchId}.");
                    continue;
                }

                if (link.LeafIndex < 0 || link.LeafIndex >= leafHexes.Count)
                {
                    problems.Add($"Record {rc.Record.RecordId}: invalid leaf_index {link.LeafIndex} for batch {link.BatchId}.");
                    continue;
                }

                // 1) Recompute leaf from record parts (tamper detection inside DB)
                // expected leaf is SHA256($"{eventId}|{correlationId}|{occurredAtUtc:O}|{payloadSha256}")
                var expectedLeafBytes = Merkle.LeafFromParts(
                    rc.Record.EventId,
                    rc.Record.CorrelationId,
                    rc.Record.OccurredAtUtc.UtcDateTime,
                    rc.Record.PayloadSha256);

                var expectedLeafHex = Convert.ToHexString(expectedLeafBytes).ToLowerInvariant();

                if (!string.Equals(expectedLeafHex, link.LeafSha256, StringComparison.OrdinalIgnoreCase))
                {
                    problems.Add($"Record {rc.Record.RecordId}: recomputed leaf mismatch for batch {link.BatchId} (tamper or inconsistent source).");
                    continue;
                }

                // 2) Sanity: DB leaf list ordering must match leaf at index
                var expectedLeafFromBatch = leafHexes[link.LeafIndex];
                if (!string.Equals(expectedLeafFromBatch, link.LeafSha256, StringComparison.OrdinalIgnoreCase))
                {
                    problems.Add($"Record {rc.Record.RecordId}: leaf mismatch for batch {link.BatchId} (index {link.LeafIndex}).");
                    continue;
                }

                // 3) Proof -> root must match batch root
                var proof = Merkle.BuildProofHex(leafHexes, link.LeafIndex);
                var rootFromProof = Merkle.ComputeRootFromProofHex(proof);

                if (!string.Equals(rootFromProof, link.MerkleRootSha256, StringComparison.OrdinalIgnoreCase))
                {
                    problems.Add($"Record {rc.Record.RecordId}: proof->root mismatch for batch {link.BatchId}.");
                    continue;
                }

                // Keep one proof per record for export (prefer verified anchor link if exists, else prefer any tx)
                var prefer =
                    link.VerifiedAtUtc.HasValue ||
                    !string.IsNullOrWhiteSpace(link.TxSignature);

                if (!proofs.ContainsKey(rc.Record.RecordId) || prefer)
                    proofs[rc.Record.RecordId] = proof;

                // NOTE: We do NOT RPC-verify memo here (rate limits). The worker already filled VerifiedAtUtc+slot+blockTime.
            }
        }

        // verdict rules:
        // - any mismatch => NOT_VERIFIED
        // - else if all records have at least one VERIFIED anchor => VERIFIED
        // - else => INCONCLUSIVE (missing links or pending anchor verify)
        var hasMismatch = problems.Any(p =>
            p.Contains("mismatch", StringComparison.OrdinalIgnoreCase) ||
            p.Contains("tamper", StringComparison.OrdinalIgnoreCase));

        var verdict =
            hasMismatch ? "NOT_VERIFIED"
            : (anchored == total ? "VERIFIED" : "INCONCLUSIVE");

        var result = new AuditCaseVerifyResult(
            correlationId,
            verdict,
            DateTimeOffset.UtcNow,
            problems,
            RecordsTotal: total,
            RecordsAnchored: anchored
        );

        return (result, proofs);
    }

    public async Task<byte[]> ExportZipAsync(
        Guid correlationId,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        int? take,
        bool includePayload,
        CancellationToken ct)
    {
        var snap = await GetSnapshotAsync(correlationId, fromUtc, toUtc, take, includePayload, ct);
        var (verify, proofs) = await VerifyAsync(correlationId, fromUtc, toUtc, take, ct);

        var manifest = new AuditCaseExportManifest(
            correlationId,
            DateTimeOffset.UtcNow,
            fromUtc,
            toUtc,
            verify.RecordsTotal,
            verify.RecordsAnchored,
            verify.Verdict
        );

        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteJson(zip, "case.json", snap);
            WriteJson(zip, "verify-report.json", verify);
            WriteJson(zip, "manifest.json", manifest);

            foreach (var kvp in proofs)
                WriteJson(zip, $"proofs/{kvp.Key}.json", kvp.Value);

            var coverage = snap.Records.Select(r => new
            {
                r.Record.RecordId,
                r.Record.EventId,
                Links = r.Links.Select(l => new
                {
                    l.BatchId,
                    l.LeafIndex,
                    l.LeafSha256,
                    l.MerkleRootSha256,
                    l.TxSignature,
                    l.Chain,
                    l.Network,
                    l.VerifiedAtUtc,
                    l.Commitment,
                    l.Slot,
                    l.BlockTimeUtc,
                    l.AnchorerPubkey
                })
            });

            WriteJson(zip, "coverage.json", coverage);
        }

        return ms.ToArray();
    }

    private static void WriteJson<T>(ZipArchive zip, string path, T value)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Fastest);
        using var s = entry.Open();
        using var w = new StreamWriter(s);
        w.Write(JsonSerializer.Serialize(value, JsonOpts));
    }
}
