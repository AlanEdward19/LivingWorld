using LivingWorld.Api.Visual.Projection;
using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Geography.Map;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Decision;
using LivingWorld.Simulation.Behavior.Needs;
using LivingWorld.Simulation.Cities;
using LivingWorld.Simulation.Cities.Founding;
using LivingWorld.Simulation.Cities.Migration;
using LivingWorld.Simulation.Cities.Queries;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Cities.Founding;

/// <summary>dynamic-city-growth, T7 (CITYGROW-04): <see cref="SpatialSettlementFoundingSystem"/> —
/// funda uma cidade nova a partir de um cluster de overflow que reúne população materializada
/// suficiente pra cruzar a MESMA fórmula/limiar de <see cref="SettlementFoundingSystem"/>, nunca
/// um limiar mais fraco por contagem de prédios. Mesmo padrão de teste de
/// <see cref="SettlementFoundingSystemTests"/> (mundo em memória, `ctx.ScheduleEvent`/`HandleEvent`
/// diretos).</summary>
public class SpatialSettlementFoundingSystemTests
{
    private sealed class RecordingSink : IWorldEventSink
    {
        public List<WorldEvent> Events { get; } = [];
        public void Record(WorldEvent evt) => Events.Add(evt);
    }

    private static readonly GeographyCatalog TinyCatalog = new(
        TerrainIds: new HashSet<int> { 1 }, BiomeIds: new HashSet<int> { 1 }, ResourceIds: new HashSet<int>());

    private static readonly CostWeights TinyCostWeights = new(
        Base: 1.0, AltitudeWeight: 0.5, TerrainWeight: new Dictionary<int, double> { [1] = 1.0 });

    private static readonly Personality NeutralPersonality =
        Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    private static CityRules MakeRules(
        double foundingConcentrationThreshold = 0.5, long organizationTicks = 10) => CityRules.Create(
        enabled: true, foodShortageThreshold: 20, housingShortageThreshold: 20, securityShortageThreshold: 20,
        emigrationRatePerDeficitUnit: 0.1, migrationEmploymentWeight: 1, migrationFoodWeight: 1,
        migrationSecurityWeight: 1, migrationFamilyTiesWeight: 1, foundingConcentrationThreshold,
        foundingResourceThreshold: 0, foundingRouteThreshold: 0, foundingDefensibilityThreshold: 0,
        foundingLeadershipThreshold: 0, organizationTicks, materializationIdleTicksBeforeEligible: 5)
        .Value!;

    // Mapa bem maior que map/2 pra nenhum teste bater no teto de borda por acidente (mesma tática
    // de OverflowClusterFinderTests/CityOccupancyTests).
    private static WorldState MakeWorld(CityRules rules, ulong seed)
    {
        var map = MapGenerator.Generate(seed, width: 300, height: 300, regionSize: 300, TinyCatalog, TinyCostWeights, [])
            .Value ?? throw new InvalidOperationException("mapa de teste inválido — bug no teste, não no gerador");
        return new WorldState(
            ScenarioRunner.DefaultCalendar, seed, map, ScenarioRunner.DefaultPopulationCatalog,
            ScenarioRunner.DefaultPopulationRules, ScenarioRunner.DefaultNeedsRules,
            ScenarioRunner.DefaultActionCatalog, ScenarioRunner.DefaultLifeStageRules, cityRules: rules);
    }

    private static TickContext MakeCtx(WorldState world) => new(world, world.Rng, world.Scheduler);

    private static Npc MakeNpc(WorldState world, long id, CellCoord location) => new(
        new NpcId(id), $"npc-{id}", Sex.Male, world.CurrentDate.AddYears(-30), ScenarioRunner.DefaultCulture,
        location, motherId: null, fatherId: null, household: null, health: 80,
        personality: NeutralPersonality, profession: ProfessionType.None, currentLocation: location);

