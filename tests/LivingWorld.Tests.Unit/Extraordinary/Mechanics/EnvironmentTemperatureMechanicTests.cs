using LivingWorld.Domain.Extraordinary;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Geography.Map;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Production;
using LivingWorld.Simulation.Extraordinary.Engine;
using LivingWorld.Simulation.Extraordinary.Mechanics;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Extraordinary.Mechanics;

public sealed class EnvironmentTemperatureMechanicTests
{
    [Fact]
    public void Cell_under_environment_temperature_reports_adjusted_value_while_active_and_base_after_duration()
    {
        var setup = WorldWithClimate("environment.temperature:0:-8:4");
        var cell = setup.World.Map.CellAt(new CellCoord(0, 0));
        float baseline = cell.Temperature;
        var ctx = new TickContext(setup.World, setup.World.Rng, setup.World.Scheduler);

        var invoked = ExtraordinaryInvocationEngine.Invoke(
            setup.World, ctx,
            new ExtraordinaryInvocation(301, setup.Carrier.Id, "climate-power", setup.Carrier.Id));

        Assert.True(invoked.IsSuccess, invoked.Error);
        Assert.Equal(baseline - 8, EnvironmentTemperatureMechanic.EffectiveTemperature(
            setup.World, cell.Coord, ctx.CurrentTick));

        setup.World.CurrentDate = setup.World.CurrentDate.AddHours(4);
        Assert.Equal(baseline, EnvironmentTemperatureMechanic.EffectiveTemperature(
            setup.World, cell.Coord, setup.World.CurrentDate.TotalHours));
    }

    [Fact]
    public void Same_seed_reproduces_the_same_generated_base_temperature()
    {
        var first = MapGenerator.Generate(42, 10, 10, 5, Catalog, Cost, []).Value!;
        var second = MapGenerator.Generate(42, 10, 10, 5, Catalog, Cost, []).Value!;

        Assert.Equal(
            first.Cells.Select(cell => (cell.Coord, cell.Temperature)),
            second.Cells.Select(cell => (cell.Coord, cell.Temperature)));
    }

    [Fact]
    public void Without_a_climate_power_temperature_stays_at_generated_base()
    {
        var setup = WorldWithClimate(effect: null);
        var cell = setup.World.Map.CellAt(new CellCoord(0, 0));

        Assert.Equal(
            cell.Temperature,
            EnvironmentTemperatureMechanic.EffectiveTemperature(
                setup.World, cell.Coord, setup.World.CurrentDate.TotalHours));
    }

    [Fact]
    public void Crop_system_reads_the_effective_cell_temperature_including_active_climate_power()
    {
        var setup = WorldWithClimate("environment.temperature:0:3:10");
        var cell = setup.World.Map.CellAt(new CellCoord(0, 0));
        var ctx = new TickContext(setup.World, setup.World.Rng, setup.World.Scheduler);
        ExtraordinaryInvocationEngine.Invoke(
            setup.World, ctx,
            new ExtraordinaryInvocation(302, setup.Carrier.Id, "climate-power", setup.Carrier.Id));

        Assert.Equal(
            EnvironmentTemperatureMechanic.EffectiveTemperature(setup.World, cell.Coord, ctx.CurrentTick),
            CropSystem.ReadCellTemperature(setup.World, cell.Coord, ctx.CurrentTick));
    }

    [Fact]
    public void Cells_outside_the_declared_region_keep_generated_base_while_the_region_is_adjusted()
    {
        var setup = WorldWithClimate("environment.temperature:0:12:20");
        var inside = new CellCoord(0, 0);
        var outside = new CellCoord(9, 9);
        var ctx = new TickContext(setup.World, setup.World.Rng, setup.World.Scheduler);
        ExtraordinaryInvocationEngine.Invoke(
            setup.World, ctx,
            new ExtraordinaryInvocation(303, setup.Carrier.Id, "climate-power", setup.Carrier.Id));

        Assert.Equal(
            setup.World.Map.CellAt(inside).Temperature + 12,
            EnvironmentTemperatureMechanic.EffectiveTemperature(setup.World, inside, ctx.CurrentTick));
        Assert.Equal(
            setup.World.Map.CellAt(outside).Temperature,
            EnvironmentTemperatureMechanic.EffectiveTemperature(setup.World, outside, ctx.CurrentTick));
    }

    private static readonly GeographyCatalog Catalog = new(
        TerrainIds: [1, 2, 3], BiomeIds: [1, 2], ResourceIds: [1]);

    private static readonly CostWeights Cost = new(
        Base: 1.0, AltitudeWeight: 0.5, TerrainWeight: new Dictionary<int, double> { [1] = 1.0, [2] = 1.5, [3] = 3.0 });

    private static (WorldState World, Npc Carrier) WorldWithClimate(string? effect)
    {
        PowerDescriptor[] descriptors = effect is null
            ? []
            : [new PowerDescriptor(
                "climate-power", "test-source", [effect], "Active", [], "Guaranteed", [], [], [], [])];
        ExtraordinaryCarrierState[] carriers = effect is null
            ? []
            : [new ExtraordinaryCarrierState(
                new NpcId(1), ["climate-power"], true, "active",
                new ExtraordinaryAppearanceState(1, "", ""), null, 1)];
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, 42, ScenarioRunner.DefaultMap(42),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules,
            extraordinary: new ExtraordinaryScenarioData(true, descriptors),
            extraordinaryCarriers: carriers);
        var carrier = new Npc(
            new NpcId(1), "carrier", Sex.Male, WorldDate.Epoch(ScenarioRunner.DefaultCalendar),
            ScenarioRunner.DefaultCulture, new CellCoord(0, 0), motherId: null, fatherId: null,
            household: new HouseholdId(1), health: 100,
            personality: Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!,
            profession: ProfessionType.None, currentLocation: new CellCoord(0, 0));
        world.AddNpc(carrier);
        world.AddHousehold(new Household(
            new HouseholdId(1), new CellCoord(0, 0), carrier.Id, [carrier.Id],
            new Dictionary<ResourceType, long>()));
        return (world, carrier);
    }
}
