using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Invention;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Decision;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Market;
using LivingWorld.Simulation.Invention;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Invention;

/// <summary>AD-182 (08c itens 111 e 147): quem sai para comprar leva o recipiente da casa mesmo sem arrumar lanche, e a volta pelo
/// que ficou na banca guarda quanto ficou e só antecipa a ida se o resto vale a viagem.</summary>
public class MarketTripTests
{
    private static readonly InventionRules On = ScenarioRunner.DefaultInventionRules;
    private static readonly InventionRules Off = On with { MarketTripOverride = MarketTripRules.Disabled };

    private static WorldState NewWorld(InventionRules rules) => ScenarioRunner.Create(1, inventionRules: rules).World;

    private static Household HomeOf(WorldState world, Npc npc) => world.FindHousehold(npc.Household!.Value)!;

    /// <summary>Adulto de casa com outro adulto, a casa com um recipiente.</summary>
    private static (Npc Buyer, Npc Other, Household Home) Pair(WorldState world)
    {
        var buyer = world.Npcs.Where(n => n.IsAlive && n.Household is not null && n.AgeYears(world.CurrentDate) >= 20).OrderBy(n => n.Id.Value)
            .First(n => HomeOf(world, n).Members.Any(m => m != n.Id && world.FindNpc(m) is { IsAlive: true } o && o.AgeYears(world.CurrentDate) >= 16));
        var home = HomeOf(world, buyer);
        var other = home.Members.Where(m => m != buyer.Id).Select(m => world.FindNpc(m)!).First(o => o.IsAlive && o.AgeYears(world.CurrentDate) >= 16);
        var ledger = world.Invention!;
        var m = On.Materials.First(x => x.Formable && !x.CoatingOnly);
        var design = Design.Create([CarryInvention.NeedSized(m, 8000)!.Value], buyer.Id.Value, 1, 0, 0, DesignOrigin.Invented);
        ledger.AddDesign(design);
        ledger.MarkCarryDesign(design.Id);
        ledger.Items.Add(new CraftedItem(ledger.NextItemId++, design.Id, home.Id.Value, buyer.City, 1, long.MaxValue, 1000));
        ledger.Touch();
        return (buyer, other, home);
    }

    /// <summary>O comprador sai de casa para o mercado (a viagem começa na porta) com a fome e a sede satisfeitas: nada a arrumar.</summary>
    private static long DepartForMarket(InventionRules rules)
    {
        var world = NewWorld(rules);
        var (buyer, _, home) = Pair(world);
        long now = world.CurrentDate.TotalHours;
        buyer.MoveTo(home.Location, now);
        buyer.SetHunger(100, now);
        buyer.SetThirst(100, now);
        buyer.SetCurrentAction(ActionType.Travel, now);
        buyer.SetTravelPurpose(ActionType.Buy);
        var index = MarketIndex.BuildForTick(world);
        Assert.True(BehaviorDecisionSystem.TravelDestinationFor(world, buyer, index) is { } market && market != home.Location, "o mercado fica fora de casa");
        BehaviorDecisionSystem.PackOnDeparture(world, buyer, index, now);
        Assert.Equal(0, world.Invention!.CarriedQuantity(buyer.Id.Value, world.EconomyRules.FoodResourceId)); // nada arrumado
        return world.Invention.HeldBy(buyer.Id.Value);
    }

    [Fact]
    public void Going_to_market_without_packing_takes_the_household_container()
    {
        Assert.NotEqual(0, DepartForMarket(On));
    }

    [Fact]
    public void Going_to_market_without_packing_left_the_container_free_before_ad182()
    {
        Assert.Equal(0, DepartForMarket(Off));
    }

    /// <summary>A casa com a despensa cheia e a última compra cortada pela carga, com <paramref name="leftFood"/> de comida na banca.</summary>
    private static bool WantsToGoBack(InventionRules rules, long leftFood, bool storeUnits = true)
    {
        var world = NewWorld(rules);
        var (buyer, _, home) = Pair(world);
        var food = new ResourceType(world.EconomyRules.FoodResourceId);
        var water = new ResourceType(world.EconomyRules.WaterResourceId);
        home.Deposit(food, 100L * home.Members.Count);
        home.Deposit(water, 100L * home.Members.Count);
        buyer.CreditWallet(new Money(1000));
        world.Invention!.LeaveAtMarket(home.Id.Value, true);
        if (storeUnits) world.Invention.SetLeftUnitsAtMarket(home.Id.Value, food.Id, leftFood);
        return BehaviorDecisionSystem.NeedsRestocking(world, buyer, MarketIndex.BuildForTick(world));
    }

    [Fact]
    public void A_scrap_left_at_the_stall_does_not_call_for_an_early_trip()
    {
        Assert.False(WantsToGoBack(On, leftFood: 1));
    }

    [Fact]
    public void A_day_or_more_of_food_left_at_the_stall_calls_for_the_trip_back()
    {
        var world = NewWorld(On);
        int members = HomeOf(world, Pair(world).Buyer).Members.Count;
        Assert.True(WantsToGoBack(On, leftFood: members));
    }

    [Fact]
    public void Before_ad182_any_mark_called_for_the_trip_back()
    {
        Assert.True(WantsToGoBack(Off, leftFood: 1));
    }

    [Fact]
    public void Clearing_the_mark_clears_the_units_left()
    {
        var world = NewWorld(On);
        world.Invention!.LeaveAtMarket(7, true);
        world.Invention.SetLeftUnitsAtMarket(7, world.EconomyRules.FoodResourceId, 5);

        world.Invention.LeaveAtMarket(7, false);

        Assert.True(world.Invention.LeftAtMarketQuantities is null && !world.Invention.IsLeftAtMarket(7));
    }
}
