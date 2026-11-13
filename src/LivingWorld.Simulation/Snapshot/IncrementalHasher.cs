using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Snapshot;

/// <summary>Hash canônico incremental por entidade.</summary>
public static class IncrementalHasher
{
    private static readonly HashSet<string> HotProperties = new(StringComparer.Ordinal)
    {
        nameof(WorldState.CurrentDate),
        nameof(WorldState.PendingEvents),
        nameof(WorldState.PendingNpcWakes),
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
        nameof(WorldState.TradeLedger),
        nameof(WorldState.TreasuryLedger),
        nameof(WorldState.Infections),
        nameof(WorldState.Immunities),
        nameof(WorldState.ClimateYears),
        nameof(WorldState.EnvironmentTemperatureAdjustments),
        nameof(WorldState.Technologies),
        nameof(WorldState.Invention),
        nameof(WorldState.Facts),
        nameof(WorldState.Reports),
        nameof(WorldState.Books),
        nameof(WorldState.CanonicalMemories),
        nameof(WorldState.RestPlaces),
    };

    private static bool IsHotProperty(string propertyName) =>
        HotProperties.Contains(propertyName) || propertyName.StartsWith("Next", StringComparison.Ordinal);

    /// <summary>Propriedade canônica cujos bytes ficam no cache até alguém a marcar suja (regras, catálogos, mapa).</summary>
    internal static bool IsCacheable(string propertyName) =>
        !IsHotProperty(propertyName) && propertyName != nameof(WorldState.Npcs);

    private sealed record Canonical(PropertyInfo Property, byte[] HashPrefix, bool OmitsNull, bool Cacheable, bool IsNpcs);

    private static Canonical[] CanonicalProperties { get; } =
        typeof(WorldState).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<CanonicalAttribute>() is not null)
            .OrderBy(p => p.Name, StringComparer.Ordinal)
            .Select(p => new Canonical(
                p, Encoding.UTF8.GetBytes($"\"{p.Name}\":"), WorldSnapshot.OmitsNull(p), IsCacheable(p.Name),
                p.Name == nameof(WorldState.Npcs)))
            .ToArray();

    public static string CombineIncremental(IReadOnlyDictionary<long, string> perEntityHash)
    {
        var ordered = perEntityHash.OrderBy(kv => kv.Key).Select(kv => kv.Value);
        var payload = string.Join('\n', ordered);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }

    /// <summary>Hash canônico de produção: SHA-256 do objeto <c>{"Nome":valor,...}</c> das propriedades canônicas em ordem de
    /// nome, com o JSON compacto do serializador. Com <paramref name="useCache"/>, propriedade fria e NPC limpo saem dos
    /// bytes guardados em vez de serializados de novo; o texto é o mesmo, então o hash também.</summary>
    public static string Compute(WorldState world, bool useCache = true)
    {
        var cache = world.CanonicalHashCache;
        using var scratch = new PooledBufferWriter();
        using var writer = new Utf8JsonWriter(scratch, WorldJsonCapture.WriterOptions);
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        sha.AppendData("{"u8);
        bool first = true;
        foreach (var prop in CanonicalProperties)
        {
            var value = prop.Property.GetValue(world);
            if (prop.OmitsNull && value is null) continue;
            if (!first) sha.AppendData(","u8);
            first = false;
            sha.AppendData(prop.HashPrefix);

            if (prop.IsNpcs) AppendNpcs(sha, world, useCache, scratch, writer);
            else if (useCache && prop.Cacheable && cache.TryGetPropertyBytes(prop.Property.Name, out var cached))
                sha.AppendData(cached);
            else
            {
                var bytes = Serialize(scratch, writer, value, prop.Property.PropertyType);
                sha.AppendData(bytes);
                if (useCache && prop.Cacheable) cache.StorePropertyBytes(prop.Property.Name, bytes.ToArray());
            }
        }

        sha.AppendData("}"u8);
        return Convert.ToHexStringLower(sha.GetHashAndReset());
    }

    /// <summary>Verifica equivalência incremental vs hash recomputado do zero.</summary>
    public static bool MatchesCanonical(WorldState world) =>
        Compute(world, useCache: false) == Compute(world, useCache: true);

    private static ReadOnlySpan<byte> Serialize(PooledBufferWriter scratch, Utf8JsonWriter writer, object? value, Type type)
    {
        scratch.Clear();
        writer.Reset(scratch);
        JsonSerializer.Serialize(writer, value, type, WorldSnapshot.SnapshotJsonOptions);
        writer.Flush();
        return scratch.WrittenSpan;
    }

    private static void AppendNpcs(IncrementalHash sha, WorldState world, bool useCache, PooledBufferWriter scratch, Utf8JsonWriter writer)
    {
        var cache = world.CanonicalHashCache;
        sha.AppendData("["u8);
        bool first = true;
        foreach (var npc in world.Npcs)
        {
            if (!first) sha.AppendData(","u8);
            first = false;
            long id = npc.Id.Value;
            if (useCache && cache.TryGetNpcBytes(id, out var cached))
            {
                sha.AppendData(cached);
                continue;
            }

            var bytes = Serialize(scratch, writer, npc, typeof(Npc));
            sha.AppendData(bytes);
            if (useCache) cache.StoreNpcBytes(id, bytes.ToArray());
        }

        sha.AppendData("]"u8);
    }
}
