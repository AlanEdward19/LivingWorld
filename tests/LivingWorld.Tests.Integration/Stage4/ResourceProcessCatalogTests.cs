using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Decision;
using LivingWorld.Simulation.Behavior.Needs;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Production;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Integration.Stage4;

/// <summary>Fase 15.1, Stage 4, T14 (LWV-03): catálogo de recurso/processo — preparação,
/// comestibilidade, insumos estagiados e conservação em conclusão/cancelamento/morte.</summary>
public class ResourceProcessCatalogTests
{
    private static readonly ResourceType Wheat = new(1);
    private static readonly ResourceType Bread = new(3);
    private static readonly WorldCalendar Calendar = new(24, 30, 12);
    private static readonly Personality Neutral =
        Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    private static readonly EconomyRules Economy = EconomyRules.Create(
        enabled: true, foodResourceId: 1, waterResourceId: 2,
        capacityByResourceLocation: new Dictionary<(int, int), long>(),
        spoilagePerDayByResource: new Dictionary<int, double>(),
        wageByProfession: new Dictionary<int, long>(),
        priceFloor: new Dictionary<int, long>(), priceCeiling: new Dictionary<int, long>(),
        priceSensitivity: 0.1, demandBaselinePerNpc: new Dictionary<int, double>()).Value!;

    private static readonly ResourceCatalog WheatInedible = new(new Dictionary<int, ResourceSpec>
    {
        [1] = ResourceSpec.Create(1, PreparationState.Raw, edible: false).Value!,
        [3] = ResourceSpec.Create(3, PreparationState.Prepared, edible: true).Value!,
    });

    private static readonly ProcessRecipe CookBread = ProcessRecipe.Create(
        ProcessKind.Cook, new Dictionary<int, long> { [1] = 2 }, outputResourceId: 3, outputQuantity: 1,
        workplaceTypeId: null, durationTicks: 2).Value!;

    private static WorldState MakeWorld(IReadOnlyList<ProcessRecipe>? recipes = null)
    {
        return new WorldState(
            Calendar, seed: 14, ScenarioRunner.DefaultMap(14), ScenarioRunner.DefaultPopulationCatalog,
            ScenarioRunner.DefaultPopulationRules, ScenarioRunner.DefaultNeedsRules,
            ScenarioRunner.DefaultActionCatalog, ScenarioRunner.DefaultLifeStageRules,
            economyRules: Economy, resourceCatalog: WheatInedible, processRecipes: recipes ?? [CookBread]);
    }

    private static (WorldState World, Npc Npc, Household Household) ResidentWithWheat(long wheat = 4)
    {
        var world = MakeWorld();
        var location = new CellCoord(1, 1);
        var npc = new Npc(
            world.NextNpcIdAndAdvance(), "cook", Sex.Female, WorldDate.Epoch(Calendar).AddYears(-30), new CultureId(1),
            location, null, null, world.NextHouseholdIdAndAdvance(), 100, Neutral, ProfessionType.None, location,
            hunger: 0, currentAction: ActionType.Eat, actionStartedAtTick: 0);
        var household = new Household(npc.Household!.Value, location, npc.Id, [npc.Id],
            new Dictionary<ResourceType, long> { [Wheat] = wheat });
        world.AddNpc(npc);
        world.AddHousehold(household);
        var ctx = new TickContext(world, world.Rng, world.Scheduler);
        NpcWakeScheduler.ScheduleWake(world, ctx, npc.Id.Value, world.CurrentDate.TotalHours + 1);
        return (world, npc, household);
    }

    [Fact]
    public void Resource_spec_rejects_non_positive_id()
    {
        var result = ResourceSpec.Create(0, PreparationState.Raw, true);

        Assert.False(result.IsSuccess);
        Assert.Contains("Resources[].Id", result.Error);
    }

    [Fact]
    public void Process_recipe_rejects_non_positive_duration_or_output()
    {
        Assert.Contains("DurationTicks", ProcessRecipe.Create(
            ProcessKind.Cook, new Dictionary<int, long>(), 3, 1, null, 0).Error);
        Assert.Contains("OutputQuantity", ProcessRecipe.Create(
            ProcessKind.Cook, new Dictionary<int, long>(), 3, 0, null, 1).Error);
    }