    /// <summary>Prédio de overflow bem distante dos bounds da cidade-mãe (nunca absorvido) — em
    /// (200,200), sempre a célula (0,0) do footprint (canto superior-esquerdo), presente em toda
    /// planta gerada.</summary>
    private static (City MotherCity, Building Overflow) SeedOneOverflowBuilding(WorldState world)
    {
        var motherCity = new City(world.NextCityId(), new CellCoord(100, 100), 0, null, AggregatePopulationPool.Empty);
        world.AddCity(motherCity);
        var overflow = new Building(world.NextBuildingIdAndAdvance(), motherCity.Id, buildingTypeId: 1, completedAtTick: 0,
            position: new CellCoord(200, 200), orientation: 0);
        world.AddBuilding(overflow);
        return (motherCity, overflow);
    }

    // --- Tick: schedule ---

    [Fact]
    public void Tick_schedules_founding_when_the_cluster_clears_the_concentration_threshold()
    {
        var rules = MakeRules(foundingConcentrationThreshold: 0.5, organizationTicks: 10);
        var world = MakeWorld(rules, seed: 901);
        var (_, overflow) = SeedOneOverflowBuilding(world);
        world.AddNpc(MakeNpc(world, 1, overflow.Position!.Value)); // 1/(1+1)=0.5 >= 0.5

        new SpatialSettlementFoundingSystem().Tick(world, MakeCtx(world));

        var pending = Assert.Single(world.PendingEvents);
        Assert.Equal(world.CurrentDate.TotalHours + 10, pending.TargetTick);
        Assert.NotNull(overflow.ClusterFoundingScheduledAtTick);
    }

    [Fact]
    public void Tick_does_not_schedule_when_the_cluster_has_buildings_but_no_residents()
    {
        var rules = MakeRules(foundingConcentrationThreshold: 0.5);
        var world = MakeWorld(rules, seed: 902);
        SeedOneOverflowBuilding(world); // 0 residentes -> concentração 0/(0+1)=0 < 0.5

        new SpatialSettlementFoundingSystem().Tick(world, MakeCtx(world));

        Assert.Empty(world.PendingEvents);
    }

    [Fact]
    public void Tick_does_not_schedule_for_a_cluster_already_within_absorption_range_of_an_existing_city()
    {
        var rules = MakeRules(foundingConcentrationThreshold: 0.01); // trivial de bater se elegível
        var world = MakeWorld(rules, seed: 903);
        var motherCity = new City(world.NextCityId(), new CellCoord(100, 100), 0, null, AggregatePopulationPool.Empty);
        world.AddCity(motherCity);
        // Bounds populacionais (99,99)-(101,101); prédio em x=102 -> a 1 célula da borda,
        // absorvido -> OverflowClusterFinder nunca reporta este como overflow (precedência de
        // absorção, spec Edge Cases).
        var absorbed = new Building(world.NextBuildingIdAndAdvance(), motherCity.Id, buildingTypeId: 1, completedAtTick: 0,
            position: new CellCoord(102, 100), orientation: 0);
        world.AddBuilding(absorbed);
        world.AddNpc(MakeNpc(world, 1, absorbed.Position!.Value));

        new SpatialSettlementFoundingSystem().Tick(world, MakeCtx(world));

        Assert.Empty(world.PendingEvents);
    }

    [Fact]
    public void Tick_never_reschedules_the_same_cluster_twice()
    {
        var rules = MakeRules(foundingConcentrationThreshold: 0.5);
        var world = MakeWorld(rules, seed: 904);
        var (_, overflow) = SeedOneOverflowBuilding(world);
        world.AddNpc(MakeNpc(world, 1, overflow.Position!.Value));
        var system = new SpatialSettlementFoundingSystem();
        system.Tick(world, MakeCtx(world));

        system.Tick(world, MakeCtx(world)); // segundo mês, limiar continua batido

        Assert.Single(world.PendingEvents); // não duplicou o evento
    }

    // --- HandleEvent: fire-time re-verify + founding ---

