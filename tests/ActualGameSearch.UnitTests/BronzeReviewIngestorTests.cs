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

public class BronzeReviewIngestorTests
{
    private class FakeSteam : ISteamClient
    {
        private readonly int _total; private int _emitted;
        public FakeSteam(int total) { _total = total; }
        public Task<SteamAppListResponse?> GetAppListAsync(CancellationToken ct = default) => Task.FromResult<SteamAppListResponse?>(null);
        public Task<System.Collections.Generic.Dictionary<string, AppDetailsResponse>?> GetAppDetailsAsync(int appId, string cc = "us", string l = "en", CancellationToken ct = default) => Task.FromResult<System.Collections.Generic.Dictionary<string, AppDetailsResponse>?>(null);
        public Task<SteamNewsResponse?> GetNewsForAppAsync(int appId, int count = 10, string? tags = null, CancellationToken ct = default) => Task.FromResult<SteamNewsResponse?>(null);
        public Task<(SteamReviewsResponse? payload, System.Net.HttpStatusCode status)> GetReviewsPageAsync(int appId, string cursor = "*", int perPage = 100, string filter = "recent", string language = "all", string purchaseType = "all", CancellationToken ct = default)
        {
            var take = System.Math.Min(perPage, _total - _emitted);
            var arr = new SteamReviewItem[take];
            for (int i = 0; i < take; i++) arr[i] = new SteamReviewItem($"id-{_emitted+i}", "text", 0, 0, 0, "en", true, true, false, null);
            _emitted += take;
            var resp = new SteamReviewsResponse(1, arr, _emitted >= _total ? null : "next");
            return Task.FromResult<(SteamReviewsResponse?, System.Net.HttpStatusCode)>((resp, System.Net.HttpStatusCode.OK));
        }
    }

    [Fact]
    public async Task WritesPagedJsonGz_UntilCap()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "agsv3-bronze-" + System.Guid.NewGuid().ToString("N"));
        var steam = new FakeSteam(total: 250);
        var ingestor = new BronzeReviewIngestor(steam, tmp, capPerApp: 180);
        var today = new System.DateTime(2025, 9, 25);
        var runId = "run-bronze-1";

        var count = await ingestor.IngestReviewsAsync(480, runId, today);
        Assert.True(count <= 180);

        var dir = Path.Combine(tmp, "bronze", "reviews", "2025", "09", "25", runId, "appid=480");
        Assert.True(Directory.Exists(dir));
        var files = Directory.GetFiles(dir, "page=*.json.gz");
        Assert.NotEmpty(files);

        // Spot check one page can be read/decompressed and has reviews array
        using var fs = File.OpenRead(files[0]);
        using var gz = new GZipStream(fs, CompressionMode.Decompress);
        using var doc = await JsonDocument.ParseAsync(gz);
        Assert.True(doc.RootElement.TryGetProperty("reviews", out _));
    }
}
