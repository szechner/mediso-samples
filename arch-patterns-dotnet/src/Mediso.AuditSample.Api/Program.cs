using Mediso.AuditSample.Infrastructure;
using Mediso.AuditSample.Infrastructure.Storage;
using Mediso.PaymentSample.SharedKernel.Audit;
using Mediso.PaymentSample.SharedKernel.Logging;
using Mediso.PaymentSample.SharedKernel.Tracing;
using Serilog;
using Serilog.Enrichers.Span;
using Wolverine;
using Wolverine.Kafka;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog for structured logging
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithEnvironmentName()
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
    .Enrich.WithSpan()
    .CreateLogger();

builder.Host.UseSerilog();

var connectionString = builder.Configuration.GetConnectionString("AuditDb");
if (!string.IsNullOrEmpty(connectionString))
{
    builder.Services.AddHealthChecks().AddNpgSql(connectionString);
}

builder.Services.AddAuditInfrastructure(builder.Configuration);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<ILoggingContext, LoggingContext>();

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

// Add request correlation ID middleware
app.Use(async (context, next) =>
{
    var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault() ?? Guid.NewGuid().ToString();
    context.Response.Headers["X-Correlation-ID"] = correlationId;

    using var activity = AuditTracingConstants.ApiActivitySource.StartActivity(AuditTracingConstants.Activities.HttpRequest);
    activity?.SetTag(AuditTracingConstants.Tags.CorrelationId, correlationId);
    activity?.SetTag("http.method", context.Request.Method);
    activity?.SetTag("http.path", context.Request.Path);

    using (Serilog.Context.LogContext.PushProperty("CorrelationId", correlationId))
    {
        await next(context);
    }
});

app.UseSwagger();
app.UseSwaggerUI();


app.MapHealthChecks("/health");

// Global exception handler
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        var exceptionFeature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();

        if (exceptionFeature?.Error != null)
        {
            using var activity = AuditTracingConstants.ApiActivitySource.StartActivity("error-handling");
            activity?.SetTag("error.type", exceptionFeature.Error.GetType().Name);
            activity?.SetTag("error.message", exceptionFeature.Error.Message);

            logger.LogError(exceptionFeature.Error, "Unhandled exception occurred: {ErrorMessage}", exceptionFeature.Error.Message);
        }

        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(
            System.Text.Json.JsonSerializer.Serialize(new
            {
                error = "An internal server error occurred."
            }));
    });
});


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

try
{
    Log.Information("Starting {ServiceName} v{ServiceVersion}", AuditTracingConstants.ServiceName, AuditTracingConstants.ServiceVersion);
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.Information("Shutting down {ServiceName}", AuditTracingConstants.ServiceName);
    Log.CloseAndFlush();
}

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