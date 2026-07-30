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
        Converters =
        {
            new JsonStringEnumConverter(), new ResourceTypeKeyConverter(), new ResourceLocationKeyConverter(),
            new RelationshipKeyConverter(), new RelationshipDeltaKeyConverter(),
        },
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

    /// <summary>Mesmo resultado que <see cref="CanonicalHash"/>; caminho reservado para
    /// recomputar só NPCs sujos antes do <see cref="Hash"/> (PERF-12).</summary>
    internal static string CanonicalHashFromEntityParts(WorldState world) =>
        CanonicalHash(world);

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
        var needsRules = node["NeedsRules"].Deserialize<NeedsRules>(JsonOptions)!;
        var actionCatalog = node["ActionCatalog"].Deserialize<ActionCatalog>(JsonOptions)!;
        var lifeStageRules = node["LifeStageRules"].Deserialize<LifeStageRules>(JsonOptions)!;
        var rngStreams = node["RngStreams"].Deserialize<List<RngStreamState>>(JsonOptions)!;
        var pendingEvents = node["PendingEvents"].Deserialize<List<ScheduledEvent>>(JsonOptions)!;
        var nextEventId = node["NextEventId"]!.GetValue<long>();
        var exampleCounts = node["ExampleTickCounts"].Deserialize<Dictionary<TickFrequency, long>>(JsonOptions)!;
        var npcs = node["Npcs"].Deserialize<List<Npc>>(JsonOptions)!;
        var households = node["Households"].Deserialize<List<Household>>(JsonOptions)!;
        var nextNpcId = node["NextNpcId"]!.GetValue<long>();
        var nextHouseholdId = node["NextHouseholdId"]!.GetValue<long>();
        var branchId = new BranchId(node["BranchId"]!["Value"]!.GetValue<long>());
        var moneyMinted = new Money(node["MoneyMinted"]!["Amount"]!.GetValue<long>());
        var moneyDestroyed = new Money(node["MoneyDestroyed"]!["Amount"]!.GetValue<long>());
        var economyRules = node["EconomyRules"].Deserialize<EconomyRules>(JsonOptions)!;
        var economyCatalog = node["EconomyCatalog"].Deserialize<EconomyCatalog>(JsonOptions)!;
        var workplaces = node["Workplaces"].Deserialize<List<Workplace>>(JsonOptions)!;
        var nextWorkplaceId = node["NextWorkplaceId"]!.GetValue<long>();
        var familyRules = node["FamilyRules"].Deserialize<FamilyRules>(JsonOptions)!;
        var relationships = node["Relationships"].Deserialize<Dictionary<RelationshipKey, Relationship>>(JsonOptions)!;
        var cities = node["Cities"].Deserialize<List<City>>(JsonOptions)!;
        var buildings = node["Buildings"].Deserialize<List<Building>>(JsonOptions)!;
        var nextBuildingId = node["NextBuildingId"]!.GetValue<long>();
        var cityRules = node["CityRules"].Deserialize<CityRules>(JsonOptions)!;
        var cityCatalog = node["CityCatalog"].Deserialize<CityCatalog>(JsonOptions)!;
        var perfRules = node.TryGetPropertyValue("PerfRules", out var perfNode) && perfNode is not null
            ? perfNode.Deserialize<PerfRules>(JsonOptions)!
            : PerfRules.Default;
        var historyRules = node.TryGetPropertyValue("HistoryRules", out var histNode) && histNode is not null
            ? histNode.Deserialize<HistoryRules>(JsonOptions)!
            : HistoryRules.Disabled;
        var facts = node.TryGetPropertyValue("Facts", out var factsNode) && factsNode is not null
            ? factsNode.Deserialize<List<Fact>>(JsonOptions)!
            : [];
        var nextFactId = node.TryGetPropertyValue("NextFactId", out var nextFactNode) && nextFactNode is not null
            ? nextFactNode.GetValue<long>()
            : 0L;
        var nextReportId = node.TryGetPropertyValue("NextReportId", out var nextReportNode) && nextReportNode is not null
            ? nextReportNode.GetValue<long>()
            : 0L;

        return new WorldState(
            calendar, currentDate, seed, map, populationCatalog, populationRules, needsRules, actionCatalog,
            lifeStageRules, rngStreams, pendingEvents, nextEventId, exampleCounts, npcs, households, nextNpcId,
            nextHouseholdId, branchId, moneyMinted, moneyDestroyed, economyRules, economyCatalog, workplaces,
            nextWorkplaceId, familyRules, relationships, cities, buildings, nextBuildingId, cityRules, cityCatalog,
            perfRules, historyRules, facts, nextFactId, nextReportId);
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
