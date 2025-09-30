using System.ComponentModel.DataAnnotations;

namespace ActualGameSearch.Worker.Configuration;

/// <summary>
/// Root configuration options for the Worker application.
/// </summary>
public class WorkerOptions
{
    public const string SectionName = "Worker";
    
    [Required]
    public CandidacyOptions Candidacy { get; set; } = new();
    
    public DataLakeOptions DataLake { get; set; } = new();
    
    [Required]
    public EmbeddingOptions Embedding { get; set; } = new();
    
    [Required]
    public IngestionOptions Ingestion { get; set; } = new();
    
    [Required]
    public SteamOptions Steam { get; set; } = new();
    
    [Required]
    public CosmosOptions Cosmos { get; set; } = new();
}

/// <summary>
/// Configuration for Bronze tier candidacy criteria.
/// </summary>
public class CandidacyOptions
{
    /// <summary>
    /// Minimum number of recommendations (positive + negative reviews) required for Bronze tier inclusion.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int MinRecommendationsForInclusion { get; set; } = 10;

    /// <summary>
    /// Minimum number of reviews required before computing embeddings for performance optimization.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int MinReviewsForEmbedding { get; set; } = 20;

    /// <summary>
    /// Maximum number of associated appids (DLC, demos, etc.) per true game in bronze dataset.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int MaxAssociatedAppIds { get; set; } = 99;
}

/// <summary>
/// Configuration for embedding generation.
/// </summary>
public class EmbeddingOptions
{
    public string OllamaBaseUrl { get; set; } = "http://localhost:11434";
    public string Model { get; set; } = "nomic-embed-text";

    [Range(1, 32768)]
    public int ContextLength { get; set; } = 8192;

    [Range(1, 1000)]
    public int BatchSize { get; set; } = 10;

    [Range(1, int.MaxValue)]
    public int MaxRetries { get; set; } = 3;

    [Range(100, 60000)]
    public int TimeoutMs { get; set; } = 30000;
}

/// <summary>
/// Configuration for data ingestion processes.
/// </summary>
public class IngestionOptions
{
    [Range(1, int.MaxValue)]
    public int DefaultConcurrency { get; set; } = 4;

    [Range(1, 10000)]
    public int ReviewsCapPerApp { get; set; } = 100;

    [Range(1, 1000)]
    public int NewsCount { get; set; } = 20;

    public string NewsTagsFilter { get; set; } = "all";

    [Range(1, int.MaxValue)]
    public int SampleSize { get; set; } = 600;
}

/// <summary>
/// Configuration for data lake storage.
/// </summary>
public class DataLakeOptions
{
    public string? RootPath { get; set; }
}

/// <summary>
/// Configuration for Steam API integration.
/// </summary>
public class SteamOptions
{
    public string BaseUrl { get; set; } = "https://store.steampowered.com";

    [Range(100, 60000)]
    public int RequestTimeoutMs { get; set; } = 15000;

    [Range(100, 10000)]
    public int DelayBetweenRequestsMs { get; set; } = 1000;

    [Range(1, 10)]
    public int MaxRetries { get; set; } = 3;
}

/// <summary>
/// Configuration for Cosmos DB integration.
/// </summary>
public class CosmosOptions
{
    public string ConnectionString { get; set; } = "";
    public string DatabaseName { get; set; } = "ActualGameSearch";
    public string GamesContainerName { get; set; } = "Games";
    public string ReviewsContainerName { get; set; } = "Reviews";
}