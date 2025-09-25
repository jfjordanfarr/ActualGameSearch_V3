using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ActualGameSearch.Worker.Ingestion;
using ActualGameSearch.Worker.Models;
using ActualGameSearch.Worker.Services;
using Xunit;

namespace ActualGameSearch.UnitTests;

public class BronzeNewsIngestorTests
{
    private class FakeSteam : ISteamClient
    {
        public Task<SteamAppListResponse?> GetAppListAsync(CancellationToken ct = default) => Task.FromResult<SteamAppListResponse?>(null);
        public Task<System.Collections.Generic.Dictionary<string, AppDetailsResponse>?> GetAppDetailsAsync(int appId, string cc = "us", string l = "en", CancellationToken ct = default) => Task.FromResult<System.Collections.Generic.Dictionary<string, AppDetailsResponse>?>(null);
        public Task<(SteamReviewsResponse? payload, System.Net.HttpStatusCode status)> GetReviewsPageAsync(int appId, string cursor = "*", int perPage = 100, string filter = "recent", string language = "all", string purchaseType = "all", CancellationToken ct = default) => Task.FromResult<(SteamReviewsResponse?, System.Net.HttpStatusCode)>((null, System.Net.HttpStatusCode.OK));
        public Task<SteamNewsResponse?> GetNewsForAppAsync(int appId, int count = 10, string? tags = null, CancellationToken ct = default)
        {
            var item = new SteamNewsItem("gid", "title", "url", "author", 10, 1700000000, "feedlabel", "feedname", 0, appId, "<p>contents</p>");
            var resp = new SteamNewsResponse(new SteamNewsApp(appId, new[] { item }));
            return Task.FromResult<SteamNewsResponse?>(resp);
        }
    }

    [Fact]
    public async Task WritesNewsPageJsonGz()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "agsv3-bronze-news-" + System.Guid.NewGuid().ToString("N"));
        var steam = new FakeSteam();
        var ingestor = new BronzeNewsIngestor(steam, tmp);
        var today = new System.DateTime(2025, 9, 25);
        var runId = "run-news-1";

        var count = await ingestor.IngestNewsAsync(480, runId, today, count: 5, tags: "patchnotes");
        Assert.Equal(1, count);

        var file = ActualGameSearch.Worker.Storage.DataLakePaths.Bronze.NewsPage(tmp, today, runId, 480, 1);
        Assert.True(File.Exists(file));

        using var fs = File.OpenRead(file);
        using var gz = new GZipStream(fs, CompressionMode.Decompress);
        using var doc = await JsonDocument.ParseAsync(gz);
        Assert.True(doc.RootElement.TryGetProperty("appnews", out _));
    }
}
