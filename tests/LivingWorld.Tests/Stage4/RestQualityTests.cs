using LivingWorld.Domain;
using LivingWorld.Simulation;
using LivingWorld.Simulation.Behavior;

namespace LivingWorld.Tests.Stage4;

/// <summary>Fase 15.1, Stage 4, T12 (LWV-03.1): catálogo de lugares de descanso e sono com alvo
/// real — chão/casa/cama com eficiências distintas; lugar inalcançável bloqueia sem teleporte
/// nem efeito remoto.</summary>
public class RestQualityTests
{
    private static readonly WorldCalendar Calendar = new(24, 30, 12);

    private static readonly Personality Neutral =
        Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    private static readonly LifeStageRules Stages = LifeStageRules.Create(childMaxAge: 14, adultMaxAge: 64).Value!;

    private static readonly GeographyCatalog GeoCatalog = new(
        TerrainIds: new HashSet<int> { 1 }, BiomeIds: new HashSet<int> { 1 }, ResourceIds: new HashSet<int>());

    private static readonly NeedsRules Rules = NeedsRules.Create(
        hungerDecayPerHour: 0, thirstDecayPerHour: 0, sleepDecayPerHour: 0, socialDecayPerHour: 0,
        urgencyThreshold: 70, maxActionSelectionSteps: 10, hysteresisEnabled: false,
        continuityBonus: 0, homelessSleepEfficiency: 0.4).Value!;

    private static readonly RestPlaceCatalog Catalog = RestPlaceCatalog.Create(
        groundEfficiency: 0.4, dwellingEfficiency: 0.7, bedEfficiency: 1.0).Value!;

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

    private static ActionCatalog MakeSleepCatalog() => ActionCatalog.Create(
        maxDurationHours: new Dictionary<ActionType, int>
        {
            [ActionType.Eat] = 1,
            [ActionType.Sleep] = 1,
            [ActionType.Work] = 1,
            [ActionType.Socialize] = 1,
            [ActionType.Travel] = 1,
            [ActionType.Idle] = 100,
            [ActionType.Buy] = 1,
        },
        routineSlots: [],
        defaultAction: ActionType.Idle).Value!;

    private static (WorldState World, Npc Npc) BuildHomeless(WorldMap map, CellCoord location)
    {
        var world = new WorldState(
            Calendar, seed: 7, map, ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            Rules, MakeSleepCatalog(), Stages, restPlaceCatalog: Catalog);

        var npc = new Npc(
            world.NextNpcIdAndAdvance(), "npc", Sex.Male, WorldDate.Epoch(Calendar).AddYears(-30), new CultureId(1), location,
            motherId: null, fatherId: null, household: null, health: 100,
            personality: Neutral, profession: ProfessionType.None, currentLocation: location,
            sleep: 0, homelessSince: WorldDate.Epoch(Calendar));
        world.AddNpc(npc);
        Wake(world, npc);
        return (world, npc);
    }

    private static (WorldState World, Npc Npc, Household Household) BuildHoused(
        WorldMap map, CellCoord npcLocation, CellCoord householdLocation, bool withBed)
    {
        var world = new WorldState(
            Calendar, seed: 7, map, ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            Rules, MakeSleepCatalog(), Stages, restPlaceCatalog: Catalog);

        var npc = new Npc(
            world.NextNpcIdAndAdvance(), "npc", Sex.Male, WorldDate.Epoch(Calendar).AddYears(-30), new CultureId(1), npcLocation,
            motherId: null, fatherId: null, household: world.NextHouseholdIdAndAdvance(), health: 100,
            personality: Neutral, profession: ProfessionType.None, currentLocation: npcLocation,
            sleep: 0);
        var household = new Household(npc.Household!.Value, householdLocation, npc.Id, [npc.Id]);
        world.AddNpc(npc);
        world.AddHousehold(household);
        if (withBed)
            world.AddRestPlace(new RestPlace(world.NextRestPlaceIdAndAdvance(), RestPlaceKind.Bed, householdLocation, household.Id));
        Wake(world, npc);
        return (world, npc, household);
    }

    private static void Wake(WorldState world, Npc npc)
    {
        var ctx = new TickContext(world, world.Rng, world.Scheduler);
        NpcWakeScheduler.ScheduleWake(world, ctx, npc.Id.Value, world.CurrentDate.TotalHours + 1);
    }

