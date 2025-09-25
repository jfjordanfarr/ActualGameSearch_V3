using System.Net;
using ActualGameSearch.Worker.Models;

namespace ActualGameSearch.Worker.Services;

public interface ISteamClient
{
    Task<SteamAppListResponse?> GetAppListAsync(CancellationToken ct = default);
    Task<Dictionary<string, AppDetailsResponse>?> GetAppDetailsAsync(int appId, string cc = "us", string l = "en", CancellationToken ct = default);
    Task<(SteamReviewsResponse? payload, HttpStatusCode status)> GetReviewsPageAsync(int appId, string cursor = "*", int perPage = 100, string filter = "recent", string language = "all", string purchaseType = "all", CancellationToken ct = default);
    Task<SteamNewsResponse?> GetNewsForAppAsync(int appId, int count = 10, string? tags = null, CancellationToken ct = default);
}
