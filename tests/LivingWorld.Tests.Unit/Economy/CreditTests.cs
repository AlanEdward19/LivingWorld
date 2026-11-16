using LivingWorld.Domain.Economy;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Credit;
using LivingWorld.Simulation.Economy.Labor;
using LivingWorld.Simulation.Economy.Venture;
using LivingWorld.Simulation.Population.Lifecycle;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Economy;

/// <summary>AD-155: empréstimo em moeda que existe, parcela, calote, capital de giro e falência.</summary>
public sealed class CreditTests
{
    private const int Forge = 2;

    private sealed class Events : IWorldEventSink
    {
        public List<WorldEvent> All { get; } = [];
        public void Record(WorldEvent evt) => All.Add(evt);
    }

    private sealed record Village(WorldState World, VentureRules Rules, CreditRules Credit, VentureTrade Trade, Events Log, TickContext Ctx);

    private static Village Make(ulong seed = 7, CreditRules? credit = null)
    {
        var economy = credit is null ? null : ScenarioRunner.DefaultEconomyRules with { Credit = credit };
        var (world, _) = ScenarioRunner.Create(seed, initialPopulation: 30, economyRules: economy);
        var log = new Events();
        var trade = VentureTrade.Of(world, ScenarioRunner.DefaultSkillsRules, Forge)!;
        var v = new Village(world, world.EconomyRules.Venture!, world.EconomyRules.Credit!, trade, log,
            new TickContext(world, world.Rng, world.Scheduler, log));
        // a fazenda contrata: há encomenda de ferramenta, a forja nova tem freguesia
        var farm = world.Workplaces.First(w => w.LocationType.Id == 1);
        foreach (var n in world.Npcs.Where(n => n.IsAlive && n.AgeYears(world.CurrentDate) is >= 21 and < 60 && n.Employer is null).Take(6))
        {
            farm.Hire(n.Id);
            n.Hire(farm.Id);
        }

        foreach (var n in world.Npcs) n.TryDebitWallet(n.Wallet); // ninguém tem moeda, salvo quem o teste der
        foreach (var c in world.Cities) c.TryDebitTreasury(c.Treasury);
        return v;
    }

    private static long Coin(WorldState w) =>
        w.Npcs.Sum(n => n.Wallet.Amount) + w.Workplaces.Sum(x => x.Treasury.Amount) + w.Cities.Sum(c => c.Treasury.Amount);

    private static Npc Person(WorldState world, string name, int ageYears, double riskAversion = 50, Npc? parent = null, int ambition = 50)
    {
        var anyone = world.Npcs.First(n => n.IsAlive);
        var npc = new Npc(world.NextNpcIdAndAdvance(), name, Sex.Male, world.CurrentDate.AddYears(-ageYears), ScenarioRunner.DefaultCulture,
            anyone.CurrentLocation, motherId: parent is { Sex: Sex.Female } ? parent.Id : null, fatherId: parent is { Sex: Sex.Male } ? parent.Id : null,
            household: null, health: 100, personality: Personality.Create(50, 50, 50, 50, 50, ambition, 50, 50, 50, (int)riskAversion).Value!,
            profession: new ProfessionType(2), currentLocation: anyone.CurrentLocation, city: anyone.City);
        world.AddNpc(npc);
        return npc;
    }

    private static Npc Smith(Village v, Npc npc, long wallet)
    {
        npc.GainSkill(v.Trade.Skill, 80, 100);
        npc.TryDebitWallet(npc.Wallet);
        npc.CreditWallet(new Money(wallet));
        return npc;
    }

    private static VenturePlan Plan(Village v, Npc npc) => VenturePlan.For(v.World, v.Rules, v.Trade, npc.City);

    private static Workplace? Found(Village v, Npc founder) =>
        VentureFounding.TryFound(v.World, v.Ctx, v.Rules, v.Trade, Plan(v, founder), founder);

