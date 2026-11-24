using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Cognition;
using LivingWorld.Domain.Crime;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Crime;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;
using static LivingWorld.Tests.Unit.Crime.CrimeTestWorld;

namespace LivingWorld.Tests.Unit.Crime;

/// <summary>Crime contra a pessoa (AD-154): violência doméstica, sequestro e violência sexual saem do estado (temperamento,
/// estresse, mágoa, pobreza, oportunidade, dissuasão) e deixam consequências (ferimento, medo, mágoa conjugal, cativeiro com
/// comida do sequestrador, resgate em moeda conservada, trauma, gravidez pelas regras de concepção, família que sabe) e um
/// caso para a justiça comum, em que a vítima sabe quem foi. Antes do AD-154 nada disso existia.</summary>
public class PersonCrimeTests
{
    private static readonly Personality Hot = Personality.Create(50, 10, 10, 10, 50, 50, 50, 10, 90, 20).Value!;
    private static readonly Personality Kind = Personality.Create(50, 90, 90, 90, 50, 50, 50, 90, 10, 80).Value!;
    private static readonly CellCoord Away = new(6, 6);

    private static CrimeRules Rules(JusticeRules? justice = null) => CrimeRules.Default with
    {
        Justice = justice ?? new JusticeRules { WitnessSeeChance = 0, SleepingSeeChance = 0 },
    };

    private static Npc ChildOf(WorldState world, Npc mother, Npc father, int age, CellCoord at, HouseholdId home)
    {
        var kid = new Npc(world.NextNpcIdAndAdvance(), "c", Sex.Female, WorldDate.Epoch(Calendar).AddYears(-age), new CultureId(1),
            at, mother.Id, father.Id, household: home, health: 100, personality: Average, profession: new ProfessionType(1), currentLocation: at);
        world.AddNpc(kid);
        world.FindHousehold(home)!.AddMember(kid.Id);
        return kid;
    }

    private static void Wed(Npc a, Npc b) { a.Marry(b.Id); b.Marry(a.Id); a.SetUnion(new UnionState()); b.SetUnion(new UnionState()); }

    [Fact]
    public void Domestic_violence_grows_with_temper_and_household_stress_and_not_from_a_loose_draw()
    {
        var world = New(crime: Rules());
        var rules = world.CrimeRules!;
        var hot = Add(world, Hot);
        var wife = Add(world, Average, sex: Sex.Female);
        var calm = Add(world, Kind);
        var calmWife = Add(world, Average, sex: Sex.Female);
        var hotHome = Home(world, Here, hot, wife);
        var calmHome = Home(world, Away, calm, calmWife);
        calmHome.Deposit(new ResourceType(world.EconomyRules.FoodResourceId), 500);
        calmHome.Deposit(new ResourceType(world.EconomyRules.WaterResourceId), 500);
        calm.CreditWallet(new Money(1000)); // mesma casa folgada que a do esquentado depois: só o temperamento difere
        long now = world.CurrentDate.TotalHours;
        wife.SetHunger(0, now); // a casa passa fome e não tem como comprar

        double stressed = DomesticViolence.ChancePerYear(world, rules.Domestic!, rules.Violence, hot, now, 0, out string motive);
        wife.SetHunger(100, now);
        hotHome.Deposit(new ResourceType(world.EconomyRules.FoodResourceId), 500);
        hotHome.Deposit(new ResourceType(world.EconomyRules.WaterResourceId), 500);
        hot.CreditWallet(new Money(1000));
        double relaxed = DomesticViolence.ChancePerYear(world, rules.Domestic!, rules.Violence, hot, now, 0, out _);
        double gentle = DomesticViolence.ChancePerYear(world, rules.Domestic!, rules.Violence, calm, now, 0, out _);

        Assert.True(stressed > 2 * relaxed, $"o estresse da casa não pesa: {stressed} x {relaxed}");
        Assert.True(relaxed > 4 * gentle, $"o temperamento não pesa: {relaxed} x {gentle}");
        Assert.NotEqual("temperamento", motive);
        Assert.Equal(0, DomesticViolence.ChancePerYear(world, rules.Domestic!, rules.Violence, hot, now, 1.0, out _));
    }

