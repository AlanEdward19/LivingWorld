using LivingWorld.Domain;
using LivingWorld.Simulation;
using LivingWorld.Simulation.Economy;
using LivingWorld.Simulation.Geography;

namespace LivingWorld.Tests.Ecology;

public sealed class TemperatureSeasonSystemTests
{
    [Fact]
    public void Cell_under_opposite_seasons_reads_different_temperature_without_power()
    {
        var (world, clock) = ScenarioRunner.Create(seed: 7, initialPopulation: 0);
        var cell = new CellCoord(0, 0);
        float baseline = world.Map.CellAt(cell).Temperature;

        clock.Run(world, 24);
        float winter = ReadTemp(world, cell);
        Assert.Equal(baseline - 6, winter, precision: 2);

        AdvanceToMonthStart(world, clock, targetMonth: 6);
        float summer = ReadTemp(world, cell);
        Assert.Equal(baseline + 10, summer, precision: 2);
        Assert.NotEqual(winter, summer);
    }

    [Fact]
    public void Without_climate_power_temperature_follows_seasonal_curve_not_a_single_value()
    {
        var (world, clock) = ScenarioRunner.Create(seed: 11, initialPopulation: 0);
        var cell = new CellCoord(0, 0);
        var readings = new List<float>();

        foreach (int month in new[] { 0, 3, 6, 9 })
        {
            AdvanceToMonthStart(world, clock, targetMonth: month);
            clock.Run(world, 24);
            readings.Add(ReadTemp(world, cell));
        }

        Assert.True(readings.Distinct().Count() >= 3, "temperatura efetiva deve variar entre estações");
    }

    [Fact]
    public void Active_climate_power_delta_sums_on_seasonal_value_not_fixed_base()
    {
        var setup = WorldWithClimate("environment.temperature:0:5:100");
        var cell = new CellCoord(0, 0);
        float baseline = setup.World.Map.CellAt(cell).Temperature;
        var ctx = new TickContext(setup.World, setup.World.Rng, setup.World.Scheduler);

        TemperatureSeasonSystem.ApplySeason(setup.World, seasonIndex: 2);
        float seasonalOnly = ReadTemp(setup.World, cell);
        Assert.Equal(baseline + 10, seasonalOnly, precision: 2);

        ExtraordinaryInvocationEngine.Invoke(
            setup.World, ctx,
            new ExtraordinaryInvocation(401, setup.Carrier.Id, "climate-power", setup.Carrier.Id));

        Assert.Equal(seasonalOnly + 5, ReadTemp(setup.World, cell), precision: 2);
        Assert.NotEqual(baseline + 5, ReadTemp(setup.World, cell), precision: 2);
    }

    [Fact]
    public void Crop_system_reads_combined_seasonal_and_power_temperature()
    {
        var setup = WorldWithClimate("environment.temperature:0:-4:50");
        var cell = new CellCoord(0, 0);
        var ctx = new TickContext(setup.World, setup.World.Rng, setup.World.Scheduler);

        TemperatureSeasonSystem.ApplySeason(setup.World, seasonIndex: 0);
        ExtraordinaryInvocationEngine.Invoke(
            setup.World, ctx,
            new ExtraordinaryInvocation(402, setup.Carrier.Id, "climate-power", setup.Carrier.Id));

        long tick = setup.World.CurrentDate.TotalHours;
        Assert.Equal(
            EnvironmentTemperatureMechanic.EffectiveTemperature(setup.World, cell, tick),
            CropSystem.ReadCellTemperature(setup.World, cell, tick));
    }

    private static float ReadTemp(WorldState world, CellCoord cell) =>
        EnvironmentTemperatureMechanic.EffectiveTemperature(world, cell, world.CurrentDate.TotalHours);

    private static void AdvanceToMonthStart(WorldState world, WorldClock clock, int targetMonth)
    {
        long targetHours = targetMonth * world.Calendar.HoursPerMonth;
        long delta = targetHours - world.CurrentDate.TotalHours;
        if (delta > 0)
            clock.Run(world, delta);
    }

    private static (WorldState World, Npc Carrier) WorldWithClimate(string effect)
    {
        PowerDescriptor[] descriptors =
        [
            new PowerDescriptor(
                "climate-power", "test-source", [effect], "Active", [], "Guaranteed", [], [], [], []),
        ];
        ExtraordinaryCarrierState[] carriers =
        [
            new ExtraordinaryCarrierState(
                new NpcId(1), ["climate-power"], true, "active",
                new ExtraordinaryAppearanceState(1, "", ""), null, 1),
        ];
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, 42, ScenarioRunner.DefaultMap(42),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules,
            extraordinary: new ExtraordinaryScenarioData(true, descriptors),
            extraordinaryCarriers: carriers,
            biomeSeasonTemperatureRules: ScenarioRunner.DefaultBiomeSeasonTemperatureRules);
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
