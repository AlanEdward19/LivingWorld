using System.Text.Json.Nodes;
using LivingWorld.Domain;
using LivingWorld.Domain.Llm;
using LivingWorld.Simulation;

namespace LivingWorld.Tests;

public class WorldSnapshotTests
{
    // Fase 5 (T12): EconomyRules/EconomyCatalog/Workplaces não podem ficar vazios na fixture —
    // mesmo motivo do RngStream/PendingEvent forçados abaixo, e do resto do arquivo: uma coleção
    // vazia não tem folha primitiva pro mutador genérico de teste perturbar.
    // Enabled falso de propósito: ligar a economia nesta fixture genérica (sem estoque de
    // Household, sem ProductionSystem cablado ainda) faz Eat nunca restaurar e mata a população
    // inteira em ~50h (HungerDecayPerHour=2.0) — Households acaba vazio, quebra este e outros
    // testes deste arquivo. As demais fases da economia (T14-T22) usam suas próprias fixtures
    // pequenas, isoladas; esta aqui só precisa dos campos [Canonical] não-vazios.
    private static readonly EconomyRules SampleEconomyRules = EconomyRules.Create(
        enabled: false, foodResourceId: 1, waterResourceId: 2,
        capacityByResourceLocation: new Dictionary<(int, int), long> { [(1, 1)] = 100 },
        spoilagePerDayByResource: new Dictionary<int, double> { [1] = 0.1 },
        wageByProfession: new Dictionary<int, long> { [1] = 10 },
        priceFloor: new Dictionary<int, long> { [1] = 1 },
        priceCeiling: new Dictionary<int, long> { [1] = 100 },
        priceSensitivity: 0.5,
        demandBaselinePerNpc: new Dictionary<int, double> { [1] = 1.0 }).Value!;

    private static readonly EconomyCatalog SampleEconomyCatalog = new(
        new Dictionary<int, ProductionRecipe>
        {
            [1] = ProductionRecipe.Create(new Dictionary<int, long>(), new Dictionary<int, long> { [1] = 1 }, null, 1).Value!,
        },
        [1],
        new Dictionary<int, int> { [1] = 1 });

    // Fase 8 (T9): world.CityCatalog precisa de ao menos 1 receita não-vazia — mesmo motivo de
    // SampleEconomyCatalog acima (CityCatalog.Empty não tem folha primitiva pro mutador genérico
    // perturbar, o que faria o teste de mutação de campo canônico falhar silenciosamente).
    private static readonly CityCatalog SampleCityCatalog = new(
        new Dictionary<int, BuildingRecipe>
        {
            [1] = BuildingRecipe.Create(new Dictionary<ResourceType, long> { [new ResourceType(1)] = 10 }, ticksToBuild: 5, housingCapacityProvided: 4).Value!,
        });

    private static readonly ExtraordinaryScenarioData SampleExtraordinary = new(true,
    [
        new PowerDescriptor(
            "sample-power", "sample-source", ["movement:trail"], "Conditional", [], "Guaranteed",
            [], [], [], [], new ExtraordinaryAppearanceDescriptor(1.2, "pallor", "mist"),
            new NeedSubstitutionDescriptor("hunger", new ResourceType(1), 1), 0, "world:is-night"),
    ]);

    private static readonly IReadOnlyList<ExtraordinaryCarrierState> SampleExtraordinaryCarriers =
    [
        new(
            new NpcId(1), ["sample-power"], true, "night-active",
            new ExtraordinaryAppearanceState(1.2, "pallor", "mist"),
            new NeedSubstitutionDescriptor("hunger", new ResourceType(1), 1), 0),
    ];

    private static readonly IReadOnlyList<ExtraordinaryConstruct> SampleExtraordinaryConstructs =
    [
        new(
            0, new NpcId(1), "sample-power", 1, ScenarioRunner.DefaultVillageLocation,
            [ScenarioRunner.DefaultVillageLocation], 10, 10, 0, 10_000, "sample-aura"),
    ];

