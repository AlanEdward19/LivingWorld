using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Behavior;

/// <summary>Fase 4, task 14: deslocamento com custo real (NEEDS-14) e moradia — sono na
/// residência ou, sem-teto, no local atual com eficiência reduzida (NEEDS-15) — e consulta de
/// NPCs sem-teto (NEEDS-16).</summary>
public class BehaviorDecisionSystemTravelTests
{
    private static readonly WorldCalendar Calendar = new(24, 30, 12);

    private static readonly Personality Neutral =
        Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    private static readonly LifeStageRules Stages = LifeStageRules.Create(childMaxAge: 14, adultMaxAge: 64).Value!;

    private static readonly GeographyCatalog GeoCatalog = new(
        TerrainIds: new HashSet<int> { 1 }, BiomeIds: new HashSet<int> { 1 }, ResourceIds: new HashSet<int>());

    /// <summary>Mapa 2x1 com custo suficiente pra exigir mais de 1 tick de viagem entre as duas
    /// células (dist=1 × terrainFactor=1 × base=2.5 = 2.5 -> ceil = 3 ticks), pra provar que o
    /// deslocamento consome tempo de verdade, não só 1 tick trivial.</summary>
    private static WorldMap MakeTwoCellMap()
    {
        var cost = new CostWeights(Base: 2.5, AltitudeWeight: 0, TerrainWeight: new Dictionary<int, double> { [1] = 1.0 });
        var cells = new List<MapCell>
        {
            new(new CellCoord(0, 0), new TerrainType(1), new BiomeType(1), Altitude: 0, HasWater: false, Resources: []),
            new(new CellCoord(1, 0), new TerrainType(1), new BiomeType(1), Altitude: 0, HasWater: false, Resources: []),
        };
        var regions = RegionGrid.Partition(width: 2, height: 1, regionSize: 2);
        return WorldMap.Create(width: 2, height: 1, seed: 1, GeoCatalog, cost, cells, regions, settlements: []).Value!;
    }

    private static NeedsRules MakeRules(double homelessSleepEfficiency = 0.5) => NeedsRules.Create(
        hungerDecayPerHour: 0, thirstDecayPerHour: 0, sleepDecayPerHour: 0, socialDecayPerHour: 0,
        urgencyThreshold: 70, maxActionSelectionSteps: 10, hysteresisEnabled: false,
        continuityBonus: 0, homelessSleepEfficiency).Value!;

    private static ActionCatalog MakeCatalog() => ActionCatalog.Create(
        maxDurationHours: new Dictionary<ActionType, int>
        {
            [ActionType.Eat] = 1,
            [ActionType.Sleep] = 1,
            [ActionType.Work] = 1,
            [ActionType.Socialize] = 1,
            [ActionType.Travel] = 1,
            [ActionType.Idle] = 1,
        },
        routineSlots: [],
        defaultAction: ActionType.Idle).Value!;

    private static (WorldState World, Npc Npc) BuildWorldWithHousehold(
        WorldMap map, NeedsRules rules, ActionCatalog catalog, CellCoord npcLocation, CellCoord householdLocation, int sleep)
    {
        var world = new WorldState(
            Calendar, seed: 1, map, ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            rules, catalog, Stages);

        var npc = new Npc(
            new NpcId(1), "npc", Sex.Male, WorldDate.Epoch(Calendar).AddYears(-30), new CultureId(1), npcLocation,
            motherId: null, fatherId: null, household: new HouseholdId(1), health: 100,
            personality: Neutral, profession: ProfessionType.None, currentLocation: npcLocation,
            sleep: sleep);

        var household = new Household(new HouseholdId(1), householdLocation, npc.Id, [npc.Id]);
        world.AddNpc(npc);
        world.AddHousehold(household);
        world.AdvanceNpcIdTo(2);
        world.AdvanceHouseholdIdTo(2);

        return (world, npc);
    }

    private static (WorldState World, Npc Npc) BuildHomelessWorld(
        WorldMap map, NeedsRules rules, ActionCatalog catalog, CellCoord location, int sleep)
    {
        var world = new WorldState(
            Calendar, seed: 1, map, ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            rules, catalog, Stages);

        var npc = new Npc(
            new NpcId(1), "npc", Sex.Male, WorldDate.Epoch(Calendar).AddYears(-30), new CultureId(1), location,
            motherId: null, fatherId: null, household: null, health: 100,
            personality: Neutral, profession: ProfessionType.None, currentLocation: location,
            sleep: sleep, homelessSince: WorldDate.Epoch(Calendar));

        world.AddNpc(npc);
        world.AdvanceNpcIdTo(2);
        return (world, npc);
    }

