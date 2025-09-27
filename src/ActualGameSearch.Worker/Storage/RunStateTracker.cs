using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace ActualGameSearch.Worker.Storage;

/// <summary>
/// Tracks per-run item states (started/succeeded) and yields pending items in random-without-replacement order.
/// Implementation is in-memory for now; file-backed persistence can be added later.
/// </summary>
public class RunStateTracker
{
    private readonly string _runId;
    private readonly ConcurrentDictionary<int, ItemState> _state = new();
    private readonly string? _persistPath;
    private readonly object _persistLock = new();
    private enum ItemState { Pending = 0, Started = 1, Succeeded = 2 }

    public RunStateTracker(string runId, string? persistPath = null)
    {
        _runId = runId;
        _persistPath = persistPath;
        if (!string.IsNullOrWhiteSpace(_persistPath) && File.Exists(_persistPath))
        {
            try
            {
                var json = File.ReadAllText(_persistPath);
                var map = System.Text.Json.JsonSerializer.Deserialize<Dictionary<int, string>>(json);
                if (map != null)
                {
                    foreach (var kv in map)
                    {
                        _state[kv.Key] = kv.Value switch
                        {
                            "S" => ItemState.Succeeded,
                            "P" => ItemState.Pending,
                            _ => ItemState.Started
                        };
                    }
                }
            }
            catch { /* ignore corrupt state; treat as fresh */ }
        }
    }

    public ValueTask MarkStartedAsync(int itemId)
    {
        _state.AddOrUpdate(itemId, ItemState.Started, (_, __) => ItemState.Started);
        PersistSafe();
        return ValueTask.CompletedTask;
    }

    public ValueTask MarkSucceededAsync(int itemId)
    {
        _state.AddOrUpdate(itemId, ItemState.Succeeded, (_, __) => ItemState.Succeeded);
        PersistSafe();
        return ValueTask.CompletedTask;
    }

    public async IAsyncEnumerable<int> GetPendingShuffledAsync(IEnumerable<int> allItems, [EnumeratorCancellation] System.Threading.CancellationToken cancellationToken = default)
    {
        // Any item not explicitly marked Succeeded is considered pending; skip Started to avoid duplicates when resuming.
        var pending = allItems.Where(id => _state.GetValueOrDefault(id) != ItemState.Succeeded).ToList();
        foreach (var id in Shuffle(pending))
        {
            cancellationToken.ThrowIfCancellationRequested();
            // If it transitioned to Succeeded while iterating, skip
            if (_state.GetValueOrDefault(id) == ItemState.Succeeded) continue;
            yield return id;
            await Task.Yield();
        }
    }

    private IEnumerable<int> Shuffle(IList<int> list)
    {
        if (list.Count <= 1) return list.ToArray();

        // Derive a pseudo-random seed combining runId hash and wall time to reduce identical sequences across runs
        int seed = _runId.GetHashCode() ^ Environment.TickCount;
        var rng = new Random(seed);
        // Simpler, safe shuffle suitable for our needs here
        return list.OrderBy(_ => rng.Next()).ToArray();
    }

    private void PersistSafe()
    {
        if (string.IsNullOrWhiteSpace(_persistPath)) return;
        try
        {
            // Snapshot into a compact dictionary
            var map = _state.ToDictionary(kv => kv.Key, kv => kv.Value == ItemState.Succeeded ? "S" : (kv.Value == ItemState.Started ? "T" : "P"));
            var json = System.Text.Json.JsonSerializer.Serialize(map);
            var dir = Path.GetDirectoryName(_persistPath)!;
            Directory.CreateDirectory(dir);
            var tmp = _persistPath + ".tmp";
            lock (_persistLock)
            {
                File.WriteAllText(tmp, json);
                if (File.Exists(_persistPath)) File.Delete(_persistPath);
                File.Move(tmp, _persistPath);
            }
        }
        catch { /* best-effort */ }
    }
}
