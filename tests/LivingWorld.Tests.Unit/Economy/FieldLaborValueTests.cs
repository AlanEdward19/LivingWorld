using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Labor;
using LivingWorld.Simulation.Economy.Production;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Economy;

/// <summary>AD-191: a lavoura vale, na escolha de emprego, o grão que a mão acrescenta. Antes a comida da lavoura não contava e a
/// fazenda valia só a água do poço: o lavrador saía para qualquer oficina que valesse 1,25 vez isso, e na metrópole as fazendas
/// chegavam à ceifa com um quarto das mãos.</summary>
public sealed class FieldLaborValueTests
{
    private static (WorldState World, Workplace Farm) Village(bool fieldValue)
    {
        var (world, _) = ScenarioRunner.Create(seed: 7, agricultureRules: AgricultureRules.Default with { FieldLaborValue = fieldValue });
        return (world, world.Workplaces.First(w => w.LocationType.Id == CropSystem.DefaultFarmLocationTypeId));
    }

    private static double FoodPrice(WorldState world, Workplace farm) =>
        farm.Prices.GetValueOrDefault(new ResourceType(world.EconomyRules.FoodResourceId), 1);

    [Fact]
    public void The_growing_field_adds_the_grain_one_more_hand_brings_in_over_the_year()
    {
        var (on, farm) = Village(fieldValue: true);
        var (off, offFarm) = Village(fieldValue: false);
        var ag = on.AgricultureRules;
        var field = on.FindCropAt(farm.Location)!;
        int hands = ToolMechanic.WorkingHands(on, farm);
        double marginal = (ag.ExpectedHarvest(field.ArableSeed, hands + 1) - ag.ExpectedHarvest(field.ArableSeed, hands)) / 360.0;

        double gain = LaborValueIndex.BuildForTick(on).ValueOf(farm) - LaborValueIndex.BuildForTick(off).ValueOf(offFarm);

        Assert.Equal(marginal * FoodPrice(on, farm), gain, 6);
        Assert.True(gain > 10, $"{gain}");
    }

    [Fact]
    public void Whoever_already_works_the_land_values_his_own_share_at_least_as_much_as_a_newcomer()
    {
        var (world, clock) = ScenarioRunner.Create(seed: 7);
        clock.Run(world, 3 * 24);
        var farm = world.Workplaces.First(w => w.LocationType.Id == CropSystem.DefaultFarmLocationTypeId);
        Assert.NotEmpty(farm.Employees);
        var value = LaborValueIndex.FieldLaborValue(world, farm, FoodPrice(world, farm))!.Value;
        Assert.True(value.Stay >= value.Join && value.Join > 0, $"{value}");
    }

    [Fact]
    public void At_harvest_a_day_of_reaping_outweighs_a_full_forge_order_so_no_one_leaves_the_field()
    {
        var (on, farm) = Village(fieldValue: true);
        var (off, offFarm) = Village(fieldValue: false);
        on.FindCropAt(farm.Location)!.Ripen(on.CurrentDate.TotalHours, 40_000);
        off.FindCropAt(offFarm.Location)!.Ripen(off.CurrentDate.TotalHours, 40_000);

        double reaping = LaborValueIndex.BuildForTick(on).ValueOf(farm) - LaborValueIndex.BuildForTick(off).ValueOf(offFarm);

        Assert.Equal(on.AgricultureRules.ReapPerWorkerDay * FoodPrice(on, farm), reaping, 6);
        // a forja com toda a encomenda descoberta: 5 ferramentas por dia ao preço da forja; o empregado troca acima de 1,25 vez
        Assert.True(reaping > 1.25 * 5.0 * ScenarioRunner.ForgeToolPrice, $"ceifa {reaping}");
    }

    [Fact]
    public void Without_the_rule_the_field_adds_nothing()
    {
        var (world, farm) = Village(fieldValue: false);
        Assert.Null(LaborValueIndex.FieldLaborValue(world, farm, FoodPrice(world, farm)));
    }
}
