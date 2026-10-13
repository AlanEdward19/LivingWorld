using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Ecology;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Extraordinary;
using LivingWorld.Domain.Fauna;
using LivingWorld.Domain.Flora;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Geography.Map;
using LivingWorld.Domain.Geography.Spatial;
using LivingWorld.Domain.Health;
using LivingWorld.Domain.History;
using LivingWorld.Domain.History.Distortion;
using LivingWorld.Domain.Llm;
using LivingWorld.Domain.Performance;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Extraordinary.Mechanics;
using LivingWorld.Simulation.Scheduling;
using LivingWorld.Simulation.Serialization;
using LivingWorld.Simulation.Snapshot;

namespace LivingWorld.Simulation.Core;

/// <summary>Serialização completa do mundo e as duas funções de hash:
/// canônico (o que alimenta decisão) e volátil (o resto). Ambas construídas por reflexão sobre
/// as propriedades públicas de <see cref="WorldState"/> — um campo novo sem
/// <see cref="CanonicalAttribute"/> nem <see cref="VolatileAttribute"/> não entra em nenhum
/// hash, o que o teste de cobertura (LivingWorld.Tests) detecta.</summary>
public static class WorldSnapshot
{
    internal static JsonSerializerOptions SnapshotJsonOptions { get; } = new()
    {
        Converters =
        {
            new JsonStringEnumConverter(), new ResourceTypeKeyConverter(), new ResourceLocationKeyConverter(),
            new RelationshipKeyConverter(), new RelationshipDeltaKeyConverter(),
            new MapCellJsonConverter(),
        },
        PropertyNameCaseInsensitive = true,
    };

    private static JsonSerializerOptions JsonOptions => SnapshotJsonOptions;

    private static readonly PropertyInfo[] Properties =
        typeof(WorldState).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .OrderBy(p => p.Name, StringComparer.Ordinal)
            .ToArray();

    /// <summary>Toda propriedade pública persistível — exclui <see cref="EphemeralAttribute"/>
    /// (sessão/LOD/cognição reconstruídos ao reidratar).</summary>
    public static string Serialize(WorldState world)
    {
        var json = BuildJson(world, static p =>
            p.GetCustomAttribute<EphemeralAttribute>() is null);
        SnapshotStringInterning.Apply(json);
        return json.ToJsonString(JsonOptions);
    }

    public static string CanonicalHash(WorldState world) =>
        Snapshot.IncrementalHasher.Compute(world, useCache: true);

    /// <summary>Mesmo resultado que <see cref="CanonicalHash"/> via fragmentos por entidade.</summary>
    internal static string CanonicalHashFromEntityParts(WorldState world) =>
        Snapshot.IncrementalHasher.Compute(world, useCache: true);

    public static string VolatileHash(WorldState world) =>
        Hash(BuildJson(world, static p => p.GetCustomAttribute<VolatileAttribute>() is not null));

    public static WorldState Deserialize(string json) => FromNode(JsonNode.Parse(json)!.AsObject());

    /// <summary>Árvore persistível do mundo, sem o interning de strings: é a forma que o codec posicional e os diffs
    /// consomem (a tabela de strings do binário já deduplica).</summary>
    internal static JsonObject BuildPersistentTree(WorldState world) =>
        BuildJson(world, static p => p.GetCustomAttribute<EphemeralAttribute>() is null);

