using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Labor;
using LivingWorld.Simulation.Population.Family;
using LivingWorld.Simulation.Population.Lifecycle;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;
using LivingWorld.Simulation.Snapshot;

namespace LivingWorld.Tests.Unit.Realism;

/// <summary>AD-128: vínculos por convivência real, fome que leva semanas, dinheiro da casa, esmola da cidade, vaga de quem
/// morreu e emprego que não paga. Cada teste falha no comportamento antigo.</summary>
public class SocialAndSurvivalRealismTests
{
    private const long Year = 360L * 24;
    private static readonly FamilyRules Rules = ScenarioRunner.DefaultFamilyRules;
    private static readonly SocialBondRules Bonds = SocialBondRules.Default;

    private static Personality Flat(int value) => Personality.Create(value, value, value, value, value, value, value, value, value, value).Value!;

    // --- Vínculo ---

    [Fact]
    public void Living_together_approaches_the_pair_ceiling_with_shrinking_gains_and_never_passes_it()
    {
        var relationship = Relationship.Initial(0);
        double previous = 0, previousGain = double.MaxValue;
        for (int day = 0; day < 2000; day++)
        {
            relationship.ApplyBondingEvent(RelationshipEventType.Cohabitation, Rules, trustCeiling: 80, affectionCeiling: 55);
            double gain = relationship.Affection - previous;
            Assert.True(gain <= previousGain + 1e-12, $"o ganho cresceu no dia {day}");
            previous = relationship.Affection;
            previousGain = gain;
        }

        Assert.InRange(relationship.Affection, 54.0, 55.0);
        Assert.InRange(relationship.Trust, 79.0, 80.0);
    }

    [Fact]
    public void Who_is_alike_and_agreeable_is_liked_more_and_the_feeling_is_not_symmetric()
    {
        var calm = Flat(50);
        var twin = Flat(52);
        var opposite = Personality.Create(100, 0, 100, 0, 100, 0, 100, 0, 100, 0).Value!;
        var pleasant = calm with { Agreeableness = 100 };

        Assert.True(SocialBondRules.Likability(calm, twin) > SocialBondRules.Likability(calm, opposite));
        Assert.NotEqual(SocialBondRules.Likability(calm, pleasant), SocialBondRules.Likability(pleasant, calm));
        Assert.True(Bonds.AffectionCeiling(calm, opposite, sameHousehold: true) >= Bonds.HouseholdCeilingFloor);
        Assert.True(Bonds.AffectionCeiling(calm, opposite, sameHousehold: false) < Bonds.HouseholdCeilingFloor);
    }

    [Fact]
    public void Without_contact_a_bond_cools_by_a_fraction_and_kin_never_cool_below_their_floor()
    {
        var friend = Relationship.FromAxes(80, 80, 0, 0, 0);
        var kin = Relationship.FromAxes(80, 80, 0, 0, 0);
        for (int week = 0; week < 400; week++)
        {
            friend.Fade(Rules, 7, kinFloor: 0);
            kin.Fade(Rules, 7, kinFloor: Bonds.KinBondFloor);
        }

        Assert.True(friend.IsFaded(Rules.NeutralAxisValue));
        Assert.Equal(Bonds.KinBondFloor, kin.Affection);
        Assert.False(kin.IsFaded(Rules.NeutralAxisValue));

        var halfYear = Relationship.FromAxes(80, 80, 0, 0, 0);
        for (int week = 0; week < 26; week++) halfYear.Fade(Rules, 7, kinFloor: 0);
        Assert.InRange(halfYear.Affection, 45.0, 55.0); // meio ano sem se ver: a amizade firme cai a pouco mais da metade
    }

    [Fact]
    public void After_a_year_people_have_a_few_strong_ties_and_many_weak_ones_instead_of_loving_everyone()
    {
        var (world, clock) = ScenarioRunner.Create(3);
        clock.Run(world, Year);

        var alive = world.Npcs.Where(n => n.IsAlive).Select(n => n.Id).ToHashSet();
        var mine = world.Relationships.Where(kv => alive.Contains(kv.Key.From)).ToList();
        double perPerson = (double)mine.Count / alive.Count;
        double strong = (double)mine.Count(kv => kv.Value.Affection >= 60) / alive.Count;
        double weak = (double)mine.Count(kv => kv.Value.Affection < 20) / mine.Count;

        Assert.InRange(perPerson, 5, alive.Count * 0.5); // antes: quase todo mundo conhecia quase todo mundo
        Assert.InRange(strong, 1, 20);
        Assert.True(weak > 0.3, $"só {weak:P0} dos vínculos são fracos");
        Assert.DoesNotContain(mine, kv => kv.Value.Affection > 99.5 && kv.Value.Trust > 99.5);
    }

