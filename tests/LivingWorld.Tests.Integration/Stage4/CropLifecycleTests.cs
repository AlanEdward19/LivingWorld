using LivingWorld.Api.Visual.Scope;
using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Production;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Integration.Stage4;

/// <summary>Fase 15.1, Stage 4, T17 (LWV-03.3/LWV-06): cadeia de cultivo
/// plant→water→mature→harvest — sem trigo instantâneo nem colheita antecipada; pistas e replay.</summary>
public class CropLifecycleTests
{
    private static readonly WorldCalendar Calendar = new(24, 30, 12);
    private static readonly Personality Neutral =
        Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;
    private static readonly ResourceType Wheat = new(1);
    private static readonly ResourceType Water = new(2);
    private static readonly EconomyRules Economy = EconomyRules.Create(
        enabled: true, foodResourceId: 1, waterResourceId: 2,
        capacityByResourceLocation: new Dictionary<(int, int), long>(),
        spoilagePerDayByResource: new Dictionary<int, double>(),
        wageByProfession: new Dictionary<int, long>(),
        priceFloor: new Dictionary<int, long>(), priceCeiling: new Dictionary<int, long>(),
        priceSensitivity: 0.1, demandBaselinePerNpc: new Dictionary<int, double>()).Value!;

    private static (WorldState World, Npc Npc, Household Household) Farm(long water = 4, bool withPlantRecipe = true)
    {
        IReadOnlyList<ProcessRecipe> recipes = withPlantRecipe
            ? [ProcessRecipe.Create(ProcessKind.Plant, new Dictionary<int, long>(), 1, 1, null, 1).Value!]
            : [];
        var world = new WorldState(
            Calendar, 17, ScenarioRunner.DefaultMap(17), ScenarioRunner.DefaultPopulationCatalog,
            ScenarioRunner.DefaultPopulationRules, ScenarioRunner.DefaultNeedsRules,
            ScenarioRunner.DefaultActionCatalog, ScenarioRunner.DefaultLifeStageRules,
            economyRules: Economy, processRecipes: recipes);
        var here = ScenarioRunner.DefaultVillageLocation;
        var npc = new Npc(
            world.NextNpcIdAndAdvance(), "farmer", Sex.Male, WorldDate.Epoch(Calendar).AddYears(-32), new CultureId(1),
            here, null, null, world.NextHouseholdIdAndAdvance(), 100, Neutral, new ProfessionType(1), here);
        var household = new Household(npc.Household!.Value, here, npc.Id, [npc.Id],
            new Dictionary<ResourceType, long> { [Water] = water });
        world.AddNpc(npc);
        world.AddHousehold(household);
        return (world, npc, household);
    }

    private static void Finish(WorldState world, ResourceProcess process)
    {
        world.CurrentDate = new WorldDate(Calendar, process.CompletesAtTick);
        new ResourceProcessSystem().Tick(world, new TickContext(world, world.Rng, world.Scheduler));
    }

    [Fact]
    public void Planting_creates_a_growing_batch_that_is_not_immediately_harvestable()
    {
        var (world, npc, household) = Farm();

        var planted = CropSystem.Plant(world, npc, now: 0, matureDelayTicks: 4, waterRequired: 1);
        Assert.True(planted.IsSuccess);
        Finish(world, planted.Value!);

        var crop = Assert.Single(world.CropBatches);
        Assert.Equal(CropStatus.Growing, crop.Status);
        Assert.False(crop.IsHarvestable(1));
        Assert.Equal(0, household.Stock.GetValueOrDefault(Wheat));
    }

    [Fact]
    public void Early_harvest_is_rejected_before_maturity()
    {
        var (world, npc, household) = Farm();
        var planted = CropSystem.Plant(world, npc, 0, 8, waterRequired: 0).Value!;
        Finish(world, planted);

        var harvested = CropSystem.Harvest(world, npc, now: 1, quantity: 10);

        Assert.Contains("maduro", harvested.Error);
        Assert.Equal(0, household.Stock.GetValueOrDefault(Wheat));
        Assert.NotEqual(CropStatus.Harvested, world.CropBatches[0].Status);
    }

    [Fact]
    public void Harvest_without_declared_water_is_rejected()
    {
        var (world, npc, household) = Farm();
        var planted = CropSystem.Plant(world, npc, 0, 1, waterRequired: 2).Value!;
        Finish(world, planted);
        world.CurrentDate = new WorldDate(Calendar, 4);

        var harvested = CropSystem.Harvest(world, npc, 4, 10);

        Assert.Contains("água", harvested.Error);
        Assert.Equal(0, household.Stock.GetValueOrDefault(Wheat));
    }

    [Fact]
    public void Watering_then_maturity_allows_a_conserved_harvest()
    {
        var (world, npc, household) = Farm();
        var planted = CropSystem.Plant(world, npc, 0, 2, waterRequired: 1).Value!;
        Finish(world, planted);
        var watered = CropSystem.Water(world, npc, world.CurrentDate.TotalHours, quantity: 1);
        Assert.True(watered.IsSuccess);
        Finish(world, watered.Value!);

        world.CurrentDate = new WorldDate(Calendar, 3);
        var harvested = CropSystem.Harvest(world, npc, 3, quantity: 10);
        Assert.True(harvested.IsSuccess);
        Finish(world, harvested.Value!);

        Assert.Equal(CropStatus.Harvested, world.CropBatches[0].Status);
        Assert.Equal(10, household.Stock[Wheat]);
        Assert.Equal(10, world.ResourceProduced[Wheat]);
        Assert.Equal(3, household.Stock[Water]);
    }

