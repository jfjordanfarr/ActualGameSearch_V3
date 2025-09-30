using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;

namespace ActualGameSearch.Worker.Configuration;

/// <summary>
/// Extension methods for configuring strongly-typed options in the Worker application.
/// </summary>
public static class ConfigurationExtensions
{
    /// <summary>
    /// Configures all Worker-specific options with validation.
    /// </summary>
    public static IServiceCollection AddWorkerConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure the main WorkerOptions with validation
        services.Configure<WorkerOptions>(configuration.GetSection(WorkerOptions.SectionName))
                .AddSingleton<IValidateOptions<WorkerOptions>, ValidateOptionsWithDataAnnotations<WorkerOptions>>();

        // Individual option configurations for easier injection
        services.Configure<CandidacyOptions>(configuration.GetSection($"{WorkerOptions.SectionName}:Candidacy"))
                .AddSingleton<IValidateOptions<CandidacyOptions>, ValidateOptionsWithDataAnnotations<CandidacyOptions>>();

        services.Configure<EmbeddingOptions>(configuration.GetSection($"{WorkerOptions.SectionName}:Embedding"))
                .AddSingleton<IValidateOptions<EmbeddingOptions>, ValidateOptionsWithDataAnnotations<EmbeddingOptions>>();

        services.Configure<IngestionOptions>(configuration.GetSection($"{WorkerOptions.SectionName}:Ingestion"))
                .AddSingleton<IValidateOptions<IngestionOptions>, ValidateOptionsWithDataAnnotations<IngestionOptions>>();

        services.Configure<SteamOptions>(configuration.GetSection($"{WorkerOptions.SectionName}:Steam"))
                .AddSingleton<IValidateOptions<SteamOptions>, ValidateOptionsWithDataAnnotations<SteamOptions>>();

        services.Configure<CosmosOptions>(configuration.GetSection($"{WorkerOptions.SectionName}:Cosmos"))
                .AddSingleton<IValidateOptions<CosmosOptions>, ValidateOptionsWithDataAnnotations<CosmosOptions>>();

        return services;
    }

    /// <summary>
    /// Generic options validator using data annotations.
    /// </summary>
    public class ValidateOptionsWithDataAnnotations<T> : IValidateOptions<T> where T : class
    {
        public ValidateOptionsResult Validate(string? name, T options)
        {
            var context = new ValidationContext(options, serviceProvider: null, items: null);
            var results = new List<ValidationResult>();
            
            if (Validator.TryValidateObject(options, context, results, validateAllProperties: true))
            {
                return ValidateOptionsResult.Success;
            }

            var failures = results.Select(r => r.ErrorMessage ?? "Unknown validation error").ToArray();
            return ValidateOptionsResult.Fail(failures);
        }
    }

    /// <summary>
    /// Helper to resolve data root path with fallbacks.
    /// </summary>
    public static string ResolveDataRoot(string? configured)
    {
        // Prefer configured absolute/relative path if provided
        if (!string.IsNullOrWhiteSpace(configured))
        {
            var path = configured;
            // If relative, make it relative to current directory
            if (!Path.IsPathRooted(path))
            {
                path = Path.GetFullPath(path);
            }
            return path;
        }

        // Fallback hierarchy: env var -> default relative path
        var envVar = Environment.GetEnvironmentVariable("DATA_ROOT");
        if (!string.IsNullOrWhiteSpace(envVar))
        {
            return Path.IsPathRooted(envVar) ? envVar : Path.GetFullPath(envVar);
        }

        // Final fallback to default relative path
        return Path.GetFullPath("AI-Agent-Workspace/Artifacts/DataLake");
    }
}