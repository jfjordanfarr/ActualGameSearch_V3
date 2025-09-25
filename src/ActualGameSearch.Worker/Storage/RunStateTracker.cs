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
    private enum ItemState { Pending = 0, Started = 1, Succeeded = 2 }

    public RunStateTracker(string runId)
    {
        _runId = runId;
    }

    public ValueTask MarkStartedAsync(int itemId)
    {
        _state.AddOrUpdate(itemId, ItemState.Started, (_, __) => ItemState.Started);
        return ValueTask.CompletedTask;
    }

    public ValueTask MarkSucceededAsync(int itemId)
    {
        _state.AddOrUpdate(itemId, ItemState.Succeeded, (_, __) => ItemState.Succeeded);
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
}