    [Fact]
    public void A_beaten_spouse_is_hurt_afraid_and_closer_to_leaving_and_the_child_at_home_remembers()
    {
        var world = New(crime: Rules());
        var husband = Add(world, Hot);
        var wife = Add(world, Average, sex: Sex.Female);
        Wed(husband, wife);
        var home = Home(world, Here, husband, wife);
        var kid = ChildOf(world, wife, husband, 6, Here, home.Id);
        var sink = new Sink();

        DomesticViolence.Assault(world, Ctx(world, sink), husband, wife, "ciume", world.Rng.OneShot("d"));

        Assert.True(wife.Health < 100 && wife.IsAlive);
        Assert.True(wife.Union!.Grievance > 0);
        Assert.Contains(wife.Psyche!.Memories, m => m.Kind == LifeEventKind.Violence && m.Other == husband.Id.Value);
        Assert.Contains(kid.Psyche!.Memories, m => m.Kind == LifeEventKind.Violence && m.Other == husband.Id.Value);
        var parts = Assert.Single(sink.Of(WorldEventKind.DomesticViolence)).Payload.Split('|');
        Assert.Equal([husband.Id.Value.ToString(), wife.Id.Value.ToString(), "conjuge", "ciume"], parts[..4]);
        var crime = Assert.Single(world.Crime!.Cases);
        Assert.Equal((CrimeKind.DomesticAssault, husband.Id.Value), (crime.Kind, crime.Accused));
        Assert.Contains(crime.Knowers, k => k.Npc == wife.Id.Value);
    }

    /// <summary>AD-162: casal com o marido esquentado; <paramref name="severeHarm"/> é o dano a partir do qual a agressão é grave.</summary>
    private static (WorldState World, Npc Husband, Npc Wife, TickContext Ctx) BeatenWife(int severeHarm)
    {
        var world = New(crime: Rules() with { Domestic = CrimeRules.Default.Domestic! with { SevereHarm = severeHarm } });
        var husband = Add(world, Hot);
        var wife = Add(world, Average, sex: Sex.Female);
        Wed(husband, wife);
        Home(world, Here, husband, wife);
        return (world, husband, wife, Ctx(world, new Sink()));
    }

    [Fact]
    public void A_first_moderate_blow_adds_only_the_tolerated_share_of_the_marital_grievance()
    {
        var (world, husband, wife, ctx) = BeatenWife(severeHarm: int.MaxValue);
        var rules = world.CrimeRules!.Domestic!;

        DomesticViolence.Assault(world, ctx, husband, wife, "ciume", world.Rng.OneShot("d1"));

        Assert.Equal(rules.SpouseGrievance * rules.ToleratedGrievanceShare, wife.Union!.Grievance, 6);
    }

    [Fact]
    public void A_repeated_blow_weighs_in_full_on_the_marital_grievance()
    {
        var (world, husband, wife, ctx) = BeatenWife(severeHarm: int.MaxValue);
        var rules = world.CrimeRules!.Domestic!;
        DomesticViolence.Assault(world, ctx, husband, wife, "ciume", world.Rng.OneShot("d1"));
        double first = wife.Union!.Grievance;

        DomesticViolence.Assault(world, ctx, husband, wife, "ciume", world.Rng.OneShot("d2"));

        Assert.Equal(first + rules.SpouseGrievance, wife.Union!.Grievance, 6);
    }

    [Fact]
    public void A_severe_blow_weighs_in_full_on_the_marital_grievance_at_once()
    {
        var (world, husband, wife, ctx) = BeatenWife(severeHarm: 1);

        DomesticViolence.Assault(world, ctx, husband, wife, "ciume", world.Rng.OneShot("d1"));

        Assert.Equal(world.CrimeRules!.Domestic!.SpouseGrievance, wife.Union!.Grievance, 6);
    }

