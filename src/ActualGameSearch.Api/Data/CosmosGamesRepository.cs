using ActualGameSearch.Core.Models;
using ActualGameSearch.Core.Repositories;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using ActualGameSearch.Core.Services;

namespace ActualGameSearch.Api.Data;

public sealed class CosmosGamesRepository : IGamesRepository
{
    private readonly Container _container;
    private readonly string _dbName = "actualgames";
    private readonly string _containerName = "games";
    private readonly string _distanceFn;
    private readonly string _gvecPath;
    private readonly CosmosVectorQueryHelper.DistanceFormPreference _formPref;

    public CosmosGamesRepository(CosmosClient client, IConfiguration config)
    {
        _container = client.GetContainer(_dbName, _containerName);
        _gvecPath = (config["Cosmos:GamesVector:Path"] ?? "/gvector");
        var distance = (config["Cosmos:Vector:DistanceFunction"] ?? "cosine").ToLowerInvariant();
        _distanceFn = distance switch
        {
            "euclidean" => "euclidean",
            "dotproduct" or "dot_product" => "dotproduct",
            _ => "cosine"
        };
        _formPref = CosmosVectorQueryHelper.ParsePreference(config["Cosmos:Vector:DistanceForm"]);
    }

    public async Task<IReadOnlyList<GameSummary>> SearchAsync(string query, int top, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT TOP {top} c.id, c.title, c.reviewCount FROM c WHERE CONTAINS(c.title, @q)";
        var qd = new QueryDefinition(sql).WithParameter("@q", query);
        var it = _container.GetItemQueryIterator<dynamic>(qd, requestOptions: new QueryRequestOptions { MaxConcurrency = 1 });
        var results = new List<GameSummary>(top);
        while (it.HasMoreResults)
        {
            var page = await it.ReadNextAsync(cancellationToken);
            foreach (var doc in page)
            {
                string id = doc.id ?? "";
                string title = doc.title ?? "";
                int reviewCount = (int?)doc.reviewCount ?? 0;
                results.Add(new GameSummary(id, title, Array.Empty<string>(), reviewCount));
            }
        }
        return results;
    }

    public async Task<IReadOnlyList<GameSummary>> HybridSearchAsync(string query, float[] queryVector, int top, CancellationToken cancellationToken = default)
    {
        // Resolve distance function form for this container
    var resolved = await CosmosVectorQueryHelper.GetOrResolveFormAsync(_container, _dbName, _containerName, _distanceFn, queryVector, _formPref, cancellationToken, _gvecPath);
    var expr = CosmosVectorQueryHelper.VectorDistanceExpr("@embedding", _distanceFn, resolved, _gvecPath);

        // Compute a simple hybrid score: distance term plus small negative boost if title does not contain query
        // We use ORDER BY {expr} ASC (smaller is more similar for distances like cosine/2-arg), but our helper returns the expression consistent with ORDER BY
        // To blend, we project both expr and a contains flag; then order by expr primarily, contains secondarily.

        var sql = $"SELECT TOP {top} c.id, c.title, c.reviewCount, {expr} AS similarity, CASE WHEN CONTAINS(c.title, @q) THEN 1 ELSE 0 END AS titleHit FROM c ORDER BY titleHit DESC, similarity";
        var qd = new QueryDefinition(sql)
            .WithParameter("@embedding", queryVector)
            .WithParameter("@q", query);
        var it = _container.GetItemQueryIterator<dynamic>(qd, requestOptions: new QueryRequestOptions { MaxConcurrency = 1 });
        var results = new List<GameSummary>(top);
        while (it.HasMoreResults)
        {
            var page = await it.ReadNextAsync(cancellationToken);
            foreach (var doc in page)
            {
                string id = doc.id ?? "";
                string title = doc.title ?? "";
                int reviewCount = (int?)doc.reviewCount ?? 0;
                results.Add(new GameSummary(id, title, Array.Empty<string>(), reviewCount));
            }
        }
        return results;
    }
}
