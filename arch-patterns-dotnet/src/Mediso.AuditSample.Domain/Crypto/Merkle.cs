using System.Security.Cryptography;
using System.Text;

namespace Mediso.AuditSample.Domain.Crypto;

public static class Merkle
{
    public sealed record MerkleProofStep(string SiblingHex, string Side); // Side: "L"|"R"
    public sealed record MerkleProof(string LeafHex, int LeafIndex, IReadOnlyList<MerkleProofStep> Steps);

    public static string ComputeRootHex(IReadOnlyList<byte[]> leaves)
    {
        if (leaves.Count == 0)
            throw new ArgumentException("Leaves must not be empty.", nameof(leaves));

        var level = leaves.Select(x => x).ToList();

        while (level.Count > 1)
        {
            var next = new List<byte[]>((level.Count + 1) / 2);

            for (int i = 0; i < level.Count; i += 2)
            {
                var left = level[i];
                var right = (i + 1 < level.Count) ? level[i + 1] : left; // duplicate last
                next.Add(Sha256(Concat(left, right)));
            }

            level = next;
        }

        return Convert.ToHexString(level[0]).ToLowerInvariant();
    }

    public static byte[] LeafFromParts(Guid eventId, Guid correlationId, DateTime occurredAtUtc, string payloadSha256)
    {
        // occurredAtUtc v ISO "o" formátu (UTC)
        var s = $"{eventId:D}|{correlationId:D}|{DateTime.SpecifyKind(occurredAtUtc, DateTimeKind.Utc):O}|{payloadSha256}";
        return Sha256(Encoding.UTF8.GetBytes(s));
    }

    public static MerkleProof BuildProofHex(IReadOnlyList<string> leafHexes, int leafIndex)
    {
        if (leafHexes.Count == 0) throw new ArgumentException("Leaves must not be empty.", nameof(leafHexes));
        if (leafIndex < 0 || leafIndex >= leafHexes.Count) throw new ArgumentOutOfRangeException(nameof(leafIndex));

        var level = leafHexes.Select(h => Convert.FromHexString(h)).ToList();
        var idx = leafIndex;

        var steps = new List<MerkleProofStep>();

        while (level.Count > 1)
        {
            int siblingIndex;
            string side;

            if (idx % 2 == 0)
            {
                siblingIndex = (idx + 1 < level.Count) ? idx + 1 : idx; // duplicate last
                side = "R";
            }
            else
            {
                siblingIndex = idx - 1;
                side = "L";
            }

            steps.Add(new MerkleProofStep(
                Convert.ToHexString(level[siblingIndex]).ToLowerInvariant(),
                side
            ));

            var next = new List<byte[]>((level.Count + 1) / 2);
            for (int i = 0; i < level.Count; i += 2)
            {
                var left = level[i];
                var right = (i + 1 < level.Count) ? level[i + 1] : left;
                next.Add(Sha256(Concat(left, right)));
            }

            level = next;
            idx = idx / 2;
        }

        return new MerkleProof(leafHexes[leafIndex], leafIndex, steps);
    }

    public static string ComputeRootFromProofHex(MerkleProof proof)
    {
        var cur = Convert.FromHexString(proof.LeafHex);

        foreach (var s in proof.Steps)
        {
            var sib = Convert.FromHexString(s.SiblingHex);

            cur = s.Side == "L"
                ? Sha256(Concat(sib, cur))
                : Sha256(Concat(cur, sib));
        }

        return Convert.ToHexString(cur).ToLowerInvariant();
    }

    private static byte[] Sha256(byte[] data) => SHA256.HashData(data);

    private static byte[] Concat(byte[] a, byte[] b)
    {
        var r = new byte[a.Length + b.Length];
        Buffer.BlockCopy(a, 0, r, 0, a.Length);
        Buffer.BlockCopy(b, 0, r, a.Length, b.Length);
        return r;
    }
}