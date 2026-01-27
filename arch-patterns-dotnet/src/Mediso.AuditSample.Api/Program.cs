using Mediso.AuditSample.Infrastructure;
using Mediso.AuditSample.Infrastructure.Storage;
using Mediso.PaymentSample.SharedKernel.Audit;
using Wolverine;
using Wolverine.Kafka;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuditInfrastructure(builder.Configuration);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Wolverine + Kafka (v docker síti!)
builder.Host.UseWolverine(opts =>
{
    var bootstrap = builder.Configuration["Kafka:BootstrapServers"] ?? "kafka:29092";
    opts.UseKafka(bootstrap);

    // listen
    opts.ListenToKafkaTopic("payments.audit.v1");
});

builder.Services.AddHostedService<Mediso.AuditSample.Api.Batching.AuditBatchingWorker>();
builder.Services.AddHostedService<Mediso.AuditSample.Api.Anchoring.AuditAnchoringWorker>();

var app = builder.Build();

// Ensure schema
using (var scope = app.Services.CreateScope())
{
    var ds = scope.ServiceProvider.GetRequiredService<Npgsql.NpgsqlDataSource>();
    await AuditSchema.EnsureAsync(ds, CancellationToken.None);
}

app.UseSwagger();
app.UseSwaggerUI();


app.MapGet("/health", () => Results.Ok(new { ok = true }));

// dev endpoint: list records for correlationId
app.MapGet("/dev/audit/case/{correlationId:guid}", async (
    Guid correlationId,
    int? take,
    IAuditRecordStore store,
    CancellationToken ct
) =>
{
    var items = await store.GetByCorrelationIdAsync(correlationId, take ?? 50, ct);
    return Results.Ok(items);
});

app.Run();

/// <summary>
/// Wolverine handler for Kafka messages.
/// </summary>
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