    [Fact]
    public void A_faded_bond_between_people_who_are_not_kin_is_forgotten()
    {
        var (world, clock) = ScenarioRunner.Create(3);
        clock.Run(world, 2 * Year);

        long now = world.CurrentDate.TotalHours;
        long stale = (Rules.ContactLossThresholdDays + 2L * Bonds.FadeEveryDays) * 24;
        Assert.All(world.Relationships, kv =>
        {
            if (now - kv.Value.LastContactTick <= stale || !kv.Value.IsFaded(Rules.NeutralAxisValue)) return;
            Assert.True(RelationshipSystem.IsKin(world.FindNpc(kv.Key.From), world.FindNpc(kv.Key.To)), $"{kv.Key} esfriou e ficou");
        });
    }

    [Fact]
    public void A_pair_forgotten_and_met_again_hashes_the_same_whether_the_world_kept_running_or_was_reopened()
    {
        var (world, clock) = ScenarioRunner.Create(3);
        clock.Run(world, 60 * 24);
        var key = world.Relationships.Keys.First();
        Assert.True(world.RemoveRelationship(key));
        world.GetOrCreateRelationship(key, world.CurrentDate.TotalHours);

        Assert.Equal(key, world.Relationships.Keys.Last()); // quem volta vai para o fim, não para a vaga antiga
        var reopened = PositionalSnapshot.DecodeFull(PositionalSnapshot.EncodeFull(world, out _));
        Assert.Equal(WorldSnapshot.CanonicalHash(world), WorldSnapshot.CanonicalHash(reopened));
        Assert.Equal(world.Relationships.Keys, reopened.Relationships.Keys);
    }

    // --- Fome, casa e esmola ---

    [Fact]
    public void Starving_takes_the_weeks_the_scenario_declares_and_the_old_rule_when_it_declares_none()
    {
        var legacy = NeedsRules.Create(2.0, 3.0, 1.5, 1.0, 70, 10, true, 5.0, 0.5).Value!;
        Assert.Equal(50, legacy.StarvationTicks);
        Assert.Equal(21 * 24, ScenarioRunner.DefaultNeedsRules.StarvationTicks);
    }

    private static (WorldState World, Household Home, Npc Grown, Npc? Child) DestituteHome(bool needChild)
    {
        var (world, clock) = ScenarioRunner.Create(3);
        clock.Run(world, 24);
        bool IsChild(Npc n) => world.LifeStageRules.LifeStageOf(n.AgeYears(world.CurrentDate)) == LifeStage.Child;
        var home = world.Households.OrderBy(h => h.Id.Value).First(h =>
        {
            var members = h.Members.Select(id => world.FindNpc(id)!).Where(n => n.IsAlive).ToList();
            return members.Any(n => !IsChild(n)) && (!needChild || members.Any(IsChild));
        });

        foreach (var npc in world.Npcs.Where(n => n.Wallet.Amount > 0)) npc.TryDebitWallet(npc.Wallet);
        foreach (var resource in home.Stock.Keys.ToList()) home.Withdraw(resource, home.Stock[resource]);
        var members = home.Members.Select(id => world.FindNpc(id)!).Where(n => n.IsAlive).OrderBy(n => n.Id.Value).ToList();
        return (world, home, members.First(n => !IsChild(n)), members.FirstOrDefault(IsChild));
    }

    private static long HomeWallets(WorldState world, Household home) => home.Members.Sum(id => world.FindNpc(id)!.Wallet.Amount);

