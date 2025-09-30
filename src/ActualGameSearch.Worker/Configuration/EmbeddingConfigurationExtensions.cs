using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ActualGameSearch.Worker.Embeddings;

namespace ActualGameSearch.Worker.Configuration;

/// <summary>
/// Extension to bridge AppHost environment variables (magic strings) with typed configuration.
/// </summary>
public static class EmbeddingConfigurationExtensions
{
    /// <summary>
    /// Registers embedding services with configuration that honors both AppHost env vars and typed options.
    /// </summary>
    public static IServiceCollection AddEmbeddingServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Create a hybrid embedding options that merges AppHost magic strings with structured config
        services.Configure<EmbeddingOptions>(options =>
        {
            // Start with structured config
            configuration.GetSection($"{WorkerOptions.SectionName}:Embedding").Bind(options);
            
            // Override with AppHost-provided values if present (magic strings take precedence)
            var appHostEndpoint = configuration["Ollama:Endpoint"];
            if (!string.IsNullOrWhiteSpace(appHostEndpoint))
            {
                options.OllamaBaseUrl = appHostEndpoint.TrimEnd('/');
            }
            
            var appHostModel = configuration["Ollama:Model"];
            if (!string.IsNullOrWhiteSpace(appHostModel))
            {
                options.Model = appHostModel;
            }
            
            // Override context length from AppHost if provided
            if (int.TryParse(configuration["Embeddings:NumCtx"], out var numCtx))
            {
                options.ContextLength = numCtx;
            }
        });

        // Register embedding client with configuration
        services.AddSingleton<IEmbeddingClient>(serviceProvider =>
        {
            var httpClient = serviceProvider.GetRequiredService<HttpClient>();
            var embeddingOptions = serviceProvider.GetRequiredService<IOptions<EmbeddingOptions>>();
            var allowFallback = configuration.GetValue<bool>("Embeddings:AllowDeterministicFallback", false);
            
            return new EmbeddingClient(httpClient, embeddingOptions, allowFallback);
        });

        return services;
    }
}