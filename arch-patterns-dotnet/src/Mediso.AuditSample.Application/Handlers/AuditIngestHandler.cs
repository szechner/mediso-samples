using Mediso.AuditSample.Domain.Services;
using Mediso.PaymentSample.SharedKernel.Audit;
using Microsoft.Extensions.Logging;
using Wolverine.Attributes;

namespace Mediso.AuditSample.Application.Handlers;

/// <summary>
/// Wolverine handler for Kafka messages.
/// </summary>
[WolverineHandler]
public sealed class AuditIngestHandler
{
    private readonly IAuditRecordStore _store;
    private readonly ILogger<AuditIngestHandler> _log;

    public AuditIngestHandler(IAuditRecordStore store, ILogger<AuditIngestHandler> log)
    {
        _store = store;
        _log = log;
    }

    public async Task Handle(AuditEventV1 msg, CancellationToken ct)
    {
        var result = await _store.TryInsertAsync(msg, ct);

        if (!result.Inserted)
            _log.LogInformation("Duplicate audit event ignored. EventId={EventId}", msg.EventId);
    }
}