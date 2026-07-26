using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using LivingWorld.Domain;

namespace LivingWorld.Simulation;

/// <summary>Serialização completa do mundo (task 7) e as duas funções de hash (ADR-0006):
/// canônico (o que alimenta decisão) e volátil (o resto). Ambas construídas por reflexão sobre
/// as propriedades públicas de <see cref="WorldState"/> — um campo novo sem
/// <see cref="CanonicalAttribute"/> nem <see cref="VolatileAttribute"/> não entra em nenhum
/// hash, o que o teste de cobertura (LivingWorld.Tests) detecta.</summary>
public static class WorldSnapshot
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
        PropertyNameCaseInsensitive = true,
    };

    private static readonly PropertyInfo[] Properties =
        typeof(WorldState).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .OrderBy(p => p.Name, StringComparer.Ordinal)
            .ToArray();

    /// <summary>Toda propriedade pública, para round-trip completo (Serialize/Deserialize).</summary>
    public static string Serialize(WorldState world) => BuildJson(world, static _ => true).ToJsonString(JsonOptions);

    public static string CanonicalHash(WorldState world) =>
        Hash(BuildJson(world, static p => p.GetCustomAttribute<CanonicalAttribute>() is not null));

    public static string VolatileHash(WorldState world) =>
        Hash(BuildJson(world, static p => p.GetCustomAttribute<VolatileAttribute>() is not null));

    public static WorldState Deserialize(string json)
    {
        var node = JsonNode.Parse(json)!.AsObject();

        var calendar = node["Calendar"].Deserialize<WorldCalendar>(JsonOptions)!;
        var totalHours = node["CurrentDate"]!["TotalHours"]!.GetValue<long>();
        var currentDate = new WorldDate(calendar, totalHours);
        var seed = node["Seed"]!.GetValue<ulong>();
        var map = node["Map"].Deserialize<WorldMap>(JsonOptions)!;
        var populationCatalog = node["PopulationCatalog"].Deserialize<PopulationCatalog>(JsonOptions)!;
        var populationRules = node["PopulationRules"].Deserialize<PopulationRules>(JsonOptions)!;
        var rngStreams = node["RngStreams"].Deserialize<List<RngStreamState>>(JsonOptions)!;
        var pendingEvents = node["PendingEvents"].Deserialize<List<ScheduledEvent>>(JsonOptions)!;
        var nextEventId = node["NextEventId"]!.GetValue<long>();
        var exampleCounts = node["ExampleTickCounts"].Deserialize<Dictionary<TickFrequency, long>>(JsonOptions)!;
        var npcs = node["Npcs"].Deserialize<List<Npc>>(JsonOptions)!;
        var households = node["Households"].Deserialize<List<Household>>(JsonOptions)!;
        var nextNpcId = node["NextNpcId"]!.GetValue<long>();
        var nextHouseholdId = node["NextHouseholdId"]!.GetValue<long>();

        return new WorldState(
            calendar, currentDate, seed, map, populationCatalog, populationRules,
            rngStreams, pendingEvents, nextEventId, exampleCounts, npcs, households, nextNpcId, nextHouseholdId);
    }

    private static JsonObject BuildJson(WorldState world, Func<PropertyInfo, bool> filter)
    {
        var obj = new JsonObject();
        foreach (var prop in Properties)
        {
            if (!filter(prop)) continue;
            var value = prop.GetValue(world);
            obj[prop.Name] = JsonSerializer.SerializeToNode(value, prop.PropertyType, JsonOptions);
        }
        return obj;
    }

    private static string Hash(JsonObject json)
    {
        var bytes = Encoding.UTF8.GetBytes(json.ToJsonString(JsonOptions));
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }

    /// <summary>Exposto para os testes de cobertura/classificação por reflexão.</summary>
    public static IReadOnlyList<PropertyInfo> ReflectedProperties => Properties;
}
