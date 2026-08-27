using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Ecology;

/// <summary>REALISM-07/08/11 — flora avança por temperatura/estação; poder multiplica a taxa base.</summary>
public sealed class FloraLifecycleAdvanceTests
{
    private sealed class RecordingSink : IWorldEventSink
    {
        public List<WorldEvent> Events { get; } = [];
        public void Record(WorldEvent evt) => Events.Add(evt);
    }

    [Fact]
    public void Plant_advances_stage_without_power_when_temperature_in_tolerance()
    {
        var rules = WheatRules(min: 0, max: 40, maturity: 5);
        var (world, plantId) = WorldWithPlant("wheat", stage: 0, rules, tempDelta: 0);
        Assert.False(world.Extraordinary.Enabled);

        new WorldClock([new FloraLifecycleSystem()]).Tick(world);

        Assert.True(world.FindPlant(plantId)!.GrowthStage > 0);
    }

    [Fact]
    public void Outside_tolerance_never_advances_normally_and_may_revert()
    {
        var rules = WheatRules(min: 20, max: 30, maturity: 5);
        // Base ~12; force far below tolerance via seasonal overlay.
        var (world, plantId) = WorldWithPlant("wheat", stage: 2, rules, tempDelta: -40);
        float temp = EnvironmentTemperatureMechanic.EffectiveTemperature(
            world, world.FindPlant(plantId)!.Position, world.CurrentDate.TotalHours);
        Assert.True(temp < rules.MinToleratedTemp);

        new WorldClock([new FloraLifecycleSystem()]).Tick(world);

        Assert.True(world.FindPlant(plantId)!.GrowthStage < 2);
    }

    [Fact]
    public void Plant_never_in_tolerance_dies_without_reaching_maturity()
    {
        var rules = WheatRules(min: 50, max: 60, maturity: 3);
        var sink = new RecordingSink();
        var (world, plantId) = WorldWithPlant("wheat", stage: 0, rules, tempDelta: 0);
        var clock = new WorldClock([new FloraLifecycleSystem()], sink: sink);

        clock.Tick(world);

        Assert.Null(world.FindPlant(plantId));
        var death = Assert.Single(sink.Events, e => e.Kind == WorldEventKind.Death);
        Assert.Equal(plantId.Value.ToString(), death.Payload);
        Assert.Equal(FloraLifecycleSystem.SystemName, death.SourceSystem);
    }

    [Fact]
    public void Flora_growth_rate_power_multiplies_base_rate_does_not_replace_it()
    {
        var rules = WheatRules(min: 0, max: 40, maturity: 20);
        var baseline = WorldWithPlant("wheat", stage: 0, rules, tempDelta: 0, enabledExtraordinary: false);
        var powered = WorldWithGrowthPower("wheat", stage: 0, rules, multiplier: 5, radius: 10);
        var plant = baseline.World.FindPlant(baseline.PlantId)!;
        double baseRate = FloraLifecycleSystem.BaseGrowthRate(
            baseline.World, plant, rules, baseline.World.CurrentDate.TotalHours);

        new WorldClock([new FloraLifecycleSystem()]).Tick(baseline.World);
        new WorldClock([new FloraLifecycleSystem()]).Tick(powered.World);

        int baseAdvance = baseline.World.FindPlant(baseline.PlantId)!.GrowthStage;
        int poweredAdvance = powered.World.FindPlant(powered.PlantId)!.GrowthStage;

        Assert.Equal((int)Math.Floor(baseRate), baseAdvance);
        Assert.Equal((int)Math.Floor(baseRate * 5), poweredAdvance);
        Assert.True(poweredAdvance > baseAdvance);
    }

    [Fact]
    public void Advance_runs_when_extraordinary_is_disabled()
    {
        var rules = WheatRules(min: 0, max: 40, maturity: 5);
        var (world, plantId) = WorldWithPlant("wheat", stage: 0, rules, tempDelta: 0, enabledExtraordinary: false);
        Assert.False(world.Extraordinary.Enabled);

        new FloraLifecycleSystem().Tick(world, new TickContext(world, world.Rng, world.Scheduler));

        Assert.True(world.FindPlant(plantId)!.GrowthStage >= 1);
    }

    [Fact]
    public void Reaching_maturity_logs_event_with_flora_source()
    {
        var rules = WheatRules(min: 0, max: 40, maturity: 1);
        var sink = new RecordingSink();
        var (world, plantId) = WorldWithPlant("wheat", stage: 0, rules, tempDelta: 0);
        var clock = new WorldClock([new FloraLifecycleSystem()], sink: sink);

        clock.Tick(world);

        Assert.Equal(1, world.FindPlant(plantId)!.GrowthStage);
        var matured = Assert.Single(sink.Events, e => e.Kind == WorldEventKind.PlantMatured);
        Assert.Equal(plantId.Value.ToString(), matured.Payload);
        Assert.Equal(FloraLifecycleSystem.SystemName, matured.SourceSystem);
    }

    private static PlantSpeciesRules WheatRules(float min, float max, int maturity) =>
        new("wheat", min, max, maturity, CropResourceId: 1, YieldPerMaturePlant: 10,
            ReproduceRadius: 2, ReproduceProbability: 0);

    private static (WorldState World, PlantId PlantId) WorldWithPlant(
        string species, int stage, PlantSpeciesRules rules, float tempDelta,
        bool enabledExtraordinary = false)
    {
        var plant = new Plant(new PlantId(1), species, new CellCoord(0, 0), stage);
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, 42, ScenarioRunner.DefaultMap(42),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules,
            extraordinary: new ExtraordinaryScenarioData(enabledExtraordinary, []),
            flora: [plant],
            plantSpeciesRules: [rules],
            environmentTemperatureAdjustments: tempDelta == 0
                ? []
                : [new EnvironmentTemperatureAdjustment(new RegionId(0), tempDelta, long.MaxValue)]);
        return (world, plant.Id);
    }

    private static (WorldState World, PlantId PlantId) WorldWithGrowthPower(
        string species, int stage, PlantSpeciesRules rules, int multiplier, int radius)
    {
        var plant = new Plant(new PlantId(1), species, new CellCoord(0, 0), stage);
        var descriptor = new PowerDescriptor(
            "flora-power", "test-source",
            [$"area:radius:{radius}", $"flora.growth-rate:{multiplier}"],
            "Active", [], "Guaranteed", [], [], [], []);
        var state = new ExtraordinaryCarrierState(
            new NpcId(1), [descriptor.Id], true, "manifested",
            new ExtraordinaryAppearanceState(1, "", ""), null, 1);
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, 42, ScenarioRunner.DefaultMap(42),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules,
            extraordinary: new ExtraordinaryScenarioData(true, [descriptor]),
            extraordinaryCarriers: [state],
            flora: [plant],
            plantSpeciesRules: [rules]);
        var carrier = new Npc(
            new NpcId(1), "carrier", Sex.Male, WorldDate.Epoch(ScenarioRunner.DefaultCalendar),
            ScenarioRunner.DefaultCulture, new CellCoord(0, 0), motherId: null, fatherId: null,
            household: null, health: 100,
            personality: Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!,
            profession: ProfessionType.None, currentLocation: new CellCoord(0, 0));
        world.AddNpc(carrier);
        return (world, plant.Id);
    }
}
