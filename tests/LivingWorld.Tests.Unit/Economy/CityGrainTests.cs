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
