namespace ActualGameSearch.Core.Models;

public sealed record GameSummary(
    string GameId,
    string GameTitle,
    IReadOnlyList<string> TagSummary,
    int ReviewCount
);
