using LivingWorld.Domain;
using LivingWorld.Simulation;
using LivingWorld.Simulation.Geography;

namespace LivingWorld.Tests.Ecology;

/// <summary>REALISM-09/10 — flora madura alimenta estoque de cultivo e se reproduz.</summary>
public sealed class FloraLifecycleProduceReproduceTests
{
    private sealed class RecordingSink : IWorldEventSink
    {
        public List<WorldEvent> Events { get; } = [];
        public void Record(WorldEvent evt) => Events.Add(evt);
    }

    [Fact]
    public void Mature_plant_deposits_into_workplace_stock_not_a_plant_keyed_inventory()
    {
        var rules = WheatRules(maturity: 1, reproduceProbability: 0);
        var (world, plantId, workplace) = WorldWithFarmPlant("wheat", stage: 0, rules);
        var food = new ResourceType(rules.CropResourceId);

        new WorldClock([new FloraLifecycleSystem()]).Tick(world);

        Assert.True(world.FindPlant(plantId)!.GrowthStage >= rules.MaturityStage);
        Assert.Equal((long)rules.YieldPerMaturePlant, workplace.Stock.GetValueOrDefault(food));
        Assert.Equal((long)rules.YieldPerMaturePlant, world.ResourceProduced.GetValueOrDefault(food));
        // Nenhum segundo estoque keyed por Plant — só workplace + ResourceProduced canônico/volátil.
        Assert.Single(world.Flora);
    }

    [Fact]
    public void Mature_plant_with_free_compatible_space_sprouts_new_plant_deterministically()
    {
        var rules = WheatRules(maturity: 0, reproduceProbability: 1.0, reproduceRadius: 2);
        var sink = new RecordingSink();
        var (world, _, _) = WorldWithFarmPlant("wheat", stage: 0, rules);
        Assert.Single(world.Flora);

        new WorldClock([new FloraLifecycleSystem()], sink: sink).Tick(world);

        Assert.Equal(2, world.Flora.Count);
        Assert.Contains(world.Flora, p => p.GrowthStage == 0 && p.Id.Value != 1);
        var birth = Assert.Single(sink.Events, e => e.Kind == WorldEventKind.Birth);
        Assert.Equal(FloraLifecycleSystem.SystemName, birth.SourceSystem);

        var again = WorldWithFarmPlant("wheat", stage: 0, rules).World;
        new WorldClock([new FloraLifecycleSystem()]).Tick(again);
        Assert.Equal(
            world.Flora.Select(p => (p.Id.Value, p.Position, p.GrowthStage)).OrderBy(x => x.Value),
            again.Flora.Select(p => (p.Id.Value, p.Position, p.GrowthStage)).OrderBy(x => x.Value));
    }

    [Fact]
    public void Independent_two_seasons_yield_different_stage_advance_rates()
    {
        var rules = WheatRules(maturity: 100, reproduceProbability: 0);
        var winter = WorldWithPlantOnly("wheat", 0, rules);
        var summer = WorldWithPlantOnly("wheat", 0, rules);
        var plant = winter.FindPlant(new PlantId(1))!;

        TemperatureSeasonSystem.ApplySeason(winter, seasonIndex: 0); // delta -6
        TemperatureSeasonSystem.ApplySeason(summer, seasonIndex: 2); // delta +10

        double winterRate = FloraLifecycleSystem.BaseGrowthRate(
            winter, plant, rules, winter.CurrentDate.TotalHours);
        double summerRate = FloraLifecycleSystem.BaseGrowthRate(
            summer, plant, rules, summer.CurrentDate.TotalHours);

        Assert.NotEqual(winterRate, summerRate);

        new WorldClock([new FloraLifecycleSystem()]).Run(winter, ticks: 3);
        new WorldClock([new FloraLifecycleSystem()]).Run(summer, ticks: 3);

        Assert.NotEqual(
            winter.FindPlant(new PlantId(1))!.GrowthStage,
            summer.FindPlant(new PlantId(1))!.GrowthStage);
    }

    private static PlantSpeciesRules WheatRules(
        int maturity, double reproduceProbability, double reproduceRadius = 2) =>
        new("wheat", MinToleratedTemp: 0, MaxToleratedTemp: 40, MaturityStage: maturity,
            CropResourceId: 1, YieldPerMaturePlant: 10, ReproduceRadius: reproduceRadius,
            ReproduceProbability: reproduceProbability);

    private static (WorldState World, PlantId PlantId, Workplace Workplace) WorldWithFarmPlant(
        string species, int stage, PlantSpeciesRules rules)
    {
        var plant = new Plant(new PlantId(1), species, new CellCoord(0, 0), stage);
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, 42, ScenarioRunner.DefaultMap(42),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules,
            economyRules: ScenarioRunner.DefaultEconomyRules,
            extraordinary: new ExtraordinaryScenarioData(false, []),
            flora: [plant],
            plantSpeciesRules: [rules],
            biomeSeasonTemperatureRules: ScenarioRunner.DefaultBiomeSeasonTemperatureRules);
        var workplace = new Workplace(
            world.NextWorkplaceIdAndAdvance(), new LocationType(1), plant.Position, 1, [],
            new Dictionary<ResourceType, long>(), Money.Zero, new Dictionary<ResourceType, long>());
        world.AddWorkplace(workplace);
        return (world, plant.Id, workplace);
    }

    private static WorldState WorldWithPlantOnly(string species, int stage, PlantSpeciesRules rules)
    {
        var plant = new Plant(new PlantId(1), species, new CellCoord(0, 0), stage);
        return new WorldState(
            ScenarioRunner.DefaultCalendar, 42, ScenarioRunner.DefaultMap(42),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules,
            economyRules: ScenarioRunner.DefaultEconomyRules,
            extraordinary: new ExtraordinaryScenarioData(false, []),
            flora: [plant],
            plantSpeciesRules: [rules],
            biomeSeasonTemperatureRules: ScenarioRunner.DefaultBiomeSeasonTemperatureRules);
    }
}