    [Fact]
    public void HandleEvent_drops_silently_when_the_cluster_thinned_out_below_threshold_during_the_wait()
    {
        var rules = MakeRules(foundingConcentrationThreshold: 0.5, organizationTicks: 10);
        var world = MakeWorld(rules, seed: 905);
        var (_, overflow) = SeedOneOverflowBuilding(world);
        var npc = MakeNpc(world, 1, overflow.Position!.Value);
        world.AddNpc(npc);
        var system = new SpatialSettlementFoundingSystem();
        system.Tick(world, MakeCtx(world));
        var evt = Assert.Single(world.PendingEvents);

        npc.Die(world.CurrentDate); // esvaziou -> concentração cai pra 0/(0+1)=0 < 0.5
        int citiesBefore = world.Cities.Count;
        system.HandleEvent(world, MakeCtx(world), evt);

        Assert.Equal(citiesBefore, world.Cities.Count); // nenhuma cidade forçada a existir
    }

    /// <summary>Post-ship fix (Fix 2, 2026-08-23): a distância de absorção só era reverificada no
    /// AGENDAMENTO (Tick) -- se outra cidade cresce e passa a ficar dentro do alcance de absorção
    /// do cluster durante a espera de OrganizationTicks, fundar mesmo assim colaria a cidade nova
    /// na vizinha (o próprio bug relatado). Simula o crescimento chamando <see
    /// cref="City.Dematerialize"/> repetidamente numa segunda cidade posicionada perto o bastante
    /// do cluster (mesma coluna X do prédio de overflow, 6 células de distância em Y -- longe o
    /// bastante pra NÃO absorver no agendamento com população 0/lado 3, perto o bastante pra
    /// absorver depois que o lado cresce até o teto de 12) -- sem precisar tocar em nenhum prédio.
    /// Assere que a fundação é dropada silenciosamente, nenhuma cidade nova criada.</summary>
    [Fact]
    public void HandleEvent_drops_silently_when_another_city_grew_within_absorption_range_during_the_wait()
    {
        var rules = MakeRules(foundingConcentrationThreshold: 0.5, organizationTicks: 10);
        var world = MakeWorld(rules, seed: 908);
        var (motherCity, overflow) = SeedOneOverflowBuilding(world); // overflow em (200,200)
        world.AddNpc(MakeNpc(world, 1, overflow.Position!.Value)); // 1/(1+1)=0.5 >= 0.5

        // Lado 3 (pop 0): bounds y193-195, gap=5 pra y=200 (>3, não absorve ainda no agendamento).
        // Lado 12 (pop alta): bounds y188-199, gap=1 (<=3, absorve depois de crescer).
        var growingCity = new City(world.NextCityId(), new CellCoord(200, 194), 0, null, AggregatePopulationPool.Empty);
        world.AddCity(growingCity);

        var system = new SpatialSettlementFoundingSystem();
        system.Tick(world, MakeCtx(world));
        var evt = Assert.Single(world.PendingEvents); // growingCity ainda não absorve -> agendou

        for (int i = 0; i < 600; i++)
            growingCity.Dematerialize(new NpcId(10_000 + i), wealth: 0, health: 0); // população sobe, lado cresce até 12

        int citiesBefore = world.Cities.Count;
        system.HandleEvent(world, MakeCtx(world), evt);

        Assert.Equal(citiesBefore, world.Cities.Count); // fundação dropada -- absorção por growingCity tem precedência
        Assert.Equal(motherCity.Id, overflow.City); // prédio nunca reatribuído (fundação não ocorreu)
    }