    private static WorldState BuiltWorld()
    {
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, 42, ScenarioRunner.InitialMap(42, ScenarioRunner.DefaultInitialPopulation), ScenarioRunner.DefaultPopulationCatalog,
            ScenarioRunner.DefaultPopulationRules, ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules, economyRules: SampleEconomyRules, economyCatalog: SampleEconomyCatalog,
            cityCatalog: SampleCityCatalog,
            resourceCatalog: new ResourceCatalog(new Dictionary<int, ResourceSpec>
            {
                [1] = ResourceSpec.Create(1, PreparationState.Raw, edible: false).Value!,
            }),
            processRecipes:
            [
                ProcessRecipe.Create(ProcessKind.Cook, new Dictionary<int, long> { [1] = 1 }, 3, 1, 1, 2).Value!,
            ],
            extraordinary: SampleExtraordinary,
            extraordinaryCarriers: SampleExtraordinaryCarriers,
            extraordinaryConstructs: SampleExtraordinaryConstructs,
            nextExtraordinaryConstructId: 1,
            animalSpeciesRules: ScenarioRunner.DefaultAnimalSpeciesRules,
            plantSpeciesRules: ScenarioRunner.DefaultPlantSpeciesRules,
            biomeSeasonTemperatureRules: ScenarioRunner.DefaultBiomeSeasonTemperatureRules);
        PopulationSeeder.SeedInitial(world, ScenarioRunner.DefaultInitialPopulation, ScenarioRunner.DefaultCulture, ScenarioRunner.DefaultVillageLocation);
        world.AddWorkplace(new Workplace(
            world.NextWorkplaceIdAndAdvance(), new LocationType(1), ScenarioRunner.DefaultVillageLocation, maxVacancies: 1,
            employees: [], stock: new Dictionary<ResourceType, long> { [new ResourceType(1)] = 5 }, treasury: new Money(10),
            prices: new Dictionary<ResourceType, long> { [new ResourceType(1)] = 2 }));
        // Fase 8 (T5): força ao menos uma City/Building não-vazia — mesmo motivo do Workplace
        // acima (coleção vazia não tem folha primitiva pro mutador genérico perturbar).
        var city = world.ActiveCities().Single();
        world.AddBuilding(new Building(world.NextBuildingIdAndAdvance(), city.Id, buildingTypeId: 1, completedAtTick: 0));
        // Fase 15.1 T12: RestPlaces vazio não tem folha primitiva pro mutador genérico.
        var restHousehold = world.Households[0];
        world.AddRestPlace(new RestPlace(
            world.NextRestPlaceIdAndAdvance(), RestPlaceKind.Bed, restHousehold.Location, restHousehold.Id));
        world.AddResourceProcess(new ResourceProcess(
            world.NextResourceProcessIdAndAdvance(), ProcessKind.Cook, world.Npcs[0].Id, restHousehold.Id,
            new Dictionary<ResourceType, long> { [new ResourceType(1)] = 1 }, new ResourceType(3), 1,
            startedAtTick: 0, completesAtTick: 2, ProcessStatus.Completed));
        world.AddCropBatch(new CropBatch(
            world.NextCropBatchIdAndAdvance(), cropResourceId: 1, restHousehold.Location,
            plantedAtTick: 0, matureAtTick: 24, waterRequired: 1, waterDelivered: 1, CropStatus.Mature));

