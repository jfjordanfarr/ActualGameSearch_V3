using System;
using System.IO;

namespace ActualGameSearch.Worker.Storage;

/// <summary>
/// Centralized helpers for partitioned paths across bronze/silver/gold layers.
/// Follows contracts in specs/003-path-to-persistence/contracts/worker-cli.md.
/// </summary>
public static class DataLakePaths
{
    public static class Bronze
    {
        public static string ReviewsPage(string root, DateTime date, string runId, int appId, int page)
        {
            return Path.Combine(root,
                "bronze", "reviews",
                date.Year.ToString("D4"),
                date.Month.ToString("D2"),
                date.Day.ToString("D2"),
                runId,
                $"appid={appId}",
                $"page={page}.json.gz");
        }

        public static string Store(string root, DateTime date, string runId, int appId)
        {
            return Path.Combine(root,
                "bronze", "store",
                date.Year.ToString("D4"),
                date.Month.ToString("D2"),
                date.Day.ToString("D2"),
                runId,
                $"appid={appId}.json.gz");
        }

        public static string NewsPage(string root, DateTime date, string runId, int appId, int page)
        {
            return Path.Combine(root,
                "bronze", "news",
                date.Year.ToString("D4"),
                date.Month.ToString("D2"),
                date.Day.ToString("D2"),
                runId,
                $"appid={appId}",
                $"page={page}.json.gz");
        }

        public static string Catalog(string root, DateTime date, string runId)
        {
            return Path.Combine(root,
                "bronze", "catalog",
                date.Year.ToString("D4"),
                date.Month.ToString("D2"),
                date.Day.ToString("D2"),
                runId,
                "catalog.json.gz");
        }

        public static string Manifest(string root, string runId)
        {
            return Path.Combine(root, "bronze", "manifests", $"{runId}.manifest.json");
        }

        public static string LogsDir(string root, DateTime date, string runId)
        {
            return Path.Combine(root,
                "bronze", "logs",
                date.Year.ToString("D4"),
                date.Month.ToString("D2"),
                date.Day.ToString("D2"),
                runId);
        }

        public static string ConsoleLog(string root, DateTime date, string runId)
        {
            return Path.Combine(LogsDir(root, date, runId), "worker.console.log");
        }

        public static string ReportsDir(string root, DateTime date, string runId)
        {
            return Path.Combine(root,
                "bronze", "reports",
                date.Year.ToString("D4"),
                date.Month.ToString("D2"),
                date.Day.ToString("D2"),
                runId);
        }

        public static string SanityReport(string root, DateTime date, string runId)
        {
            return Path.Combine(ReportsDir(root, date, runId), "sanity.json");
        }
    }

    public static class Silver
    {
        public static string Dataset(string root, string datasetName, string yyyymmdd, string fileName)
        {
            return Path.Combine(root, "silver", datasetName, $"partition={yyyymmdd}", fileName);
        }

        public static string Manifest(string root, string runId)
        {
            return Path.Combine(root, "silver", "manifests", $"{runId}.manifest.json");
        }
    }

    public static class Gold
    {
        public static string Dataset(string root, string datasetName, string yyyymmdd, string fileName)
        {
            return Path.Combine(root, "gold", datasetName, $"partition={yyyymmdd}", fileName);
        }

        public static string Manifest(string root, string runId)
        {
            return Path.Combine(root, "gold", "manifests", $"{runId}.manifest.json");
        }
    }
}