    [Fact]
    public void The_beaten_wife_can_report_her_husband_where_before_kin_always_kept_silent()
    {
        var world = New(crime: Rules(new JusticeRules { WitnessSeeChance = 0, SleepingSeeChance = 0, ReportChance = 1, FearSilence = 0 })
            with
        { Domestic = new DomesticViolenceRules { ReportFactor = 1 } });
        var husband = Add(world, Hot);
        var wife = Add(world, Average, sex: Sex.Female);
        Wed(husband, wife);
        Home(world, Here, husband, wife);
        var sink = new Sink();
        DomesticViolence.Assault(world, Ctx(world, sink), husband, wife, "temperamento", world.Rng.OneShot("d"));

        Justice.Daily(world, Ctx(world, sink), 1);

        var report = Assert.Single(sink.Of(WorldEventKind.CrimeReported)).Payload.Split('|');
        Assert.Equal([wife.Id.Value.ToString(), husband.Id.Value.ToString()], report[..2]);
        Assert.Equal("violencia_domestica", report[3]);
    }

    [Fact]
    public void A_kidnapping_needs_a_motive_in_the_world_and_vengeance_takes_the_enemy_s_small_child()
    {
        var world = New(crime: Rules());
        var rules = world.CrimeRules!;
        var avenger = Add(world, Hot, at: Away);
        Home(world, Away, avenger);
        var enemy = Add(world, Average);
        var enemyWife = Add(world, Average, sex: Sex.Female);
        var home = Home(world, Here, enemy, enemyWife);
        var kid = ChildOf(world, enemyWife, enemy, 5, Here, home.Id);
        long now = world.CurrentDate.TotalHours;

        Assert.Equal(0, Kidnapping.ChancePerYear(world, rules.Kidnapping!, rules.Violence, avenger, now, 0, out _, out var none, out _));
        Assert.Null(none);

        Violence.AddGrudge(world, avenger.Id.Value, enemy.Id.Value, "vinganca", 90, now);
        double chance = Kidnapping.ChancePerYear(world, rules.Kidnapping!, rules.Violence, avenger, now, 0, out string motive, out var victim, out _);
        Assert.True(chance > 0);
        Assert.Equal(("vinganca", kid.Id), (motive, victim!.Id));
    }

    [Fact]
    public void A_ransom_kidnapping_holds_the_child_at_the_captor_s_house_fed_from_his_pantry_and_the_paid_ransom_moves_coin_without_creating_it()
    {
        var world = New(crime: Rules() with { Kidnapping = new KidnappingRules { RansomPayPerDay = 1, EscapePerDay = 0, SearchFindPerDay = 0 } });
        var rules = world.CrimeRules!;
        var captor = Add(world, Hot, at: Away);
        var jail = Home(world, Away, captor);
        var father = Add(world, Average, wallet: 200);
        var mother = Add(world, Average, wallet: 100, sex: Sex.Female);
        var home = Home(world, Here, father, mother);
        var kid = ChildOf(world, mother, father, 8, Here, home.Id);
        world.GetOrCreateRelationship(new RelationshipKey(captor.Id, father.Id), 0);
        long coin = Coin(world);
        long now = world.CurrentDate.TotalHours;

        Kidnapping.ChancePerYear(world, rules.Kidnapping!, rules.Violence, captor, now, 0, out string motive, out var victim, out long ransom);
        Assert.Equal(("resgate", kid.Id, 150L), (motive, victim!.Id, ransom));
        jail.Deposit(new ResourceType(world.EconomyRules.FoodResourceId), 10); // depois do plano: o que dar ao preso
        jail.Deposit(new ResourceType(world.EconomyRules.WaterResourceId), 10);

        var sink = new Sink();
        Kidnapping.Abduct(world, Ctx(world, sink), captor, kid, motive, ransom, world.Rng.OneShot("k"));
        Assert.Equal(Away, kid.CurrentLocation);
        Assert.NotNull(world.Crime!.CaptivityOf(kid.Id.Value));
        kid.SetHunger(0, now);
        kid.SetThirst(0, now);
        Assert.True(Kidnapping.Hold(world, kid, now));
        Assert.True(kid.HungerAt(now) > 50 && kid.ThirstAt(now) > 50, "o preso não comeu do que o sequestrador tem");
        Assert.Equal(9, jail.Stock[new ResourceType(world.EconomyRules.FoodResourceId)]);

        var later = new TickContext(world, world.Rng, world.Scheduler, sink);
        world.CurrentDate = world.CurrentDate.AddHours(3 * Calendar.HoursPerDay);
        Kidnapping.Daily(world, later, 3);

        Assert.Equal("resgate", Assert.Single(sink.Of(WorldEventKind.KidnappingEnded)).Payload.Split('|')[2]);
        Assert.Equal(150, captor.Wallet.Amount);
        Assert.Equal(coin, Coin(world));
        Assert.Null(world.Crime.Captives);
        Assert.Equal(Here, kid.CurrentLocation);
        Assert.Contains(kid.Psyche!.Memories, m => m.Kind == LifeEventKind.Kidnapped);
        Assert.Contains(world.Crime.Cases, c => c.Kind == CrimeKind.Kidnapping && c.Accused == captor.Id.Value);
    }

