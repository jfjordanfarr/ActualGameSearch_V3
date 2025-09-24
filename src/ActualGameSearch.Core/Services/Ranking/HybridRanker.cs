using ActualGameSearch.Core.Models;

namespace ActualGameSearch.Core.Services.Ranking;

public interface IHybridRanker
{
    IEnumerable<Candidate> Rank(IEnumerable<Candidate> candidates, ReRankWeights weights);
}

public sealed class HybridRanker : IHybridRanker
{
    public IEnumerable<Candidate> Rank(IEnumerable<Candidate> candidates, ReRankWeights weights)
    {
        // Guard weight normalization if needed
        var wS = Clamp01(weights.Semantic);
        var wT = Clamp01(weights.Text);
        if (wS + wT == 0) { wS = 0.5; wT = 0.5; }

        // Compute combined and sort with deterministic tie-breakers
        return candidates
            .Select(c => c with { CombinedScore = Safe(c.SemanticScore) * wS + Safe(c.TextScore) * wT })
            .OrderByDescending(c => c.CombinedScore)
            .ThenByDescending(c => c.ReviewMeta?.HelpfulVotes ?? 0)
            .ThenByDescending(c => c.ReviewMeta?.CreatedAt ?? DateTimeOffset.MinValue)
            .ThenBy(c => c.GameId, StringComparer.Ordinal)
            .ToList();
    }

    private static double Clamp01(double v) => v < 0 ? 0 : (v > 1 ? 1 : v);
    private static double Safe(double v) => double.IsFinite(v) ? v : 0;
}
