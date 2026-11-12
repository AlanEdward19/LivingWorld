using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Supply;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Economy;

/// <summary>AD-135: quem mora perto de rio, lago ou fonte enche a despensa de água dali, de graça; quem mora longe continua
/// dependendo do mercado e do poço da fazenda.</summary>
public sealed class NaturalWaterTests
{
    private static (WorldState World, ResourceType Water) Dry(NaturalWaterRules? rules = null)
    {
        var (world, _) = ScenarioRunner.Create(17, naturalWaterRules: rules);
        var water = new ResourceType(world.EconomyRules.WaterResourceId);
        foreach (var household in world.Households)
            household.Withdraw(water, household.Stock.GetValueOrDefault(water));
        return (world, water);
    }

    private static void Tick(WorldState world) =>
        new NaturalWaterSystem().Tick(world, new TickContext(world, world.Rng, world.Scheduler));

    [Fact]
    public void A_household_next_to_water_fills_its_pantry_for_free()
    {
        var (world, water) = Dry();
        var rules = world.NaturalWaterRules;
        var near = world.Households.Where(h => NaturalWaterSystem.WalkToWater(world.Map, h.Location, rules.SearchRadiusCells) <= rules.MaxWalkTicks).ToList();
        Assert.NotEmpty(near);

        Tick(world);

        Assert.All(near, h => Assert.Equal(h.Members.Count * (long)rules.DaysOfSupply, h.Stock.GetValueOrDefault(water)));
    }

    [Fact]
    public void A_household_far_from_water_gets_nothing()
    {
        var (world, water) = Dry(new NaturalWaterRules(true, MaxWalkTicks: 0));
        var rules = world.NaturalWaterRules;
        var far = world.Households.Where(h => NaturalWaterSystem.WalkToWater(world.Map, h.Location, rules.SearchRadiusCells) > rules.MaxWalkTicks).ToList();
        Assert.NotEmpty(far);

        Tick(world);

        Assert.All(far, h => Assert.Equal(0, h.Stock.GetValueOrDefault(water)));
    }

    [Fact]
    public void Without_natural_water_no_household_is_filled()
    {
        var (world, water) = Dry(NaturalWaterRules.Disabled);

        Tick(world);

        Assert.All(world.Households, h => Assert.Equal(0, h.Stock.GetValueOrDefault(water)));
    }

    [Fact]
    public void Water_in_the_home_cell_is_zero_hours_away()
    {
        var (world, _) = Dry();
        var wet = world.Map.Cells.First(c => c.HasWater).Coord;

        Assert.Equal(0, NaturalWaterSystem.WalkToWater(world.Map, wet, 0));
        Assert.Equal(long.MaxValue, NaturalWaterSystem.WalkToWater(world.Map, world.Map.Cells.First(c => !c.HasWater && NoWaterAround(world, c.Coord)).Coord, 1));
    }

    private static bool NoWaterAround(WorldState world, CellCoord at)
    {
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
                if (world.Map.TryGetCell(new CellCoord(at.X + dx, at.Y + dy), out var cell) && cell.HasWater) return false;
        return true;
    }
}