    [Fact]
    public void A_captive_whose_captor_has_no_food_is_not_fed_and_dies_of_the_hunger_like_anyone()
    {
        var world = New(crime: Rules());
        var captor = Add(world, Hot, at: Away);
        Home(world, Away, captor);
        var victim = Add(world, Average);
        Home(world, Here, victim);
        Kidnapping.Abduct(world, Ctx(world), captor, victim, "posse", 0, world.Rng.OneShot("k"));
        long now = world.CurrentDate.TotalHours;
        victim.SetHunger(0, now);

        Assert.True(Kidnapping.Hold(world, victim, now));
        Assert.Equal(0, victim.HungerAt(now));
        Assert.Equal(ActionType.Sleep, victim.CurrentAction);
    }

    [Fact]
    public void Sexual_violence_comes_from_callous_temper_and_impunity()
    {
        var world = New(crime: Rules());
        var rules = world.CrimeRules!;
        var hot = Add(world, Hot);
        var kind = Add(world, Kind);
        long now = world.CurrentDate.TotalHours;

        double callous = SexualViolence.ChancePerYear(world, rules.SexualViolence!, rules.Violence, hot, now, 0);
        Assert.True(callous > 10 * SexualViolence.ChancePerYear(world, rules.SexualViolence!, rules.Violence, kind, now, 0));
        Assert.Equal(0, SexualViolence.ChancePerYear(world, rules.SexualViolence!, rules.Violence, hot, now, 1.0));
    }

    [Fact]
    public void Someone_awake_nearby_takes_away_the_opportunity()
    {
        var world = New(crime: Rules());
        var offender = Add(world, Hot);
        var victim = Add(world, Average, sex: Sex.Female);
        Assert.False(SexualViolence.Watched(world, offender, victim));
        Add(world, Average);
        Assert.True(SexualViolence.Watched(world, offender, victim));
    }

