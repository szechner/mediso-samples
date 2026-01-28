using Mediso.AuditSample.Application;
using Mediso.AuditSample.Application.Handlers;
using Mediso.AuditSample.Domain.Services;
using Mediso.AuditSample.Infrastructure;
using Mediso.AuditSample.Infrastructure.DIExtensions;
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

// Case queries + service
builder.Services.AddSingleton<IPgAuditCaseQueries, PgAuditCaseQueries>();
builder.Services.AddSingleton<AuditCaseService>();

// Wolverine + Kafka (v docker síti!)
builder.Host.UseWolverine(opts =>
{
    var bootstrap = builder.Configuration["Kafka:BootstrapServers"] ?? "kafka:29092";
    opts.UseKafka(bootstrap);

    // listen
    opts.ListenToKafkaTopic("payments.audit.v1");

    opts.ApplicationAssembly = typeof(AuditIngestHandler).Assembly;
});

builder.Services.AddHostedService<Mediso.AuditSample.Api.Batching.AuditBatchingWorker>();
builder.Services.AddHostedService<Mediso.AuditSample.Api.Anchoring.AuditAnchoringWorker>();
builder.Services.AddHostedService<Mediso.AuditSample.Api.Anchoring.AuditAnchorVerificationWorker>();

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

// ========================
// Audit Case Endpoints
// ========================

// 1) Evidence + coverage snapshot
app.MapGet("/audit/cases/{correlationId:guid}", async (
    Guid correlationId,
    DateTimeOffset? fromUtc,
    DateTimeOffset? toUtc,
    int? take,
    bool? includePayload,
    AuditCaseService svc,
    CancellationToken ct
) =>
{
    var snap = await svc.GetSnapshotAsync(
        correlationId,
        fromUtc,
        toUtc,
        take,
        includePayload ?? true,
        ct
    );

    return Results.Ok(snap);
});

// 2) Verify case (Merkle proof + coverage). RPC verify můžeme doplnit později.
app.MapGet("/audit/cases/{correlationId:guid}/verify", async (
    Guid correlationId,
    DateTimeOffset? fromUtc,
    DateTimeOffset? toUtc,
    int? take,
    AuditCaseService svc,
    CancellationToken ct
) =>
{
    var (result, _) = await svc.VerifyAsync(correlationId, fromUtc, toUtc, take, ct);
    return Results.Ok(result);
});

// 3) Export ZIP (case.json + proofs + report)
app.MapGet("/audit/cases/{correlationId:guid}/export", async (
    Guid correlationId,
    DateTimeOffset? fromUtc,
    DateTimeOffset? toUtc,
    int? take,
    bool? includePayload,
    AuditCaseService svc,
    CancellationToken ct
) =>
{
    var bytes = await svc.ExportZipAsync(
        correlationId,
        fromUtc,
        toUtc,
        take,
        includePayload ?? true,
        ct
    );

    var fileName = $"audit-case-{correlationId:D}.zip";
    return Results.File(bytes, "application/zip", fileName);
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