using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Economy.Production;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Economy;

/// <summary>AD-190 (testes da AD-191): o grão tirado do celeiro de outra fazenda é pago a ela, e a cidade com a reserva curta (mas
/// acima do que chega à colheita) semeia mais.</summary>
public sealed class CityGrainTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Grain_taken_from_another_farm_is_paid_at_todays_price(bool pay)
    {
        var (world, _) = ScenarioRunner.Create(7, agricultureRules: AgricultureRules.Default with { PayForCityGrain = pay });
        var food = new ResourceType(world.EconomyRules.FoodResourceId);
        var buyer = world.Workplaces.First(w => w.LocationType.Id == CropSystem.DefaultFarmLocationTypeId);
        var seller = world.Workplaces.First(w => w.Id != buyer.Id);
        long price = buyer.Prices.GetValueOrDefault(food, 1);
        long buyerBefore = buyer.Treasury.Amount, sellerBefore = seller.Treasury.Amount;

        CropSystem.PayForGrain(world, buyer, seller, 10);

        long paid = pay ? Math.Min(buyerBefore, 10 * Math.Max(1, price)) : 0;
        Assert.Equal(sellerBefore + paid, seller.Treasury.Amount);
        Assert.Equal(buyerBefore - paid, buyer.Treasury.Amount); // a moeda só muda de caixa
    }

    [Fact]
    public void A_short_reserve_above_what_reaches_the_harvest_sows_more_and_below_it_does_not()
    {
        var (world, _) = ScenarioRunner.Create(7);
        var ag = AgricultureRules.Default;
        var farm = world.Workplaces.First(w => w.LocationType.Id == CropSystem.DefaultFarmLocationTypeId);
        var field = world.FindCropAt(farm.Location)!;
        var residents = new Dictionary<CityId, long> { [farm.City] = 100 };
        long provision = 100 * ag.InitialProvisionPerPerson;
        field.TakeBarn(field.Barn);
        field.TakeSeed(field.SeedReserve);

        field.StoreBarn(provision * 3 / 2);
        Assert.Equal(1.25, CropSystem.DearthSowBoost(ag, [(farm, field)], residents)[farm.City], 9);
        Assert.Empty(CropSystem.DearthSowBoost(ag with { DearthSowBoost = 0 }, [(farm, field)], residents));

        field.TakeBarn(field.Barn);
        field.StoreBarn(provision / 2); // abaixo do que chega à colheita: a semente a mais seria o pão de agora
        Assert.False(CropSystem.DearthSowBoost(ag, [(farm, field)], residents).ContainsKey(farm.City));

        field.StoreBarn(provision * 2); // reserva cheia: semeia as bocas
        Assert.Equal(1.0, CropSystem.DearthSowBoost(ag, [(farm, field)], residents)[farm.City], 9);
    }
}

/// <summary>AD-191: o preço do grão da fazenda olha o celeiro da cidade (lei de King), não só a banca reposta todo dia.</summary>
public sealed class BarnPriceTests
{
    private static (LivingWorld.Simulation.Core.WorldState World, Workplace Farm, CropBatch Field, double Need) Village(AgricultureRules? ag = null)
    {
        var (world, _) = ScenarioRunner.Create(7, agricultureRules: ag);
        var farm = world.Workplaces.First(w => w.LocationType.Id == CropSystem.DefaultFarmLocationTypeId);
        var field = world.FindCropAt(farm.Location)!;
        var food = new ResourceType(world.EconomyRules.FoodResourceId);
        farm.Withdraw(food, farm.Stock.GetValueOrDefault(food));
        field.TakeBarn(field.Barn);
        long residents = world.Households.Where(h => h.City == farm.City).Sum(h => h.Members.Count);
        var rules = world.AgricultureRules;
        double need = residents * rules.HandKeepPerYear / 360.0 * CropSystem.DaysToNextHarvest(world, rules, field) * (1 + rules.BarnPriceMargin);
        return (world, farm, field, need);
    }

    [Fact]
    public void A_barn_that_covers_the_year_leaves_the_price_alone_and_half_of_it_multiplies_the_floor_by_kings_law()
    {
        var (world, farm, field, need) = Village();
        field.StoreBarn((long)need + 10);
        Assert.Null(LivingWorld.Simulation.Economy.Market.MarketPricingSystem.BarnScarcityPrice(world, farm, 1));

        field.TakeBarn(field.Barn);
        field.StoreBarn((long)(need / 2));
        long price = LivingWorld.Simulation.Economy.Market.MarketPricingSystem.BarnScarcityPrice(world, farm, 1)!.Value;
        Assert.InRange(price, 5, 7); // 2^2,5 ≈ 5,7: metade da cobertura, quase seis vezes o piso
    }

    [Fact]
    public void Without_the_rule_the_barn_does_not_price()
    {
        var (world, farm, _, _) = Village(AgricultureRules.Default with { BarnReserveYears = 0 });
        Assert.Null(LivingWorld.Simulation.Economy.Market.MarketPricingSystem.BarnScarcityPrice(world, farm, 1));
    }
}
