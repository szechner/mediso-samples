using Serilog;
using Serilog.Enrichers.Span;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using Mediso.PaymentSample.SharedKernel.Tracing;
using Mediso.PaymentSample.SharedKernel.Logging;
using Mediso.PaymentSample.Api.Endpoints;
using Mediso.PaymentSample.Infrastructure.Configuration;
using Npgsql;
using OpenTelemetry;

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

// Configure OpenTelemetry with comprehensive instrumentation
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(TracingConstants.ServiceName, TracingConstants.ServiceVersion)
        .AddAttributes(new Dictionary<string, object>
        {
            ["service.environment"] = builder.Environment.EnvironmentName,
            ["service.instance.id"] = Environment.MachineName,
            ["service.version"] = TracingConstants.ServiceVersion
        }))
    .WithTracing(tracing => tracing
        .AddSource(TracingConstants.DomainActivitySource.Name)
        .AddSource(TracingConstants.ApplicationActivitySource.Name)
        .AddSource(TracingConstants.ApiActivitySource.Name)
        .AddSource(TracingConstants.InfrastructureActivitySource.Name)
        // Marten OpenTelemetry tracing sources
        .AddSource("Marten")
        .AddSource("Marten.EventStore") 
        .AddSource("Marten.DocumentSession")
        // Npgsql database tracing
        .AddSource("Npgsql")
        .AddSource("OpenTelemetry.Instrumentation.SqlClient")
        .AddAspNetCoreInstrumentation(options =>
        {
            options.RecordException = true;
            options.EnrichWithHttpRequest = (activity, request) =>
            {
                activity.SetTag("http.request.method", request.Method);
                activity.SetTag("http.request.path", request.Path);
                activity.SetTag("http.request.scheme", request.Scheme);
                activity.SetTag("http.request.host", request.Host.Value);
            };
            options.EnrichWithHttpResponse = (activity, response) =>
            {
                activity.SetTag("http.response.status_code", response.StatusCode);
            };
        })
        .AddHttpClientInstrumentation()
        .AddSqlClientInstrumentation()
        .AddNpgsql() // This will instrument Npgsql connections used by Marten
        .AddJaegerExporter(options =>
        {
            options.Endpoint = new Uri(builder.Configuration["Jaeger:Endpoint"] ?? "http://localhost:4317");
        }))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        // Add custom meters for Payment domain
        .AddMeter("Mediso.PaymentSample.Domain")
        .AddMeter("Mediso.PaymentSample.Infrastructure")
        .AddMeter("Mediso.PaymentSample.Application")
        .AddView("http.server.duration",
            new ExplicitBucketHistogramConfiguration
            {
                Boundaries = new double[] { 0, 0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 7.5, 10 }
            })
        .AddOtlpExporter());

// Add services to the container.
builder.Services.AddSingleton<ILoggingContext, LoggingContext>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add infrastructure services (including Marten and event store)
var connectionString = builder.Configuration.GetConnectionString("Default");
if (!string.IsNullOrEmpty(connectionString))
{
    builder.Services.AddMartenEventStore(connectionString);
}

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
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Map payment endpoints
app.MapPaymentEndpoints();

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
            System.Text.Json.JsonSerializer.Serialize(new { error = "An internal server error occurred." }));
    });
});

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

