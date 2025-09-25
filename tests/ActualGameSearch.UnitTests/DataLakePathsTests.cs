using System;
using System.IO;
using ActualGameSearch.Worker.Storage;
using Xunit;

namespace ActualGameSearch.UnitTests;

public class DataLakePathsTests
{
    [Fact]
    public void BuildBronzeReviewPath_FormatsCorrectly()
    {
        var root = "/tmp/datalake";
        var runId = "run-abc123";
        var date = new DateTime(2025, 03, 14);
        var appId = 570; // Dota 2
        var page = 3;

        var p = DataLakePaths.Bronze.ReviewsPage(root, date, runId, appId, page);

        // Expected: bronze/reviews/yyyy/MM/dd/runId/appid={id}/page={n}.json.gz
        var expected = Path.Combine(root,
            "bronze", "reviews",
            "2025", "03", "14",
            runId,
            $"appid={appId}",
            $"page={page}.json.gz");

        Assert.Equal(expected, p);
        Assert.EndsWith(
            Path.Combine($"appid={appId}", $"page={page}.json.gz"), p);
    }

    [Fact]
    public void BuildBronzeStorePath_FormatsCorrectly()
    {
        var root = "/tmp/datalake";
        var runId = "run-abc123";
        var date = new DateTime(2025, 01, 02);
        var appId = 12345;

        var p = DataLakePaths.Bronze.Store(root, date, runId, appId);
        var expected = Path.Combine(root,
            "bronze", "store",
            "2025", "01", "02",
            runId,
            $"appid={appId}.json.gz");

        Assert.Equal(expected, p);
        Assert.EndsWith($"appid={appId}.json.gz", p);
    }

    [Fact]
    public void BuildBronzeNewsPath_FormatsCorrectly()
    {
        var root = "/tmp/datalake";
        var runId = "run-abc123";
        var date = new DateTime(2024, 12, 31);
        var appId = 480; // Spacewar
        var page = 1;

        var p = DataLakePaths.Bronze.NewsPage(root, date, runId, appId, page);
        var expected = Path.Combine(root,
            "bronze", "news",
            "2024", "12", "31",
            runId,
            $"appid={appId}",
            $"page={page}.json.gz");

        Assert.Equal(expected, p);
    }

    [Fact]
    public void BuildBronzeCatalogPath_FormatsCorrectly()
    {
        var root = "/tmp/datalake";
        var runId = "run-abc123";
        var date = new DateTime(2025, 05, 09);

        var p = DataLakePaths.Bronze.Catalog(root, date, runId);
        var expected = Path.Combine(root,
            "bronze", "catalog",
            "2025", "05", "09",
            runId,
            "catalog.json.gz");

        Assert.Equal(expected, p);
    }

    [Fact]
    public void BuildBronzeManifestPath_FormatsCorrectly()
    {
        var root = "/tmp/datalake";
        var runId = "run-abc123";

        var p = DataLakePaths.Bronze.Manifest(root, runId);
        var expected = Path.Combine(root, "bronze", "manifests", $"{runId}.manifest.json");
        Assert.Equal(expected, p);
    }

    [Fact]
    public void BuildSilverParquetPath_FormatsCorrectly()
    {
        var root = "/tmp/datalake";
        var runId = "run-xyz";
        var yyyymmdd = "20250314";
        var file = "games-0001.parquet";
        var p = DataLakePaths.Silver.Dataset(root, "games", yyyymmdd, file);
        var expected = Path.Combine(root, "silver", "games", $"partition={yyyymmdd}", file);
        Assert.Equal(expected, p);

        var manifest = DataLakePaths.Silver.Manifest(root, runId);
        var expectedManifest = Path.Combine(root, "silver", "manifests", $"{runId}.manifest.json");
        Assert.Equal(expectedManifest, manifest);
    }

    [Fact]
    public void BuildGoldParquetPath_FormatsCorrectly()
    {
        var root = "/tmp/datalake";
        var runId = "run-xyz";
        var yyyymmdd = "20250314";
        var file = "candidates-0001.parquet";
        var p = DataLakePaths.Gold.Dataset(root, "candidates", yyyymmdd, file);
        var expected = Path.Combine(root, "gold", "candidates", $"partition={yyyymmdd}", file);
        Assert.Equal(expected, p);

        var manifest = DataLakePaths.Gold.Manifest(root, runId);
        var expectedManifest = Path.Combine(root, "gold", "manifests", $"{runId}.manifest.json");
        Assert.Equal(expectedManifest, manifest);
    }
}
