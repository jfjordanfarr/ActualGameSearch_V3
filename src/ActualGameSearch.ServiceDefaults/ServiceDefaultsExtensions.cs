using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using OpenTelemetry.Resources;
using OpenTelemetry.Logs;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter;
using System.Diagnostics;

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
            var httpOtlp = Environment.GetEnvironmentVariable("ASPIRE_DASHBOARD_OTLP_HTTP_ENDPOINT_URL");
            if (!string.IsNullOrWhiteSpace(httpOtlp) && Uri.TryCreate(httpOtlp, UriKind.Absolute, out var httpOtlpUri))
            {
                o.AddOtlpExporter(exp =>
                {
                    exp.Endpoint = httpOtlpUri;
                    exp.Protocol = OtlpExportProtocol.HttpProtobuf;
                });
            }
            else
            {
                o.AddOtlpExporter();
            }
        });

        // Register custom ActivitySources for manual spans (optional)
        var activitySourceNames = new[] { "AGS.Api", "AGS.Worker" };

        // Wire OTLP export to the Aspire Dashboard's OTLP endpoint when available
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(serviceName: builder.Environment.ApplicationName))
            .WithTracing(t => t
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddSource(activitySourceNames)
                .AddOtlpExporter(exp =>
                {
                    var httpOtlp = Environment.GetEnvironmentVariable("ASPIRE_DASHBOARD_OTLP_HTTP_ENDPOINT_URL");
                    if (!string.IsNullOrWhiteSpace(httpOtlp) && Uri.TryCreate(httpOtlp, UriKind.Absolute, out var httpOtlpUri))
                    {
                        exp.Endpoint = httpOtlpUri;
                        exp.Protocol = OtlpExportProtocol.HttpProtobuf;
                    }
                }))
            .WithMetrics(m => m
                .AddRuntimeInstrumentation()
                .AddAspNetCoreInstrumentation()
                .AddOtlpExporter(exp =>
                {
                    var httpOtlp = Environment.GetEnvironmentVariable("ASPIRE_DASHBOARD_OTLP_HTTP_ENDPOINT_URL");
                    if (!string.IsNullOrWhiteSpace(httpOtlp) && Uri.TryCreate(httpOtlp, UriKind.Absolute, out var httpOtlpUri))
                    {
                        exp.Endpoint = httpOtlpUri;
                        exp.Protocol = OtlpExportProtocol.HttpProtobuf;
                    }
                }));

        return builder;
    }
}