    /// <summary>Post-ship fix (user-reported, 2026-08-23, "MorNorHol" fundada fora do mapa): a
    /// cidade-mãe fica encostada na borda (0,0) e o único prédio do cluster de overflow tem
    /// posição AUTORADA negativa (gap pré-existente e fora de escopo desta correção --
    /// <c>BuildingPlacementResolver</c> nunca valida uma <c>Position</c> autorada -- mas usado aqui
    /// só pra reproduzir determinística e minimamente um centroide fora do mapa, sem depender
    /// desse gap ser corrigido). O centroide resultante cai fora de <c>world.Map</c>; a fundação
    /// deve ser dropada silenciosamente, mesmo padrão dos dois outros re-checks de HandleEvent.</summary>
    [Fact]
    public void HandleEvent_drops_silently_when_the_computed_centroid_would_land_outside_the_map()
    {
        var rules = MakeRules(foundingConcentrationThreshold: 0.5, organizationTicks: 10);
        var world = MakeWorld(rules, seed: 909);
        var motherCity = new City(world.NextCityId(), new CellCoord(0, 0), 0, null, AggregatePopulationPool.Empty);
        world.AddCity(motherCity);
        var offMapOverflow = new Building(world.NextBuildingIdAndAdvance(), motherCity.Id, buildingTypeId: 1,
            completedAtTick: 0, position: new CellCoord(-20, -20), orientation: 0);
        world.AddBuilding(offMapOverflow);
        world.AddNpc(MakeNpc(world, 1, offMapOverflow.Position!.Value)); // 1/(1+1)=0.5 >= 0.5

        var system = new SpatialSettlementFoundingSystem();
        system.Tick(world, MakeCtx(world));
        var evt = Assert.Single(world.PendingEvents);

        int citiesBefore = world.Cities.Count;
        system.HandleEvent(world, MakeCtx(world), evt);

        Assert.Equal(citiesBefore, world.Cities.Count); // nenhuma cidade fora do mapa foi fundada
        Assert.Equal(motherCity.Id, offMapOverflow.City); // prédio nunca reatribuído (fundação não ocorreu)
    }

    [Fact]
    public void HandleEvent_founds_a_new_city_at_the_cluster_centroid_and_reassigns_the_clusters_buildings()
    {
        var rules = MakeRules(foundingConcentrationThreshold: 0.5, organizationTicks: 10);
        var world = MakeWorld(rules, seed: 906);
        var (motherCity, overflow) = SeedOneOverflowBuilding(world);
        world.AddNpc(MakeNpc(world, 1, overflow.Position!.Value));
        var system = new SpatialSettlementFoundingSystem();
        system.Tick(world, MakeCtx(world));
        var evt = Assert.Single(world.PendingEvents);

        system.HandleEvent(world, MakeCtx(world), evt);

        Assert.Equal(2, world.Cities.Count);
        var newCity = world.Cities.Single(c => c.Id != motherCity.Id);
        Assert.Equal(motherCity.Id, newCity.FoundedFromCityId);
        Assert.Equal(newCity.Id, overflow.City); // prédio do cluster reatribuído
    }

    [Fact]
    public void HandleEvent_reassigns_households_and_member_npcs_geometrically_inside_the_new_citys_bounds()
    {
        var rules = MakeRules(foundingConcentrationThreshold: 0.5, organizationTicks: 10);
        var world = MakeWorld(rules, seed: 907);
        var (motherCity, overflow) = SeedOneOverflowBuilding(world);
        var head = MakeNpc(world, 1, overflow.Position!.Value);
        world.AddNpc(head);
        var household = new Household(
            new HouseholdId(1), overflow.Position!.Value, head.Id, [head.Id], city: motherCity.Id);
        world.AddHousehold(household);
        var system = new SpatialSettlementFoundingSystem();
        system.Tick(world, MakeCtx(world));
        var evt = Assert.Single(world.PendingEvents);

        system.HandleEvent(world, MakeCtx(world), evt);

        var newCity = world.Cities.Single(c => c.Id != motherCity.Id);
        Assert.Equal(newCity.Id, household.City);
        Assert.Equal(newCity.Id, head.City);
    }

