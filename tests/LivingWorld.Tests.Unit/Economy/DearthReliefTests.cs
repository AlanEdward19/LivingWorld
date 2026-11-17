using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Decision;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Market;
using LivingWorld.Simulation.Economy.Production;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Economy;

/// <summary>AD-182 (08c item 148): na carestia o celeiro racionava certo até a colheita, mas a primeira casa a comprar levava a semana
/// do pouco que saía no dia. Agora o vendedor reparte a compra e a semente do ano seguinte não vai à venda.</summary>
public sealed class DearthReliefTests
{
    private sealed class NoSink : IWorldEventSink
    {
        public void Record(WorldEvent evt) { }
    }

    private static (WorldState World, TickContext Ctx, Workplace Farm, CropBatch Field) Village(AgricultureRules? rules = null)
    {
        var (world, _) = ScenarioRunner.Create(23, agricultureRules: rules);
        var farm = world.Workplaces.OrderBy(w => w.Id.Value).First(w => w.LocationType.Id == CropSystem.DefaultFarmLocationTypeId);
        return (world, new TickContext(world, world.Rng, world.Scheduler, new NoSink()), farm, world.FindCropAt(farm.Location)!);
    }

    private static Household EmptyHouse(WorldState world)
    {
        var food = new ResourceType(world.EconomyRules.FoodResourceId);
        var house = world.Households.OrderBy(h => h.Id.Value).First(h => h.Members.Count >= 2);
        long have = house.Stock.GetValueOrDefault(food);
        if (have > 0) house.Withdraw(food, have);
        return house;
    }

    /// <summary>Comprador com moeda farta e a casa vazia no mercado da fazenda; devolve a comida que a compra trouxe.</summary>
    private static (long Bought, long Cap) BuyAtRationedStall(bool rationed)
    {
        var (world, _, farm, _) = Village();
        var house = EmptyHouse(world);
        var food = new ResourceType(world.EconomyRules.FoodResourceId);
        farm.Deposit(food, 5_000, world.EconomyRules);
        farm.SetRationedSale(rationed);
        var buyer = world.FindNpc(house.Head)!;
        buyer.CreditWallet(new Money(100_000));
        long now = world.CurrentDate.TotalHours;
        buyer.MoveTo(farm.Location, now);
        buyer.SetHunger(100, now);
        buyer.SetThirst(100, now);
        world.Invention!.Carried.Clear();
        BehaviorDecisionSystem.ResolveFoodPlan(world, buyer, MarketIndex.BuildForTick(world), now, ActionType.Buy);
        return (house.Stock.GetValueOrDefault(food), (long)house.Members.Count * world.AgricultureRules.DearthSaleDays);
    }

    [Fact]
    public void At_a_rationed_stall_a_purchase_takes_at_most_a_few_days_for_the_house()
    {
        var (bought, cap) = BuyAtRationedStall(rationed: true);
        Assert.InRange(bought, 1, cap);
    }

    [Fact]
    public void Without_rationing_the_same_purchase_takes_more()
    {
        Assert.True(BuyAtRationedStall(rationed: false).Bought > BuyAtRationedStall(rationed: true).Bought);
    }

    /// <summary>Celeiro vazio, semente guardada, banca vazia: um dia da lavoura; devolve a semente que sobrou.</summary>
    private static long SeedAfterADayWithEmptyBarn(AgricultureRules rules)
    {
        var (world, ctx, farm, field) = Village(rules);
        world.CurrentDate = world.CurrentDate.AddHours(24); // fora da virada do mês (a perda do mês na semente não conta)
        field.TakeBarn(field.Barn);
        field.StoreSeed(1_000);
        long seed = field.SeedReserve;
        var food = new ResourceType(world.EconomyRules.FoodResourceId);
        if (farm.Stock.GetValueOrDefault(food) > 0) farm.Withdraw(food, farm.Stock.GetValueOrDefault(food));
        new CropSystem().Tick(world, ctx);
        return seed - field.SeedReserve;
    }

    [Fact]
    public void The_seed_for_next_year_is_not_sold_when_the_barn_is_empty()
    {
        Assert.Equal(0, SeedAfterADayWithEmptyBarn(AgricultureRules.Default));
    }

    [Fact]
    public void Before_ad182_the_empty_barn_sold_the_seed()
    {
        Assert.True(SeedAfterADayWithEmptyBarn(AgricultureRules.Default with { SeedKeptFromSale = false }) > 0);
    }
}
