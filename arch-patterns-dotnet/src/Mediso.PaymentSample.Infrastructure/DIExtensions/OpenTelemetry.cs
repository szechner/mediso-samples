using Mediso.PaymentSample.SharedKernel.Tracing;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;

namespace Mediso.PaymentSample.Infrastructure.DIExtensions;

public static class OpenTelemetryExtensions
{
    public static void ConfigureOpenTelemetry(this IServiceCollection services, string environmentName, string? jaegerEndpoint = null)
    {
        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(TracingConstants.ServiceName, TracingConstants.ServiceVersion)
                .AddAttributes(new Dictionary<string, object>
                {
                    ["service.environment"] = environmentName,
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
                // .AddSource("Npgsql")
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
                    options.EnrichWithHttpResponse = (activity, response) => { activity.SetTag("http.response.status_code", response.StatusCode); };
                })
                .AddHttpClientInstrumentation()
                .AddSqlClientInstrumentation()
                // .AddNpgsql() // This will instrument Npgsql connections used by Marten
                .AddJaegerExporter(options => { options.Endpoint = new Uri(jaegerEndpoint ?? "http://localhost:4317"); }))
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
    }
}