    /// <summary>Repro da fundação a partir de overflow: a posição corrente identifica a família
    /// fundadora mesmo quando sua residência ainda aponta para a cidade-mãe. Ao fundar, residência
    /// e workplace contidos no cluster precisam acompanhar a família para não puxá-la de volta.</summary>
    [Fact]
    public void HandleEvent_reassigns_a_household_by_its_heads_current_location_even_when_household_location_is_stale()
    {
        var rules = MakeRules(foundingConcentrationThreshold: 0.5, organizationTicks: 10);
        var world = MakeWorld(rules, seed: 910);
        var (motherCity, overflow) = SeedOneOverflowBuilding(world);
        var head = MakeNpc(world, 1, overflow.Position!.Value); // CurrentLocation dentro do cluster
        world.AddNpc(head);
        // Residência ainda registrada em (0,0), longe do cluster onde a família se estabeleceu.
        var household = new Household(
            new HouseholdId(1), new CellCoord(0, 0), head.Id, [head.Id], city: motherCity.Id);
        household.Deposit(new ResourceType(world.EconomyRules.FoodResourceId), 1000);
        world.AddHousehold(household);
        var workplace = new Workplace(
            new WorkplaceId(1), new LocationType(1), overflow.Position.Value, 1, [head.Id],
            new Dictionary<ResourceType, long>(), Money.Zero, new Dictionary<ResourceType, long>());
        world.AddWorkplace(workplace);
        head.Hire(workplace.Id);
        var system = new SpatialSettlementFoundingSystem();
        system.Tick(world, MakeCtx(world));
        var evt = Assert.Single(world.PendingEvents);

        system.HandleEvent(world, MakeCtx(world), evt);

        var newCity = world.Cities.Single(c => c.Id != motherCity.Id);
        Assert.Equal(newCity.Id, household.City);
        Assert.Equal(newCity.Id, head.City);
        Assert.Equal(overflow.Position.Value, household.Location);
        Assert.Equal(newCity.Id, workplace.City);
        Assert.Equal(workplace.Id, head.Employer);

        var clock = new WorldClock([new BehaviorDecisionSystem(), new RelocationArrivalSystem()]);
        var ctx = MakeCtx(world);
        for (int hour = 0; hour < 2 * 24; hour++)
        {
            NpcWakeScheduler.ScheduleWake(world, ctx, head.Id.Value, world.CurrentDate.TotalHours + 1);
            clock.Tick(world);
            if ((hour + 1) % 24 == 0)
                new MigrationSystem().Tick(world, MakeCtx(world));
        }

        var newCityBounds = CityOccupancy.ResolveGrownBounds(
            world, newCity, CityPopulationQuery.Population(world, newCity.Id)).Bounds;
        Assert.Equal(newCity.Id, head.City);
        Assert.Null(household.PendingRelocationCity);
        Assert.True(newCityBounds.Contains(head.CurrentLocation));
        Assert.NotEqual(motherCity.Location, head.CurrentLocation);
    }

