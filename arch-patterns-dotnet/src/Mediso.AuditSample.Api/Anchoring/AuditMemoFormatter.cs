using System.Text.RegularExpressions;

namespace Mediso.AuditSample.Api.Anchoring;

public static class AuditMemoFormatter
{
    public static string FormatV1(Guid batchId, string merkleRootSha256)
    {
        if (merkleRootSha256.Length != 64 || !Regex.IsMatch(merkleRootSha256, "^[0-9a-f]+$"))
            throw new InvalidOperationException("Invalid Merkle root format");
        
        return $"mediso.audit.v1|batch={batchId:D}|root={merkleRootSha256}";
    }
}