        var clock = new WorldClock(ScenarioRunner.DefaultSystems());
        clock.Run(world, ticks: 400); // atravessa fronteira de dia/mês, gera streams e eventos
        // força ao menos um evento pendente e um stream de RNG usado, para o snapshot não ficar vazio
        var ctx = new TickContext(world, world.Rng, world.Scheduler);
        ctx.Rng("probe-stream").NextDouble();
        ctx.ScheduleEvent(world.CurrentDate.TotalHours + 100, "nobody");
        // Fase 7 (T8): força ao menos uma Relationship não-vazia — dict vazio não tem folha
        // primitiva pro mutador genérico de teste perturbar (mesmo motivo do resto do arquivo).
        var relationship = world.GetOrCreateRelationship(new RelationshipKey(new NpcId(1), new NpcId(2)), now: 1);
        relationship.ApplyEvent(RelationshipEventType.Cohabitation, world.FamilyRules);
        world.AddFact(new Fact(
            world.NextFactIdAndAdvance(),
            world.CurrentDate.TotalHours,
            WorldEventKind.Birth,
            [world.Npcs[0].Id],
            world.Npcs[0].City,
            0.85,
            world.Npcs[0].Id.Value.ToString()));
        var report = new ReportState(
            world.NextReportIdAndAdvance(),
            world.Facts[0].Id,
            world.Npcs[0].City,
            TransmissionMediumType.OralTradition,
            HopCount: 0,
            Weight: 0.85,
            CreatedAtTick: world.CurrentDate.TotalHours,
            LastHopTick: world.CurrentDate.TotalHours);
        world.RegisterReport(report);
        world.AddBook(new Book(
            world.NextBookIdAndAdvance(),
            report.Id,
            CopyOfBookId: null,
            Lost: false,
            LostAtTick: null,
            RediscoveredAtTick: null));
        // Fase 11 (roadmap itens 1/2): força ao menos uma NpcMemory canônica e uma volátil — mesmo
        // motivo do resto do arquivo (coleção vazia não tem folha primitiva pro mutador genérico
        // perturbar sem quebrar a rehidratação tipada).
        world.AddNpcMemory(
            world.Npcs[0].Id, MemoryCategory.Episodic, "memoria canonica de teste", importance: 80, originTick: 0,
            participants: [world.Npcs[0].Id], location: world.Npcs[0].CurrentLocation, canonicalImportanceThreshold: 50);
        world.AddNpcMemory(
            world.Npcs[0].Id, MemoryCategory.Operational, "memoria volatil de teste", importance: 10, originTick: 0,
            participants: [world.Npcs[0].Id], location: world.Npcs[0].CurrentLocation, canonicalImportanceThreshold: 50);
        // Fase 15.1 (T21): força ao menos um SpatialPortal — mesmo motivo do resto do arquivo
        // (coleção vazia não tem folha primitiva pro mutador genérico perturbar).
        world.AddPortal(new SpatialPortal(
            "portal-test", "Portão de Teste",
            new PortalEndpoint(PortalSpaceKind.World, "", new CellCoord(1, 1)),
            new PortalEndpoint(PortalSpaceKind.City, city.Id.ToString(), new CellCoord(2, 2))));
        world.AddAnimal(new Animal(
            world.NextAnimalIdAndAdvance(), "probe-wolf", new CellCoord(0, 0), true, null,
            LazyNeed.Initial(100, 0, 0)));
        world.AddPlant(new Plant(
            world.NextPlantIdAndAdvance(), "probe-oak", new CellCoord(0, 0), 1));
        // Fase 16.4: força ao menos um CombatEncounter — coleção vazia não tem folha pro mutador.
        world.AddCombatEncounter(new CombatEncounter(
            world.NextCombatEncounterIdAndAdvance(),
            world.Npcs[0].Id, world.Npcs[1].Id, Magnitude: 10, RoundsElapsed: 0,
            CombatEncounterStatus.Active));
        world.AddEnvironmentTemperatureAdjustment(new EnvironmentTemperatureAdjustment(
            world.Map.Regions[0].Id, 1.5f, world.CurrentDate.TotalHours + 24));
        return world;
    }

    // --- Cobertura por reflexão (task 7 / critério "cobertura do snapshot por reflexão") ---

    [Fact]
    public void Every_public_property_of_WorldState_appears_in_the_serialized_snapshot()
    {
        var world = BuiltWorld();
        var json = JsonNode.Parse(WorldSnapshot.Serialize(world))!.AsObject();

        foreach (var prop in WorldSnapshot.ReflectedProperties)
            Assert.True(json.ContainsKey(prop.Name), $"propriedade '{prop.Name}' ausente do snapshot serializado");
    }

    // --- Classificação por reflexão (task 7 / critério "classificação de campos por reflexão") ---

    [Fact]
    public void Every_public_property_is_classified_as_exactly_one_of_canonical_or_volatile()
    {
        foreach (var prop in WorldSnapshot.ReflectedProperties)
        {
            bool isCanonical = prop.GetCustomAttributes(typeof(CanonicalAttribute), false).Length > 0;
            bool isVolatile = prop.GetCustomAttributes(typeof(VolatileAttribute), false).Length > 0;

            Assert.True(isCanonical || isVolatile, $"propriedade '{prop.Name}' não classificada em Canonical nem Volatile");
            Assert.False(isCanonical && isVolatile, $"propriedade '{prop.Name}' classificada nas duas listas");
        }
    }

    [Fact]
    public void At_least_one_canonical_and_one_volatile_property_exist()
    {
        // Sensor de mutação (R5): se uma das duas classes ficar vazia, os testes abaixo não medem nada.
        Assert.Contains(WorldSnapshot.ReflectedProperties, p => p.GetCustomAttributes(typeof(CanonicalAttribute), false).Length > 0);
        Assert.Contains(WorldSnapshot.ReflectedProperties, p => p.GetCustomAttributes(typeof(VolatileAttribute), false).Length > 0);
    }

    // --- Mutação genérica por reflexão sobre o próprio JSON do snapshot ---
    // Critério: mutar qualquer campo canônico muda o hash canônico; mutar campo volátil não muda.

    [Theory]
    [MemberData(nameof(ReflectedPropertyNames))]
    public void Mutating_a_canonical_field_changes_canonical_hash_mutating_a_volatile_field_does_not(string propertyName)
    {
        var world = BuiltWorld();
        string originalCanonical = WorldSnapshot.CanonicalHash(world);

        var json = JsonNode.Parse(WorldSnapshot.Serialize(world))!.AsObject();
        json[propertyName] = Mutate(json[propertyName]!);
        var mutatedWorld = WorldSnapshot.Deserialize(json.ToJsonString());

        string mutatedCanonical = WorldSnapshot.CanonicalHash(mutatedWorld);

        bool isCanonical = WorldSnapshot.ReflectedProperties
            .Single(p => p.Name == propertyName)
            .GetCustomAttributes(typeof(CanonicalAttribute), false).Length > 0;

        if (isCanonical)
            Assert.NotEqual(originalCanonical, mutatedCanonical);
        else
            Assert.Equal(originalCanonical, mutatedCanonical);
    }

    public static IEnumerable<object[]> ReflectedPropertyNames() =>
        WorldSnapshot.ReflectedProperties.Select(p => new object[] { p.Name });

    // Recursivo: sempre que possível muta uma folha primitiva alcançável, em vez de só anexar
    // uma chave extra que um deserializador tipado (que ignora propriedade desconhecida)
    // descartaria sem efeito nenhum sobre o mundo reconstruído.
    private static JsonNode Mutate(JsonNode node) => node switch
    {
        JsonValue v when v.TryGetValue<bool>(out var b) => JsonValue.Create(!b),
        JsonValue v when v.TryGetValue<long>(out var l) => JsonValue.Create(l + 1),
        JsonValue v when v.TryGetValue<ulong>(out var ul) => JsonValue.Create(ul + 1),
        JsonValue v when v.TryGetValue<int>(out var i) => JsonValue.Create(i + 1),
        JsonValue v when v.TryGetValue<double>(out var d) => JsonValue.Create(d + 1),
        JsonValue v when v.TryGetValue<string>(out var s) => JsonValue.Create(s + "_mut"),
        JsonArray { Count: > 0 } arr => MutateFirstElement(arr),
        JsonArray arr => AppendSentinel(arr),
        JsonObject { Count: > 0 } obj => MutateFirstPrimitiveProperty(obj),
        _ => node,
    };

    private static JsonArray MutateFirstElement(JsonArray arr)
    {
        var clone = new JsonArray(arr.Select(n => n?.DeepClone()).ToArray());
        clone[0] = Mutate(clone[0]!);
        return clone;
    }

    // Mesma prioridade de sempre (primeiro JsonValue cru, na ordem declarada) — só pula uma
    // string que bata com o nome de um literal de enum conhecido do código (ex.
    // ActionCatalog.DefaultAction == "Idle"), porque anexar "_mut" a um enum serializado como
    // texto (JsonStringEnumConverter) quebra o parse na rehidratação. Nesse caso recorre a um
    // objeto/array aninhado (ex. ActionCatalog.MaxDurationHours) em vez da string.
    private static JsonObject MutateFirstPrimitiveProperty(JsonObject obj)
    {
        string targetKey = obj.FirstOrDefault(kv => kv.Value is JsonValue v && IsSafeToMutate(v)).Key
            ?? obj.FirstOrDefault(kv => kv.Value is JsonObject { Count: > 0 }).Key
            ?? obj.FirstOrDefault(kv => kv.Value is JsonArray { Count: > 0 }).Key
            ?? obj.FirstOrDefault(kv => kv.Value is JsonValue).Key
            ?? obj.First().Key;

        var clone = new JsonObject();
        foreach (var kv in obj)
            clone[kv.Key] = kv.Key == targetKey ? Mutate(kv.Value!) : kv.Value?.DeepClone();
        return clone;
    }

    private static readonly HashSet<string> KnownEnumLiterals =
        AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => (a.GetName().Name ?? "").StartsWith("LivingWorld."))
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsEnum)
            .SelectMany(Enum.GetNames)
            .ToHashSet(StringComparer.Ordinal);

    private static bool IsSafeToMutate(JsonValue v) =>
        !(v.TryGetValue<string>(out var s) && KnownEnumLiterals.Contains(s));

    private static JsonArray AppendSentinel(JsonArray arr)
    {
        var clone = new JsonArray(arr.Select(n => n?.DeepClone()).ToArray());
        clone.Add(JsonValue.Create("__mutation_sentinel__"));
        return clone;
    }

    // --- Reidratação sobrevive ao futuro ---

    [Fact]
    public void Rehydrating_at_tick_T_and_running_500_more_matches_the_continuous_run_to_T_plus_500()
    {
        const long splitTick = 300;
        const long extra = 500;

        var (continuousWorld, continuousClock) = ScenarioRunner.Create(seed: 99);
        continuousClock.Run(continuousWorld, splitTick + extra);

        var (worldAtSplit, splitClock) = ScenarioRunner.Create(seed: 99);
        splitClock.Run(worldAtSplit, splitTick);
        var rehydrated = WorldSnapshot.Deserialize(WorldSnapshot.Serialize(worldAtSplit));
        var resumedClock = new WorldClock(ScenarioRunner.DefaultSystems());
        resumedClock.Run(rehydrated, extra);

        Assert.Equal(WorldSnapshot.CanonicalHash(continuousWorld), WorldSnapshot.CanonicalHash(rehydrated));
    }

    [Fact]
    public void Round_trip_alone_is_not_tautological_because_it_survives_further_ticks()
    {
        var (world, clock) = ScenarioRunner.Create(seed: 5);
        clock.Run(world, 50);
        var beforeHash = WorldSnapshot.CanonicalHash(world);

        var rehydrated = WorldSnapshot.Deserialize(WorldSnapshot.Serialize(world));
        Assert.Equal(beforeHash, WorldSnapshot.CanonicalHash(rehydrated)); // round-trip simples ainda bate

        new WorldClock(ScenarioRunner.DefaultSystems()).Run(rehydrated, 50);
        Assert.NotEqual(beforeHash, WorldSnapshot.CanonicalHash(rehydrated)); // mas o mundo seguiu andando de verdade
    }

    // --- Fase 15.1, T21: SpatialPortal — round-trip preserva a coleção e o hash canônico ---

    [Fact]
    public void Round_tripping_a_world_with_portals_preserves_them_with_an_identical_canonical_hash()
    {
        var world = BuiltWorld();
        var hashBefore = WorldSnapshot.CanonicalHash(world);

        var rehydrated = WorldSnapshot.Deserialize(WorldSnapshot.Serialize(world));

        Assert.Equal(world.Portals, rehydrated.Portals);
        Assert.Equal(hashBefore, WorldSnapshot.CanonicalHash(rehydrated));
    }

    // Isola a mudança de hash à coleção Portals, não a um efeito colateral (spec.md AC6): duas
    // cargas idênticas do mesmo cenário sem portal declarado hasheiam igual entre si; declarar
    // um portal extra na segunda é a única diferença que muda o hash.
    [Fact]
    public void Adding_a_portal_is_the_only_source_of_divergence_between_two_otherwise_identical_worlds()
    {
        var (worldWithoutPortal, _) = ScenarioRunner.Create(seed: 77);
        var (worldWithPortal, _) = ScenarioRunner.Create(seed: 77);

        Assert.Equal(WorldSnapshot.CanonicalHash(worldWithoutPortal), WorldSnapshot.CanonicalHash(worldWithPortal));

        worldWithPortal.AddPortal(new SpatialPortal(
            "portal-isolation", "Portão",
            new PortalEndpoint(PortalSpaceKind.World, "", new CellCoord(0, 0)),
            new PortalEndpoint(PortalSpaceKind.World, "", new CellCoord(1, 1))));

        Assert.NotEqual(WorldSnapshot.CanonicalHash(worldWithoutPortal), WorldSnapshot.CanonicalHash(worldWithPortal));
    }

    // --- Pausa não é estado do mundo ---

    [Fact]
    public void Pause_speed_and_resume_do_not_change_the_hash_versus_running_straight_through()
    {
        var (straightWorld, straightClock) = ScenarioRunner.Create(seed: 11);
        straightClock.Run(straightWorld, 100);

        var (pausedWorld, pausedClock) = ScenarioRunner.Create(seed: 11);
        var host = new SimulationHost(new WorldHost(pausedWorld, pausedClock));
        host.FastForward(37);
        host.Pause();
        host.SetSpeed(5.0);
        host.Resume();
        host.FastForward(63);

        Assert.Equal(WorldSnapshot.CanonicalHash(straightWorld), WorldSnapshot.CanonicalHash(pausedWorld));
    }

    [Fact]
    public void Pause_flag_and_speed_are_not_part_of_the_serialized_snapshot()
    {
        var world = BuiltWorld();
        var json = WorldSnapshot.Serialize(world);

        Assert.DoesNotContain("Pause", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Speed", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("TicksPerSecond", json, StringComparison.OrdinalIgnoreCase);
    }
}
