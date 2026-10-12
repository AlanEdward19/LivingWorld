using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Decision;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Market;
using LivingWorld.Simulation.Economy.Trade;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Economy;

/// <summary>trade-ledger: toda compra que fecha entra no livro da oficina (C1), a recusada não deixa rastro (C2), o
/// comprador cria relação de negócio com quem atende e só o primeiro contato do par vira evento (C4-C6), e o livro é
/// limitado: dia a dia na janela, mês a mês depois dela, sem perder soma (C3, C7).</summary>
public class TradeLedgerTests
{
    private static readonly WorldCalendar Calendar = new(24, 30, 12);
    private static readonly CellCoord Market = new(1, 1);
    private static readonly ResourceType Wheat = new(1);
    private static readonly ResourceType Water = new(2);
    private static readonly Personality Neutral = Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    private sealed class RecordingSink : IWorldEventSink
    {
        public List<WorldEvent> Events { get; } = [];
        public void Record(WorldEvent evt) => Events.Add(evt);
    }

    private static EconomyRules Economy() => EconomyRules.Create(
        enabled: true, foodResourceId: 1, waterResourceId: 2,
        capacityByResourceLocation: new Dictionary<(int, int), long>(),
        spoilagePerDayByResource: new Dictionary<int, double>(), wageByProfession: new Dictionary<int, long>(),
        priceFloor: new Dictionary<int, long> { [1] = 1, [2] = 1 }, priceCeiling: new Dictionary<int, long> { [1] = 9, [2] = 9 },
        priceSensitivity: 0, demandBaselinePerNpc: new Dictionary<int, double>()).Value!;

    private static (WorldState World, Workplace Shop) BuildWorld(long shopStock = 1000)
    {
        var world = new WorldState(
            Calendar, 1, ScenarioRunner.DefaultMap(1), ScenarioRunner.DefaultPopulationCatalog,
            ScenarioRunner.DefaultPopulationRules, ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules, economyRules: Economy(), familyRules: ScenarioRunner.DefaultFamilyRules,
            economyCatalog: new EconomyCatalog(new Dictionary<int, ProductionRecipe>(), [1], new Dictionary<int, int>()));
        var shop = new Workplace(
            world.NextWorkplaceIdAndAdvance(), new LocationType(1), Market, 5, employees: [],
            stock: new Dictionary<ResourceType, long> { [Wheat] = shopStock, [Water] = shopStock }, treasury: Money.Zero,
            prices: new Dictionary<ResourceType, long> { [Wheat] = 4, [Water] = 3 });
        world.AddWorkplace(shop);
        return (world, shop);
    }

    private static Npc AddNpc(WorldState world, CellCoord at, long wallet = 0, Workplace? job = null, bool household = false)
    {
        var npc = new Npc(
            world.NextNpcIdAndAdvance(), "npc", Sex.Male, WorldDate.Epoch(Calendar).AddYears(-30), new CultureId(1),
            at, motherId: null, fatherId: null, household: null, health: 100,
            personality: Neutral, profession: new ProfessionType(1), currentLocation: at, wallet: new Money(wallet));
        world.AddNpc(npc);
        if (job is not null)
        {
            job.Hire(npc.Id);
            npc.Hire(job.Id);
        }

        if (household)
        {
            var home = new Household(new HouseholdId(world.Households.Count + 1), new CellCoord(5, 5), npc.Id, [npc.Id]);
            world.AddHousehold(home);
            npc.JoinHousehold(home.Id);
        }

        return npc;
    }

    private static void Buy(WorldState world, Npc buyer) =>
        BehaviorDecisionSystem.ResolveFoodPlan(world, buyer, MarketIndex.BuildForTick(world), world.CurrentDate.TotalHours, ActionType.Buy);

    [Fact]
    public void A_purchase_enters_the_ledger_with_exactly_what_left_the_wallet()
    {
        var (world, shop) = BuildWorld();
        var buyer = AddNpc(world, Market, wallet: 500, household: true);

        Buy(world, buyer);

        long spent = 500 - buyer.Wallet.Amount;
        Assert.True(spent > 0);
        var today = world.TradesOf(shop.Id, 0, 0);
        Assert.Equal(2, today.Count); // trigo e agua
        Assert.Equal(spent, today.Sum(e => e.Value));
        Assert.Equal(shop.Treasury.Amount, today.Sum(e => e.Value));
        Assert.All(today, e => Assert.Equal(1, e.Purchases));
        Assert.Equal(1000 - today.Single(e => e.ResourceId == 1).Quantity, shop.Stock[Wheat]);
    }

