using ActualGameSearch.Core.Models;
using ActualGameSearch.Core.Repositories;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using ActualGameSearch.Core.Services;

namespace ActualGameSearch.Api.Data;

public sealed class CosmosReviewsRepository : IReviewsRepository
{
    private readonly Container _container;
    private readonly string _distanceFn;
    private readonly string _dbName = "actualgames";
    private readonly string _containerName = "reviews";
    private readonly CosmosVectorQueryHelper.DistanceFormPreference _formPref;
    private readonly string _vecPath;

    public CosmosReviewsRepository(CosmosClient client, IConfiguration config)
    {
        _container = client.GetContainer(_dbName, _containerName);
        var distance = (config["Cosmos:Vector:DistanceFunction"] ?? "cosine").ToLowerInvariant();
        _distanceFn = distance switch
        {
            "euclidean" => "euclidean",
            "dotproduct" or "dot_product" => "dotproduct",
            _ => "cosine"
        };
    _formPref = CosmosVectorQueryHelper.ParsePreference(config["Cosmos:Vector:DistanceForm"]);
    _vecPath = config["Cosmos:Vector:Path"] ?? "/vector";
    }

    public async Task<IReadOnlyList<Candidate>> VectorSearchAsync(float[] queryVector, int top, CancellationToken cancellationToken = default)
    {
        async Task<IReadOnlyList<Candidate>> ExecuteAsync(string sql)
        {
            var qd = new QueryDefinition(sql).WithParameter("@embedding", queryVector);
            var it = _container.GetItemQueryIterator<dynamic>(qd, requestOptions: new QueryRequestOptions { MaxConcurrency = 1 });
            var results = new List<Candidate>(top);
            while (it.HasMoreResults)
            {
                var page = await it.ReadNextAsync(cancellationToken);
                foreach (var doc in page)
                {
                    string gameId = doc.gameId ?? "";
                    string gameTitle = doc.gameTitle ?? "";
                    string? reviewId = doc.reviewId;
                    double textScore = (double?)doc.textScore ?? 0d;
                    double semanticScore = (double?)doc.semanticScore ?? 0d;
                    double combinedScore = (double?)doc.similarity ?? 0d; // temporary: using similarity as combined
                    string? excerpt = doc.excerpt;
                    string? fullText = doc.fullText;
                    int helpful = (int?)doc.helpfulVotes ?? 0;
                    DateTimeOffset createdAt = DateTimeOffset.TryParse((string?)doc.createdAt, out var dto) ? dto : DateTimeOffset.UnixEpoch;
                    results.Add(new Candidate(gameId, gameTitle, reviewId, textScore, semanticScore, combinedScore, excerpt, new ReviewMeta(helpful, createdAt)) { FullText = fullText });
                }
            }
            return results;
        }

        // Resolve and cache supported form for this container
        var probeVec = queryVector.Length > 0 ? queryVector : new float[] { 0f };
    var resolved = await CosmosVectorQueryHelper.GetOrResolveFormAsync(_container, _dbName, _containerName, _distanceFn, probeVec, _formPref, cancellationToken, _vecPath);
    var expr = CosmosVectorQueryHelper.VectorDistanceExpr("@embedding", _distanceFn, resolved, _vecPath);
        var sql = $"SELECT TOP {top} c.id AS reviewId, c.gameId, c.gameTitle, c.textScore, c.semanticScore, c.excerpt, c.fullText, c.helpfulVotes, c.createdAt, {expr} AS similarity FROM c ORDER BY {expr}";
        return await ExecuteAsync(sql);
    }

    public async Task<IReadOnlyList<Candidate>> TextSearchAsync(string query, int top, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT TOP {top} c.id AS reviewId, c.gameId, c.gameTitle, c.excerpt, c.helpfulVotes, c.createdAt FROM c WHERE CONTAINS(c.excerpt, @q)";
        var qd = new QueryDefinition(sql).WithParameter("@q", query);
        var it = _container.GetItemQueryIterator<dynamic>(qd, requestOptions: new QueryRequestOptions { MaxConcurrency = 1 });
        var results = new List<Candidate>(top);
        while (it.HasMoreResults)
        {
            var page = await it.ReadNextAsync(cancellationToken);
            foreach (var doc in page)
            {
                string gameId = doc.gameId ?? "";
                string gameTitle = doc.gameTitle ?? "";
                string? reviewId = doc.reviewId;
                string? excerpt = doc.excerpt;
                int helpful = (int?)doc.helpfulVotes ?? 0;
                DateTimeOffset createdAt = DateTimeOffset.TryParse((string?)doc.createdAt, out var dto) ? dto : DateTimeOffset.UnixEpoch;
                results.Add(new Candidate(gameId, gameTitle, reviewId, TextScore: 0, SemanticScore: 0, CombinedScore: 0, Excerpt: excerpt, ReviewMeta: new ReviewMeta(helpful, createdAt)));
            }
        }
        return results;
    }
}