    /// <summary>O capital da abertura no mundo da semente (lido de uma abertura com dinheiro de sobra num mundo igual).</summary>
    private static long Needed(ulong seed = 7)
    {
        var v = Make(seed);
        var founder = Smith(v, Person(v.World, "medida", 30), 1_000_000);
        Found(v, founder);
        var e = v.Log.All.Single(x => x.Kind == WorldEventKind.VentureFounded);
        return long.Parse(e.Payload.Split('|')[3], System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>Fundador que tem 70% do capital acima da reserva, e um rico da vila que não é parente nem conhecido dele.</summary>
    private static (Village V, Npc Founder, Npc Rich, long Needed) ShortFounder(double riskAversion = 50)
    {
        long needed = Needed();
        var v = Make();
        var founder = Person(v.World, "fundador", 30, riskAversion);
        Smith(v, founder, 0);
        founder.CreditWallet(new Money(VentureCapital.ReserveOf(v.World, v.Rules, founder) + needed * 7 / 10));
        var rich = Person(v.World, "rico", 50);
        rich.CreditWallet(new Money(1_000_000));
        return (v, founder, rich, needed);
    }

    private static Loan Lend(Village v, Npc lender, Npc borrower, long amount, Workplace? shop, int term = 12, double charge = 0.1)
    {
        var offer = new LoanOffer(lender, null, amount, charge, term, amount);
        return Lending.Grant(v.World, v.Ctx, offer, borrower, LoanPurpose.Working, shop?.Id.Value ?? 0);
    }

    [Fact]
    public void A_founder_short_of_capital_borrows_the_rest_from_the_rich_man_with_the_shop_as_collateral()
    {
        var (v, founder, rich, _) = ShortFounder();
        long richBefore = rich.Wallet.Amount;

        var shop = Found(v, founder);

        Assert.NotNull(shop);
        var loan = Assert.Single(v.World.Credit!.Loans);
        Assert.Equal((rich.Id.Value, founder.Id.Value, shop!.Id.Value), (loan.Lender, loan.Borrower, loan.Workplace));
        Assert.Equal(richBefore - loan.Principal, rich.Wallet.Amount);
        var granted = Assert.Single(v.Log.All, e => e.Kind == WorldEventKind.LoanGranted);
        Assert.Equal("rico", granted.Payload.Split('|')[2]);
        Assert.Equal("emprestimo", v.Log.All.Single(e => e.Kind == WorldEventKind.VentureFounded).Payload.Split('|')[2]);
    }

    [Fact]
    public void Granting_a_loan_only_moves_coin_and_never_mints_it()
    {
        var (v, founder, _, _) = ShortFounder();
        long before = Coin(v.World), minted = v.World.MoneyMinted.Amount;

        Found(v, founder);

        Assert.Equal((before, minted), (Coin(v.World), v.World.MoneyMinted.Amount));
    }

    [Fact]
    public void A_stranger_charges_the_disguised_interest_and_kin_lends_for_nothing()
    {
        var (v, founder, _, _) = ShortFounder();
        Found(v, founder);
        var fromRich = v.World.Credit!.Loans.Single();
        Assert.True(fromRich.Balance > fromRich.Principal, $"{fromRich.Balance} vs {fromRich.Principal}");

        // o pai empresta o giro ao filho dono sem acréscimo (na abertura o parente com dinheiro entra de sócio)
        var v2 = Make();
        var father = Person(v2.World, "pai", 55);
        var son = Smith(v2, Person(v2.World, "filho", 30, parent: father), 50_000);
        var shop = Found(v2, son)!;
        son.TryDebitWallet(son.Wallet);
        shop.TryDebitTreasury(shop.Treasury);
        father.CreditWallet(new Money(1_000_000));

        new WorkingCapitalSystem(ScenarioRunner.DefaultSkillsRules).Tick(v2.World, v2.Ctx);

        var fromFather = v2.World.Credit!.Loans.Single();
        Assert.Equal((father.Id.Value, fromFather.Principal, LoanPurpose.Working), (fromFather.Lender, fromFather.Balance, fromFather.Purpose));
    }

    /// <summary>Fundador com 70% do capital e um pai com dinheiro de sobra, com a aversão ao risco dada.</summary>
    private static (Village V, Npc Son, Npc Father) SonAndFather(int fatherRiskAversion)
    {
        long needed = Needed();
        var v = Make();
        var father = Person(v.World, "pai", 55, fatherRiskAversion);
        father.CreditWallet(new Money(1_000_000));
        var son = Smith(v, Person(v.World, "filho", 30, parent: father), 0);
        son.CreditWallet(new Money(VentureCapital.ReserveOf(v.World, v.Rules, son) + needed * 7 / 10));
        return (v, son, father);
    }

    [Fact]
    public void A_father_who_fears_risk_and_does_not_know_the_trade_lends_instead_of_becoming_a_partner()
    {
        var (v, son, father) = SonAndFather(fatherRiskAversion: 90);

        var shop = Found(v, son)!;

        Assert.Null(shop.Partners);
        var loan = Assert.Single(v.World.Credit!.Loans);
        Assert.Equal((father.Id.Value, loan.Principal, shop.Id.Value), (loan.Lender, loan.Balance, loan.Workplace));
        Assert.Equal("emprestimo", v.Log.All.Single(e => e.Kind == WorldEventKind.VentureFounded).Payload.Split('|')[2]);
    }

    [Fact]
    public void A_father_who_takes_risks_becomes_a_partner_instead_of_lending()
    {
        var (v, son, father) = SonAndFather(fatherRiskAversion: 10);

        var shop = Found(v, son)!;

        Assert.Equal(father.Id, Assert.Single(shop.Partners!).Npc);
        Assert.Null(v.World.Credit);
    }

    [Fact]
    public void An_ambitious_founder_with_savings_borrows_part_and_keeps_part_of_his_savings()
    {
        long needed = Needed();
        var v = Make();
        var founder = Smith(v, Person(v.World, "ousado", 30, riskAversion: 20, ambition: 80), 0);
        long reserve = VentureCapital.ReserveOf(v.World, v.Rules, founder);
        founder.CreditWallet(new Money(reserve + needed));
        Person(v.World, "rico", 50).CreditWallet(new Money(1_000_000));

        Found(v, founder);

        var loan = Assert.Single(v.World.Credit!.Loans);
        Assert.True(loan.Principal > 0 && founder.Wallet.Amount > reserve, $"{loan.Principal} {founder.Wallet.Amount}");
        Assert.Equal("emprestimo", v.Log.All.Single(e => e.Kind == WorldEventKind.VentureFounded).Payload.Split('|')[2]);
    }

    [Fact]
    public void A_cautious_founder_with_savings_puts_in_his_own_money_and_borrows_nothing()
    {
        long needed = Needed();
        var v = Make();
        var founder = Smith(v, Person(v.World, "cauteloso", 30, riskAversion: 70, ambition: 40), 0);
        founder.CreditWallet(new Money(VentureCapital.ReserveOf(v.World, v.Rules, founder) + needed));
        Person(v.World, "rico", 50).CreditWallet(new Money(1_000_000));

        Assert.NotNull(Found(v, founder));
        Assert.Null(v.World.Credit);
    }

    [Fact]
    public void Nobody_lends_to_who_defaulted_recently()
    {
        var (v, founder, _, _) = ShortFounder();
        v.World.CreditBook.MarkDefault(founder.Id.Value, 999, v.World.CurrentDate.TotalHours, 0);

        Assert.Null(Found(v, founder));
        Assert.Null(v.World.Credit!.Loans.FirstOrDefault());
    }

    [Fact]
    public void A_lender_without_coin_above_his_own_reserve_does_not_lend()
    {
        var (v, founder, rich, _) = ShortFounder();
        rich.TryDebitWallet(rich.Wallet);
        rich.CreditWallet(new Money(VentureCapital.ReserveOf(v.World, v.Rules, rich)));

        Assert.Null(Found(v, founder));
    }

    [Fact]
    public void Who_fears_risk_does_not_borrow_that_much_and_does_not_open()
    {
        var (v, founder, _, _) = ShortFounder(riskAversion: 95);

        Assert.Null(Found(v, founder));
    }

    [Fact]
    public void The_installment_comes_out_of_the_pledged_shop_and_goes_to_the_lender()
    {
        var (v, founder, rich, _) = ShortFounder();
        var shop = Found(v, founder)!;
        var loan = v.World.Credit!.Loans.Single();
        loan.FirstDueTick = 0; // AD-159: passada a carência
        long richBefore = rich.Wallet.Amount, cash = shop.Treasury.Amount, balance = loan.Balance, before = Coin(v.World);

        new CreditSystem().Tick(v.World, v.Ctx);

        Assert.Equal((richBefore + loan.Installment, cash - loan.Installment, balance - loan.Installment, before),
            (rich.Wallet.Amount, shop.Treasury.Amount, loan.Balance, Coin(v.World)));
    }

    [Fact]
    public void A_new_shop_pays_no_installment_during_the_grace_months_and_is_not_counted_late()
    {
        var (v, founder, rich, _) = ShortFounder();
        var shop = Found(v, founder)!;
        var loan = v.World.Credit!.Loans.Single();
        var cal = v.World.CurrentDate.Calendar;
        Assert.Equal(v.Ctx.CurrentTick + (long)v.Credit.FoundingGraceMonths * cal.HoursPerDay * cal.DaysPerMonth, loan.FirstDueTick);
        long richBefore = rich.Wallet.Amount, cash = shop.Treasury.Amount, balance = loan.Balance;

        new CreditSystem().Tick(v.World, v.Ctx);

        Assert.Equal((richBefore, cash, balance, 0), (rich.Wallet.Amount, shop.Treasury.Amount, loan.Balance, loan.Missed));
    }

    /// <summary>Fundador com metade do capital acima da reserva e um rico da vila; devolve se abriu e o empréstimo.</summary>
    private static (bool Opened, Loan? Loan, VenturePlan Plan) HalfFounder(CreditRules credit)
    {
        long needed = Needed();
        var v = Make(credit: credit);
        // fazenda pequena: pouca encomenda de ferramenta, o lucro sozinho não paga a parcela do prazo curto
        var farm = v.World.Workplaces.First(w => w.LocationType.Id == 1);
        foreach (var id in farm.Employees.Skip(2).ToList())
        {
            farm.Fire(id);
            v.World.FindNpc(id)!.Fire();
        }
        var founder = Smith(v, Person(v.World, "fundador", 30, riskAversion: 50), 0);
        founder.CreditWallet(new Money(VentureCapital.ReserveOf(v.World, v.Rules, founder) + needed / 2));
        Person(v.World, "rico", 50).CreditWallet(new Money(1_000_000));
        var plan = Plan(v, founder);
        bool opened = Found(v, founder) is not null;
        return (opened, v.World.Credit?.Loans.SingleOrDefault(), plan);
    }

    [Fact]
    public void The_lender_counts_the_founders_own_wage_and_stretches_the_term_so_the_installment_fits()
    {
        var old = CreditRules.Default with { OwnWageInCapacityShare = 0, MaxFoundingTermMonths = CreditRules.Default.FoundingTermMonths };
        var before = HalfFounder(old);
        var now = HalfFounder(CreditRules.Default);

        Assert.False(before.Opened, $"venda {before.Plan.MonthlySales} folha {before.Plan.MonthlyPayroll}");
        Assert.True(now.Opened);
        Assert.True(now.Loan!.Installment <= 0.5 * now.Plan.MonthlySales, $"{now.Loan.Installment} vs {now.Plan.MonthlySales}");
    }

    [Fact]
    public void The_probe_tells_why_a_founder_did_not_get_the_loan()
    {
        var (v, founder, rich, _) = ShortFounder();
        rich.TryDebitWallet(rich.Wallet);
        rich.CreditWallet(new Money(VentureCapital.ReserveOf(v.World, v.Rules, rich)));
        var probe = new LivingWorld.Simulation.Observation.VentureProbe(v.World);
        v.World.VentureProbe = probe;

        probe.Begin(founder.Id.Value);
        bool opened = Found(v, founder) is not null;
        probe.End(opened);

        int Step(string s) => probe.StepCount[0, Array.IndexOf(LivingWorld.Simulation.Observation.VentureProbe.Steps, s)];
        Assert.False(opened);
        Assert.Equal((1, 1, 0, 1), (Step("pediram_credito"), Step("sem_capital"), Step("credito_concedido"), Step("decidiram")));
        Assert.Equal(1, probe.RefusalCount[0, LendRefusal.NoMoney]);
    }

    [Fact]
    public void A_missed_installment_adds_the_late_penalty_but_never_past_the_usury_cap()
    {
        var v = Make();
        var lender = Person(v.World, "credor", 50);
        lender.CreditWallet(new Money(10_000));
        var debtor = Person(v.World, "devedor", 30);
        var loan = Lend(v, lender, debtor, 1_000, shop: null);
        debtor.TryDebitWallet(debtor.Wallet);
        long balance = loan.Balance;

        new CreditSystem().Tick(v.World, v.Ctx);

        Assert.Equal((1, balance + (long)Math.Ceiling(loan.Installment * v.Credit.LatePenaltyShare)), (loan.Missed, loan.Balance));
        for (int i = 0; i < 3; i++) new CreditSystem().Tick(v.World, v.Ctx);
        Assert.True(loan.Charged <= loan.ChargeCap, $"{loan.Charged} > {loan.ChargeCap}");
    }

    [Fact]
    public void After_months_without_paying_the_lender_seizes_the_wallet_and_loses_the_rest_and_trust_is_gone()
    {
        var v = Make();
        var lender = Person(v.World, "credor", 50);
        lender.CreditWallet(new Money(10_000));
        var debtor = Person(v.World, "devedor", 30);
        var bond = v.World.GetOrCreateRelationship(new RelationshipKey(lender.Id, debtor.Id), 0);
        bond.ApplyEvent(RelationshipEventType.Help, v.World.FamilyRules);
        var loan = Lend(v, lender, debtor, 5_000, shop: null);
        debtor.TryDebitWallet(debtor.Wallet);
        long exempt = 40;

        for (int i = 0; i < v.Credit.DefaultAfterMissedMonths - 1; i++) new CreditSystem().Tick(v.World, v.Ctx);
        debtor.CreditWallet(new Money(exempt + 50)); // o devedor que juntou um pouco no último mês: não basta a parcela
        long lenderBefore = lender.Wallet.Amount;
        new CreditSystem().Tick(v.World, v.Ctx);

        Assert.DoesNotContain(loan, v.World.Credit!.Loans);
        Assert.Equal((0L, lenderBefore + exempt + 50), (debtor.Wallet.Amount, lender.Wallet.Amount));
        Assert.True(v.World.Credit.DefaultedSince(debtor.Id.Value, 0));
        Assert.Equal(0, bond.Trust);
        Assert.Single(v.Log.All, e => e.Kind == WorldEventKind.LoanDefaulted);
    }

    [Fact]
    public void Default_on_a_pledged_shop_closes_it_pays_the_lender_first_and_the_lender_takes_the_building()
    {
        var (v, founder, rich, _) = ShortFounder();
        var shop = Found(v, founder)!;
        var worker = Person(v.World, "oficial", 25);
        shop.Hire(worker.Id);
        worker.Hire(shop.Id);
        var loan = v.World.Credit!.Loans.Single();
        loan.Missed = v.Credit.DefaultAfterMissedMonths - 1;
        loan.FirstDueTick = 0; // AD-159: passada a carência
        shop.TryDebitTreasury(shop.Treasury);
        shop.CreditTreasury(new Money(10));
        founder.TryDebitWallet(founder.Wallet);
        long before = Coin(v.World);

        new CreditSystem().Tick(v.World, v.Ctx);

        Assert.True(shop.Closed && shop.PayrollFailed);
        Assert.Equal(rich.Id, shop.Owner);
        Assert.True(worker.Employer is null && founder.Employer is null);
        Assert.Single(v.Log.All, e => e.Kind == WorldEventKind.WorkplaceBankrupt);
        Assert.Equal(before, Coin(v.World));
        // o que o prédio não cobriu fica com o dono como dívida pessoal, sem garantia
        Assert.All(v.World.Credit!.Loans, l => Assert.Equal((founder.Id.Value, 0L), (l.Borrower, l.Workplace)));
    }

    [Fact]
    public void In_bankruptcy_unpaid_wages_come_before_the_lenders_and_partners_carry_their_share_of_the_debt()
    {
        var v = Make();
        var owner = Smith(v, Person(v.World, "dono", 35), 50_000);
        var partner = Person(v.World, "socio", 40);
        var shop = Found(v, owner)!;
        shop.SetPartners([new WorkplaceStake(partner.Id, WorkplaceStake.Whole / 4)]);
        var lender = Person(v.World, "credor", 50);
        lender.CreditWallet(new Money(100_000));
        var loan = Lend(v, lender, owner, 40_000, shop);
        owner.TryDebitWallet(owner.Wallet);
        var worker = Person(v.World, "oficial", 25);
        v.World.CreditBook.AddArrear(shop.Id.Value, worker.Id.Value, 300);
        shop.TryDebitTreasury(shop.Treasury);
        shop.CreditTreasury(new Money(500));

        Bankruptcy.Liquidate(v.World, v.Ctx, shop);

        Assert.Equal(300, worker.Wallet.Amount);
        Assert.Equal(lender.Id, shop.Owner);
        var debts = v.World.Credit!.Loans.Where(l => l.Lender == lender.Id.Value).ToList();
        long partnerDebt = debts.Single(l => l.Borrower == partner.Id.Value).Balance;
        long ownerDebt = debts.Single(l => l.Borrower == owner.Id.Value).Balance;
        Assert.InRange(partnerDebt, (partnerDebt + ownerDebt) / 4 - 1, (partnerDebt + ownerDebt) / 4 + 1);
    }

    [Fact]
    public void A_shop_of_an_owner_that_does_not_close_its_payroll_for_months_goes_bankrupt_and_the_city_does_not_cover_it()
    {
        var v = Make();
        var owner = Smith(v, Person(v.World, "dono", 35), 50_000);
        var shop = Found(v, owner)!;
        shop.SetPayrollFailed(true);

        for (int i = 0; i < v.Credit.BankruptcyMonths; i++) new CreditSystem().Tick(v.World, v.Ctx);
        var city = v.World.FindCity(shop.City)!;
        city.CreditTreasury(new Money(100_000));
        new OwnerDrawSystem().Tick(v.World, v.Ctx);
        new WagePaymentSystem().Tick(v.World, v.Ctx);

        Assert.True(shop.Closed && shop.PayrollFailed && shop.Treasury.Amount == 0 && shop.Employees.Count == 0);
        Assert.Equal(owner.Id, shop.Owner); // sem credor com garantia, o prédio segue dele
    }

    [Fact]
    public void An_owner_short_for_the_payroll_puts_in_his_own_savings_first()
    {
        var v = Make();
        var owner = Smith(v, Person(v.World, "dono", 35), 50_000);
        var shop = Found(v, owner)!;
        shop.TryDebitTreasury(shop.Treasury);
        long due = Payroll.Due(v.World, shop).Sum(d => d.Wage.Amount);
        long walletBefore = owner.Wallet.Amount;

        new WorkingCapitalSystem(ScenarioRunner.DefaultSkillsRules).Tick(v.World, v.Ctx);

        Assert.Equal((due, walletBefore - due), (shop.Treasury.Amount, owner.Wallet.Amount));
    }

    [Fact]
    public void Without_savings_or_credit_the_owner_lays_off_the_last_hired_but_never_himself()
    {
        var v = Make();
        var owner = Smith(v, Person(v.World, "dono", 35), 50_000);
        var shop = Found(v, owner)!;
        var first = Person(v.World, "primeiro", 25);
        var last = Person(v.World, "ultimo", 25);
        foreach (var w in new[] { first, last }) { shop.Hire(w.Id); w.Hire(shop.Id); }
        owner.TryDebitWallet(owner.Wallet);
        shop.TryDebitTreasury(shop.Treasury);
        shop.CreditTreasury(new Money(Payroll.Due(v.World, shop).Sum(d => d.Wage.Amount) - 1));

        new WorkingCapitalSystem(ScenarioRunner.DefaultSkillsRules).Tick(v.World, v.Ctx);

        Assert.Equal((shop.Id, shop.Id, (WorkplaceId?)null), (owner.Employer!.Value, first.Employer!.Value, last.Employer));
    }

    [Fact]
    public void A_household_without_food_or_coin_borrows_the_month_from_a_friend_who_trusts_the_earner()
    {
        var v = Make();
        var house = v.World.Households.OrderBy(h => h.Id.Value)
            .First(h => h.Members.Any(m => v.World.FindNpc(m)?.Employer is { } e && v.World.FindWorkplace(e)!.LocationType.Id == 1));
        foreach (var (resource, units) in house.Stock.ToList()) house.Withdraw(resource, units);
        var earner = house.Members.Select(m => v.World.FindNpc(m)!).First(m => m.Employer is not null);
        foreach (var m in house.Members.Select(id => v.World.FindNpc(id)!).Where(m => m != earner)) m.Fire(); // ele é quem ganha
        if (earner.Profession.Id != 1) earner.SwitchProfession(new ProfessionType(1)); // lavrador: tem salário
        foreach (var w in v.World.Workplaces) w.SetPrice(new LivingWorld.Domain.Geography.ResourceType(v.World.EconomyRules.FoodResourceId), 2);
        var friend = Person(v.World, "amigo", 50);
        friend.CreditWallet(new Money(100_000));
        var bond = v.World.GetOrCreateRelationship(new RelationshipKey(friend.Id, earner.Id), 0);
        for (int i = 0; i < 25; i++) bond.ApplyEvent(RelationshipEventType.Help, v.World.FamilyRules);
        v.World.GetOrCreateRelationship(new RelationshipKey(earner.Id, friend.Id), 0); // ele conhece o amigo (é a quem pede)

        new WorkingCapitalSystem(ScenarioRunner.DefaultSkillsRules).Tick(v.World, v.Ctx);

        var loan = Assert.Single(v.World.Credit!.Loans);
        Assert.Equal((LoanPurpose.Food, friend.Id.Value, earner.Id.Value), (loan.Purpose, loan.Lender, loan.Borrower));
        Assert.Equal(loan.Principal, earner.Wallet.Amount);
    }

    [Fact]
    public void A_stranger_does_not_lend_for_food_without_a_pledge()
    {
        var v = Make();
        var house = v.World.Households.OrderBy(h => h.Id.Value)
            .First(h => h.Members.Any(m => v.World.FindNpc(m)?.Employer is not null));
        foreach (var (resource, units) in house.Stock.ToList()) house.Withdraw(resource, units);
        Person(v.World, "rico", 50).CreditWallet(new Money(1_000_000));

        new WorkingCapitalSystem(ScenarioRunner.DefaultSkillsRules).Tick(v.World, v.Ctx);

        Assert.Null(v.World.Credit);
    }

    [Fact]
    public void The_estate_pays_the_debt_of_the_dead_before_the_heirs()
    {
        var v = Make();
        var lender = Person(v.World, "credor", 50);
        lender.CreditWallet(new Money(10_000));
        var father = Person(v.World, "pai", 60);
        var son = Person(v.World, "filho", 30, parent: father);
        var loan = Lend(v, lender, father, 1_000, shop: null);
        father.CreditWallet(new Money(5_000));
        long lenderBefore = lender.Wallet.Amount, wallet = father.Wallet.Amount, due = loan.Balance;

        father.Die(v.World.CurrentDate);
        EstateSystem.Settle(v.World, v.Ctx, father);

        Assert.Equal((lenderBefore + due, wallet - due, 0L), (lender.Wallet.Amount, son.Wallet.Amount, father.Wallet.Amount));
        Assert.DoesNotContain(loan, v.World.Credit!.Loans);
    }

    [Fact]
    public void The_claim_of_a_dead_lender_passes_to_his_heirs()
    {
        var v = Make();
        var lender = Person(v.World, "credor", 60);
        lender.CreditWallet(new Money(10_000));
        var son = Person(v.World, "filho", 30, parent: lender);
        var debtor = Person(v.World, "devedor", 30);
        var loan = Lend(v, lender, debtor, 1_000, shop: null);
        debtor.CreditWallet(new Money(5_000));
        lender.Die(v.World.CurrentDate);
        EstateSystem.Settle(v.World, v.Ctx, lender);
        long sonBefore = son.Wallet.Amount;

        new CreditSystem().Tick(v.World, v.Ctx);

        Assert.Equal((son.Id.Value, sonBefore + loan.Installment), (loan.Lender, son.Wallet.Amount));
    }

    [Fact]
    public void A_founder_buys_a_closed_shop_from_whoever_kept_it_instead_of_building_another()
    {
        var v = Make();
        var keeper = Person(v.World, "credor", 50);
        var old = Smith(v, Person(v.World, "velho", 40), 50_000);
        var shop = Found(v, old)!;
        Bankruptcy.Liquidate(v.World, v.Ctx, shop);
        shop.AssignOwner(keeper.Id);
        var founder = Smith(v, Person(v.World, "novo", 30), 50_000);
        int shops = v.World.Workplaces.Count;
        long keeperBefore = keeper.Wallet.Amount;

        var bought = VentureFounding.TryBuyClosed(v.World, v.Ctx, v.Rules, v.Trade, Plan(v, founder), founder);

        Assert.Equal(shop, bought);
        Assert.True(!shop.Closed && shop.Owner == founder.Id && founder.Employer == shop.Id && v.World.Workplaces.Count == shops);
        Assert.Equal(keeperBefore + Debts.ShopValue(v.World, shop), keeper.Wallet.Amount);
    }

    [Fact]
    public void The_credit_book_survives_a_snapshot_round_trip_and_an_empty_one_leaves_the_hash_as_before()
    {
        var v = Make();
        string empty = WorldSnapshot.CanonicalHash(v.World);
        var (vl, founder, _, _) = ShortFounder();
        Found(vl, founder);
        vl.World.CreditBook.AddArrear(1, 2, 3);

        var restored = WorldSnapshot.Deserialize(WorldSnapshot.Serialize(vl.World));

        Assert.Null(v.World.Credit);
        Assert.Equal(empty, WorldSnapshot.CanonicalHash(v.World));
        Assert.Equal(WorldSnapshot.CanonicalHash(vl.World), WorldSnapshot.CanonicalHash(restored));
        Assert.Equal(vl.World.Credit!.Loans.Single().Balance, restored.Credit!.Loans.Single().Balance);
    }

    [Fact]
    public void The_same_seed_lends_the_same_way()
    {
        string Run()
        {
            var (v, founder, _, _) = ShortFounder();
            Found(v, founder);
            for (int i = 0; i < 3; i++) new CreditSystem().Tick(v.World, v.Ctx);
            return string.Join(';', v.Log.All.Select(e => $"{e.Kind}:{e.Payload}"));
        }

        Assert.Equal(Run(), Run());
    }
}