    /// <summary>Post-ship fix (round 2, 2026-08-23, "population jumping between two adjacent
    /// cities"): the reassignment loop had no check that a household actually belonged to the
    /// founding cluster's own mother city -- it swept up ANY household whose head stood inside
    /// clusterBounds, including one that already properly belongs to a DIFFERENT, neighboring
    /// city. Repro: a household genuinely settled in `neighborCity` has its head standing inside
    /// the cluster's footprint (spawned from `motherCity`'s own overflow) at founding time -- it
    /// must NOT be poached into the brand-new city.</summary>
    [Fact]
    public void HandleEvent_never_reassigns_a_household_that_already_belongs_to_a_different_city()
    {
        var rules = MakeRules(foundingConcentrationThreshold: 0.5, organizationTicks: 10);
        var world = MakeWorld(rules, seed: 911);
        var (motherCity, overflow) = SeedOneOverflowBuilding(world); // overflow em (200,200)
        var founderHead = MakeNpc(world, 1, overflow.Position!.Value);
        world.AddNpc(founderHead); // 1/(1+1)=0.5 >= 0.5, funda a cidade nova de verdade

        // Cidade vizinha, já existente, sem relação nenhuma com este cluster -- longe o bastante
        // (dentro do mapa 300x300) pra nunca entrar no alcance de absorção do cluster.
        var neighborCity = new City(world.NextCityId(), new CellCoord(250, 250), 0, null, AggregatePopulationPool.Empty);
        world.AddCity(neighborCity);

        // Household que já pertence de fato à cidade vizinha -- mas seu chefe, por acaso, está
        // fisicamente parado dentro do footprint do cluster de overflow no momento exato da
        // fundação (ex.: NPC comutando/visitando). O household NÃO fundou nada e não tem relação
        // com motherCity -- reatribuí-lo seria o próprio bug relatado.
        var neighborHead = MakeNpc(world, 2, overflow.Position!.Value);
        neighborHead.JoinCity(neighborCity.Id); // já pertence de fato à cidade vizinha
        world.AddNpc(neighborHead);
        var neighborHousehold = new Household(
            new HouseholdId(1), new CellCoord(250, 250), neighborHead.Id, [neighborHead.Id], city: neighborCity.Id);
        world.AddHousehold(neighborHousehold);

        var system = new SpatialSettlementFoundingSystem();
        system.Tick(world, MakeCtx(world));
        var evt = Assert.Single(world.PendingEvents);

        system.HandleEvent(world, MakeCtx(world), evt);

        Assert.Equal(3, world.Cities.Count); // motherCity + neighborCity + a nova cidade fundada
        Assert.Equal(neighborCity.Id, neighborHousehold.City); // nunca poached
        Assert.Equal(neighborCity.Id, neighborHead.City);
    }

    // --- FixT18: cidade-filha espacial adjacente volta a integrar a cidade-mãe ---

    [Fact]
    public void Tick_schedules_exactly_one_merge_after_OrganizationTicks_for_an_adjacent_daughter()
    {
        var rules = MakeRules(organizationTicks: 10);
        var world = MakeWorld(rules, seed: 912);
        var mother = new City(world.NextCityId(), new CellCoord(100, 100), 0, null, AggregatePopulationPool.Empty);
        var daughter = new City(world.NextCityId(), new CellCoord(105, 100), 1, mother.Id, AggregatePopulationPool.Empty);
        world.AddCity(mother);
        world.AddCity(daughter); // bounds 3x3 separados por gap 3 == AbsorptionRingCells
        var system = new SpatialSettlementFoundingSystem();

        system.Tick(world, MakeCtx(world));
        system.Tick(world, MakeCtx(world));

        var pending = Assert.Single(world.PendingEvents);
        Assert.Equal(world.CurrentDate.TotalHours + rules.OrganizationTicks, pending.TargetTick);
        Assert.StartsWith("merge|", pending.Payload);
        Assert.NotNull(daughter.MergeScheduledAtTick);
    }

    [Fact]
    public void HandleEvent_cancels_merge_when_the_cities_are_no_longer_adjacent_at_fire_time()
    {
        var rules = MakeRules(organizationTicks: 10);
        var world = MakeWorld(rules, seed: 913);
        var poolIds = Enumerable.Range(10_000, 600).Select(id => new NpcId(id)).ToList();
        var mother = new City(
            world.NextCityId(), new CellCoord(100, 100), 0, null,
            new AggregatePopulationPool(600, 0, 0), poolNpcIds: poolIds);
        var daughter = new City(world.NextCityId(), new CellCoord(109, 100), 1, mother.Id, AggregatePopulationPool.Empty);
        world.AddCity(mother);
        world.AddCity(daughter); // mãe com lado 12: gap 3, adjacente
        var system = new SpatialSettlementFoundingSystem();
        system.Tick(world, MakeCtx(world));
        var pending = Assert.Single(world.PendingEvents);

        var extracted = mother.ExtractEntirePool(); // mãe volta a lado 3: gap 7, não adjacente
        system.HandleEvent(world, MakeCtx(world), pending);

        Assert.Null(daughter.MergedIntoCityId);
        Assert.Null(daughter.MergeScheduledAtTick);
        Assert.Equal(2, world.ActiveCities().Count());

        mother.AbsorbPool(extracted.Pool, extracted.PoolNpcIds); // adjacência volta a existir
        system.Tick(world, MakeCtx(world));
        Assert.NotNull(daughter.MergeScheduledAtTick);
        Assert.Equal(2, world.PendingEvents.Count); // evento antigo + nova tentativa após cancelamento
    }

