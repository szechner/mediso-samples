using System.Security.Cryptography;
using System.Text;

namespace Mediso.AuditSample.Infrastructure.Crypto;

public static class Merkle
{
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

    private static byte[] Sha256(byte[] data) => SHA256.HashData(data);

    private static byte[] Concat(byte[] a, byte[] b)
    {
        var r = new byte[a.Length + b.Length];
        Buffer.BlockCopy(a, 0, r, 0, a.Length);
        Buffer.BlockCopy(b, 0, r, a.Length, b.Length);
        return r;
    }
}