    [Fact]
    public void Loader_parses_valid_resource_and_process_schema()
    {
        var json = """
            {
              "Resources": [
                { "Id": 1, "Preparation": "Raw", "Edible": false },
                { "Id": 3, "Preparation": "Prepared", "Edible": true }
              ],
              "ProcessRecipes": [
                { "Kind": "Cook", "Inputs": { "1": 2 }, "OutputResourceId": 3, "OutputQuantity": 1, "DurationTicks": 4 }
              ]
            }
            """;

        var result = ResourceProcessCatalogLoader.Load(json);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Catalog.IsEdible(Wheat));
        Assert.True(result.Value.Catalog.IsEdible(Bread));
        Assert.Equal(ProcessKind.Cook, Assert.Single(result.Value.Recipes).Kind);
    }

    [Fact]
    public void Loader_rejects_invalid_schema_naming_the_field()
    {
        var missingPrep = ResourceProcessCatalogLoader.Load("""{ "Resources": [ { "Id": 1, "Edible": false } ] }""");
        var unknownKind = ResourceProcessCatalogLoader.Load(
            """{ "ProcessRecipes": [ { "Kind": "Alchemy", "OutputResourceId": 3, "OutputQuantity": 1, "DurationTicks": 1 } ] }""");
        var duplicate = ResourceProcessCatalogLoader.Load(
            """{ "Resources": [ { "Id": 1, "Preparation": "Raw", "Edible": false }, { "Id": 1, "Preparation": "Prepared", "Edible": true } ] }""");

        Assert.Contains("Preparation", missingPrep.Error);
        Assert.Contains("Kind", unknownKind.Error);
        Assert.Contains("duplicado", duplicate.Error);
    }

    [Fact]
    public void Raw_wheat_does_not_restore_hunger_when_catalogued_inedible()
    {
        var (world, npc, household) = ResidentWithWheat();
        var clock = new WorldClock([new BehaviorDecisionSystem()]);
        int eatHours = world.ActionCatalog.MaxDurationHours[ActionType.Eat];
        for (int i = 0; i < eatHours; i++)
            clock.Tick(world);

        Assert.Equal(0, npc.Hunger);
        Assert.Equal(4, household.Stock[Wheat]);
    }

    [Fact]
    public void Completing_a_cook_process_consumes_inputs_and_creates_the_prepared_output()
    {
        var (world, npc, household) = ResidentWithWheat();
        var started = ResourceProcessSystem.Start(world, npc, CookBread, now: 0);
        Assert.True(started.IsSuccess);
        Assert.Equal(2, household.Stock[Wheat]);

        world.CurrentDate = new WorldDate(Calendar, 2);
        new ResourceProcessSystem().Tick(world, new TickContext(world, world.Rng, world.Scheduler));

        Assert.Equal(ProcessStatus.Completed, started.Value!.Status);
        Assert.Equal(2, household.Stock[Wheat]);
        Assert.Equal(1, household.Stock[Bread]);
        Assert.Equal(2, world.ResourceConsumed[Wheat]);
        Assert.Equal(1, world.ResourceProduced[Bread]);
        Assert.True(world.ResourceCatalog.IsEdible(Bread));
        Assert.False(world.ResourceCatalog.IsEdible(Wheat));
    }

    [Fact]
    public void Cancelling_a_process_refunds_reserved_inputs_and_creates_no_output()
    {
        var (world, npc, household) = ResidentWithWheat();
        var process = ResourceProcessSystem.Start(world, npc, CookBread, now: 0).Value!;

        ResourceProcessSystem.Cancel(world, process);

        Assert.Equal(ProcessStatus.Cancelled, process.Status);
        Assert.Equal(4, household.Stock[Wheat]);
        Assert.False(household.Stock.ContainsKey(Bread));
        Assert.Equal(0, world.ResourceConsumed.GetValueOrDefault(Wheat));
        Assert.Equal(0, world.ResourceProduced.GetValueOrDefault(Bread));
    }

    [Fact]
    public void Actor_death_refunds_reserved_inputs_instead_of_finishing_the_process()
    {
        var (world, npc, household) = ResidentWithWheat();
        var process = ResourceProcessSystem.Start(world, npc, CookBread, now: 0).Value!;
        npc.Die(world.CurrentDate);
        world.CurrentDate = new WorldDate(Calendar, 4);

        new ResourceProcessSystem().Tick(world, new TickContext(world, world.Rng, world.Scheduler));

        Assert.Equal(ProcessStatus.Cancelled, process.Status);
        Assert.Equal(4, household.Stock[Wheat]);
        Assert.False(household.Stock.ContainsKey(Bread));
    }

    [Fact]
    public void Missing_workplace_blocks_a_recipe_that_requires_one()
    {
        var (world, npc, _) = ResidentWithWheat();
        var kitchen = ProcessRecipe.Create(
            ProcessKind.Cook, new Dictionary<int, long> { [1] = 1 }, 3, 1, workplaceTypeId: 9, durationTicks: 1).Value!;

        var result = ResourceProcessSystem.Start(world, npc, kitchen, now: 0);

        Assert.False(result.IsSuccess);
        Assert.Contains("workplace", result.Error);
        Assert.Equal(4, world.FindHousehold(npc.Household!.Value)!.Stock[Wheat]);
    }
}