    [Fact]
    public void HandleEvent_merges_all_causal_state_and_keeps_only_the_mother_active_and_visible()
    {
        var rules = MakeRules(organizationTicks: 10);
        var world = MakeWorld(rules, seed: 914);
        var pooledNpc = new NpcId(99);
        var mother = new City(
            world.NextCityId(), new CellCoord(100, 100), 0, null,
            new AggregatePopulationPool(2, 10, 40), poolNpcIds: [new NpcId(97), new NpcId(98)]);
        var daughter = new City(
            world.NextCityId(), new CellCoord(105, 100), 1, mother.Id,
            new AggregatePopulationPool(1, 20, 80), poolNpcIds: [pooledNpc]);
        world.AddCity(mother);
        world.AddCity(daughter);
        mother.DepositStock(new ResourceType(1), 5);
        daughter.DepositStock(new ResourceType(1), 7);
        var project = new ConstructionProject(daughter.Id, 1, new Dictionary<ResourceType, long>(), 3);
        daughter.EnqueueConstruction(project);
        var building = new Building(
            world.NextBuildingIdAndAdvance(), daughter.Id, 1, 1, new CellCoord(105, 100), 0);
        world.AddBuilding(building);
        var head = MakeNpc(world, 1, daughter.Location);
        head.JoinCity(daughter.Id);
        world.AddNpc(head);
        var household = new Household(new HouseholdId(1), daughter.Location, head.Id, [head.Id], city: daughter.Id);
        household.BeginRelocation(daughter.Id);
        world.AddHousehold(household);
        var thirdCity = new City(world.NextCityId(), new CellCoord(250, 250), 0, null, AggregatePopulationPool.Empty);
        world.AddCity(thirdCity);
        var arrivingHead = MakeNpc(world, 2, thirdCity.Location);
        arrivingHead.JoinCity(thirdCity.Id);
        world.AddNpc(arrivingHead);
        var arrivingHousehold = new Household(
            new HouseholdId(2), thirdCity.Location, arrivingHead.Id, [arrivingHead.Id], city: thirdCity.Id);
        arrivingHousehold.BeginRelocation(daughter.Id);
        world.AddHousehold(arrivingHousehold);
        var workplace = new Workplace(
            new WorkplaceId(1), new LocationType(1), daughter.Location, 2, [],
            new Dictionary<ResourceType, long>(), Money.Zero, new Dictionary<ResourceType, long>(), daughter.Id);
        world.AddWorkplace(workplace);
        var system = new SpatialSettlementFoundingSystem();
        system.Tick(world, MakeCtx(world));
        var pending = Assert.Single(world.PendingEvents);
        var sink = new RecordingSink();

        system.HandleEvent(world, new TickContext(world, world.Rng, world.Scheduler, sink), pending);

        Assert.Equal(mother.Id, daughter.MergedIntoCityId);
        Assert.Contains(daughter, world.Cities);
        Assert.Equal(mother.Id, building.City);
        Assert.Equal(mother.Id, household.City);
        Assert.Null(household.PendingRelocationCity);
        Assert.Equal(thirdCity.Id, arrivingHousehold.City);
        Assert.Equal(mother.Id, arrivingHousehold.PendingRelocationCity);
        Assert.Equal(mother.Id, head.City);
        Assert.Equal(mother.Id, workplace.City);
        Assert.Equal(mother.Id, project.City);
        Assert.Contains(project, mother.ConstructionQueue);
        Assert.Empty(daughter.ConstructionQueue);
        Assert.Equal(12, mother.Stock[new ResourceType(1)]);
        Assert.Empty(daughter.Stock);
        Assert.Equal(new AggregatePopulationPool(3, 30, 120), mother.AggregatePool);
        Assert.Equal(AggregatePopulationPool.Empty, daughter.AggregatePool);
        Assert.Equal(3, mother.PoolNpcIds.Count);
        Assert.Empty(daughter.PoolNpcIds);
        Assert.Contains(pooledNpc, mother.PoolNpcIds);
        Assert.Equal(2, world.ActiveCities().Count());
        Assert.DoesNotContain(daughter, world.ActiveCities());
        Assert.False(CityProjector.Build(world, daughter.Id).IsSuccess);
        Assert.DoesNotContain(GlobalProjector.Build(world).Cities, city => city.Id == daughter.Id);
        new MigrationSystem().Tick(world, MakeCtx(world));
        Assert.Null(household.PendingRelocationCity);
        Assert.Equal(mother.Id, arrivingHousehold.PendingRelocationCity);
        var merged = Assert.Single(sink.Events);
        Assert.Equal(WorldEventKind.CityMerged, merged.Kind);
        Assert.Equal(world.CurrentDate.TotalHours, merged.Tick);
        Assert.Equal($"{daughter.Id.Value}|{mother.Id.Value}", merged.Payload);
    }