    [Fact]
    public void A_refused_purchase_leaves_the_ledger_the_shop_and_the_relationships_untouched()
    {
        var (world, shop) = BuildWorld();
        var buyer = AddNpc(world, Market, wallet: 0, household: true);
        Buy(world, buyer);

        Assert.Empty(world.TradeLedger);
        Assert.Empty(world.Relationships);
        Assert.Equal(0, buyer.Wallet.Amount);
        Assert.Equal(Money.Zero, shop.Treasury);
        Assert.Equal(1000, shop.Stock[Wheat]);
    }

    [Fact]
    public void Two_purchases_the_same_day_sum_into_one_aggregate()
    {
        var (world, shop) = BuildWorld();
        var a = AddNpc(world, Market, wallet: 500, household: true);
        var b = AddNpc(world, Market, wallet: 500, household: true);

        Buy(world, a);
        Buy(world, b);

        var wheat = world.TradesOf(shop.Id, 0, 0).Single(e => e.ResourceId == 1);
        Assert.Equal(2, wheat.Purchases);
        Assert.Equal(wheat.Quantity * 4, wheat.Value); // 4 por unidade de trigo
    }

    [Fact]
    public void The_buyer_gains_a_trade_relationship_with_the_lowest_id_attendant_present_and_only_the_first_contact_is_an_event()
    {
        var (world, shop) = BuildWorld();
        var absent = AddNpc(world, new CellCoord(9, 9), job: shop);   // empregado, mas fora da oficina
        var attendant = AddNpc(world, Market, job: shop);              // menor id presente
        var other = AddNpc(world, Market, job: shop);
        var buyer = AddNpc(world, Market, wallet: 5000, household: true);
        var sink = new RecordingSink();
        var ctx = new TickContext(world, world.Rng, world.Scheduler, sink);

        Buy(world, buyer);
        Buy(world, buyer);
        new TradeLedgerSystem().Tick(world, ctx);

        var relation = world.Relationships[new RelationshipKey(buyer.Id, attendant.Id)];
        Assert.True(relation.Trust > 0);
        Assert.False(world.Relationships.ContainsKey(new RelationshipKey(buyer.Id, absent.Id)));
        Assert.False(world.Relationships.ContainsKey(new RelationshipKey(buyer.Id, other.Id)));
        var trade = Assert.Single(sink.Events, e => e.Kind == WorldEventKind.Trade);
        Assert.Equal($"{buyer.Id.Value}|{attendant.Id.Value}|{shop.Id.Value}|1|{trade.Payload.Split('|')[4]}", trade.Payload);
    }

    [Fact]
    public void A_shop_with_nobody_present_only_writes_the_ledger()
    {
        var (world, shop) = BuildWorld();
        var buyer = AddNpc(world, Market, wallet: 500, household: true);

        Buy(world, buyer);

        Assert.NotEmpty(world.TradeLedger);
        Assert.Empty(world.Relationships);
        Assert.Empty(world.PendingTradeContacts);
    }

    [Fact]
    public void Days_older_than_the_window_fold_into_months_without_losing_a_cent_and_the_ledger_stays_bounded()
    {
        var (world, shop) = BuildWorld();
        long total = 0;
        for (int day = 0; day < 300; day++)
        {
            world.RecordTrade(shop.Id, Wheat, day, quantity: 2, value: 8);
            total += 8;
        }

        world.CompactTradeLedger(today: 299);

        Assert.Equal(total, world.TradeLedger.Sum(e => e.Value));
        Assert.Equal(300 * 2, world.TradeLedger.Sum(e => e.Quantity));
        Assert.Equal(300, world.TradeLedger.Sum(e => e.Purchases));
        Assert.Equal(WorldState.TradeLedgerDetailDays, world.TradeLedger.Count(e => !e.Monthly));
        Assert.True(world.TradeLedger.Count(e => e.Monthly) <= 300 / 30);
        Assert.All(world.TradeLedger.Where(e => !e.Monthly), e => Assert.True(e.Period > 299 - WorldState.TradeLedgerDetailDays));
        Assert.Empty(world.TradesOf(shop.Id, 0, 100)); // o que virou mes nao volta como dia
    }

    [Fact]
    public void The_ledger_survives_a_snapshot_round_trip_and_two_equal_runs_write_the_same_ledger()
    {
        var (world, shop) = BuildWorld();
        world.RecordTrade(shop.Id, Wheat, 3, 5, 20);
        world.RecordTrade(shop.Id, Water, 3, 2, 6);

        var restored = WorldSnapshot.Deserialize(WorldSnapshot.Serialize(world));

        Assert.Equal(world.TradeLedger, restored.TradeLedger);
        Assert.Equal(WorldSnapshot.CanonicalHash(world), WorldSnapshot.CanonicalHash(restored));

        static IReadOnlyList<TradeAggregate> Run()
        {
            var (w, c) = ScenarioRunner.Create(seed: 5, initialPopulation: 30);
            c.Run(w, 24 * 60);
            return w.TradeLedger;
        }

        var first = Run();
        Assert.NotEmpty(first);
        Assert.Equal(first, Run());
    }
}
