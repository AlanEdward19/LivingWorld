using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using LivingWorld.Domain;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Snapshot;

/// <summary>Hash canônico incremental por entidade (PERF-12).</summary>
public static class IncrementalHasher
{
    private static readonly HashSet<string> HotProperties = new(StringComparer.Ordinal)
    {
        nameof(WorldState.CurrentDate),
        nameof(WorldState.PendingEvents),
        nameof(WorldState.RngStreams),
        nameof(WorldState.NextEventId),
        nameof(WorldState.NextHistoryEventId),
        nameof(WorldState.MoneyMinted),
        nameof(WorldState.MoneyDestroyed),
        nameof(WorldState.Relationships),
        nameof(WorldState.Households),
        nameof(WorldState.Workplaces),
        nameof(WorldState.Buildings),
        nameof(WorldState.Cities),
        nameof(WorldState.CropBatches),
        nameof(WorldState.ResourceProcesses),
        nameof(WorldState.ExtraordinaryCarriers),
        nameof(WorldState.ExtraordinaryConstructs),
        nameof(WorldState.Fauna),
        nameof(WorldState.Flora),
        nameof(WorldState.CombatEncounters),
        nameof(WorldState.EnvironmentTemperatureAdjustments),
        nameof(WorldState.Facts),
        nameof(WorldState.Reports),
        nameof(WorldState.Books),
        nameof(WorldState.CanonicalMemories),
        nameof(WorldState.RestPlaces),
    };

    private static bool IsHotProperty(string propertyName) =>
        HotProperties.Contains(propertyName) || propertyName.StartsWith("Next", StringComparison.Ordinal);

    private static PropertyInfo[] CanonicalProperties { get; } =
        typeof(WorldState).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<CanonicalAttribute>() is not null)
            .OrderBy(p => p.Name, StringComparer.Ordinal)
            .ToArray();

    public static string CombineIncremental(IReadOnlyDictionary<long, string> perEntityHash)
    {
        var ordered = perEntityHash.OrderBy(kv => kv.Key).Select(kv => kv.Value);
        var payload = string.Join('\n', ordered);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }

    /// <summary>Hash canônico de produção — usa cache de fragmentos quando
    /// <paramref name="useCache"/> é verdadeiro.</summary>
    public static string Compute(WorldState world, bool useCache = true) =>
        HashJson(BuildCanonicalJson(world, useCache));

    /// <summary>Verifica equivalência incremental vs hash recomputado do zero (PERF-12).</summary>
    public static bool MatchesCanonical(WorldState world) =>
        Compute(world, useCache: false) == Compute(world, useCache: true);

    internal static JsonObject BuildCanonicalJson(WorldState world, bool useCache)
    {
        var cache = world.CanonicalHashCache;
        var obj = new JsonObject();
        foreach (var prop in CanonicalProperties)
        {
            obj[prop.Name] = prop.Name == nameof(WorldState.Npcs)
                ? BuildNpcsNode(world, useCache)
                : GetPropertyNode(world, prop, useCache);
        }

        return obj;
    }

    private static JsonNode? GetPropertyNode(WorldState world, PropertyInfo prop, bool useCache)
    {
        if (IsHotProperty(prop.Name))
            return SerializeProperty(world, prop);

        var cache = world.CanonicalHashCache;
        if (useCache && cache.TryGetPropertyNode(prop.Name, out var cached))
            return cached?.DeepClone();

        var node = SerializeProperty(world, prop);
        if (useCache)
            cache.StorePropertyNode(prop.Name, node?.DeepClone());
        return node;
    }

    private static JsonNode? SerializeProperty(WorldState world, PropertyInfo prop)
    {
        var value = prop.GetValue(world);
        return JsonSerializer.SerializeToNode(value, prop.PropertyType, WorldSnapshot.SnapshotJsonOptions);
    }

    private static JsonNode BuildNpcsNode(WorldState world, bool useCache)
    {
        var cache = world.CanonicalHashCache;
        var npcs = world.Npcs;
        if (useCache && cache.TryGetNpcsArray(npcs.Count, out var cachedArray))
            return cachedArray!.DeepClone();

        var array = new JsonArray();
        foreach (var npc in npcs)
        {
            var id = npc.Id.Value;
            if (useCache && cache.TryGetNpcNode(id, out var cachedNpc))
            {
                array.Add(cachedNpc!.DeepClone());
                continue;
            }

            var node = JsonSerializer.SerializeToNode(npc, WorldSnapshot.SnapshotJsonOptions)!;
            array.Add(node);
            if (useCache)
                cache.StoreNpcNode(id, node.DeepClone());
        }

        if (useCache)
            cache.StoreNpcsArray(npcs.Count, array.DeepClone());
        return array;
    }

    private static string HashJson(JsonObject json)
    {
        var bytes = Encoding.UTF8.GetBytes(json.ToJsonString(WorldSnapshot.SnapshotJsonOptions));
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }
}
