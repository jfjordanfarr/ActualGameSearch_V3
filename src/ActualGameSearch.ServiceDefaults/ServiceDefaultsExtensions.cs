using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using OpenTelemetry.Resources;
using OpenTelemetry.Logs;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter;

namespace ActualGameSearch.ServiceDefaults;

public static class ServiceDefaultsExtensions
{
    public static IHostApplicationBuilder AddServiceDefaults(this IHostApplicationBuilder builder)
    {
        builder.Services.AddHealthChecks().AddCheck("self", () => HealthCheckResult.Healthy());

        // Prefer readable timestamps in console for local debugging and pipe logs to OTLP
        builder.Logging.AddSimpleConsole(o =>
        {
            o.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fff ";
            o.SingleLine = true;
        });
        builder.Logging.AddOpenTelemetry(o =>
        {
            o.IncludeScopes = true;
            o.IncludeFormattedMessage = true;
            o.ParseStateValues = true;
            o.AddOtlpExporter();
        });

        // Wire OTLP export to the Aspire Dashboard's OTLP endpoint when available
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(serviceName: builder.Environment.ApplicationName))
            .WithTracing(t => t
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddOtlpExporter())
            .WithMetrics(m => m
                .AddRuntimeInstrumentation()
                .AddAspNetCoreInstrumentation()
                .AddOtlpExporter());

        return builder;
    }
}
