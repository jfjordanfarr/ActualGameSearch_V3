using System.IO.Compression;
using System.Text.Json;
using ActualGameSearch.Worker.Models;
using ActualGameSearch.Worker.Services;
using ActualGameSearch.Worker.Storage;
using ActualGameSearch.Worker.Configuration;
using Microsoft.Extensions.Options;

namespace ActualGameSearch.Worker.Ingestion;

public class BronzeStoreIngestor
{
    private readonly ISteamClient _steam;
    private readonly string _dataRoot;
    private readonly JsonSerializerOptions _json = new() { WriteIndented = false };
    private readonly CandidacyOptions _candidacyOptions;

    public BronzeStoreIngestor(ISteamClient steam, string dataRoot, IOptions<CandidacyOptions> candidacyOptions)
    {
        _steam = steam;
        _dataRoot = dataRoot;
        _candidacyOptions = candidacyOptions.Value;
    }

    public async Task<int> IngestStoreAsync(int appId, string runId, DateTime today, CancellationToken ct = default)
    {
        var payload = await _steam.GetAppDetailsAsync(appId, ct: ct);
        if (payload is null) return 0;
        if (!payload.TryGetValue(appId.ToString(), out var details) || details is null)
        {
            return 0;
        }

        // Apply Bronze candidacy filter: require configurable minimum total recommendations
        var appDetails = details.data;
        var totalRecommendations = appDetails?.recommendations?.total ?? 0;
        if (totalRecommendations < _candidacyOptions.MinRecommendationsForInclusion)
        {
            // Skip this app - doesn't meet Bronze candidacy threshold
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