    [Fact]
    public void With_an_empty_pantry_the_coins_of_the_child_go_to_whoever_does_the_shopping()
    {
        var (world, home, _, child) = DestituteHome(needChild: true);
        foreach (var city in world.Cities) city.TryDebitTreasury(city.Treasury);
        child!.CreditWallet(new Money(500));

        new WorldClock([new MutualAidSystem()]).Run(world, 24);

        Assert.InRange(child.Wallet.Amount, 0, 499); // passa o que cobre a semana da casa, não mais
        Assert.Equal(500, HomeWallets(world, home)); // só mudou de mão dentro da casa
        Assert.Contains(home.Members, id => world.FindNpc(id)!.Wallet.Amount > 0
            && world.LifeStageRules.LifeStageOf(world.FindNpc(id)!.AgeYears(world.CurrentDate)) != LifeStage.Child);
    }

    [Fact]
    public void A_home_with_nothing_gets_alms_from_the_city_and_no_coin_is_created()
    {
        var (world, home, _, _) = DestituteHome(needChild: false);
        long treasuryBefore = world.Cities.Sum(c => c.Treasury.Amount);
        Assert.True(treasuryBefore > 0);

        new WorldClock([new MutualAidSystem()]).Run(world, 24);

        long given = HomeWallets(world, home);
        Assert.True(given > 0, "a casa sem nada não recebeu esmola");
        Assert.Equal(treasuryBefore, world.Cities.Sum(c => c.Treasury.Amount) + world.Npcs.Sum(n => n.Wallet.Amount));
    }

    [Fact]
    public void With_empty_treasuries_nobody_gives_alms()
    {
        var (world, home, _, _) = DestituteHome(needChild: false);
        foreach (var city in world.Cities) city.TryDebitTreasury(city.Treasury);

        new WorldClock([new MutualAidSystem()]).Run(world, 24);

        Assert.Equal(0, HomeWallets(world, home));
    }

    // --- Trabalho ---

    [Fact]
    public void Who_dies_leaves_the_job_so_the_vacancy_opens()
    {
        var (world, clock) = ScenarioRunner.Create(3);
        clock.Run(world, 30 * 24);
        var worker = world.Npcs.First(n => n.IsAlive && n.Employer is not null);
        var workplace = world.FindWorkplace(worker.Employer!.Value)!;

        NpcDeath.Apply(world, new TickContext(world, world.Rng, world.Scheduler), worker, WorldEventKind.Starvation);
        new WorldClock([new EmploymentSystem()]).Run(world, 24);

        Assert.DoesNotContain(worker.Id, workplace.Employees);
        Assert.All(world.Workplaces.SelectMany(w => w.Employees), id => Assert.True(world.FindNpc(id)!.IsAlive));
    }

    [Fact]
    public void Workers_leave_an_employer_that_cannot_pay_and_nobody_is_hired_by_it()
    {
        var (world, clock) = ScenarioRunner.Create(3);
        clock.Run(world, 30 * 24);
        var broke = world.Workplaces.Where(w => w.Employees.Count > 0).OrderBy(w => w.Employees.Count).First(); // a menor: há vaga na outra
        broke.TryDebitTreasury(broke.Treasury);
        var someone = world.FindNpc(broke.Employees[0])!;
        Assert.True(Payroll.CanPay(world, broke, someone.Profession)); // ainda não falhou folha nenhuma

        clock.Run(world, 31 * 24); // a folha do mês não fecha
        Assert.True(broke.PayrollFailed);
        Assert.False(Payroll.CanPay(world, broke, someone.Profession));

        clock.Run(world, 45 * 24); // passa a carência de troca de emprego
        Assert.True(!broke.PayrollFailed || broke.Employees.Count == 0, $"{broke.Employees.Count} continuam na oficina que não paga");
    }

    // --- Livros ---

    [Fact]
    public void Ledgers_keep_ten_years_of_months_and_drop_what_is_older()
    {
        var (world, _) = ScenarioRunner.Create(3);
        var city = world.Cities[0].Id;
        long month = world.Calendar.HoursPerMonth;
        world.RecordTreasury(city, 0, revenue: 10, spending: 0);
        world.RecordTreasury(city, 5 * 12 * month, revenue: 20, spending: 0);

        world.CompactTreasuryLedger(today: 9L * 360);
        Assert.Equal(2, world.TreasuryLedger.Count(e => e.City == city));

        world.CompactTreasuryLedger(today: 12L * 360);
        var kept = Assert.Single(world.TreasuryLedger, e => e.City == city);
        Assert.Equal(20, kept.Revenue);
    }
}