    [Fact]
    public void The_victim_of_sexual_violence_keeps_trauma_and_the_record_holds_only_who_whom_and_the_case()
    {
        var world = New(crime: Rules() with { SexualViolence = new SexualViolenceRules { DisclosureChance = 1, SingleActFecundability = 0 } });
        var offender = Add(world, Hot);
        var mother = Add(world, Average, sex: Sex.Female, age: 45);
        var victim = new Npc(world.NextNpcIdAndAdvance(), "v", Sex.Female, WorldDate.Epoch(Calendar).AddYears(-22), new CultureId(1),
            Here, mother.Id, null, household: null, health: 100, personality: Average, profession: new ProfessionType(1), currentLocation: Here);
        world.AddNpc(victim);
        var sink = new Sink();

        SexualViolence.Commit(world, Ctx(world, sink), offender, victim, world.Rng.OneShot("s"));

        Assert.Contains(victim.Psyche!.Memories, m => m.Kind == LifeEventKind.SexualAssault && m.Other == offender.Id.Value);
        Assert.Contains(mother.Psyche!.Memories, m => m.Kind == LifeEventKind.KinAssaulted);
        Assert.Equal("vinganca", world.Crime!.GrudgeOf(mother.Id.Value, offender.Id.Value)!.Motive);
        var parts = Assert.Single(sink.Of(WorldEventKind.SexualAssault)).Payload.Split('|');
        Assert.Equal(6, parts.Length);
        Assert.Equal([offender.Id.Value.ToString(), victim.Id.Value.ToString(), "adulto"], parts[..3]);
        var crime = Assert.Single(world.Crime.Cases);
        Assert.Equal((CrimeKind.SexualAssault, offender.Id.Value), (crime.Kind, crime.Accused));
        Assert.Equal(0, Justice.InfamyOf(world, offender.Id.Value, world.CurrentDate.TotalHours).All); // ninguém de fora sabe ainda
    }

    [Fact]
    public void An_adult_fertile_victim_can_conceive_by_the_rules_of_conception_and_a_child_never()
    {
        var world = New(crime: Rules() with { SexualViolence = new SexualViolenceRules { DisclosureChance = 0, SingleActFecundability = 1000 } });
        var offender = Add(world, Hot);
        var woman = Add(world, Average, sex: Sex.Female, age: 24);
        Home(world, Here, woman);
        var sink = new Sink();

        SexualViolence.Commit(world, Ctx(world, sink), offender, woman, world.Rng.OneShot("s"));
        Assert.Equal(offender.Id.Value, woman.Reproduction!.Pregnancy!.FatherId);
        Assert.EndsWith("|1", Assert.Single(sink.Of(WorldEventKind.SexualAssault)).Payload);

        var girl = Add(world, Average, sex: Sex.Female, age: 9);
        SexualViolence.Commit(world, Ctx(world, sink), offender, girl, world.Rng.OneShot("s2"));
        Assert.Null(girl.PregnantUntil);
        var last = sink.Of(WorldEventKind.SexualAssault).Last().Payload.Split('|');
        Assert.Equal(("crianca", "0"), (last[2], last[5]));
    }

    [Fact]
    public void The_captivity_survives_a_snapshot_and_an_empty_one_leaves_no_trace_in_it()
    {
        var world = New(crime: Rules());
        Assert.DoesNotContain("Captives", WorldSnapshot.Serialize(world));
        var captor = Add(world, Hot, at: Away);
        Home(world, Away, captor);
        var victim = Add(world, Average);
        Home(world, Here, victim);
        Kidnapping.Abduct(world, Ctx(world), captor, victim, "posse", 0, world.Rng.OneShot("k"));

        var restored = WorldSnapshot.Deserialize(WorldSnapshot.Serialize(world));
        Assert.Equal(WorldSnapshot.CanonicalHash(world), WorldSnapshot.CanonicalHash(restored));
        Assert.Equal(captor.Id.Value, restored.Crime!.CaptivityOf(victim.Id.Value)!.Offender);
    }

    [Fact]
    [Trait("Category", "Scenario")]
    public void With_the_three_keys_off_the_village_lives_exactly_the_history_it_had_before()
    {
        static List<string> Run(CrimeRules rules)
        {
            var world = ScenarioRunner.Create(3, crimeRules: rules).World;
            var sink = new Sink();
            new WorldClock(ScenarioRunner.DefaultSystems(), sink: sink).Run(world, 120 * 24);
            return sink.Events.Select(e => $"{e.Tick}|{e.Kind}|{e.Payload}").ToList();
        }

        var off = CrimeRules.Default with
        {
            Domestic = new DomesticViolenceRules { Enabled = false },
            Kidnapping = new KidnappingRules { Enabled = false },
            SexualViolence = new SexualViolenceRules { Enabled = false },
        };
        var before = CrimeRules.Default with { Domestic = null, Kidnapping = null, SexualViolence = null };
        Assert.Equal(Run(before), Run(off));
    }
}
