using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Decision;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Market;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Behavior.Decision;

/// <summary>AD-191, 2ª rodada: a casa com a comida curta vai à banca que tem comida (metrópole seed 2, ano 12: a mais perto vendia só
/// água e as crianças morriam com as outras bancas cheias), e a vontade de ganhar o pão só puxa ao trabalho quem tem onde ganhar
/// (vila seed 39: a órfã de 9 anos "trabalhava" parada em casa e morreu de sede com água na despensa).</summary>
public sealed class HouseholdFoodRoundTwoTests
{
    private static (WorldState World, LivingWorld.Domain.Population.Family.Household Home, Workplace Near, Workplace Far) Village(bool anyStall)
    {
        var needs = ScenarioRunner.DefaultNeedsRules;
        var (world, _) = ScenarioRunner.Create(7, needsRules: needs with { Provisioning = needs.Provisioning with { FoodFromAnyStall = anyStall } });
        var food = new ResourceType(world.EconomyRules.FoodResourceId);
        var water = new ResourceType(world.EconomyRules.WaterResourceId);
        var near = world.Workplaces.First(w => w.LocationType.Id == 1);
        near.Withdraw(food, near.Stock.GetValueOrDefault(food)); // a banca mais perto: só água
        near.Deposit(water, 100, world.EconomyRules);
        var home = world.Households.First(h => !h.IsEmpty && !h.IsHomeless);
        home.Withdraw(food, home.Stock.GetValueOrDefault(food));
        var far = new Workplace(world.NextWorkplaceIdAndAdvance(), new LocationType(1),
            new CellCoord(Math.Max(0, near.Location.X - 6), Math.Max(0, near.Location.Y - 6)), 5, employees: [],
            stock: new Dictionary<ResourceType, long> { [food] = 500 }, treasury: Money.Zero,
            prices: new Dictionary<ResourceType, long> { [food] = 2 }, city: near.City);
        world.AddWorkplace(far);
        return (world, home, near, far);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void With_no_food_at_home_the_household_goes_to_the_stall_that_has_food(bool anyStall)
    {
        var (world, home, near, far) = Village(anyStall);
        var market = BehaviorDecisionSystem.EssentialsMarket(world, home, home.Location, MarketIndex.BuildForTick(world));
        Assert.Equal(anyStall ? far.Id : near.Id, market!.Id);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Only_someone_with_a_job_or_a_site_can_earn(bool rule)
    {
        var needs = ScenarioRunner.DefaultNeedsRules;
        var (world, clock) = ScenarioRunner.Create(7, needsRules: needs with { Psyche = needs.Psyche with { IncomeNeedNeedsWork = rule } });
        clock.Run(world, 3 * 24);
        long now = world.CurrentDate.TotalHours;
        var child = world.Npcs.First(n => n.IsAlive && n.Employer is null && n.AgeYears(world.CurrentDate) is >= 8 and < 14);
        if (child.Wallet.Amount > 0) child.TryDebitWallet(new Money(child.Wallet.Amount)); // a vontade de renda só pesa sem moeda
        var worker = world.Npcs.First(n => n.IsAlive && n.Employer is not null);
        Assert.Equal(!rule, DecisionContextBuilder.Build(world, child, now).CanEarn);
        Assert.True(DecisionContextBuilder.Build(world, worker, now).CanEarn);
    }
}
