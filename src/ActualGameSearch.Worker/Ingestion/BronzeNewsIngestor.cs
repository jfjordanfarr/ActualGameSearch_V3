using System.IO.Compression;
using System.Text.Json;
using ActualGameSearch.Worker.Services;
using ActualGameSearch.Worker.Storage;

namespace ActualGameSearch.Worker.Ingestion;

public class BronzeNewsIngestor
{
    private readonly ISteamClient _steam;
    private readonly string _dataRoot;
    private readonly JsonSerializerOptions _json = new() { WriteIndented = false };

    public BronzeNewsIngestor(ISteamClient steam, string dataRoot)
    {
        _steam = steam;
        _dataRoot = dataRoot;
    }

    public async Task<int> IngestNewsAsync(int appId, string runId, DateTime today, int count = 20, string? tags = null, CancellationToken ct = default)
    {
        var payload = await _steam.GetNewsForAppAsync(appId, count: count, tags: tags, ct: ct);
        if (payload?.appnews?.newsitems is null || payload.appnews.newsitems.Length == 0) return 0;

        // For symmetry with reviews, still write a single page file (page=1). If later pagination is needed, loop similarly.
        var outPath = DataLakePaths.Bronze.NewsPage(_dataRoot, today, runId, appId, page: 1);
        Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
        using var fs = File.Create(outPath);
        using var gz = new GZipStream(fs, CompressionLevel.SmallestSize);
        await JsonSerializer.SerializeAsync(gz, payload, _json, ct);
        return payload.appnews.newsitems.Length;
    }
}
