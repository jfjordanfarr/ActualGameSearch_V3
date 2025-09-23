using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;

namespace ActualGameSearch.Core.Services;

public static class CosmosVectorQueryHelper
{
    public enum DistanceFormPreference { Auto, TwoArg, ThreeArg }
    public enum DistanceFormResolved { TwoArg, ThreeArg }

    private static readonly ConcurrentDictionary<string, DistanceFormResolved> Cache = new();

    private static string CacheKey(string database, string container, string distanceFn)
        => $"{database}/{container}:{distanceFn.ToLowerInvariant()}";

    public static string VectorDistanceExpr(string vectorParamName, string distanceFn, DistanceFormResolved form)
        => form == DistanceFormResolved.ThreeArg
            ? $"VectorDistance(c.vector, {vectorParamName}, '{NormalizeDistanceFn(distanceFn)}')"
            : $"VectorDistance(c.vector, {vectorParamName})";

    private static string NormalizeDistanceFn(string fn)
        => fn switch
        {
            "euclidean" => "euclidean",
            "dotproduct" or "dot_product" => "dotproduct",
            _ => "cosine"
        };

    public static DistanceFormPreference ParsePreference(string? pref)
        => pref?.Trim().ToLowerInvariant() switch
        {
            "two-arg" or "twoarg" or "2" => DistanceFormPreference.TwoArg,
            "three-arg" or "threearg" or "3" => DistanceFormPreference.ThreeArg,
            _ => DistanceFormPreference.Auto
        };

    public static async Task<DistanceFormResolved> GetOrResolveFormAsync(
        Container container,
        string database,
        string containerName,
        string distanceFn,
        float[] probeVector,
        DistanceFormPreference preference,
        CancellationToken ct = default)
    {
        var key = CacheKey(database, containerName, distanceFn);
        if (preference == DistanceFormPreference.TwoArg)
            return Cache.GetOrAdd(key, DistanceFormResolved.TwoArg);
        if (preference == DistanceFormPreference.ThreeArg)
            return Cache.GetOrAdd(key, DistanceFormResolved.ThreeArg);

        if (Cache.TryGetValue(key, out var cached)) return cached;

        // Probe: prefer 3-arg first, fall back to 2-arg on 400/500 NotSupported
        if (await ProbeAsync(container, probeVector, distanceFn, threeArg: true, ct))
        {
            Cache[key] = DistanceFormResolved.ThreeArg;
            return DistanceFormResolved.ThreeArg;
        }
        // else try 2-arg
        if (await ProbeAsync(container, probeVector, distanceFn, threeArg: false, ct))
        {
            Cache[key] = DistanceFormResolved.TwoArg;
            return DistanceFormResolved.TwoArg;
        }

        // If both fail for some other reason, default to TwoArg to honor policy metric
        Cache[key] = DistanceFormResolved.TwoArg;
        return DistanceFormResolved.TwoArg;
    }

    private static async Task<bool> ProbeAsync(Container container, float[] vec, string distanceFn, bool threeArg, CancellationToken ct)
    {
        try
        {
            var expr = threeArg
                ? $"VectorDistance(c.vector, @e, '{NormalizeDistanceFn(distanceFn)}')"
                : "VectorDistance(c.vector, @e)";
            // Cheap probe: order by expression, request TOP 1
            var sql = $"SELECT TOP 1 c.id FROM c ORDER BY {expr}";
            var q = new QueryDefinition(sql).WithParameter("@e", vec);
            using var it = container.GetItemQueryIterator<dynamic>(q, requestOptions: new QueryRequestOptions { MaxConcurrency = 1 });
            if (it.HasMoreResults)
            {
                var _ = await it.ReadNextAsync(ct); // success means this form is supported
                return true;
            }
            return true; // no results also implies the form was accepted
        }
        catch (CosmosException ex) when ((int)ex.StatusCode == 400 || (int)ex.StatusCode == 500)
        {
            // Likely NotSupported function call for this arg count
            return false;
        }
        catch
        {
            return false;
        }
    }
}
