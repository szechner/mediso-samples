using Mediso.AuditSample.Domain.Services;
using Mediso.AuditSample.Infrastructure.Anchoring;
using Mediso.AuditSample.Infrastructure.Solana;
using Mediso.AuditSample.Infrastructure.Storage;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Mediso.AuditSample.Api.Anchoring;

public sealed class AuditAnchorVerificationWorker : BackgroundService
{
    private readonly IAuditAnchorStore _anchors;
    private readonly IAuditBatchStore _batches;
    private readonly SolanaTxVerifier _solanaVerifier;
    private readonly ILogger<AuditAnchorVerificationWorker> _log;

    public AuditAnchorVerificationWorker(
        IAuditAnchorStore anchors,
        IAuditBatchStore batches,
        SolanaTxVerifier solanaVerifier,
        ILogger<AuditAnchorVerificationWorker> log)
    {
        _anchors = anchors;
        _batches = batches;
        _solanaVerifier = solanaVerifier;
        _log = log;
    }

   protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    _log.LogInformation("AuditAnchorVerificationWorker started.");

    var baseDelay = TimeSpan.FromSeconds(10);
    var rateLimitDelayMin = TimeSpan.FromSeconds(60);
    var rateLimitDelayMax = TimeSpan.FromSeconds(120);

    while (!stoppingToken.IsCancellationRequested)
    {
        try
        {
            var pending = await _anchors.GetPendingVerificationsAsync(take: 2, stoppingToken);

            if (pending.Count == 0)
            {
                await Task.Delay(baseDelay, stoppingToken);
                continue;
            }

            foreach (var p in pending)
            {
                var expectedMemo = AuditMemoFormatter.FormatV1(p.BatchId, p.MerkleRootSha256);

                if (p.Chain != "solana")
                {
                    await _anchors.MarkVerifyFailedAsync(p.BatchId, $"Unsupported chain '{p.Chain}'.", stoppingToken);
                    continue;
                }

                var result = await _solanaVerifier.VerifyMemoAsync(p.TxSignature, expectedMemo, stoppingToken);

                if (result.Verdict == "VERIFIED")
                {
                    await _anchors.MarkVerifiedAsync(
                        p.BatchId,
                        commitment: result.Commitment ?? "finalized",
                        slot: result.Slot ?? 0,
                        blockTimeUtc: result.BlockTimeUtc,
                        anchorerPubkey: result.AnchorrerPubkey ?? "",
                        stoppingToken);

                    _log.LogInformation("Anchor verified. BatchId={BatchId} Sig={Sig} Slot={Slot}",
                        p.BatchId, p.TxSignature, result.Slot);
                }
                else if (result.Verdict == "NOT_VERIFIED")
                {
                    await _anchors.MarkVerifyFailedAsync(p.BatchId, $"NOT_VERIFIED: {result.Problem}", stoppingToken);
                    _log.LogWarning("Anchor NOT_VERIFIED. BatchId={BatchId} Sig={Sig} Problem={Problem}",
                        p.BatchId, p.TxSignature, result.Problem);
                }
                else if (result.Verdict == "RATE_LIMITED")
                {
                    _log.LogWarning("Solana RPC rate limited. Sleeping. Problem={Problem}", result.Problem);

                    var jitter = Random.Shared.Next(
                        (int)rateLimitDelayMin.TotalMilliseconds,
                        (int)rateLimitDelayMax.TotalMilliseconds);

                    await Task.Delay(TimeSpan.FromMilliseconds(jitter), stoppingToken);
                    // break: nemá smysl zkoušet další v tomhle ticku
                    break;
                }
                else
                {
                    _log.LogInformation("Anchor inconclusive (will retry). BatchId={BatchId} Sig={Sig} Problem={Problem}",
                        p.BatchId, p.TxSignature, result.Problem);
                }

                // malá pauza i mezi položkama, aby ses nedostal do burstu
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }

            await Task.Delay(baseDelay, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        catch (Exception ex)
        {
            _log.LogError(ex, "AuditAnchorVerificationWorker tick failed.");
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
    }

    _log.LogInformation("AuditAnchorVerificationWorker stopped.");
}

}