    [Fact]
    public void Merge_state_survives_snapshot_round_trip_before_and_after_confirmation()
    {
        var world = MakeWorld(MakeRules(organizationTicks: 10), seed: 915);
        var mother = new City(world.NextCityId(), new CellCoord(100, 100), 0, null, AggregatePopulationPool.Empty);
        var daughter = new City(world.NextCityId(), new CellCoord(105, 100), 1, mother.Id, AggregatePopulationPool.Empty);
        world.AddCity(mother);
        world.AddCity(daughter);
        var system = new SpatialSettlementFoundingSystem();
        system.Tick(world, MakeCtx(world));

        var scheduledHash = WorldSnapshot.CanonicalHash(world);
        var scheduledWorld = WorldSnapshot.Deserialize(WorldSnapshot.Serialize(world));
        Assert.Equal(scheduledHash, WorldSnapshot.CanonicalHash(scheduledWorld));
        var scheduledDaughter = scheduledWorld.FindCity(daughter.Id)!;
        Assert.Equal(world.CurrentDate.TotalHours, scheduledDaughter.MergeScheduledAtTick);

        system.HandleEvent(scheduledWorld, MakeCtx(scheduledWorld), Assert.Single(scheduledWorld.PendingEvents));
        var mergedHash = WorldSnapshot.CanonicalHash(scheduledWorld);
        var mergedWorld = WorldSnapshot.Deserialize(WorldSnapshot.Serialize(scheduledWorld));

        Assert.Equal(mergedHash, WorldSnapshot.CanonicalHash(mergedWorld));
        Assert.Equal(mother.Id, mergedWorld.FindCity(daughter.Id)!.MergedIntoCityId);
        Assert.Equal(mother.Id, mergedWorld.FindActiveCity(daughter.Id)!.Id);
        Assert.Equal(2, mergedWorld.Cities.Count);
        Assert.Single(mergedWorld.ActiveCities());
    }

    [Fact]
    public void FindActiveCity_resolves_a_chain_of_merged_city_ids_to_the_final_active_city()
    {
        var world = MakeWorld(MakeRules(), seed: 916);
        var root = new City(world.NextCityId(), new CellCoord(100, 100), 0, null, AggregatePopulationPool.Empty);
        var middle = new City(world.NextCityId(), new CellCoord(105, 100), 1, root.Id, AggregatePopulationPool.Empty);
        var leaf = new City(world.NextCityId(), new CellCoord(110, 100), 2, middle.Id, AggregatePopulationPool.Empty);
        world.AddCity(root);
        world.AddCity(middle);
        world.AddCity(leaf);
        middle.MarkMergedInto(root.Id);
        leaf.MarkMergedInto(middle.Id);

        Assert.Equal(root, world.FindActiveCity(leaf.Id));
        Assert.Equal(root, Assert.Single(world.ActiveCities()));
    }
}
