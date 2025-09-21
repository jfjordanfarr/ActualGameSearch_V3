using Microsoft.Azure.Cosmos;

namespace ActualGameSearch.Api.Infrastructure;

public sealed class CosmosBootstrapper(CosmosClient client, IConfiguration config, ILogger<CosmosBootstrapper> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var dbName = config["Cosmos:Database"] ?? "actualgames";
        var gamesContainer = config["Cosmos:GamesContainer"] ?? "games";
        var reviewsContainer = config["Cosmos:ReviewsContainer"] ?? "reviews";

        var vectorPath = config["Cosmos:Vector:Path"] ?? "/vector";
        var dimensions = int.TryParse(config["Cosmos:Vector:Dimensions"], out var d) ? d : 768;
        var distance = (config["Cosmos:Vector:DistanceFunction"] ?? "cosine").ToLowerInvariant();
        var indexTypeRaw = (config["Cosmos:Vector:IndexType"] ?? "diskANN").ToLowerInvariant();

        var database = await client.CreateDatabaseIfNotExistsAsync(dbName, cancellationToken: cancellationToken);

        // Ensure games container exists (default indexing ok for now)
        var gamesProps = new ContainerProperties(id: gamesContainer, partitionKeyPath: "/id");
        await database.Database.CreateContainerIfNotExistsAsync(gamesProps, throughput: 400, cancellationToken: cancellationToken);

        // Ensure reviews container exists with vector policy and index
        var embeddings = new List<Embedding>
        {
            new Embedding
            {
                Path = vectorPath,
                DataType = VectorDataType.Float32,
                DistanceFunction = distance switch
                {
                    "euclidean" => DistanceFunction.Euclidean,
                    "dotproduct" or "dot_product" => DistanceFunction.DotProduct,
                    _ => DistanceFunction.Cosine
                },
                Dimensions = dimensions
            }
        };

        var reviewsProps = new ContainerProperties(id: reviewsContainer, partitionKeyPath: "/id")
        {
            VectorEmbeddingPolicy = new VectorEmbeddingPolicy(new System.Collections.ObjectModel.Collection<Embedding>(embeddings)),
            IndexingPolicy = new IndexingPolicy
            {
                // Include everything by default
                IncludedPaths = { new IncludedPath { Path = "/*" } },
                // Exclude the vector path subtree for write efficiency
                ExcludedPaths = { new ExcludedPath { Path = $"{vectorPath}/*" } },
                VectorIndexes =
                {
                    new VectorIndexPath
                    {
                        Path = vectorPath,
                        Type = indexTypeRaw switch
                        {
                            "flat" => VectorIndexType.Flat,
                            "quantizedflat" or "quantized_flat" => VectorIndexType.QuantizedFlat,
                            _ => VectorIndexType.DiskANN
                        }
                    }
                }
            }
        };

        try
        {
            await database.Database.CreateContainerIfNotExistsAsync(reviewsProps, throughput: 400, cancellationToken: cancellationToken);
        }
        catch (CosmosException ex)
        {
            logger.LogWarning(ex, "Cosmos container bootstrap warning: {Message}", ex.Message);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
