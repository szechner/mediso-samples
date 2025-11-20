using Serilog;
using Serilog.Enrichers.Span;
using Mediso.PaymentSample.SharedKernel.Tracing;
using Mediso.PaymentSample.SharedKernel.Logging;
using Mediso.PaymentSample.Api.Endpoints;
using Mediso.PaymentSample.Infrastructure.Configuration;
using Mediso.PaymentSample.Application.Configuration;
using Mediso.PaymentSample.Infrastructure.DIExtensions;
using Mediso.PaymentSample.Infrastructure.Modules;
using Mediso.PaymentSample.SharedKernel.Modules;

var builder = WebApplication.CreateBuilder(args);

// Configure Wolverine with Marten integration
builder.Host.UseWolverineWithMarten();

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

var connectionString = builder.Configuration.GetConnectionString("Default");
if (!string.IsNullOrEmpty(connectionString))
{
    builder.Services.AddMartenEventStore(connectionString);
    builder.Services.AddHealthChecks().AddNpgSql(connectionString);
}

var environmentName = builder.Environment.EnvironmentName;
var jaegerEndpoint = builder.Configuration["Jaeger:Endpoint"];

// Configure OpenTelemetry with comprehensive instrumentation
builder.Services.ConfigureOpenTelemetry(environmentName, jaegerEndpoint);

builder.Services.AddModularArchitecture(builder.Configuration, bootstrapper =>
{
    bootstrapper.RegisterModule<AccountsModuleRegistration>();
    bootstrapper.RegisterModule<ComplianceModuleRegistration>();
    bootstrapper.RegisterModule<LedgerModuleRegistration>();
    bootstrapper.RegisterModule<PaymentsModuleRegistration>();
});

// Add services to the container.
builder.Services.AddApplicationServices();
builder.Services.AddSingleton<ILoggingContext, LoggingContext>();
builder.Services.AddMemoryCache(); // Required by PaymentQueryHandlers
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Add request correlation ID middleware
app.Use(async (context, next) =>
{
    var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault() ?? Guid.NewGuid().ToString();
    context.Response.Headers["X-Correlation-ID"] = correlationId;

    using var activity = TracingConstants.ApiActivitySource.StartActivity(TracingConstants.Activities.HttpRequest);
    activity?.SetTag(TracingConstants.Tags.CorrelationId, correlationId);
    activity?.SetTag("http.method", context.Request.Method);
    activity?.SetTag("http.path", context.Request.Path);

    using (Serilog.Context.LogContext.PushProperty("CorrelationId", correlationId))
    {
        await next(context);
    }
});

// Configure the HTTP request pipeline.
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
            using var activity = TracingConstants.ApiActivitySource.StartActivity("error-handling");
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

// Map payment endpoints
app.MapPaymentEndpoints();

try
{
    Log.Information("Starting {ServiceName} v{ServiceVersion}", TracingConstants.ServiceName, TracingConstants.ServiceVersion);
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.Information("Shutting down {ServiceName}", TracingConstants.ServiceName);
    Log.CloseAndFlush();
}