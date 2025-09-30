namespace ActualGameSearch.Worker.Configuration;

/// <summary>
/// Constants for all configuration keys used throughout the Worker application.
/// This eliminates magic strings and provides a central location for all configuration references.
/// </summary>
public static class ConfigurationKeys
{
    // Main Worker section
    public const string WorkerSection = "Worker";
    
    // Legacy configuration sections (maintained for backward compatibility with API/other services)
    public const string CosmosSection = "Cosmos";
    public const string OllamaSection = "Ollama";
    public const string DataLakeSection = "DataLake";
    public const string IngestionSection = "Ingestion";
    public const string CandidacySection = "Candidacy";
    public const string SeedingSection = "Seeding";
    public const string EmbeddingsSection = "Embeddings";
    public const string CadenceSection = "Cadence";

    // Worker configuration paths
    public static class Worker
    {
        public const string DataRoot = $"{WorkerSection}:DataRoot";
        
        public static class Candidacy
        {
            private const string Section = $"{WorkerSection}:Candidacy";
            public const string MinRecommendations = $"{Section}:MinRecommendations";
            public const string MinReviewsForEmbedding = $"{Section}:MinReviewsForEmbedding";
            public const string MaxAssociatedAppIds = $"{Section}:MaxAssociatedAppIds";
        }
        
        public static class Embedding
        {
            private const string Section = $"{WorkerSection}:Embedding";
            public const string OllamaBaseUrl = $"{Section}:OllamaBaseUrl";
            public const string Model = $"{Section}:Model";
            public const string ContextLength = $"{Section}:ContextLength";
            public const string BatchSize = $"{Section}:BatchSize";
            public const string MaxRetries = $"{Section}:MaxRetries";
            public const string TimeoutMs = $"{Section}:TimeoutMs";
        }
        
        public static class Ingestion
        {
            private const string Section = $"{WorkerSection}:Ingestion";
            public const string MaxConcurrency = $"{Section}:MaxConcurrency";
            public const string SampleSize = $"{Section}:SampleSize";
            public const string ReviewsCapPerApp = $"{Section}:ReviewsCapPerApp";
            public const string NewsCountPerApp = $"{Section}:NewsCountPerApp";
            public const string NewsTagsFilter = $"{Section}:NewsTagsFilter";
            public const string HttpTimeoutMs = $"{Section}:HttpTimeoutMs";
            public const string MaxRetries = $"{Section}:MaxRetries";
        }
        
        public static class Steam
        {
            private const string Section = $"{WorkerSection}:Steam";
            public const string BaseUrl = $"{Section}:BaseUrl";
            public const string TimeoutMs = $"{Section}:TimeoutMs";
            public const string MaxRetries = $"{Section}:MaxRetries";
            public const string DelayBetweenRequestsMs = $"{Section}:DelayBetweenRequestsMs";
        }
        
        public static class Cosmos
        {
            private const string Section = $"{WorkerSection}:Cosmos";
            public const string ConnectionString = $"{Section}:ConnectionString";
            public const string DatabaseName = $"{Section}:DatabaseName";
            public const string GamesContainerName = $"{Section}:GamesContainerName";
            public const string ReviewsContainerName = $"{Section}:ReviewsContainerName";
            public const string MaxConcurrency = $"{Section}:MaxConcurrency";
            public const string MaxItemCount = $"{Section}:MaxItemCount";
        }
    }

    // Legacy configuration paths (for backward compatibility)
    public static class Legacy
    {
        public static class Cosmos
        {
            public const string Database = "Cosmos:Database";
            public const string GamesContainer = "Cosmos:GamesContainer";
            public const string ReviewsContainer = "Cosmos:ReviewsContainer";
            public const string VectorPath = "Cosmos:Vector:Path";
            public const string VectorDimensions = "Cosmos:Vector:Dimensions";
            public const string VectorDistanceFunction = "Cosmos:Vector:DistanceFunction";
            public const string VectorDistanceForm = "Cosmos:Vector:DistanceForm";
            public const string VectorIndexType = "Cosmos:Vector:IndexType";
            public const string GamesVectorPath = "Cosmos:GamesVector:Path";
            public const string PatchVectorPath = "Cosmos:PatchVector:Path";
        }
        
        public static class Ollama
        {
            public const string Endpoint = "Ollama:Endpoint";
            public const string Model = "Ollama:Model";
        }
        
        public static class DataLake
        {
            public const string Root = "DataLake:Root";
        }
        
        public static class Ingestion
        {
            public const string MaxConcurrency = "Ingestion:MaxConcurrency";
            public const string Concurrency = "Ingestion:Concurrency";
            public const string ReviewCapBronze = "Ingestion:ReviewCapBronze";
            public const string EnableReviewDeltaPolicy = "Ingestion:EnableReviewDeltaPolicy";
        }
        
        public static class Candidacy
        {
            public const string BronzeMinRecommendationsForInclusion = "Candidacy:Bronze:MinRecommendationsForInclusion";
            public const string BronzeMinRecommendationsForEmbedding = "Candidacy:Bronze:MinRecommendationsForEmbedding";
            public const string BronzeMaxAssociatedAppIds = "Candidacy:Bronze:MaxAssociatedAppIds";
            public const string SilverMinUniqueWordsPerReview = "Candidacy:Silver:MinUniqueWordsPerReview";
            public const string SilverRequireSteamPurchase = "Candidacy:Silver:RequireSteamPurchase";
            public const string GoldMaxReviewsPerGame = "Candidacy:Gold:MaxReviewsPerGame";
            public const string GoldMaxNewsItemsPerGame = "Candidacy:Gold:MaxNewsItemsPerGame";
            public const string GoldMaxWorkshopItemsPerGame = "Candidacy:Gold:MaxWorkshopItemsPerGame";
        }
        
        public static class Seeding
        {
            public const string MaxReviewsPerGame = "Seeding:MaxReviewsPerGame";
            public const string FailOnNoReviews = "Seeding:FailOnNoReviews";
            public const string MinReviewCount = "Seeding:MinReviewCount";
            public const string MinQualifiedReviews = "Seeding:MinQualifiedReviews";
            public const string MinUniqueWordsPerReview = "Seeding:MinUniqueWordsPerReview";
            public const string RequireSteamPurchase = "Seeding:RequireSteamPurchase";
        }
        
        public static class Embeddings
        {
            public const string AllowDeterministicFallback = "Embeddings:AllowDeterministicFallback";
            public const string NumCtx = "Embeddings:NumCtx";
            public const string MaxBatch = "Embeddings:MaxBatch";
            public const string HttpTimeoutSeconds = "Embeddings:HttpTimeoutSeconds";
        }
        
        public static class Cadence
        {
            public const string StorePagesDays = "Cadence:StorePagesDays";
            public const string NewsDays = "Cadence:NewsDays";
        }
    }
}