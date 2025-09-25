using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using ActualGameSearch.Worker.Storage;
using Xunit;

namespace ActualGameSearch.UnitTests;

public class ManifestWriterTests
{
    [Fact]
    public async Task WritesBronzeManifest_WithCountsErrorsAndInput()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "agsv3-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmp);
        var runId = "run-manifest-1";
        var input = new Dictionary<string, string>
        {
            ["concurrency"] = "4",
            ["reviewCapBronze"] = "10"
        };

        var mw = new ManifestWriter(tmp, runId, "bronze", input);
        mw.Start();
        mw.RecordItem("reviewsPages", 5);
        mw.RecordItem("storePages", 10);
        mw.RecordError(new InvalidOperationException());
        mw.RecordError(new InvalidOperationException());
        mw.Finish();
        await mw.SaveAsync();

        var expectedPath = Path.Combine(tmp, "bronze", "manifests", $"{runId}.manifest.json");
        Assert.True(File.Exists(expectedPath));

        var json = await File.ReadAllTextAsync(expectedPath);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal(runId, root.GetProperty("runId").GetString());
        Assert.Equal("bronze", root.GetProperty("layer").GetString());
        Assert.True(root.GetProperty("durationMs").GetInt64() >= 0);

        var counts = root.GetProperty("counts");
        Assert.Equal(5, counts.GetProperty("reviewsPages").GetInt32());
        Assert.Equal(10, counts.GetProperty("storePages").GetInt32());

        var errors = root.GetProperty("errors");
        Assert.Equal(2, errors.GetProperty("InvalidOperationException").GetInt32());

        var inputEl = root.GetProperty("input");
        Assert.Equal("4", inputEl.GetProperty("concurrency").GetString());
        Assert.Equal("10", inputEl.GetProperty("reviewCapBronze").GetString());
    }
}
