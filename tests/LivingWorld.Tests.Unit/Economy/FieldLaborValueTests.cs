using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Labor;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Economy;

/// <summary>AD-191: a lavoura vale, na escolha de emprego, o grão que a mão acrescenta. Antes a fazenda valia o mínimo (1) e o
/// lavrador saía para qualquer oficina que valesse 1,25 — na metrópole as fazendas chegavam à ceifa com um quarto das mãos.</summary>
public sealed class FieldLaborValueTests
{
    private static (WorldState World, Workplace Farm) Village(bool fieldValue)
    {
        var (world, _) = ScenarioRunner.Create(seed: 7, agricultureRules: AgricultureRules.Default with { FieldLaborValue = fieldValue });
        return (world, world.Workplaces.First(w => w.LocationType.Id == 1));
    }

    private static double FoodPrice(WorldState world, Workplace farm) =>
        farm.Prices.GetValueOrDefault(new ResourceType(world.EconomyRules.FoodResourceId), 1);

    [Fact]
    public void Without_the_rule_the_farm_is_worth_the_minimum()
    {
        var (world, farm) = Village(fieldValue: false);
        Assert.Equal(LaborValueIndex.MinimumValue, LaborValueIndex.BuildForTick(world).ValueOf(farm), 9);
    }

    [Fact]
    public void The_growing_field_is_worth_the_grain_one_more_hand_adds_over_the_year()
    {
        var (world, farm) = Village(fieldValue: true);
        var ag = world.AgricultureRules;
        var field = world.FindCropAt(farm.Location)!;
        int hands = LivingWorld.Simulation.Economy.Production.ToolMechanic.WorkingHands(world, farm);
        double marginal = (ag.ExpectedHarvest(field.ArableSeed, hands + 1) - ag.ExpectedHarvest(field.ArableSeed, hands)) / 360.0;
        double value = LaborValueIndex.BuildForTick(world).ValueOf(farm);
        Assert.Equal(LaborValueIndex.MinimumValue + marginal * FoodPrice(world, farm), value, 6);
        Assert.True(value > 5 * LaborValueIndex.MinimumValue, $"{value}");
    }

    [Fact]
    public void Whoever_already_works_the_land_values_his_own_share_at_least_as_much_as_a_newcomer()
    {
        var (world, farm) = Village(fieldValue: true);
        var index = LaborValueIndex.BuildForTick(world);
        var worker = world.FindNpc(farm.Employees[0])!;
        Assert.True(index.ValueOf(farm, worker) >= index.ValueOf(farm), $"{index.ValueOf(farm, worker)} vs {index.ValueOf(farm)}");
    }

    [Fact]
    public void At_harvest_a_day_of_reaping_outweighs_a_full_forge_order_so_no_one_leaves_the_field()
    {
        var (world, farm) = Village(fieldValue: true);
        var field = world.FindCropAt(farm.Location)!;
        field.Ripen(world.CurrentDate.TotalHours, 40_000);
        var worker = world.FindNpc(farm.Employees[0])!;
        double reaping = LaborValueIndex.BuildForTick(world).ValueOf(farm, worker);
        // a forja com toda a encomenda descoberta: 5 ferramentas por dia ao preço da forja
        double forge = 5.0 * ScenarioRunner.ForgeToolPrice;
        Assert.True(reaping > 1.25 * forge, $"ceifa {reaping} vs forja {forge}");
        Assert.Equal(LaborValueIndex.MinimumValue + world.AgricultureRules.ReapPerWorkerDay * FoodPrice(world, farm), reaping, 6);
    }
}