    [Fact]
    public void Instant_wheat_production_is_skipped_when_a_plant_recipe_exists()
    {
        var (world, npc, _) = Farm(withPlantRecipe: true);
        var workplace = new Workplace(
            world.NextWorkplaceIdAndAdvance(), new LocationType(1), npc.CurrentLocation, 1, [npc.Id],
            new Dictionary<ResourceType, long>(), new Money(0), new Dictionary<ResourceType, long>());
        world.AddWorkplace(workplace);
        npc.Hire(workplace.Id);
        var catalogued = new WorldState(
            Calendar, 17, world.Map, ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog, ScenarioRunner.DefaultLifeStageRules,
            economyRules: Economy, economyCatalog: ScenarioRunner.DefaultEconomyCatalog,
            processRecipes: world.ProcessRecipes);
        // Re-home the same actors into a world that also has the farm production recipe.
        var farmer = new Npc(
            catalogued.NextNpcIdAndAdvance(), "farmer", Sex.Male, WorldDate.Epoch(Calendar).AddYears(-32), new CultureId(1),
            npc.CurrentLocation, null, null, catalogued.NextHouseholdIdAndAdvance(), 100, Neutral, new ProfessionType(1),
            npc.CurrentLocation);
        var home = new Household(farmer.Household!.Value, farmer.CurrentLocation, farmer.Id, [farmer.Id]);
        var farm = new Workplace(
            catalogued.NextWorkplaceIdAndAdvance(), new LocationType(1), farmer.CurrentLocation, 1, [farmer.Id],
            new Dictionary<ResourceType, long>(), new Money(0), new Dictionary<ResourceType, long>());
        catalogued.AddNpc(farmer);
        catalogued.AddHousehold(home);
        catalogued.AddWorkplace(farm);
        farmer.Hire(farm.Id);

        new ProductionSystem().Tick(catalogued, new TickContext(catalogued, catalogued.Rng, catalogued.Scheduler));

        Assert.Equal(0, farm.Stock.GetValueOrDefault(Wheat));
        Assert.True(farm.Stock.GetValueOrDefault(Water) > 0);
    }

    [Fact]
    public void Crop_system_does_not_harvest_without_a_present_worker()
    {
        var world = new WorldState(
            Calendar, 17, ScenarioRunner.DefaultMap(17), ScenarioRunner.DefaultPopulationCatalog,
            ScenarioRunner.DefaultPopulationRules, ScenarioRunner.DefaultNeedsRules,
            ScenarioRunner.DefaultActionCatalog, ScenarioRunner.DefaultLifeStageRules, economyRules: Economy);
        var plot = ScenarioRunner.DefaultVillageLocation;
        world.AddWorkplace(new Workplace(
            world.NextWorkplaceIdAndAdvance(), new LocationType(1), plot, 1, [],
            new Dictionary<ResourceType, long>(), new Money(0), new Dictionary<ResourceType, long>()));
        world.AddCropBatch(new CropBatch(
            world.NextCropBatchIdAndAdvance(), 1, plot, 0, 0, waterRequired: 0, waterDelivered: 0, CropStatus.Mature));
        world.CurrentDate = new WorldDate(Calendar, 24);

        new CropSystem().Tick(world, new TickContext(world, world.Rng, world.Scheduler));

        Assert.Equal(CropStatus.Mature, world.CropBatches[0].Status);
        Assert.Equal(0, world.Workplaces[0].Stock.GetValueOrDefault(Wheat));
    }

    [Fact]
    public void Growing_crop_projects_a_friendly_cue()
    {
        var (world, npc, _) = Farm();
        var planted = CropSystem.Plant(world, npc, 0, 8, 1).Value!;
        Finish(world, planted);
        var city = new City(world.NextCityId(), npc.CurrentLocation, 0, null, AggregatePopulationPool.Empty);
        world.AddCity(city);
        npc.JoinCity(city.Id);

        var process = Assert.Single(
            LivingScopeProjector.Build(world, new VisualScope(VisualScopeKind.World, "")).Processes,
            item => item.Kind == "crop");

        Assert.Equal("water-crop", process.DescriptorKey);
        Assert.DoesNotContain("CropStatus", process.DescriptorKey);
        Assert.Equal(npc.CurrentLocation, process.Location);
    }

    [Fact]
    public void Crop_process_delta_replays_to_the_fresh_projection()
    {
        var (world, npc, _) = Farm();
        var planted = CropSystem.Plant(world, npc, 0, 8, 0).Value!;
        Finish(world, planted);
        var city = new City(world.NextCityId(), npc.CurrentLocation, 0, null, AggregatePopulationPool.Empty);
        world.AddCity(city);
        npc.JoinCity(city.Id);
        var scope = new VisualScope(VisualScopeKind.World, "");

        var before = LivingScopeProjector.Build(world, scope);
        world.CurrentDate = new WorldDate(Calendar, 3);
        var after = LivingScopeProjector.Build(world, scope);
        var replayed = LivingDeltaReducer.Apply(before, ScopeDeltaBuilder.Diff(3, before, after));

        Assert.Equal(after, replayed);
    }
}
