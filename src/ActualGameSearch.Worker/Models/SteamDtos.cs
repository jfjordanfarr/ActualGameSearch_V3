using System.Text.Json;

namespace ActualGameSearch.Worker.Models;

public record AppDetailsResponse(System.Text.Json.JsonElement success, SteamAppDetails? data)
{
    public bool IsSuccess => success.ValueKind switch
    {
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Number => success.TryGetInt32(out var n) && n == 1,
        JsonValueKind.String => int.TryParse(success.GetString(), out var n2) && n2 == 1,
        _ => false
    };
}
public record SteamAppDetails(
    int? steam_appid,
    string? name,
    string? short_description,
    string? detailed_description,
    PriceOverview? price_overview,
    ReleaseDate? release_date,
    SimpleNamed[]? categories,
    SimpleNamed[]? genres,
    SteamRecommendations? recommendations,
    string? header_image,
    string? type
);

public record SteamRecommendations(int? total);
public record PriceOverview(int? final, string? currency);
public record ReleaseDate(string? date, bool? coming_soon);
public record SimpleNamed(JsonElement? id, string? name, string? description);

public record SteamReviewsResponse(int success, SteamReviewItem[] reviews, string? cursor);
public record SteamReviewItem(
    string? recommendationid,
    string review,
    int votes_up,
    int votes_funny,
    long timestamp_created,
    string? language,
    bool voted_up,
    bool steam_purchase,
    bool received_for_free,
    JsonElement? weighted_vote_score
);
public record SteamUserReview(
    string id,
    string text,
    int votesUp,
    int votesFunny,
    DateTimeOffset createdAt,
    string? lang,
    bool votedUp,
    bool steamPurchase,
    bool receivedForFree,
    string? weightedVoteScore
);

public record SteamAppListResponse(SteamAppList applist);
public record SteamAppList(SteamAppIdName[] apps);
public record SteamAppIdName(int appid, string name);

public record SteamNewsResponse(SteamNewsApp appnews);
public record SteamNewsApp(int appid, SteamNewsItem[] newsitems);
public record SteamNewsItem(string gid, string title, string url, string author, int contents_length, long date, string feedlabel, string feedname, string feed_type, int appid, string? contents);