    [Fact]
    public void Deciding_to_sleep_at_a_different_household_location_travels_there_consuming_real_ticks()
    {
        var map = MakeTwoCellMap();
        var origin = new CellCoord(0, 0);
        var homeLocation = new CellCoord(1, 0);
        long ticksNeeded = TravelResolution.TicksBetween(map, origin, homeLocation);
        Assert.True(ticksNeeded > 1, "cenário precisa de mais de 1 tick pra provar consumo real de tempo");

        var rules = MakeRules();
        var catalog = MakeCatalog();
        var (world, npc) = BuildWorldWithHousehold(map, rules, catalog, origin, homeLocation, sleep: 0);
        var clock = new WorldClock([new BehaviorDecisionSystem()]);

        clock.Tick(world);

        // NEEDS-14: decidiu ir dormir em casa — ação efetiva do tick é Travel, não Sleep, e o NPC
        // não chega nem executa o efeito de destino no mesmo tick em que decidiu ir.
        Assert.Equal(ActionType.Travel, npc.CurrentAction);
        Assert.Equal(origin, npc.CurrentLocation);
        Assert.Equal(0, npc.Sleep);

        // Roda ticks suficientes pra completar a viagem e depois a duração de Sleep.
        for (int i = 0; i < ticksNeeded + 5; i++)
            clock.Tick(world);

        Assert.Equal(homeLocation, npc.CurrentLocation);
        Assert.Equal(100, npc.Sleep);
    }

    [Fact]
    public void Homeless_npc_sleeps_at_its_current_location_with_reduced_efficiency_without_throwing()
    {
        var map = MakeTwoCellMap();
        var location = new CellCoord(0, 0);
        var rules = MakeRules(homelessSleepEfficiency: 0.4);
        var catalog = MakeCatalog();
        var (world, npc) = BuildHomelessWorld(map, rules, catalog, location, sleep: 0);
        var clock = new WorldClock([new BehaviorDecisionSystem()]);

        var exception = Record.Exception(() =>
        {
            for (int i = 0; i < 5; i++)
                clock.Tick(world);
        });

        Assert.Null(exception);
        Assert.Equal(location, npc.CurrentLocation); // nunca viaja pra dormir sem residência
        Assert.Equal((int)(100 * rules.HomelessSleepEfficiency), npc.Sleep);
    }

    [Fact]
    public void Homeless_npcs_are_queryable_by_HomelessSince()
    {
        var map = MakeTwoCellMap();
        var rules = MakeRules();
        var catalog = MakeCatalog();
        var (world, homelessNpc) = BuildHomelessWorld(map, rules, catalog, new CellCoord(0, 0), sleep: 100);
        var (housedWorld, housedNpc) = BuildWorldWithHousehold(
            map, rules, catalog, new CellCoord(0, 0), new CellCoord(0, 0), sleep: 100);

        Assert.Contains(homelessNpc, world.Npcs.Where(n => n.HomelessSince is not null));
        Assert.DoesNotContain(housedNpc, housedWorld.Npcs.Where(n => n.HomelessSince is not null));
    }

    [Fact]
    public void Npc_that_dies_in_transit_never_arrives_or_applies_the_destination_action_effect()
    {
        var map = MakeTwoCellMap();
        var origin = new CellCoord(0, 0);
        var homeLocation = new CellCoord(1, 0);
        long ticksNeeded = TravelResolution.TicksBetween(map, origin, homeLocation);

        var rules = MakeRules();
        var catalog = MakeCatalog();
        var (world, npc) = BuildWorldWithHousehold(map, rules, catalog, origin, homeLocation, sleep: 0);
        var clock = new WorldClock([new BehaviorDecisionSystem()]);

        clock.Tick(world); // decide ir dormir em casa -> Travel
        Assert.Equal(ActionType.Travel, npc.CurrentAction);

        npc.Die(world.CurrentDate); // morre em trânsito (evento de morte, Fase 3, processa antes)

        for (int i = 0; i < ticksNeeded + 5; i++)
            clock.Tick(world);

        Assert.Equal(origin, npc.CurrentLocation); // nunca chegou
        Assert.Equal(0, npc.Sleep); // efeito de Sleep nunca aplicado
    }
}
