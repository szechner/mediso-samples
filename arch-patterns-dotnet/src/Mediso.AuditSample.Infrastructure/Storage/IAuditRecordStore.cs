using Mediso.PaymentSample.SharedKernel.Audit;

namespace Mediso.AuditSample.Infrastructure.Storage;

public readonly record struct InsertResult(bool Inserted);

public interface IAuditRecordStore
{
    Task<InsertResult> TryInsertAsync(AuditEventV1 msg, CancellationToken ct);
    Task<IReadOnlyList<AuditEventV1>> GetByCorrelationIdAsync(Guid correlationId, int take, CancellationToken ct);
}