    private static void TickUntilSleepSettles(WorldState world, int ticks = 5)
    {
        var clock = new WorldClock([new BehaviorDecisionSystem()]);
        for (int i = 0; i < ticks; i++)
            clock.Tick(world);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void Rest_place_catalog_rejects_efficiency_outside_unit_interval(double invalid)
    {
        Assert.Contains("RestPlaces.Ground", RestPlaceCatalog.Create(invalid, 1, 1).Error);
        Assert.Contains("RestPlaces.Dwelling", RestPlaceCatalog.Create(0.5, invalid, 1).Error);
        Assert.Contains("RestPlaces.Bed", RestPlaceCatalog.Create(0.5, 1, invalid).Error);
    }

    [Fact]
    public void Homeless_npc_sleeps_on_the_ground_at_catalogued_ground_efficiency()
    {
        var (world, npc) = BuildHomeless(MakeTwoCellMap(), new CellCoord(0, 0));

        TickUntilSleepSettles(world);

        Assert.Equal(new CellCoord(0, 0), npc.CurrentLocation);
        Assert.Equal((int)(100 * Catalog.GroundEfficiency), npc.Sleep);
    }

    [Fact]
    public void Housed_npc_without_a_bed_sleeps_at_dwelling_efficiency_not_ground()
    {
        var home = new CellCoord(0, 0);
        var (world, npc, _) = BuildHoused(MakeTwoCellMap(), home, home, withBed: false);

        TickUntilSleepSettles(world);

        Assert.Equal(home, npc.CurrentLocation);
        Assert.Equal((int)(100 * Catalog.DwellingEfficiency), npc.Sleep);
        Assert.NotEqual((int)(100 * Catalog.GroundEfficiency), npc.Sleep);
    }

    [Fact]
    public void Housed_npc_with_a_bed_sleeps_at_bed_efficiency()
    {
        var home = new CellCoord(0, 0);
        var (world, npc, _) = BuildHoused(MakeTwoCellMap(), home, home, withBed: true);

        TickUntilSleepSettles(world);

        Assert.Equal(home, npc.CurrentLocation);
        Assert.Equal((int)(100 * Catalog.BedEfficiency), npc.Sleep);
    }

    [Fact]
    public void Ground_dwelling_and_bed_recovery_are_strictly_increasing()
    {
        var map = MakeTwoCellMap();
        var home = new CellCoord(0, 0);

        var (groundWorld, groundNpc) = BuildHomeless(map, home);
        var (houseWorld, houseNpc, _) = BuildHoused(map, home, home, withBed: false);
        var (bedWorld, bedNpc, _) = BuildHoused(map, home, home, withBed: true);

        TickUntilSleepSettles(groundWorld);
        TickUntilSleepSettles(houseWorld);
        TickUntilSleepSettles(bedWorld);

        Assert.True(groundNpc.Sleep < houseNpc.Sleep, "chão deve recuperar menos que a moradia");
        Assert.True(houseNpc.Sleep < bedNpc.Sleep, "moradia deve recuperar menos que a cama");
        Assert.Equal((int)(100 * Catalog.GroundEfficiency), groundNpc.Sleep);
        Assert.Equal((int)(100 * Catalog.DwellingEfficiency), houseNpc.Sleep);
        Assert.Equal((int)(100 * Catalog.BedEfficiency), bedNpc.Sleep);
    }

    [Fact]
    public void Unreachable_rest_place_blocks_without_teleport_or_sleep_effect()
    {
        var origin = new CellCoord(0, 0);
        var offMap = new CellCoord(9, 9);
        var (world, npc, household) = BuildHoused(MakeTwoCellMap(), origin, offMap, withBed: false);
        var rest = RestPlaceResolver.Resolve(world, npc);
        Assert.Equal(offMap, rest.Location);
        Assert.False(RestPlaceResolver.IsReachable(world.Map, origin, household.Location));

        var exception = Record.Exception(() => TickUntilSleepSettles(world, ticks: 8));

        Assert.Null(exception);
        Assert.Equal(origin, npc.CurrentLocation);
        Assert.Equal(0, npc.Sleep);
        Assert.Equal(ActionType.Sleep, npc.CurrentAction);
    }

    [Fact]
    public void Sleep_does_not_restore_until_the_npc_arrives_at_the_rest_place()
    {
        var origin = new CellCoord(0, 0);
        var home = new CellCoord(1, 0);
        long ticksNeeded = TravelResolution.TicksBetween(MakeTwoCellMap(), origin, home);
        Assert.True(ticksNeeded > 1);

        var (world, npc, _) = BuildHoused(MakeTwoCellMap(), origin, home, withBed: false);
        var clock = new WorldClock([new BehaviorDecisionSystem()]);

        clock.Tick(world);
        Assert.Equal(ActionType.Travel, npc.CurrentAction);
        Assert.Equal(origin, npc.CurrentLocation);
        Assert.Equal(0, npc.Sleep);

        clock.Tick(world);
        Assert.Equal(origin, npc.CurrentLocation);
        Assert.Equal(0, npc.Sleep);

        for (int i = 0; i < ticksNeeded + 5; i++)
            clock.Tick(world);

        Assert.Equal(home, npc.CurrentLocation);
        Assert.Equal((int)(100 * Catalog.DwellingEfficiency), npc.Sleep);
    }

    [Fact]
    public void Omitted_rest_places_json_migrates_homeless_sleep_efficiency_to_the_ground_entry()
    {
        var json = """
            {
              "HungerDecayPerHour": 2, "ThirstDecayPerHour": 3, "SleepDecayPerHour": 1.5, "SocialDecayPerHour": 1,
              "UrgencyThreshold": 70, "MaxActionSelectionSteps": 10, "HysteresisEnabled": true,
              "ContinuityBonus": 5, "HomelessSleepEfficiency": 0.4,
              "MaxDurationHours": { "Eat": 1, "Sleep": 1, "Work": 1, "Socialize": 1, "Travel": 1, "Idle": 1, "Buy": 1 },
              "RoutineSlots": [], "DefaultAction": "Idle"
            }
            """;

        var result = BehaviorScenarioLoader.Load(json);

        Assert.True(result.IsSuccess);
        Assert.Equal(0.4, result.Value!.RestPlaceCatalog.GroundEfficiency);
        Assert.Equal(1.0, result.Value.RestPlaceCatalog.DwellingEfficiency);
        Assert.Equal(1.0, result.Value.RestPlaceCatalog.BedEfficiency);
        Assert.Equal(result.Value.NeedsRules.HomelessSleepEfficiency, result.Value.RestPlaceCatalog.GroundEfficiency);
    }
}
