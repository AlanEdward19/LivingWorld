using LivingWorld.Api.Visual;
using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Stage4;

/// <summary>Fase 15.1, Stage 4, T16 (LWV-03.2/LWV-06): recurso e preparo cru vs preparado no
/// inspector; pista <c>eat-prepared</c>/<c>eat-raw</c> no mapa; replay de delta.</summary>
public class FoodPresentationTests
{
    private static readonly WorldCalendar Calendar = new(24, 30, 12);
    private static readonly Personality Neutral =
        Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;
    private static readonly ResourceType Wheat = new(1);
    private static readonly ResourceType Bread = new(3);
    private static readonly EconomyRules Economy = EconomyRules.Create(
        enabled: true, foodResourceId: 1, waterResourceId: 2,
        capacityByResourceLocation: new Dictionary<(int, int), long>(),
        spoilagePerDayByResource: new Dictionary<int, double>(),
        wageByProfession: new Dictionary<int, long>(),
        priceFloor: new Dictionary<int, long>(), priceCeiling: new Dictionary<int, long>(),
        priceSensitivity: 0.1, demandBaselinePerNpc: new Dictionary<int, double>()).Value!;
    private static readonly ResourceCatalog Catalog = new(new Dictionary<int, ResourceSpec>
    {
        [1] = ResourceSpec.Create(1, PreparationState.Raw, edible: false).Value!,
        [2] = ResourceSpec.Create(2, PreparationState.Raw, edible: false).Value!,
        [3] = ResourceSpec.Create(3, PreparationState.Prepared, edible: true).Value!,
    });

    private static ActionCatalog EatCatalog() => ActionCatalog.Create(
        maxDurationHours: new Dictionary<ActionType, int>
        {
            [ActionType.Eat] = 2,
            [ActionType.Sleep] = 1,
            [ActionType.Work] = 1,
            [ActionType.Socialize] = 1,
            [ActionType.Travel] = 1,
            [ActionType.Idle] = 100,
            [ActionType.Buy] = 1,
        },
        routineSlots: [], defaultAction: ActionType.Idle).Value!;

    private static (WorldState World, Npc Npc) Eater(Dictionary<ResourceType, long>? stock = null)
    {
        var world = new WorldState(
            Calendar, 16, ScenarioRunner.DefaultMap(16), ScenarioRunner.DefaultPopulationCatalog,
            ScenarioRunner.DefaultPopulationRules, ScenarioRunner.DefaultNeedsRules, EatCatalog(),
            ScenarioRunner.DefaultLifeStageRules, economyRules: Economy, resourceCatalog: Catalog);
        var here = ScenarioRunner.DefaultVillageLocation;
        var npc = new Npc(
            world.NextNpcIdAndAdvance(), "eater", Sex.Female, WorldDate.Epoch(Calendar).AddYears(-28), new CultureId(1),
            here, null, null, world.NextHouseholdIdAndAdvance(), 100, Neutral, ProfessionType.None, here, hunger: 0);
        var household = new Household(npc.Household!.Value, here, npc.Id, [npc.Id], stock ?? []);
        world.AddNpc(npc);
        world.AddHousehold(household);
        npc.SetCurrentAction(ActionType.Eat, 0);
        return (world, npc);
    }

    [Fact]
    public void Inspection_names_prepared_food_and_raw_vs_prepared_state()
    {
        var (world, npc) = Eater(new Dictionary<ResourceType, long> { [Bread] = 1 });
        world.CurrentDate = new WorldDate(Calendar, 1);

        var food = NpcInspectionQuery.Inspect(world, npc.Id).Value!.Food;

        Assert.NotNull(food);
        Assert.Equal(3, food.ResourceId);
        Assert.Equal(PreparationState.Prepared, food.Preparation);
        Assert.Equal(1, food.RemainingHours);
        Assert.False(food.Blocked);
    }

    [Fact]
    public void Inspection_marks_blocked_when_only_raw_wheat_is_available()
    {
        var (world, npc) = Eater(new Dictionary<ResourceType, long> { [Wheat] = 4 });

        var food = NpcInspectionQuery.Inspect(world, npc.Id).Value!.Food;

        Assert.NotNull(food);
        Assert.True(food.Blocked);
        Assert.Equal(PreparationState.Raw, food.Preparation);
        Assert.Equal(0, food.ResourceId);
    }

    [Fact]
    public void Scope_projection_emits_eat_prepared_process_with_progress()
    {
        var (world, npc) = Eater(new Dictionary<ResourceType, long> { [Bread] = 1 });
        world.CurrentDate = new WorldDate(Calendar, 1);
        var city = new City(world.NextCityId(), npc.CurrentLocation, 0, null, AggregatePopulationPool.Empty);
        world.AddCity(city);
        npc.JoinCity(city.Id);

        var process = Assert.Single(LivingScopeProjector.Build(world, new VisualScope(VisualScopeKind.City, city.Id.ToString())).Processes);

        Assert.Equal("food", process.Kind);
        Assert.Equal("eat-prepared", process.DescriptorKey);
        Assert.Equal(npc.Id.Value, process.TargetId);
        Assert.Equal(1, process.RemainingHours);
        Assert.Equal(0.5, process.Progress, 3);
        Assert.DoesNotContain("Prepared", process.DescriptorKey);
    }

    [Fact]
    public void Food_process_delta_replays_to_the_fresh_projection()
    {
        var (world, npc) = Eater(new Dictionary<ResourceType, long> { [Bread] = 1 });
        var city = new City(world.NextCityId(), npc.CurrentLocation, 0, null, AggregatePopulationPool.Empty);
        world.AddCity(city);
        npc.JoinCity(city.Id);
        var scope = new VisualScope(VisualScopeKind.City, city.Id.ToString());

        var before = LivingScopeProjector.Build(world, scope);
        world.CurrentDate = new WorldDate(Calendar, 1);
        var after = LivingScopeProjector.Build(world, scope);
        var replayed = LivingDeltaReducer.Apply(before, ScopeDeltaBuilder.Diff(1, before, after));

        Assert.Equal(after, replayed);
        Assert.Equal("eat-prepared", Assert.Single(replayed.Processes).DescriptorKey);
    }
}
