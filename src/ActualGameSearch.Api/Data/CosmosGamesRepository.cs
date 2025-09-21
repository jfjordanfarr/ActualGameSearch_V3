using ActualGameSearch.Core.Models;
using ActualGameSearch.Core.Repositories;
using Microsoft.Azure.Cosmos;

namespace ActualGameSearch.Api.Data;

public sealed class CosmosGamesRepository(CosmosClient client) : IGamesRepository
{
    private readonly Container _container = client.GetContainer("actualgames", "games");

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
}
