using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Labor;
using LivingWorld.Simulation.Economy.Venture;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Economy;

/// <summary>AD-152: o NPC decide abrir o próprio negócio e paga a abertura com moeda que existe (poupança, herança, sócio).</summary>
public sealed class VentureTests
{
    private const int Forge = 2;

    private sealed class Events : IWorldEventSink
    {
        public List<WorldEvent> All { get; } = [];
        public void Record(WorldEvent evt) => All.Add(evt);
    }

    private sealed record Village(WorldState World, VentureRules Rules, VentureTrade Trade, Events Log, TickContext Ctx);

    private static Village Make(ulong seed = 7)
    {
        var (world, _) = ScenarioRunner.Create(seed, initialPopulation: 30);
        var log = new Events();
        var trade = VentureTrade.Of(world, ScenarioRunner.DefaultSkillsRules, Forge)!;
        return new Village(world, world.EconomyRules.Venture!, trade, log, new TickContext(world, world.Rng, world.Scheduler, log));
    }

    private static long Coin(WorldState w) =>
        w.Npcs.Sum(n => n.Wallet.Amount) + w.Workplaces.Sum(x => x.Treasury.Amount) + w.Cities.Sum(c => c.Treasury.Amount);

    /// <summary>Um adulto da vila (que não é dono de nada) e um filho dele, de 25 anos, morando na mesma cidade: o fundador e
    /// um parente que pode virar sócio.</summary>
    private static (Npc Founder, Npc Parent) ChildAndParent(WorldState world)
    {
        var parent = world.Npcs.OrderBy(n => n.Id.Value).First(n => n.IsAlive && n.AgeYears(world.CurrentDate) is >= 45 and < 60
            && !world.Workplaces.Any(w => w.Owner == n.Id));
        var child = new Npc(world.NextNpcIdAndAdvance(), "filho", Sex.Male, world.CurrentDate.AddYears(-25), ScenarioRunner.DefaultCulture,
            parent.CurrentLocation, motherId: parent.Sex == Sex.Female ? parent.Id : null, fatherId: parent.Sex == Sex.Male ? parent.Id : null,
            household: null, health: 100, personality: Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!,
            profession: new ProfessionType(2), currentLocation: parent.CurrentLocation, city: parent.City);
        world.AddNpc(child);
        return (child, parent);
    }

    private static Npc Smith(Village v, Npc npc, long wallet)
    {
        npc.GainSkill(v.Trade.Skill, 80, 100);
        npc.TryDebitWallet(npc.Wallet);
        npc.CreditWallet(new Money(wallet));
        return npc;
    }

    private static Workplace? Found(Village v, Npc founder) =>
        VentureFounding.TryFound(v.World, v.Ctx, v.Rules, v.Trade, VenturePlan.For(v.World, v.Rules, v.Trade, founder.City), founder);

    [Fact]
    public void A_smith_with_savings_opens_a_forge_he_owns_and_works_in()
    {
        var v = Make();
        var founder = Smith(v, ChildAndParent(v.World).Parent, 50_000);

        var shop = Found(v, founder);

        Assert.NotNull(shop);
        Assert.Equal(founder.Id, shop!.Owner);
        Assert.Equal(shop.Id, founder.Employer);
        Assert.Null(shop.Partners);
    }

    [Fact]
    public void The_new_shop_starts_with_the_working_capital_the_founder_put_in()
    {
        var v = Make();
        var founder = Smith(v, ChildAndParent(v.World).Parent, 50_000);
        long payroll = VenturePlan.For(v.World, v.Rules, v.Trade, founder.City).MonthlyPayroll;

        var shop = Found(v, founder)!;

        Assert.Equal((long)Math.Ceiling(v.Rules.WorkingCapitalMonths * payroll), shop.Treasury.Amount);
    }

    [Fact]
    public void Opening_only_moves_coin_and_never_mints_it()
    {
        var v = Make();
        var founder = Smith(v, ChildAndParent(v.World).Parent, 50_000);
        long before = Coin(v.World), minted = v.World.MoneyMinted.Amount;

        Found(v, founder);

        Assert.Equal((before, minted), (Coin(v.World), v.World.MoneyMinted.Amount));
    }

    [Fact]
    public void Without_the_capital_nothing_opens_and_no_coin_moves()
    {
        var v = Make();
        var (child, parent) = ChildAndParent(v.World);
        parent.TryDebitWallet(parent.Wallet);
        var founder = Smith(v, child, 10);
        int shops = v.World.Workplaces.Count;
        long before = Coin(v.World);

        Assert.Null(Found(v, founder));
        Assert.Equal((shops, before, 10L), (v.World.Workplaces.Count, Coin(v.World), founder.Wallet.Amount));
    }

