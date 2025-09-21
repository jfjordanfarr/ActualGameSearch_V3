using ActualGameSearch.Core.Models;
using ActualGameSearch.Core.Repositories;
using Microsoft.Azure.Cosmos;

namespace ActualGameSearch.Api.Data;

public sealed class CosmosReviewsRepository(CosmosClient client) : IReviewsRepository
{
    private readonly Container _container = client.GetContainer("actualgames", "reviews");

    public async Task<IReadOnlyList<Candidate>> VectorSearchAsync(float[] queryVector, int top, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT TOP {top} c.id AS reviewId, c.gameId, c.gameTitle, c.textScore, c.semanticScore, c.excerpt, c.helpfulVotes, c.createdAt, VectorDistance(c.vector, @embedding) AS similarity FROM c ORDER BY VectorDistance(c.vector, @embedding)";
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
                int helpful = (int?)doc.helpfulVotes ?? 0;
                DateTimeOffset createdAt = DateTimeOffset.TryParse((string?)doc.createdAt, out var dto) ? dto : DateTimeOffset.UnixEpoch;
                results.Add(new Candidate(gameId, gameTitle, reviewId, textScore, semanticScore, combinedScore, excerpt, new ReviewMeta(helpful, createdAt)));
            }
        }
        return results;
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
