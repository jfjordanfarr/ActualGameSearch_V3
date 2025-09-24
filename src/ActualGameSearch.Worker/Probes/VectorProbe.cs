using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using ActualGameSearch.Core.Services;

namespace ActualGameSearch.Worker.Probes;

public static class VectorProbe
{
    public static async Task TryProbeAsync(Container container, string dbName, string containerName, string distanceFn, float[] vec, CosmosVectorQueryHelper.DistanceFormPreference pref, string? fieldPath = null, CancellationToken ct = default)
    {
        var resolved = await CosmosVectorQueryHelper.GetOrResolveFormAsync(container, dbName, containerName, distanceFn, vec, pref, ct, fieldPath);
        // Attempt a single run with resolved form; if a NotSupported occurs, try the other form once, then stop.
        await TryOnceAsync(container, vec, distanceFn, resolved, fieldPath, ct, alreadyRetried: false);
    }

    private static async Task TryOnceAsync(Container container, float[] vec, string distanceFn, CosmosVectorQueryHelper.DistanceFormResolved form, string? fieldPath, CancellationToken ct, bool alreadyRetried)
    {
        var expr = CosmosVectorQueryHelper.VectorDistanceExpr("@e", distanceFn, form, fieldPath);
        var sql = $"SELECT TOP 5 c.id, c.gameTitle, {expr} AS sim FROM c ORDER BY {expr}";
        var q = new QueryDefinition(sql).WithParameter("@e", vec);
        try
        {
            using var it = container.GetItemQueryIterator<dynamic>(q, requestOptions: new QueryRequestOptions{ MaxConcurrency = 1 });
            if (it.HasMoreResults)
            {
                var page = await it.ReadNextAsync(ct);
                foreach (var doc in page)
                {
                    Console.WriteLine($"  {doc.gameTitle ?? (string)doc.id}  sim={doc.sim}");
                }
            }
            Console.WriteLine($"Top 5 semantic matches for probe ({(form == CosmosVectorQueryHelper.DistanceFormResolved.ThreeArg ? "3-arg" : "2-arg")})");
        }
        catch (CosmosException ex)
        {
            var msg = ex.Message ?? string.Empty;
            var twoArgNotSupported = msg.Contains("Not supported function call VECTORDISTANCE with 2 arguments", StringComparison.OrdinalIgnoreCase);
            var threeArgNotSupported = msg.Contains("Not supported function call VECTORDISTANCE with 3 arguments", StringComparison.OrdinalIgnoreCase);
            if ((twoArgNotSupported || threeArgNotSupported) && !alreadyRetried)
            {
                var alt = form == CosmosVectorQueryHelper.DistanceFormResolved.TwoArg ? CosmosVectorQueryHelper.DistanceFormResolved.ThreeArg : CosmosVectorQueryHelper.DistanceFormResolved.TwoArg;
                Console.WriteLine($"VectorDistance form '{(form == CosmosVectorQueryHelper.DistanceFormResolved.ThreeArg ? "3-arg" : "2-arg")}' not supported; retrying once with '{(alt == CosmosVectorQueryHelper.DistanceFormResolved.ThreeArg ? "3-arg" : "2-arg")}'.");
                await TryOnceAsync(container, vec, distanceFn, alt, fieldPath, ct, alreadyRetried: true);
                return;
            }
            Console.WriteLine($"Vector probe failed: {ex.Message}");
        }
    }
}
