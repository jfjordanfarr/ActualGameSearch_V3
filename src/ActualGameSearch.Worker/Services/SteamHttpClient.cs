using System.Net;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json;
using ActualGameSearch.Worker.Models;

namespace ActualGameSearch.Worker.Services;

public class SteamHttpClient : ISteamClient
{
    private readonly IHttpClientFactory _factory;
    private readonly JsonSerializerOptions _json;

    public SteamHttpClient(IHttpClientFactory factory)
    {
        _factory = factory;
        _json = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    private HttpClient Client => _factory.CreateClient("steam");

    public async Task<SteamAppListResponse?> GetAppListAsync(CancellationToken ct = default)
    {
        return await Client.GetFromJsonAsync<SteamAppListResponse>("https://api.steampowered.com/ISteamApps/GetAppList/v2/", _json, ct);
    }

    public async Task<Dictionary<string, AppDetailsResponse>?> GetAppDetailsAsync(int appId, string cc = "us", string l = "en", CancellationToken ct = default)
    {
        var url = $"https://store.steampowered.com/api/appdetails?appids={appId}&cc={cc}&l={l}";
        using var resp = await Client.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode) return null;
        var json = await resp.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<Dictionary<string, AppDetailsResponse>>(json, _json);
    }

    public async Task<(SteamReviewsResponse? payload, HttpStatusCode status)> GetReviewsPageAsync(int appId, string cursor = "*", int perPage = 100, string filter = "recent", string language = "all", string purchaseType = "all", CancellationToken ct = default)
    {
        // Polite tiny jitter between pages
        try { await Task.Delay(TimeSpan.FromMilliseconds(Random.Shared.Next(25, 95)), ct); } catch { }
        var cursorEnc = Uri.EscapeDataString(cursor);
        var url =
            $"https://store.steampowered.com/appreviews/{appId}?json=1&filter={filter}&language={language}&purchase_type={purchaseType}&num_per_page={perPage}&cursor={cursorEnc}";
        using var resp = await Client.GetAsync(url, ct);
        var status = resp.StatusCode;
        if ((int)status == 429)
        {
            // Honor Retry-After if present; otherwise wait a reasonable default before signaling caller
            try
            {
                if (resp.Headers.RetryAfter is { } ra)
                {
                    if (ra.Delta.HasValue) await Task.Delay(ra.Delta.Value + TimeSpan.FromMilliseconds(Random.Shared.Next(25, 120)), ct);
                    else if (ra.Date.HasValue)
                    {
                        var delta = ra.Date.Value - DateTimeOffset.UtcNow;
                        if (delta > TimeSpan.Zero) await Task.Delay(delta + TimeSpan.FromMilliseconds(Random.Shared.Next(25, 120)), ct);
                    }
                }
                else
                {
                    await Task.Delay(TimeSpan.FromSeconds(6 + Random.Shared.NextDouble() * 4), ct);
                }
            }
            catch { }
            return (null, status);
        }
        if (!resp.IsSuccessStatusCode) return (null, status);
        var json = await resp.Content.ReadAsStringAsync(ct);
        var payload = JsonSerializer.Deserialize<SteamReviewsResponse>(json, _json);
        return (payload, status);
    }

    public async Task<SteamNewsResponse?> GetNewsForAppAsync(int appId, int count = 10, string? tags = null, CancellationToken ct = default)
    {
        var baseUrl = $"https://api.steampowered.com/ISteamNews/GetNewsForApp/v2/?appid={appId}&count={count}";
        var url = string.IsNullOrWhiteSpace(tags) ? baseUrl : baseUrl + "&tags=" + UrlEncoder.Default.Encode(tags);
        using var resp = await Client.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode) return null;
        var json = await resp.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<SteamNewsResponse>(json, _json);
    }
}
