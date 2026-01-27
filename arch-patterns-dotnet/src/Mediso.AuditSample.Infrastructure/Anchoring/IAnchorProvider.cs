namespace Mediso.AuditSample.Infrastructure.Anchoring;

public interface IAnchorProvider
{
    Task<string> AnchorMemoAsync(string memoText, CancellationToken ct);
}