    [Fact]
    public void A_parent_with_money_puts_in_what_is_missing_and_gets_a_stake()
    {
        var v = Make();
        var (child, parent) = ChildAndParent(v.World);
        var founder = Smith(v, child, 600);
        parent.CreditWallet(new Money(100_000));

        var shop = Found(v, founder)!;

        var stake = Assert.Single(shop.Partners!);
        Assert.True(stake.Npc == parent.Id && stake.Basis > 0 && stake.Basis < WorkplaceStake.Whole, $"{stake}");
    }

    [Fact]
    public void The_event_names_the_partnership_as_the_origin_of_the_capital()
    {
        var v = Make();
        var (child, parent) = ChildAndParent(v.World);
        var founder = Smith(v, child, 600);
        parent.CreditWallet(new Money(100_000));

        Found(v, founder);

        var founded = Assert.Single(v.Log.All, e => e.Kind == WorldEventKind.VentureFounded);
        Assert.Equal("sociedade", founded.Payload.Split('|')[2]);
    }

    [Fact]
    public void Money_that_came_from_an_inheritance_is_counted_as_inheritance_and_spent_from_the_counter()
    {
        var v = Make();
        var founder = Smith(v, ChildAndParent(v.World).Parent, 50_000);
        founder.NoteInheritance(50_000);

        Found(v, founder);

        var founded = Assert.Single(v.Log.All, e => e.Kind == WorldEventKind.VentureFounded);
        long capital = long.Parse(founded.Payload.Split('|')[3], System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(("heranca", 50_000 - capital), (founded.Payload.Split('|')[2], founder.Inheritance));
    }

    [Fact]
    public void A_partner_takes_his_share_of_the_monthly_draw()
    {
        var v = Make();
        var (child, parent) = ChildAndParent(v.World);
        var founder = Smith(v, child, 600);
        parent.CreditWallet(new Money(100_000));
        var shop = Found(v, founder)!;
        int basis = shop.Partners![0].Basis;
        shop.TryDebitTreasury(shop.Treasury);
        shop.CreditTreasury(new Money(Payroll.WorkingReserve(v.World, shop) + 10_000));
        long parentBefore = parent.Wallet.Amount;

        new OwnerDrawSystem().Tick(v.World, v.Ctx);

        Assert.Equal(10_000L * basis / WorkplaceStake.Whole, parent.Wallet.Amount - parentBefore);
    }

    [Fact]
    public void The_stake_of_a_dead_partner_passes_to_his_heirs()
    {
        var v = Make();
        var (child, parent) = ChildAndParent(v.World);
        var founder = Smith(v, child, 600);
        parent.CreditWallet(new Money(100_000));
        var shop = Found(v, founder)!;
        parent.Die(v.World.CurrentDate);
        shop.CreditTreasury(new Money(Payroll.WorkingReserve(v.World, shop) + 10_000));

        new OwnerDrawSystem().Tick(v.World, v.Ctx);

        Assert.DoesNotContain(shop.Partners ?? [], s => s.Npc == parent.Id);
    }

    [Fact]
    public void Equipment_comes_from_the_maker_never_from_the_tools_the_farm_is_using()
    {
        var v = Make();
        var farm = v.World.Workplaces.First(w => w.LocationType.Id == 1);
        var iron = new ResourceType(4);
        farm.Deposit(iron, 30, v.World.EconomyRules);
        var founder = Smith(v, ChildAndParent(v.World).Parent, 50_000);

        Found(v, founder);

        Assert.Equal(30, farm.Stock.GetValueOrDefault(iron));
    }

    [Fact]
    public void The_owner_of_a_stopped_shop_reopens_it_with_his_savings()
    {
        var v = Make();
        var founder = Smith(v, ChildAndParent(v.World).Parent, 50_000);
        var shop = Found(v, founder)!;
        shop.TryDebitTreasury(shop.Treasury);
        shop.SetPayrollFailed(true);
        shop.Fire(founder.Id);
        founder.Fire();
        int shops = v.World.Workplaces.Count;
        var plan = VenturePlan.For(v.World, v.Rules, v.Trade, founder.City);

        var reopened = VentureFounding.TryReopen(v.World, v.Ctx, v.Rules, v.Trade, plan, founder, shop);

        Assert.True(reopened == shop && !shop.PayrollFailed && shop.Treasury.Amount > 0 && founder.Employer == shop.Id
            && v.World.Workplaces.Count == shops);
    }

    [Fact]
    public void Ambition_raises_and_fear_of_risk_lowers_the_chance_of_opening()
    {
        var v = Make();
        var plan = new VenturePlan(0.5, 300, 110, new Dictionary<ResourceType, long>());
        var bold = Npc(v.World, ambition: 90, riskAversion: 10);
        var timid = Npc(v.World, ambition: 20, riskAversion: 80);

        Assert.True(VentureSystem.ChanceOf(v.World, v.Rules, v.Trade, plan, bold) > 10 * VentureSystem.ChanceOf(v.World, v.Rules, v.Trade, plan, timid));
    }

    [Fact]
    public void Nobody_opens_what_nobody_buys()
    {
        var v = Make(); // dia zero: a fazenda ainda não contratou ninguém, não há encomenda de ferramenta

        Assert.Equal(0, VenturePlan.For(v.World, v.Rules, v.Trade, v.World.Cities[0].Id).Margin);
    }

    [Fact]
    public void A_newcomer_counts_on_only_his_share_of_the_customers_of_a_working_rival()
    {
        var v = Make();
        var farm = v.World.Workplaces.First(w => w.LocationType.Id == 1);
        foreach (var n in v.World.Npcs.Where(n => n.IsAlive && n.AgeYears(v.World.CurrentDate) is >= 21 and < 60).Take(6))
        {
            farm.Hire(n.Id);
            n.Hire(farm.Id);
        }
        var city = farm.City;
        long alone = VenturePlan.For(v.World, v.Rules, v.Trade, city).MonthlySales;
        var forge = v.World.Workplaces.First(w => w.LocationType.Id == Forge);
        var smith = v.World.Npcs.First(n => n.IsAlive && n.Employer is null && n.AgeYears(v.World.CurrentDate) is >= 21 and < 60);
        forge.Hire(smith.Id);
        smith.Hire(forge.Id);

        long shared = VenturePlan.For(v.World, v.Rules, v.Trade, city).MonthlySales;

        Assert.True(alone > 0 && shared < alone, $"{shared} vs {alone}");
    }

    [Fact]
    public void Against_a_working_rival_the_newcomer_charges_less_to_win_customers()
    {
        var v = Make();
        var forge = v.World.Workplaces.First(w => w.LocationType.Id == Forge);
        var smith = v.World.Npcs.First(n => n.IsAlive && n.AgeYears(v.World.CurrentDate) is >= 21 and < 60);
        forge.Hire(smith.Id);
        smith.Hire(forge.Id);
        long rival = forge.Prices[new ResourceType(4)];

        var plan = VenturePlan.For(v.World, v.Rules, v.Trade, forge.City);

        Assert.True(plan.Prices[new ResourceType(4)] < rival, $"{plan.Prices[new ResourceType(4)]} vs {rival}");
    }

    [Fact]
    public void A_village_without_venture_rules_has_no_venture_and_keeps_its_world()
    {
        var (world, _) = ScenarioRunner.Create(7, initialPopulation: 30,
            economyRules: ScenarioRunner.DefaultEconomyRules with { Venture = null });
        string before = WorldSnapshot.CanonicalHash(world);

        new VentureSystem(ScenarioRunner.DefaultSkillsRules).Tick(world, new TickContext(world, world.Rng, world.Scheduler));

        Assert.Equal(before, WorldSnapshot.CanonicalHash(world));
    }

    [Fact]
    public void Partners_inheritance_and_the_rules_survive_a_snapshot_round_trip()
    {
        var v = Make();
        var (child, parent) = ChildAndParent(v.World);
        var founder = Smith(v, child, 600);
        parent.CreditWallet(new Money(100_000));
        Found(v, founder);
        parent.NoteInheritance(123);

        var restored = WorldSnapshot.Deserialize(WorldSnapshot.Serialize(v.World));

        Assert.Equal(WorldSnapshot.CanonicalHash(v.World), WorldSnapshot.CanonicalHash(restored));
    }

    [Fact]
    public void Same_seed_gives_the_same_world_and_another_seed_does_not()
    {
        static string Hash(ulong seed)
        {
            var (world, clock) = ScenarioRunner.Create(seed, initialPopulation: 30);
            clock.Run(world, 61 * 24);
            return WorldSnapshot.CanonicalHash(world);
        }

        Assert.Equal(Hash(3), Hash(3));
        Assert.NotEqual(Hash(3), Hash(4));
    }

    private static Npc Npc(WorldState world, int ambition, int riskAversion)
    {
        var city = world.Cities[0];
        return new Npc(new NpcId(999_999), "x", Sex.Male, world.CurrentDate.AddYears(-30), ScenarioRunner.DefaultCulture,
            city.Location, motherId: null, fatherId: null, household: null, health: 100,
            personality: Personality.Create(50, 50, 50, 50, 50, ambition, 50, 50, 50, riskAversion).Value!,
            profession: new ProfessionType(1), currentLocation: city.Location, city: city.Id);
    }
}
