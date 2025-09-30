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
using Polly;

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
            // Prefer standard OTEL envs set by Aspire; fall back to optional dashboard env
            var endpointStr = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")
                               ?? Environment.GetEnvironmentVariable("ASPIRE_DASHBOARD_OTLP_HTTP_ENDPOINT_URL");
            if (!string.IsNullOrWhiteSpace(endpointStr) && Uri.TryCreate(endpointStr, UriKind.Absolute, out var otlpUri))
            {
                o.AddOtlpExporter(exp =>
                {
                    exp.Endpoint = otlpUri;
                    exp.Protocol = OtlpExportProtocol.HttpProtobuf;
                });
            }
            else
            {
                // Still add exporter and let defaults/env drive it
                o.AddOtlpExporter(exp =>
                {
                    exp.Protocol = OtlpExportProtocol.HttpProtobuf;
                });
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
                    var endpointStr = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")
                                       ?? Environment.GetEnvironmentVariable("ASPIRE_DASHBOARD_OTLP_HTTP_ENDPOINT_URL");
                    if (!string.IsNullOrWhiteSpace(endpointStr) && Uri.TryCreate(endpointStr, UriKind.Absolute, out var otlpUri))
                    {
                        exp.Endpoint = otlpUri;
                    }
                    exp.Protocol = OtlpExportProtocol.HttpProtobuf;
                }))
            .WithMetrics(m => m
                .AddRuntimeInstrumentation()
                .AddAspNetCoreInstrumentation()
                .AddOtlpExporter(exp =>
                {
                    var endpointStr = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")
                                       ?? Environment.GetEnvironmentVariable("ASPIRE_DASHBOARD_OTLP_HTTP_ENDPOINT_URL");
                    if (!string.IsNullOrWhiteSpace(endpointStr) && Uri.TryCreate(endpointStr, UriKind.Absolute, out var otlpUri))
                    {
                        exp.Endpoint = otlpUri;
                    }
                    exp.Protocol = OtlpExportProtocol.HttpProtobuf;
                }));

        // Global HttpClient with standard resilience (applies to all clients by default)
        builder.Services.AddHttpClient();
        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            // Note: We keep a conservative but non-aggressive global policy because named clients
            // (e.g., "steam", "ollama") also add their own handlers. If the global attempt timeout is
            // too short, it can trigger cancellations before the named pipeline completes long-running work.
            http.AddStandardResilienceHandler(options =>
            {
                options.Retry.MaxRetryAttempts = 5;
                // Allow long-running requests to complete when needed (named clients can still be stricter)
                options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(5);
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(90);
                // Circuit breaker sampling must be at least 2x attempt timeout (90s * 2 = 180s minimum)
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromMinutes(4);
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
                // Keep a single connection to respect Steam API rate limits and reduce 429s.
                MaxConnectionsPerServer = 1,
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
            })
            .AddStandardResilienceHandler(options =>
            {
                options.Retry.MaxRetryAttempts = 8;
                options.Retry.BackoffType = DelayBackoffType.Exponential;
                options.Retry.Delay = TimeSpan.FromSeconds(1);
                options.Retry.UseJitter = true;
                // Respect Retry-After header if provided (e.g., 429/503)
                options.Retry.DelayGenerator = static args =>
                {
                    try
                    {
                        if (args.Outcome.Result is HttpResponseMessage resp)
                        {
                            if (resp.StatusCode == HttpStatusCode.TooManyRequests || resp.StatusCode == HttpStatusCode.ServiceUnavailable)
                            {
                                if (resp.Headers.RetryAfter is { } ra)
                                {
                                    if (ra.Delta.HasValue)
                                        return new ValueTask<TimeSpan?>(ra.Delta.Value + TimeSpan.FromMilliseconds(Random.Shared.Next(25, 125)));
                                    if (ra.Date.HasValue)
                                    {
                                        var delta = ra.Date.Value - DateTimeOffset.UtcNow;
                                        if (delta > TimeSpan.Zero) return new ValueTask<TimeSpan?>(delta + TimeSpan.FromMilliseconds(Random.Shared.Next(25, 125)));
                                    }
                                }
                                // Fallback when no header is present
                                return new ValueTask<TimeSpan?>(TimeSpan.FromSeconds(5 + Random.Shared.NextDouble() * 5));
                            }
                        }
                    }
                    catch { }
                    return new ValueTask<TimeSpan?>(null as TimeSpan?);
                };
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(40);
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(12);
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromMinutes(2);
                options.CircuitBreaker.MinimumThroughput = 20;
                options.CircuitBreaker.FailureRatio = 0.15;
            });

        // Named client for Ollama embeddings with generous timeouts (avoid premature 10s attempt timeouts)
        builder.Services
            .AddHttpClient("ollama", client =>
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("ActualGameSearch/1.0 (+https://actualgamesearch.com)");
                client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                MaxConnectionsPerServer = 2,
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(10),
            })
            .AddStandardResilienceHandler(options =>
            {
                // Embedding requests can take time; increase attempt + total timeout
                options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(5);
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(90);
                options.Retry.MaxRetryAttempts = 3;
                options.Retry.BackoffType = DelayBackoffType.Exponential;
                options.Retry.Delay = TimeSpan.FromSeconds(2);
                options.Retry.UseJitter = true;
                // Validation requires SamplingDuration >= 2x AttemptTimeout (>= 180s for 90s attempt)
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromMinutes(4);
                options.CircuitBreaker.MinimumThroughput = 10;
                options.CircuitBreaker.FailureRatio = 0.25;
            });

        return builder;
    }
}
