using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace ActualGameSearch.Worker.Storage;

public class ManifestWriter
{
    private readonly string _root;
    private readonly string _runId;
    private readonly string _layer; // bronze|silver|gold
    private readonly IDictionary<string, string> _input;
    private readonly ConcurrentDictionary<string, int> _counts = new();
    private readonly ConcurrentDictionary<string, int> _errors = new();
    private DateTimeOffset? _start;
    private DateTimeOffset? _end;

    public ManifestWriter(string root, string runId, string layer, IDictionary<string, string> input)
    {
        _root = root;
        _runId = runId;
        _layer = layer;
        _input = input;
    }

    public void Start() => _start = DateTimeOffset.UtcNow;
    public void Finish() => _end = DateTimeOffset.UtcNow;

    public void RecordItem(string name, int count = 1)
    {
        _counts.AddOrUpdate(name, count, (_, existing) => existing + count);
    }

    public void RecordError(Exception ex)
    {
        var key = ex.GetType().Name;
        _errors.AddOrUpdate(key, 1, (_, existing) => existing + 1);
    }

    public async Task SaveAsync()
    {
        var durationMs = (_start.HasValue && _end.HasValue) ? (long)(_end.Value - _start.Value).TotalMilliseconds : 0L;
        var manifestObj = new
        {
            runId = _runId,
            layer = _layer,
            startedAt = _start,
            finishedAt = _end,
            durationMs,
            counts = _counts,
            errors = _errors,
            input = _input
        };

        var json = JsonSerializer.Serialize(manifestObj, new JsonSerializerOptions { WriteIndented = true });
        var outPath = _layer.ToLowerInvariant() switch
        {
            "bronze" => DataLakePaths.Bronze.Manifest(_root, _runId),
            "silver" => DataLakePaths.Silver.Manifest(_root, _runId),
            "gold" => DataLakePaths.Gold.Manifest(_root, _runId),
            _ => Path.Combine(_root, "manifests", $"{_runId}.manifest.json")
        };

        Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
        await File.WriteAllTextAsync(outPath, json);
    }
}
