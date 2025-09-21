using ActualGameSearch.Core.Models;
using ActualGameSearch.Core.Services.Ranking;

namespace ActualGameSearch.UnitTests;

public class RankingTests
{
    private static Candidate C(string id, double text, double semantic, int helpful = 0, int daysAgo = 0)
        => new(id, $"Game {id}", null, text, semantic, 0, null,
            new ReviewMeta(helpful, DateTimeOffset.UtcNow.AddDays(-daysAgo)));

    [Fact]
    public void Combines_Text_And_Semantic_Weights()
    {
        var ranker = new HybridRanker();
        var items = new List<Candidate>
        {
            C("A", text: 0.9, semantic: 0.1),
            C("B", text: 0.1, semantic: 0.9)
        };

        var rankedTextHeavy = ranker.Rank(items, new ReRankWeights(Semantic: 0.2, Text: 0.8)).ToList();
        Assert.Equal("A", rankedTextHeavy[0].GameId);

        var rankedSemanticHeavy = ranker.Rank(items, new ReRankWeights(Semantic: 0.8, Text: 0.2)).ToList();
        Assert.Equal("B", rankedSemanticHeavy[0].GameId);
    }

    [Fact]
    public void Stable_TieBreakers_By_Combined_Helpful_Then_Recency_Then_Id()
    {
        var ranker = new HybridRanker();
        var items = new List<Candidate>
        {
            C("A", text: 0.5, semantic: 0.5, helpful: 5, daysAgo: 10),
            C("B", text: 0.5, semantic: 0.5, helpful: 10, daysAgo: 20),
            C("C", text: 0.5, semantic: 0.5, helpful: 10, daysAgo: 5)
        };

        // Combined equal (1.0), prefer higher helpful, then more recent (smaller daysAgo), then id
        var ranked = ranker.Rank(items, new ReRankWeights(0.5, 0.5)).ToList();
        Assert.Equal(new[] { "C", "B", "A" }, ranked.Select(r => r.GameId).ToArray());
    }
}
