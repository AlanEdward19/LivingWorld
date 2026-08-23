using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Cities;

/// <summary>dynamic-city-growth, T7 (CITYGROW-04): <see cref="SpatialSettlementFoundingSystem"/> —
/// funda uma cidade nova a partir de um cluster de overflow que reúne população materializada
/// suficiente pra cruzar a MESMA fórmula/limiar de <see cref="SettlementFoundingSystem"/>, nunca
/// um limiar mais fraco por contagem de prédios. Mesmo padrão de teste de
/// <see cref="SettlementFoundingSystemTests"/> (mundo em memória, `ctx.ScheduleEvent`/`HandleEvent`
/// diretos).</summary>
public class SpatialSettlementFoundingSystemTests
{
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

    /// <summary>Post-ship fix (Fix 2, 2026-08-23, ghost-town report): Household.Location é gravado
    /// uma única vez na criação e nunca atualizado depois -- checar contra ele (o comportamento
    /// antigo) quase nunca reatribui um household de verdade, porque ele nunca reflete pra onde a
    /// família se mudou desde então. Este é o repro real do bug: Location autorada longe de
    /// qualquer cluster, mas o chefe está fisicamente (Npc.CurrentLocation) dentro do cluster de
    /// overflow no momento da fundação -- o household DEVE ser reatribuído mesmo assim.</summary>
    [Fact]
    public void HandleEvent_reassigns_a_household_by_its_heads_current_location_even_when_household_location_is_stale()
    {
        var rules = MakeRules(foundingConcentrationThreshold: 0.5, organizationTicks: 10);
        var world = MakeWorld(rules, seed: 910);
        var (motherCity, overflow) = SeedOneOverflowBuilding(world);
        var head = MakeNpc(world, 1, overflow.Position!.Value); // CurrentLocation dentro do cluster
        world.AddNpc(head);
        // Location autorada em (0,0) -- bem longe do cluster em (200,200) -- nunca atualizada
        // depois da criação, mesmo que o chefe já esteja de fato morando junto ao overflow.
        var household = new Household(
            new HouseholdId(1), new CellCoord(0, 0), head.Id, [head.Id], city: motherCity.Id);
        world.AddHousehold(household);
        var system = new SpatialSettlementFoundingSystem();
        system.Tick(world, MakeCtx(world));
        var evt = Assert.Single(world.PendingEvents);

        system.HandleEvent(world, MakeCtx(world), evt);

        var newCity = world.Cities.Single(c => c.Id != motherCity.Id);
        Assert.Equal(newCity.Id, household.City);
        Assert.Equal(newCity.Id, head.City);
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
}
