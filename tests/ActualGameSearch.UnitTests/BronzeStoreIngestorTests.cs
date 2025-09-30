using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ActualGameSearch.Worker.Ingestion;
using ActualGameSearch.Worker.Models;
using ActualGameSearch.Worker.Services;
using ActualGameSearch.Worker.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace ActualGameSearch.UnitTests;

public class BronzeStoreIngestorTests
{
    private class FakeSteam : ISteamClient
    {
        public Task<SteamAppListResponse?> GetAppListAsync(CancellationToken ct = default) => Task.FromResult<SteamAppListResponse?>(null);
        public Task<(SteamReviewsResponse? payload, System.Net.HttpStatusCode status)> GetReviewsPageAsync(int appId, string cursor = "*", int perPage = 100, string filter = "recent", string language = "all", string purchaseType = "all", CancellationToken ct = default) => Task.FromResult<(SteamReviewsResponse?, System.Net.HttpStatusCode)>((null, System.Net.HttpStatusCode.OK));
        public Task<SteamNewsResponse?> GetNewsForAppAsync(int appId, int count = 10, string? tags = null, CancellationToken ct = default) => Task.FromResult<SteamNewsResponse?>(null);
        public Task<System.Collections.Generic.Dictionary<string, AppDetailsResponse>?> GetAppDetailsAsync(int appId, string cc = "us", string l = "en", CancellationToken ct = default)
        {
            // Intentionally do not dispose the JsonDocument to keep the JsonElement valid for serialization
            var doc = JsonDocument.Parse("true");
            var details = new SteamAppDetails(appId, $"App {appId}", "short", null, null, null, null, null, new SteamRecommendations(123), null, "game");
            var resp = new AppDetailsResponse(doc.RootElement, details);
            var dict = new System.Collections.Generic.Dictionary<string, AppDetailsResponse>
            {
                [appId.ToString()] = resp
            };
            return Task.FromResult<System.Collections.Generic.Dictionary<string, AppDetailsResponse>?>(dict);
        }
    }

    [Fact]
    public async Task WritesStoreJsonGz()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "agsv3-bronze-store-" + System.Guid.NewGuid().ToString("N"));
        var steam = new FakeSteam();
        var mockCandidacyOptions = Options.Create(new CandidacyOptions
        {
            MinRecommendationsForInclusion = 10,
            MinReviewsForEmbedding = 100,
            MaxAssociatedAppIds = 1000
        });

        var ingestor = new BronzeStoreIngestor(steam, tmp, mockCandidacyOptions);
        var today = new System.DateTime(2025, 9, 25);
        var runId = "run-store-1";

        var count = await ingestor.IngestStoreAsync(480, runId, today);
        Assert.Equal(1, count);

        var file = ActualGameSearch.Worker.Storage.DataLakePaths.Bronze.Store(tmp, today, runId, 480);
        Assert.True(File.Exists(file));

        using var fs = File.OpenRead(file);
        using var gz = new GZipStream(fs, CompressionMode.Decompress);
        using var doc = await JsonDocument.ParseAsync(gz);
        Assert.True(doc.RootElement.TryGetProperty("data", out _));
    }
}
