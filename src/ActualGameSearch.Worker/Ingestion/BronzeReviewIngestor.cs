using System.IO.Compression;
using System.Text.Json;
using ActualGameSearch.Worker.Services;
using ActualGameSearch.Worker.Storage;
using ActualGameSearch.Worker.Processing;

namespace ActualGameSearch.Worker.Ingestion;

public class BronzeReviewIngestor
{
    private readonly ISteamClient _steam;
    private readonly string _dataRoot;
    private readonly int _capPerApp;
    private readonly JsonSerializerOptions _json = new() { WriteIndented = false };

    public BronzeReviewIngestor(ISteamClient steam, string dataRoot, int capPerApp)
    {
        _steam = steam;
        _dataRoot = dataRoot;
        _capPerApp = Math.Max(1, capPerApp);
    }

    public async Task<int> IngestReviewsAsync(int appId, string runId, DateTime today, CancellationToken ct = default)
    {
        var total = 0;
        var page = 1;
        string cursor = "*";
        int throttleBackoffSeconds = 2;
        while (total < _capPerApp)
        {
            var remaining = _capPerApp - total;
            var perPage = Math.Min(100, remaining);
            var (payload, status) = await _steam.GetReviewsPageAsync(appId, cursor: cursor, perPage: perPage, ct: ct);
            if ((int)status == 429)
            {
                // Too many requests – back off and retry same cursor/page without advancing
                var delay = TimeSpan.FromSeconds(Math.Min(60, throttleBackoffSeconds) + Random.Shared.NextDouble());
                try { await Task.Delay(delay, ct); } catch { }
                throttleBackoffSeconds = Math.Min(60, throttleBackoffSeconds * 2); // exponential backoff up to 60s
                continue;
            }
            if (payload?.reviews is null || payload.reviews.Length == 0) break;

            var outPath = DataLakePaths.Bronze.ReviewsPage(_dataRoot, today, runId, appId, page);
            Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
            using (var fs = File.Create(outPath))
            using (var gz = new GZipStream(fs, CompressionLevel.SmallestSize))
            using (var writer = new Utf8JsonWriter(gz))
            {
                writer.WriteStartObject();
                writer.WriteNumber("success", payload.success);
                writer.WritePropertyName("reviews");
                writer.WriteStartArray();
                foreach (var r in payload.reviews)
                {
                    var elem = JsonSerializer.SerializeToElement(r, _json);
                    var sanitized = ReviewSanitizer.Sanitize(elem);
                    sanitized.WriteTo(writer);
                }
                writer.WriteEndArray();
                if (payload.cursor is not null)
                    writer.WriteString("cursor", payload.cursor);
                writer.WriteEndObject();
            }

            total += payload.reviews.Length;
            page++;
            cursor = payload.cursor ?? cursor;
            throttleBackoffSeconds = 2; // reset after a successful page
            // Steam repeats the same cursor when done; stop if no progress
            if (payload.reviews.Length < perPage) break;
        }
        return total;
    }
}
