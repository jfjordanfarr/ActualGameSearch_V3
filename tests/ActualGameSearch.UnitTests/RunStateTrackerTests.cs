using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ActualGameSearch.Worker.Storage;
using Xunit;

namespace ActualGameSearch.UnitTests;

public class RunStateTrackerTests
{
    [Fact]
    public async Task Resume_FromLastSuccess_NoDuplicates()
    {
        var runId = "run-resume-test";
        var tracker = new RunStateTracker(runId);
        var allAppIds = Enumerable.Range(1, 10).ToList();

        // Simulate processing first 4 successfully
        foreach (var id in allAppIds.Take(4))
        {
            await tracker.MarkStartedAsync(id);
            await tracker.MarkSucceededAsync(id);
        }

        // Now get remaining in random-without-replacement order and mark succeed
        var remaining = new HashSet<int>(allAppIds.Skip(4));
        var seen = new HashSet<int>();
        await foreach (var next in tracker.GetPendingShuffledAsync(allAppIds))
        {
            Assert.DoesNotContain(next, seen);
            seen.Add(next);
            Assert.Contains(next, remaining);
            await tracker.MarkStartedAsync(next);
            await tracker.MarkSucceededAsync(next);
        }

        Assert.Equal(remaining.Count, seen.Count);
        // Verify tracker reports no pending after completion
        var remainingAfter = new List<int>();
        await foreach (var next in tracker.GetPendingShuffledAsync(allAppIds))
        {
            remainingAfter.Add(next);
        }
        Assert.Empty(remainingAfter);
    }

    [Fact]
    public async Task Idempotency_MarkSucceededTwice_NoError()
    {
        var runId = "run-idem";
        var tracker = new RunStateTracker(runId);
        await tracker.MarkStartedAsync(42);
        await tracker.MarkSucceededAsync(42);
        await tracker.MarkSucceededAsync(42);
        await foreach (var next in tracker.GetPendingShuffledAsync(new[] { 42 }))
        {
            Assert.Fail("No pending expected for already succeeded item");
        }
    }

    [Fact]
    public async Task RandomWithoutReplacement_ReasonableDistribution()
    {
        var runId = "run-rwr";
        var tracker = new RunStateTracker(runId);
        var items = Enumerable.Range(1, 100).ToArray();

        // Draw order twice; ensure not identical (probabilistic, but extremely unlikely)
        var order1 = await CollectAsync(tracker.GetPendingShuffledAsync(items));
        // Reset tracker state to simulate a new draw without successes
        tracker = new RunStateTracker(runId + "-2");
        var order2 = await CollectAsync(tracker.GetPendingShuffledAsync(items));

        Assert.NotEqual(order1, order2);
        Assert.Equal(items.Length, order1.Distinct().Count());
        Assert.Equal(items.Length, order2.Distinct().Count());
    }

    private static async Task<List<int>> CollectAsync(IAsyncEnumerable<int> source)
    {
        var list = new List<int>();
        await foreach (var i in source)
            list.Add(i);
        return list;
    }
}
