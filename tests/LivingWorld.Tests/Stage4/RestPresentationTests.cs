using LivingWorld.Api.Visual;
using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Stage4;

/// <summary>Fase 15.1, Stage 4, T13 (LWV-03.1/LWV-06): qualidade/progresso de descanso chegam
/// ao inspector e à pista de mapa via processo projetado, com replay e rótulo acessível.</summary>
public class RestPresentationTests
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
    private static readonly RestPlaceCatalog Catalog = RestPlaceCatalog.Create(0.4, 0.7, 1.0).Value!;

    private static ActionCatalog SleepCatalog(int duration = 8) => ActionCatalog.Create(
        maxDurationHours: new Dictionary<ActionType, int>
        {
            [ActionType.Eat] = 1,
            [ActionType.Sleep] = duration,
            [ActionType.Work] = 1,
            [ActionType.Socialize] = 1,
            [ActionType.Travel] = 1,
            [ActionType.Idle] = 1,
            [ActionType.Buy] = 1,
        },
        routineSlots: [], defaultAction: ActionType.Idle).Value!;

    private static WorldMap OneCellMap()
    {
        var cost = new CostWeights(Base: 1, AltitudeWeight: 0, TerrainWeight: new Dictionary<int, double> { [1] = 1.0 });
        var cell = new MapCell(new CellCoord(0, 0), new TerrainType(1), new BiomeType(1), 0, false, []);
        var regions = RegionGrid.Partition(width: 1, height: 1, regionSize: 1);
        return WorldMap.Create(1, 1, seed: 1, GeoCatalog, cost, [cell], regions, []).Value!;
    }

    private static WorldMap TwoCellMap()
    {
        var cost = new CostWeights(Base: 2.5, AltitudeWeight: 0, TerrainWeight: new Dictionary<int, double> { [1] = 1.0 });
        var cells = new List<MapCell>
        {
            new(new CellCoord(0, 0), new TerrainType(1), new BiomeType(1), 0, false, []),
            new(new CellCoord(1, 0), new TerrainType(1), new BiomeType(1), 0, false, []),
        };
        return WorldMap.Create(2, 1, seed: 1, GeoCatalog, cost, cells, RegionGrid.Partition(2, 1, 2), []).Value!;
    }

    private static (WorldState World, Npc Npc) HousedSleeper(bool withBed, int duration = 8, CellCoord? home = null)
    {
        var location = home ?? new CellCoord(0, 0);
        var world = new WorldState(
            Calendar, 13, OneCellMap(), ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            Rules, SleepCatalog(duration), Stages, restPlaceCatalog: Catalog);
        var npc = new Npc(
            world.NextNpcIdAndAdvance(), "lina", Sex.Female, WorldDate.Epoch(Calendar).AddYears(-27), new CultureId(1), location,
            null, null, world.NextHouseholdIdAndAdvance(), 100, Neutral, ProfessionType.None, location);
        var household = new Household(npc.Household!.Value, location, npc.Id, [npc.Id]);
        world.AddNpc(npc);
        world.AddHousehold(household);
        if (withBed)
            world.AddRestPlace(new RestPlace(world.NextRestPlaceIdAndAdvance(), RestPlaceKind.Bed, location, household.Id));
        npc.SetCurrentAction(ActionType.Sleep, 0);
        return (world, npc);
    }

    [Fact]
    public void Inspection_exposes_dwelling_quality_location_and_remaining_duration_while_sleeping()
    {
        var (world, npc) = HousedSleeper(withBed: false, duration: 8);
        world.CurrentDate = new WorldDate(Calendar, 3);

        var rest = NpcInspectionQuery.Inspect(world, npc.Id).Value!.Rest;

        Assert.NotNull(rest);
        Assert.Equal(RestPlaceKind.Dwelling, rest.Kind);
        Assert.Equal(0.7, rest.Quality);
        Assert.Equal(new CellCoord(0, 0), rest.Location);
        Assert.Equal(5, rest.RemainingHours);
        Assert.False(rest.Blocked);
    }

    [Fact]
    public void Bed_sleep_inspection_uses_bed_quality()
    {
        var (world, npc) = HousedSleeper(withBed: true);

        var rest = NpcInspectionQuery.Inspect(world, npc.Id).Value!.Rest!;

        Assert.Equal(RestPlaceKind.Bed, rest.Kind);
        Assert.Equal(1.0, rest.Quality);
    }

    [Fact]
    public void Scope_projection_emits_a_rest_process_with_progress_and_friendly_descriptor()
    {
        var (world, npc) = HousedSleeper(withBed: true, duration: 8);
        world.CurrentDate = new WorldDate(Calendar, 2);
        var city = new City(world.NextCityId(), npc.CurrentLocation, 0, null, AggregatePopulationPool.Empty);
        world.AddCity(city);
        npc.JoinCity(city.Id);

        var state = LivingScopeProjector.Build(world, new VisualScope(VisualScopeKind.City, city.Id.ToString()));
        var process = Assert.Single(state.Processes);

        Assert.Equal("rest", process.Kind);
        Assert.Equal(npc.Id.Value, process.TargetId);
        Assert.Equal("sleep-bed", process.DescriptorKey);
        Assert.Equal(1.0, process.Quality);
        Assert.Equal(6, process.RemainingHours);
        Assert.Equal(new CellCoord(0, 0), process.Location);
        Assert.Equal(0.25, process.Progress, 3);
        Assert.DoesNotContain("Bed", process.DescriptorKey); // nunca o enum cru
        Assert.DoesNotContain("RestPlaceKind", process.DescriptorKey);
    }

    [Fact]
    public void Rest_process_delta_replays_to_the_fresh_projection()
    {
        var (world, npc) = HousedSleeper(withBed: false, duration: 8);
        var city = new City(world.NextCityId(), npc.CurrentLocation, 0, null, AggregatePopulationPool.Empty);
        world.AddCity(city);
        npc.JoinCity(city.Id);
        var scope = new VisualScope(VisualScopeKind.City, city.Id.ToString());

        var before = LivingScopeProjector.Build(world, scope);
        world.CurrentDate = new WorldDate(Calendar, 4);
        var after = LivingScopeProjector.Build(world, scope);
        var replayed = LivingDeltaReducer.Apply(before, ScopeDeltaBuilder.Diff(4, before, after));

        Assert.Equal(after, replayed);
        Assert.Equal(4, Assert.Single(replayed.Processes).RemainingHours);
    }

    [Fact]
    public void Unreachable_sleep_is_marked_blocked_in_inspection_and_does_not_invent_progress_effects()
    {
        var origin = new CellCoord(0, 0);
        var world = new WorldState(
            Calendar, 13, TwoCellMap(), ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            Rules, SleepCatalog(8), Stages, restPlaceCatalog: Catalog);
        var npc = new Npc(
            world.NextNpcIdAndAdvance(), "lina", Sex.Female, WorldDate.Epoch(Calendar).AddYears(-27), new CultureId(1), origin,
            null, null, world.NextHouseholdIdAndAdvance(), 100, Neutral, ProfessionType.None, origin, sleep: 0);
        world.AddNpc(npc);
        world.AddHousehold(new Household(npc.Household!.Value, new CellCoord(9, 9), npc.Id, [npc.Id]));
        npc.SetCurrentAction(ActionType.Sleep, 0);

        var rest = NpcInspectionQuery.Inspect(world, npc.Id).Value!.Rest!;

        Assert.True(rest.Blocked);
        Assert.Equal(new CellCoord(9, 9), rest.Location);
        Assert.Equal(0, npc.Sleep);
    }

    [Fact]
    public void Remaining_duration_drops_as_canonical_ticks_elapse()
    {
        var (world, npc) = HousedSleeper(withBed: false, duration: 8);
        var first = NpcInspectionQuery.Inspect(world, npc.Id).Value!.Rest!.RemainingHours;
        world.CurrentDate = new WorldDate(Calendar, 3);
        var later = NpcInspectionQuery.Inspect(world, npc.Id).Value!.Rest!.RemainingHours;

        Assert.Equal(8, first);
        Assert.Equal(5, later);
        Assert.True(later < first);
    }
}
