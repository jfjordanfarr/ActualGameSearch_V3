using System.IO.Compression;
using System.Text.Json;
using ActualGameSearch.Worker.Services;
using ActualGameSearch.Worker.Storage;

namespace ActualGameSearch.Worker.Ingestion;

public class BronzeStoreIngestor
{
    private readonly ISteamClient _steam;
    private readonly string _dataRoot;
    private readonly JsonSerializerOptions _json = new() { WriteIndented = false };

    public BronzeStoreIngestor(ISteamClient steam, string dataRoot)
    {
        _steam = steam;
        _dataRoot = dataRoot;
    }

    public async Task<int> IngestStoreAsync(int appId, string runId, DateTime today, CancellationToken ct = default)
    {
        var payload = await _steam.GetAppDetailsAsync(appId, ct: ct);
        if (payload is null) return 0;
        if (!payload.TryGetValue(appId.ToString(), out var details) || details is null)
        {
            return 0;
        }

        var outPath = DataLakePaths.Bronze.Store(_dataRoot, today, runId, appId);
        Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
        using var fs = File.Create(outPath);
        using var gz = new GZipStream(fs, CompressionLevel.SmallestSize);
        await JsonSerializer.SerializeAsync(gz, details, _json, ct);
        return 1;
    }
}
