using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Labor;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Economy;

/// <summary>taxes-treasury: a cidade do empregador retém a alíquota do salário, a moeda total não muda e o livro do tesouro
/// registra o mês.</summary>
public class WageTaxTests
{
    private static readonly WorldCalendar Calendar = new(24, 30, 12);
    private static readonly Personality Neutral = Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    private sealed class NullSink : IWorldEventSink
    {
        public void Record(WorldEvent evt) { }
    }

    private static (WorldState World, City City, Workplace Shop, Npc Worker) Build(double taxRate, long employerCash = 10_000, bool withCity = true)
    {
        var rules = EconomyRules.Create(
            enabled: true, foodResourceId: 1, waterResourceId: 2,
            capacityByResourceLocation: new Dictionary<(int, int), long>(),
            spoilagePerDayByResource: new Dictionary<int, double>(),
            wageByProfession: new Dictionary<int, long> { [1] = 200 },
            priceFloor: new Dictionary<int, long> { [1] = 1, [2] = 1 }, priceCeiling: new Dictionary<int, long> { [1] = 9, [2] = 9 },
            priceSensitivity: 0, demandBaselinePerNpc: new Dictionary<int, double>(), wageTaxRate: taxRate).Value!;
        var world = new WorldState(
            Calendar, 1, ScenarioRunner.DefaultMap(1), ScenarioRunner.DefaultPopulationCatalog,
            ScenarioRunner.DefaultPopulationRules, ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules, economyRules: rules, familyRules: ScenarioRunner.DefaultFamilyRules);
        var city = new City(world.NextCityId(), new CellCoord(1, 1), 0, null, AggregatePopulationPool.Empty);
        world.AddCity(city);
        var shop = new Workplace(
            world.NextWorkplaceIdAndAdvance(), new LocationType(1), new CellCoord(1, 1), 5, employees: [],
            stock: new Dictionary<ResourceType, long>(), treasury: new Money(employerCash), prices: new Dictionary<ResourceType, long>(),
            city: withCity ? city.Id : default);
        world.AddWorkplace(shop);
        var worker = new Npc(
            world.NextNpcIdAndAdvance(), "w", Sex.Male, WorldDate.Epoch(Calendar).AddYears(-30), new CultureId(1),
            new CellCoord(1, 1), null, null, household: null, health: 100, personality: Neutral, profession: new ProfessionType(1),
            currentLocation: new CellCoord(1, 1));
        world.AddNpc(worker);
        shop.Hire(worker.Id);
        worker.Hire(shop.Id);
        return (world, city, shop, worker);
    }

    private static void Pay(WorldState world)
    {
        var ctx = new TickContext(world, world.Rng, world.Scheduler, new NullSink());
        new WagePaymentSystem().Tick(world, ctx);
    }

    [Fact]
    public void The_employer_city_withholds_the_rate_and_the_worker_gets_the_net_with_total_money_unchanged()
    {
        var (world, city, shop, worker) = Build(taxRate: 0.05);
        long before = worker.Wallet.Amount + shop.Treasury.Amount + city.Treasury.Amount;

        Pay(world);

        Assert.Equal(190, worker.Wallet.Amount);
        Assert.Equal(10, city.Treasury.Amount);
        Assert.Equal(9_800, shop.Treasury.Amount);
        Assert.Equal(before, worker.Wallet.Amount + shop.Treasury.Amount + city.Treasury.Amount);
        var month = Assert.Single(world.TreasuryLedger);
        Assert.Equal((city.Id, 10L, 0L), (month.City, month.Revenue, month.Spending));
    }

    [Fact]
    public void A_zero_rate_or_a_workplace_without_a_city_withholds_nothing()
    {
        var (zeroWorld, zeroCity, _, zeroWorker) = Build(taxRate: 0.0);
        Pay(zeroWorld);
        Assert.Equal(200, zeroWorker.Wallet.Amount);
        Assert.Equal(0, zeroCity.Treasury.Amount);
        Assert.Empty(zeroWorld.TreasuryLedger);

        var (orphanWorld, orphanCity, _, orphanWorker) = Build(taxRate: 0.05, withCity: false);
        Pay(orphanWorld);
        Assert.Equal(200, orphanWorker.Wallet.Amount);
        Assert.Equal(0, orphanCity.Treasury.Amount);
    }

    [Fact]
    public void An_employer_short_on_cash_still_conserves_money_and_taxes_only_what_it_paid()
    {
        var (world, city, shop, worker) = Build(taxRate: 0.05, employerCash: 100);
        long before = worker.Wallet.Amount + shop.Treasury.Amount + city.Treasury.Amount;

        Pay(world);

        Assert.Equal(before, worker.Wallet.Amount + shop.Treasury.Amount + city.Treasury.Amount);
        Assert.Equal(5, city.Treasury.Amount); // 5% dos 100 pagos
        Assert.Equal(95, worker.Wallet.Amount);
    }

    [Fact]
    public void The_treasury_ledger_sums_a_city_month_and_keeps_months_apart()
    {
        var (world, city, _, _) = Build(taxRate: 0.05);
        long month = Calendar.HoursPerMonth;

        world.RecordTreasury(city.Id, 5, revenue: 10, spending: 0);
        world.RecordTreasury(city.Id, 9, revenue: 5, spending: 4);
        world.RecordTreasury(city.Id, month + 1, revenue: 1, spending: 0);

        Assert.Equal([(0L, 15L, 4L), (1L, 1L, 0L)], world.TreasuryLedger.Select(e => (e.Month, e.Revenue, e.Spending)).ToArray());
        Assert.Single(world.TreasuryOf(city.Id, 0, 29));
        Assert.Equal(2, world.TreasuryOf(city.Id, 0, 59).Count);
    }

    [Fact]
    public void The_ledger_survives_a_snapshot_round_trip()
    {
        var (world, city, _, _) = Build(taxRate: 0.05);
        world.RecordTreasury(city.Id, 5, revenue: 10, spending: 3);

        var restored = WorldSnapshot.Deserialize(WorldSnapshot.Serialize(world));

        Assert.Equal(world.TreasuryLedger, restored.TreasuryLedger);
        Assert.Equal(0.05, restored.EconomyRules.WageTaxRate);
        Assert.Equal(WorldSnapshot.CanonicalHash(world), WorldSnapshot.CanonicalHash(restored));
    }
}
