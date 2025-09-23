namespace ActualGameSearch.Core.Models;

public sealed record ReviewMeta(int HelpfulVotes, DateTimeOffset CreatedAt);

public sealed record Candidate(
    string GameId,
    string GameTitle,
    string? ReviewId,
    double TextScore,
    double SemanticScore,
    double CombinedScore,
    string? Excerpt,
    ReviewMeta? ReviewMeta
)
{
    // Optional: include only when requested via API (fields=full)
    public string? FullText { get; init; }
}
