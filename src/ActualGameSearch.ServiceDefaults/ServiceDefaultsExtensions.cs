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
using System.Net;
using Microsoft.Extensions.Http.Resilience;

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

        // Global HttpClient with standard resilience (applies to all clients by default)
        builder.Services.AddHttpClient();
        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            http.AddStandardResilienceHandler(options =>
            {
                options.Retry.MaxRetryAttempts = 5;
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
                // Default transient predicates include 5xx/408/timeout and honor Retry-After on 429/503.
            });
        });

        // Named client for Steam with etiquette + resilience
        builder.Services
            .AddHttpClient("steam", client =>
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("ActualGameSearch/1.0 (+https://actualgamesearch.com)");
                client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                MaxConnectionsPerServer = 4,
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
            })
            .AddStandardResilienceHandler(options =>
            {
                options.Retry.MaxRetryAttempts = 5;
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromMinutes(2);
                options.CircuitBreaker.MinimumThroughput = 20;
                options.CircuitBreaker.FailureRatio = 0.2;
            });

        return builder;
    }
}
