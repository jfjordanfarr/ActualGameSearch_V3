namespace ActualGameSearch.Core.Models;

public sealed record PurchaseOriginRatio(double Steam, double Other, double Unknown, int TotalSampled);

public sealed record GameCandidates(
    string GameId,
    string GameTitle,
    IReadOnlyList<string> TagSummary,
    int ReviewCount,
    PurchaseOriginRatio PurchaseOriginRatio,
    IReadOnlyList<Candidate> Candidates
);
