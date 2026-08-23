using LivingWorld.Api.Visual;
using LivingWorld.Domain;
using LivingWorld.Simulation;
using LivingWorld.Simulation.Behavior;
using LivingWorld.Simulation.Economy;

namespace LivingWorld.Tests.Stage4;

/// <summary>Fase 15.1, Stage 4, T16 (LWV-03.5/LWV-06): cadeia de comida collect→cook→eat —
/// trigo cru não restaura fome; refeição preparada sim; lugar de cozinha válido; conservação,
/// pista visual e replay.</summary>
public class CookingLifecycleTests
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
    private static readonly ResourceType Water = new(2);
    private static readonly ProcessRecipe Cook = ProcessRecipe.Create(
        ProcessKind.Cook, new Dictionary<int, long> { [1] = 2, [2] = 1 }, 3, 1, workplaceTypeId: 1, durationTicks: 2).Value!;

    private static ActionCatalog EatCatalog() => ActionCatalog.Create(
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
        routineSlots: [], defaultAction: ActionType.Idle).Value!;

    private static (WorldState World, Npc Npc, Household Household) Kitchen(long wheat = 4, bool employ = true)
    {
        var world = new WorldState(
            Calendar, 16, ScenarioRunner.DefaultMap(16), ScenarioRunner.DefaultPopulationCatalog,
            ScenarioRunner.DefaultPopulationRules, ScenarioRunner.DefaultNeedsRules, EatCatalog(),
            ScenarioRunner.DefaultLifeStageRules, economyRules: Economy, resourceCatalog: Catalog,
            processRecipes: [Cook]);
        var here = ScenarioRunner.DefaultVillageLocation;
        var npc = new Npc(
            world.NextNpcIdAndAdvance(), "cook", Sex.Female, WorldDate.Epoch(Calendar).AddYears(-28), new CultureId(1),
            here, null, null, world.NextHouseholdIdAndAdvance(), 100, Neutral, ProfessionType.None, here, hunger: 0);
        var household = new Household(npc.Household!.Value, here, npc.Id, [npc.Id],
            new Dictionary<ResourceType, long> { [Wheat] = wheat, [Water] = 1 });
        world.AddNpc(npc);
        world.AddHousehold(household);
        if (employ)
        {
            var workplace = new Workplace(
                world.NextWorkplaceIdAndAdvance(), new LocationType(1), here, 1, [npc.Id],
                new Dictionary<ResourceType, long>(), new Money(0), new Dictionary<ResourceType, long>());
            world.AddWorkplace(workplace);
            npc.Hire(workplace.Id);
        }
        var ctx = new TickContext(world, world.Rng, world.Scheduler);
        NpcWakeScheduler.ScheduleWake(world, ctx, npc.Id.Value, 1);
        return (world, npc, household);
    }

    private static void Finish(WorldState world, ResourceProcess process)
    {
        world.CurrentDate = new WorldDate(Calendar, process.CompletesAtTick);
        new ResourceProcessSystem().Tick(world, new TickContext(world, world.Rng, world.Scheduler));
    }

    private static void EatUntilComplete(WorldState world)
    {
        var clock = new WorldClock([new BehaviorDecisionSystem()]);
        int hours = world.ActionCatalog.MaxDurationHours[ActionType.Eat] + 1;
        for (int i = 0; i < hours; i++)
            clock.Tick(world);
    }

    [Fact]
    public void Raw_wheat_does_not_restore_hunger()
    {
        var (world, npc, household) = Kitchen();

        EatUntilComplete(world);

        Assert.Equal(0, npc.Hunger);
        Assert.Equal(4, household.Stock[Wheat]);
        Assert.False(household.Stock.ContainsKey(Bread));
    }

    [Fact]
    public void Cooking_without_household_water_is_blocked()
    {
        var (world, npc, household) = Kitchen();
        household.Withdraw(Water, 1);

        var result = ResourceProcessSystem.Start(world, npc, Cook, now: 0);

        Assert.False(result.IsSuccess);
        Assert.Equal(4, household.Stock[Wheat]);
        Assert.Equal(0, household.Stock.GetValueOrDefault(Water));
    }

    [Fact]
    public void Cooking_away_from_a_valid_kitchen_is_blocked()
    {
        var (world, npc, household) = Kitchen(employ: false);

        var result = ResourceProcessSystem.Start(world, npc, Cook, now: 0);

        Assert.Contains("workplace", result.Error);
        Assert.Equal(4, household.Stock[Wheat]);
    }

    [Fact]
    public void Collecting_inputs_and_cooking_creates_prepared_edible_bread()
    {
        var (world, npc, household) = Kitchen();

        var cooked = ResourceProcessSystem.Start(world, npc, Cook, now: 0);
        Assert.True(cooked.IsSuccess);
        Assert.Equal(2, household.Stock[Wheat]);
        Finish(world, cooked.Value!);

        Assert.Equal(ProcessStatus.Completed, cooked.Value!.Status);
        Assert.Equal(1, household.Stock[Bread]);
        Assert.True(world.ResourceCatalog.IsEdible(Bread));
        Assert.False(world.ResourceCatalog.IsEdible(Wheat));
    }

    [Fact]
    public void Eating_prepared_bread_restores_hunger_and_consumes_it()
    {
        var (world, npc, household) = Kitchen();
        var cooked = ResourceProcessSystem.Start(world, npc, Cook, now: 0).Value!;
        Finish(world, cooked);
        npc.SetCurrentAction(ActionType.Eat, world.CurrentDate.TotalHours);
        var ctx = new TickContext(world, world.Rng, world.Scheduler);
        NpcWakeScheduler.ScheduleWake(world, ctx, npc.Id.Value, world.CurrentDate.TotalHours + 1);

        EatUntilComplete(world);

        Assert.Equal(100, npc.Hunger);
        Assert.Equal(0, household.Stock.GetValueOrDefault(Bread));
        Assert.Equal(1, world.ResourceConsumed.GetValueOrDefault(Bread));
    }

    [Fact]
    public void Cook_plus_eat_conserves_the_prepared_output()
    {
        var (world, npc, household) = Kitchen();
        var cooked = ResourceProcessSystem.Start(world, npc, Cook, now: 0).Value!;
        Finish(world, cooked);
        Assert.Equal(2, world.ResourceConsumed[Wheat]);
        Assert.Equal(1, world.ResourceProduced[Bread]);

        npc.SetCurrentAction(ActionType.Eat, world.CurrentDate.TotalHours);
        NpcWakeScheduler.ScheduleWake(world, new TickContext(world, world.Rng, world.Scheduler), npc.Id.Value,
            world.CurrentDate.TotalHours + 1);
        EatUntilComplete(world);

        Assert.Equal(0, household.Stock.GetValueOrDefault(Bread));
        Assert.Equal(1, world.ResourceConsumed[Bread]);
        Assert.Equal(2, household.Stock[Wheat]);
    }

    [Fact]
    public void Cooking_projects_a_friendly_process_cue()
    {
        var (world, npc, _) = Kitchen();
        ResourceProcessSystem.Start(world, npc, Cook, now: 0);
        var city = new City(world.NextCityId(), npc.CurrentLocation, 0, null, AggregatePopulationPool.Empty);
        world.AddCity(city);
        npc.JoinCity(city.Id);

        var process = Assert.Single(LivingScopeProjector.Build(world, new VisualScope(VisualScopeKind.World, "")).Processes);

        Assert.Equal("cook", process.Kind);
        Assert.Equal("cook-food", process.DescriptorKey);
        Assert.DoesNotContain("Cook", process.DescriptorKey);
        Assert.Equal(2, process.RemainingHours);
    }

    private static void JoinCity(WorldState world, Npc npc)
    {
        var city = new City(world.NextCityId(), npc.CurrentLocation, 0, null, AggregatePopulationPool.Empty);
        world.AddCity(city);
        npc.JoinCity(city.Id);
    }

    [Fact]
    public void Eating_projects_a_friendly_eat_prepared_process_cue()
    {
        var (world, npc, _) = Kitchen();
        var cooked = ResourceProcessSystem.Start(world, npc, Cook, now: 0).Value!;
        Finish(world, cooked);
        npc.SetCurrentAction(ActionType.Eat, world.CurrentDate.TotalHours);
        JoinCity(world, npc);
        var cityId = world.Cities.Single().Id.ToString();

        var process = Assert.Single(
            LivingScopeProjector.Build(world, new VisualScope(VisualScopeKind.City, cityId)).Processes,
            item => item.Kind == "food");

        Assert.Equal("eat-prepared", process.DescriptorKey);
        Assert.DoesNotContain("Eat", process.DescriptorKey);
    }

    [Fact]
    public void Collect_cook_and_eat_chain_projects_each_step_cue()
    {
        var (world, npc, _) = Kitchen();
        ResourceProcessSystem.Start(world, npc, Cook, now: 0);
        JoinCity(world, npc);
        var cityId = world.Cities.Single().Id.ToString();

        Assert.Equal("cook-food", Assert.Single(
            LivingScopeProjector.Build(world, new VisualScope(VisualScopeKind.City, cityId)).Processes).DescriptorKey);

        var cooked = world.ResourceProcesses.Single();
        Finish(world, cooked);
        npc.SetCurrentAction(ActionType.Eat, world.CurrentDate.TotalHours);

        var eatCue = Assert.Single(
            LivingScopeProjector.Build(world, new VisualScope(VisualScopeKind.City, cityId)).Processes,
            item => item.Kind == "food");
        Assert.Equal("eat-prepared", eatCue.DescriptorKey);
    }

    [Fact]
    public void Cooking_process_delta_replays_to_the_fresh_projection()
    {
        var (world, npc, _) = Kitchen();
        ResourceProcessSystem.Start(world, npc, Cook, now: 0);
        var city = new City(world.NextCityId(), npc.CurrentLocation, 0, null, AggregatePopulationPool.Empty);
        world.AddCity(city);
        npc.JoinCity(city.Id);
        var scope = new VisualScope(VisualScopeKind.World, "");

        var before = LivingScopeProjector.Build(world, scope);
        world.CurrentDate = new WorldDate(Calendar, 1);
        var after = LivingScopeProjector.Build(world, scope);
        var replayed = LivingDeltaReducer.Apply(before, ScopeDeltaBuilder.Diff(1, before, after));

        Assert.Equal(after, replayed);
        Assert.Equal(1, Assert.Single(replayed.Processes).RemainingHours);
    }
}