    /// <summary>Reidrata o mundo de uma árvore já em memória (leitor binário), sem passar por texto JSON.</summary>
    internal static WorldState FromNode(JsonObject node)
    {
        SnapshotStringInterning.Resolve(node);

        var calendar = node["Calendar"].Deserialize<WorldCalendar>(JsonOptions)!;
        var totalHours = node["CurrentDate"]!["TotalHours"]!.Deserialize<long>();
        var currentDate = new WorldDate(calendar, totalHours);
        var seed = node["Seed"]!.Deserialize<ulong>();
        var map = node["Map"].Deserialize<WorldMap>(JsonOptions)!;
        var populationCatalog = node["PopulationCatalog"].Deserialize<PopulationCatalog>(JsonOptions)!;
        var populationRules = node["PopulationRules"].Deserialize<PopulationRules>(JsonOptions)!;
        var needsRules = node["NeedsRules"].Deserialize<NeedsRules>(JsonOptions)!;
        var actionCatalog = node["ActionCatalog"].Deserialize<ActionCatalog>(JsonOptions)!;
        var lifeStageRules = node["LifeStageRules"].Deserialize<LifeStageRules>(JsonOptions)!;
        var rngStreams = node["RngStreams"].Deserialize<List<RngStreamState>>(JsonOptions)!;
        var pendingEvents = node["PendingEvents"].Deserialize<List<ScheduledEvent>>(JsonOptions)!;
        var nextEventId = node["NextEventId"]!.Deserialize<long>();
        var nextHistoryEventId = node.TryGetPropertyValue("NextHistoryEventId", out var nextHistoryNode)
            && nextHistoryNode is not null
            ? nextHistoryNode.Deserialize<long>()
            : 0L;
        var exampleCounts = node["ExampleTickCounts"].Deserialize<Dictionary<TickFrequency, long>>(JsonOptions)!;
        var npcs = node["Npcs"].Deserialize<List<Npc>>(JsonOptions)!;
        var households = node["Households"].Deserialize<List<Household>>(JsonOptions)!;
        var nextNpcId = node["NextNpcId"]!.Deserialize<long>();
        var nextHouseholdId = node["NextHouseholdId"]!.Deserialize<long>();
        var branchId = new BranchId(node["BranchId"]!["Value"]!.Deserialize<long>());
        var moneyMinted = new Money(node["MoneyMinted"]!["Amount"]!.Deserialize<long>());
        var moneyDestroyed = new Money(node["MoneyDestroyed"]!["Amount"]!.Deserialize<long>());
        var economyRules = node["EconomyRules"].Deserialize<EconomyRules>(JsonOptions)!;
        var economyCatalog = node["EconomyCatalog"].Deserialize<EconomyCatalog>(JsonOptions)!;
        var workplaces = node["Workplaces"].Deserialize<List<Workplace>>(JsonOptions)!;
        var nextWorkplaceId = node["NextWorkplaceId"]!.Deserialize<long>();
        var familyRules = node["FamilyRules"].Deserialize<FamilyRules>(JsonOptions)!;
        var bodyRules = node.TryGetPropertyValue("BodyRules", out var bodyNode) && bodyNode is not null
            ? bodyNode.Deserialize<BodyRules>(JsonOptions)!
            : BodyRules.Default;
        var relationships = node["Relationships"].Deserialize<Dictionary<RelationshipKey, Relationship>>(JsonOptions)!;
        var cities = node["Cities"].Deserialize<List<City>>(JsonOptions)!;
        var buildings = node["Buildings"].Deserialize<List<Building>>(JsonOptions)!;
        var nextBuildingId = node["NextBuildingId"]!.Deserialize<long>();
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
            ? nextFactNode.Deserialize<long>()
            : 0L;
        var nextReportId = node.TryGetPropertyValue("NextReportId", out var nextReportNode) && nextReportNode is not null
            ? nextReportNode.Deserialize<long>()
            : 0L;
        var reports = node.TryGetPropertyValue("Reports", out var reportsNode) && reportsNode is not null
            ? reportsNode.Deserialize<List<ReportState>>(JsonOptions)!
            : [];
        var books = node.TryGetPropertyValue("Books", out var booksNode) && booksNode is not null
            ? booksNode.Deserialize<List<Book>>(JsonOptions)!
            : [];
        var nextBookId = node.TryGetPropertyValue("NextBookId", out var nextBookNode) && nextBookNode is not null
            ? nextBookNode.Deserialize<long>()
            : 0L;
        var canonicalMemories = node.TryGetPropertyValue("CanonicalMemories", out var canonicalMemoriesNode) && canonicalMemoriesNode is not null
            ? canonicalMemoriesNode.Deserialize<List<NpcMemory>>(JsonOptions)!
            : [];
        var volatileMemories = node.TryGetPropertyValue("VolatileMemories", out var volatileMemoriesNode) && volatileMemoriesNode is not null
            ? volatileMemoriesNode.Deserialize<List<NpcMemory>>(JsonOptions)!
            : [];
        var nextMemoryId = node.TryGetPropertyValue("NextMemoryId", out var nextMemoryIdNode) && nextMemoryIdNode is not null
            ? nextMemoryIdNode.Deserialize<long>()
            : 0L;
        var name = node.TryGetPropertyValue("Name", out var nameNode) && nameNode is not null
            ? nameNode.GetValue<string>()
            : "";
        var portals = node.TryGetPropertyValue("Portals", out var portalsNode) && portalsNode is not null
            ? portalsNode.Deserialize<List<SpatialPortal>>(JsonOptions)!
            : [];
        var restPlaceCatalog = node.TryGetPropertyValue("RestPlaceCatalog", out var restCatalogNode) && restCatalogNode is not null
            ? restCatalogNode.Deserialize<RestPlaceCatalog>(JsonOptions)!
            : RestPlaceCatalog.FromGround(needsRules.HomelessSleepEfficiency);
        var restPlaces = node.TryGetPropertyValue("RestPlaces", out var restPlacesNode) && restPlacesNode is not null
            ? restPlacesNode.Deserialize<List<RestPlace>>(JsonOptions)!
            : [];
        var nextRestPlaceId = node.TryGetPropertyValue("NextRestPlaceId", out var nextRestPlaceNode) && nextRestPlaceNode is not null
            ? nextRestPlaceNode.Deserialize<long>()
            : 0L;
        var resourceCatalog = node.TryGetPropertyValue("ResourceCatalog", out var resourceCatalogNode) && resourceCatalogNode is not null
            ? resourceCatalogNode.Deserialize<ResourceCatalog>(JsonOptions)!
            : ResourceCatalog.Empty;
        var processRecipes = node.TryGetPropertyValue("ProcessRecipes", out var processRecipesNode) && processRecipesNode is not null
            ? processRecipesNode.Deserialize<List<ProcessRecipe>>(JsonOptions)!
            : [];
        var resourceProcesses = node.TryGetPropertyValue("ResourceProcesses", out var resourceProcessesNode) && resourceProcessesNode is not null
            ? resourceProcessesNode.Deserialize<List<ResourceProcess>>(JsonOptions)!
            : [];
        var nextResourceProcessId = node.TryGetPropertyValue("NextResourceProcessId", out var nextProcessNode) && nextProcessNode is not null
            ? nextProcessNode.Deserialize<long>()
            : 0L;
        var cropBatches = node.TryGetPropertyValue("CropBatches", out var cropBatchesNode) && cropBatchesNode is not null
            ? cropBatchesNode.Deserialize<List<CropBatch>>(JsonOptions)!
            : [];
        var nextCropBatchId = node.TryGetPropertyValue("NextCropBatchId", out var nextCropNode) && nextCropNode is not null
            ? nextCropNode.Deserialize<long>()
            : 0L;
        var extraordinary = node.TryGetPropertyValue("Extraordinary", out var extraordinaryNode) && extraordinaryNode is not null
            ? extraordinaryNode.Deserialize<ExtraordinaryScenarioData>(JsonOptions)!
            : ExtraordinaryScenarioData.Disabled;
        var extraordinaryCarriers = node.TryGetPropertyValue("ExtraordinaryCarriers", out var carriersNode) && carriersNode is not null
            ? carriersNode.Deserialize<List<ExtraordinaryCarrierState>>(JsonOptions)!
            : [];
        var extraordinaryConstructs = node.TryGetPropertyValue("ExtraordinaryConstructs", out var constructsNode) && constructsNode is not null
            ? constructsNode.Deserialize<List<ExtraordinaryConstruct>>(JsonOptions)!
            : [];
        var nextExtraordinaryConstructId = node.TryGetPropertyValue(
            "NextExtraordinaryConstructId", out var nextConstructNode) && nextConstructNode is not null
            ? nextConstructNode.Deserialize<long>()
            : 0L;
        var fauna = node.TryGetPropertyValue("Fauna", out var faunaNode) && faunaNode is not null
            ? faunaNode.Deserialize<List<Animal>>(JsonOptions)!
            : [];
        var nextAnimalId = node.TryGetPropertyValue("NextAnimalId", out var nextAnimalNode) && nextAnimalNode is not null
            ? nextAnimalNode.Deserialize<long>()
            : 0L;
        var flora = node.TryGetPropertyValue("Flora", out var floraNode) && floraNode is not null
            ? floraNode.Deserialize<List<Plant>>(JsonOptions)!
            : [];
        var nextPlantId = node.TryGetPropertyValue("NextPlantId", out var nextPlantNode) && nextPlantNode is not null
            ? nextPlantNode.Deserialize<long>()
            : 0L;
        var environmentTemperatureAdjustments =
            node.TryGetPropertyValue("EnvironmentTemperatureAdjustments", out var temperatureNode)
            && temperatureNode is not null
                ? temperatureNode.Deserialize<List<EnvironmentTemperatureAdjustment>>(JsonOptions)!
                : [];
        var animalSpeciesRules = node.TryGetPropertyValue("AnimalSpeciesRules", out var animalRulesNode)
            && animalRulesNode is not null
                ? animalRulesNode.Deserialize<List<AnimalSpeciesRules>>(JsonOptions)!
                : [];
        var plantSpeciesRules = node.TryGetPropertyValue("PlantSpeciesRules", out var plantRulesNode)
            && plantRulesNode is not null
                ? plantRulesNode.Deserialize<List<PlantSpeciesRules>>(JsonOptions)!
                : [];
        var biomeSeasonTemperatureRules =
            node.TryGetPropertyValue("BiomeSeasonTemperatureRules", out var biomeSeasonNode)
            && biomeSeasonNode is not null
                ? biomeSeasonNode.Deserialize<List<BiomeSeasonTemperatureRules>>(JsonOptions)!
                : [];
        var combatEncounters =
            node.TryGetPropertyValue("CombatEncounters", out var combatNode) && combatNode is not null
                ? combatNode.Deserialize<List<CombatEncounter>>(JsonOptions)!
                : [];
        var nextCombatEncounterId =
            node.TryGetPropertyValue("NextCombatEncounterId", out var nextCombatNode)
            && nextCombatNode is not null
                ? nextCombatNode.Deserialize<long>()
                : 0L;
        var combatRules =
            node.TryGetPropertyValue("CombatRules", out var combatRulesNode) && combatRulesNode is not null
                ? combatRulesNode.Deserialize<CombatRules>(JsonOptions)!
                : CombatRules.Default;
        var tradeLedger =
            node.TryGetPropertyValue("TradeLedger", out var tradeNode) && tradeNode is not null
                ? tradeNode.Deserialize<List<TradeAggregate>>(JsonOptions)!
                : [];
        var treasuryLedger =
            node.TryGetPropertyValue("TreasuryLedger", out var treasuryNode) && treasuryNode is not null
                ? treasuryNode.Deserialize<List<TreasuryAggregate>>(JsonOptions)!
                : [];
        var diseaseRules =
            node.TryGetPropertyValue("DiseaseRules", out var diseaseRulesNode) && diseaseRulesNode is not null
                ? diseaseRulesNode.Deserialize<DiseaseRules>(JsonOptions)!
                : DiseaseRules.Disabled;
        var infections =
            node.TryGetPropertyValue("Infections", out var infectionsNode) && infectionsNode is not null
                ? infectionsNode.Deserialize<List<Infection>>(JsonOptions)!
                : [];
        var immunities =
            node.TryGetPropertyValue("Immunities", out var immunitiesNode) && immunitiesNode is not null
                ? immunitiesNode.Deserialize<List<Immunity>>(JsonOptions)!
                : [];

        return new WorldState(
            calendar, currentDate, seed, map, populationCatalog, populationRules, needsRules, actionCatalog,
            lifeStageRules, rngStreams, pendingEvents, nextEventId, exampleCounts, npcs, households, nextNpcId,
            nextHouseholdId, branchId, moneyMinted, moneyDestroyed, economyRules, economyCatalog, workplaces,
            nextWorkplaceId, familyRules, bodyRules, relationships, cities, buildings, nextBuildingId, cityRules, cityCatalog,
            perfRules, historyRules, facts, nextFactId, nextReportId, reports, books, nextBookId,
            canonicalMemories, volatileMemories, nextMemoryId, name, portals,
            restPlaceCatalog, restPlaces, nextRestPlaceId,
            resourceCatalog, processRecipes, resourceProcesses, nextResourceProcessId,
            cropBatches, nextCropBatchId, extraordinary, extraordinaryCarriers,
            extraordinaryConstructs, nextExtraordinaryConstructId,
            fauna, nextAnimalId, flora, nextPlantId, environmentTemperatureAdjustments,
            animalSpeciesRules, plantSpeciesRules, biomeSeasonTemperatureRules,
            nextHistoryEventId, combatEncounters, nextCombatEncounterId, combatRules, tradeLedger, treasuryLedger, diseaseRules, infections, immunities);
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
