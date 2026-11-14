using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Cognition;
using LivingWorld.Domain.Crime;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Decision;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Crime;
using static LivingWorld.Tests.Unit.Crime.CrimeTestWorld;

namespace LivingWorld.Tests.Unit.Crime;

/// <summary>Justiça conforme a época (AD-150; achado 26): quem viu aponta (às vezes errado); sem ninguém, impunidade; o boato
/// corre de pessoa a pessoa e distorce; a vítima denuncia ou cala (parente); a corte condena pela prova e pela fama, multa com
/// restituição, castiga quem não paga, enforca ou bane o homicida; a punição dissuade quem pensa em furtar. Antes do AD-150
/// o furto descoberto só tirava confiança, e ninguém era julgado.</summary>
public class JusticeTests
{
    private static CrimeRules Rules(JusticeRules? justice = null) => CrimeRules.Default with
    {
        Violence = new ViolenceRules { Enabled = false },
        Justice = justice ?? new JusticeRules(),
    };

    private static TheftRules Sure => TheftRules.Default with { BaseSuccess = 1.0 };

    private static void Steal(WorldState world, Npc thief, Sink sink, string key = "t")
    {
        var target = Theft.FindTarget(world, thief, TheftMotive.Poverty)!.Value;
        Theft.Commit(world, Ctx(world, sink), thief, TheftMotive.Poverty, target, world.Rng.OneShot(key));
    }

    [Fact]
    public void Nobody_awake_sees_a_theft_and_it_goes_unpunished()
    {
        var world = New(Sure, Rules(new JusticeRules { SleepingSeeChance = 0 }));
        var thief = Add(world, Criminal);
        var victim = Add(world, Honest, wallet: 100);
        victim.SetCurrentAction(ActionType.Sleep, 0);
        var sink = new Sink();

        Steal(world, thief, sink);

        Assert.Equal(50, thief.Wallet.Amount);
        Assert.Empty(world.Crime!.Cases);
        Assert.EndsWith("|levou|0|-1", Assert.Single(sink.Of(WorldEventKind.TheftCommitted)).Payload);
        Assert.Empty(sink.Of(WorldEventKind.TheftDiscovered));
    }

    [Fact]
    public void A_witness_with_a_bad_memory_can_point_at_the_wrong_person()
    {
        var right = New(Sure, Rules(new JusticeRules { WitnessSeeChance = 1, MisidentifyChance = 0 }));
        var thief = Add(right, Criminal);
        Add(right, Honest, wallet: 100);
        Steal(right, thief, new Sink());
        Assert.Equal(thief.Id.Value, Assert.Single(right.Crime!.Cases).Accused);

        var wrong = New(Sure, Rules(new JusticeRules { WitnessSeeChance = 1, MisidentifyChance = 1 }));
        var thief2 = Add(wrong, Criminal);
        Add(wrong, Honest, wallet: 100);
        var first = Add(wrong, Honest);
        Add(wrong, Honest);
        Steal(wrong, thief2, new Sink());
        var crime = Assert.Single(wrong.Crime!.Cases);
        Assert.Equal(first.Id.Value, crime.Accused); // a vítima e o outro passante apontam o primeiro passante
    }

    [Fact]
    public void The_rumour_spreads_person_to_person_and_raises_the_accused_s_infamy()
    {
        var world = New(Sure, Rules(new JusticeRules { WitnessSeeChance = 1, MisidentifyChance = 0, RumorTellsPerDay = 1, ReportChance = 0 }));
        var thief = Add(world, Criminal);
        var victim = Add(world, Honest, wallet: 100);
        var friends = Enumerable.Range(0, 6).Select(_ => Add(world, Honest, at: new CellCoord(8, 8))).ToList();
        foreach (var f in friends)
        {
            world.GetOrCreateRelationship(new RelationshipKey(victim.Id, f.Id), 0);
            world.GetOrCreateRelationship(new RelationshipKey(f.Id, victim.Id), 0);
        }
        Steal(world, thief, new Sink());
        double before = Justice.InfamyOf(world, thief.Id.Value, 0).All;

        for (long day = 1; day <= 10; day++) Justice.Daily(world, Ctx(world), day);

        var crime = Assert.Single(world.Crime!.Cases);
        Assert.True(crime.Knowers.Count > 2, $"o boato não correu ({crime.Knowers.Count})");
        Assert.True(Justice.InfamyOf(world, thief.Id.Value, 0).All > before);
    }

    [Fact]
    public void The_victim_reports_and_the_court_fines_the_thief_with_restitution_and_coin_is_conserved()
    {
        var world = New(Sure, Rules(new JusticeRules { WitnessSeeChance = 1, MisidentifyChance = 0, ReportChance = 1, FearSilence = 0, ConvictBase = 1, PettyTheftValue = 0 }));
        var thief = Add(world, Criminal, wallet: 200);
        var victim = Add(world, Honest, wallet: 40);
        var sink = new Sink();
        Steal(world, thief, sink); // leva 20 da carteira da vítima
        long coin = Coin(world);

        Justice.Daily(world, Ctx(world, sink), 1); // denúncia
        Justice.Daily(world, Ctx(world, sink), world.CrimeRules!.Justice.CourtEveryDays); // dia de corte

        Assert.Single(sink.Of(WorldEventKind.CrimeReported));
        var verdict = Assert.Single(sink.Of(WorldEventKind.Verdict)).Payload.Split('|');
        Assert.Equal(["culpado", "multa", "40", "1"], verdict[4..8]); // 20 de volta e 20 de multa
        Assert.Equal(40, victim.Wallet.Amount);
        Assert.Equal(coin, Coin(world)); // a moeda só muda de mão (sem cidade no teste, a multa fica com o condenado)
        Assert.Contains(thief.Psyche!.Memories, m => m.Kind == LifeEventKind.Punished);
    }

    [Fact]
    public void Kin_stay_silent_and_without_authority_nobody_judges_but_the_victim_keeps_a_grudge()
    {
        var world = New(Sure, Rules(new JusticeRules { WitnessSeeChance = 1, MisidentifyChance = 0, ReportChance = 1 }));
        var thief = Add(world, Criminal);
        var mother = Add(world, Honest, wallet: 100, sex: Sex.Female, age: 50);
        var son = new Npc(world.NextNpcIdAndAdvance(), "s", Sex.Male, WorldDate.Epoch(Calendar).AddYears(-20), new CultureId(1),
            Here, mother.Id, null, household: null, health: 100, personality: Criminal, profession: new ProfessionType(1), currentLocation: Here);
        world.AddNpc(son);
        var target = new TheftTarget(TheftTargetKind.Person, mother, null, null, 0, 0, 10, 10, Here);
        var sink = new Sink();
        Theft.Commit(world, Ctx(world, sink), son, TheftMotive.Opportunity, target, world.Rng.OneShot("k"));
        Assert.Single(world.Crime!.Cases);
        Justice.Daily(world, Ctx(world, sink), 1);
        Assert.Empty(sink.Of(WorldEventKind.CrimeReported)); // a mãe não denuncia o filho (e quem viu sem ser vítima não denuncia o furto)

        var lawless = New(Sure, Rules(new JusticeRules { Authority = JusticeAuthority.None, WitnessSeeChance = 1, MisidentifyChance = 0 }));
        var t = Add(lawless, Criminal);
        var v = Add(lawless, Honest, wallet: 100);
        Steal(lawless, t, new Sink());
        Justice.Daily(lawless, Ctx(lawless), 1);
        Assert.NotNull(lawless.Crime!.GrudgeOf(v.Id.Value, t.Id.Value));
    }

    [Fact]
    public void A_flogged_or_beaten_neighbour_never_drops_to_the_danger_floor_that_makes_everyone_flee()
    {
        // antes: o açoite levava a saúde a 0 sem matar, e quem estava perto fugia (Travel) sem parar até morrer de sede
        var world = New(Sure, Rules(new JusticeRules
        {
            WitnessSeeChance = 1,
            MisidentifyChance = 0,
            ReportChance = 1,
            FearSilence = 0,
            ConvictBase = 1,
            PettyTheftValue = 0,
            CorporalHarm = 50
        }));
        var thief = Add(world, Criminal);
        var victim = Add(world, Honest, wallet: 40);
        var neighbour = Add(world, Honest);
        thief.SetHealth(10);
        Steal(world, thief, new Sink());
        Justice.Daily(world, Ctx(world), 1);
        Justice.Daily(world, Ctx(world), world.CrimeRules!.Justice.CourtEveryDays);

        Assert.True(thief.IsAlive);
        Assert.True(thief.Health > AliveNpcIndex.EndangeredHealth, $"saúde {thief.Health}");
        Assert.Equal(ActionType.Eat, BehaviorDecisionSystem.ApplyPerceptionWithin(world, neighbour, ActionType.Eat, 1));
        Assert.True(victim.Wallet.Amount > 0);
    }

    [Fact]
    public void A_convicted_killer_hangs_or_is_banished_and_leaves_the_world()
    {
        var world = New(Sure, CrimeRules.Default with
        {
            Violence = new ViolenceRules { LethalChance = 1 },
            Justice = new JusticeRules { WitnessSeeChance = 1, MisidentifyChance = 0, ReportChance = 1, FearSilence = 0, ConvictBase = 1, HomicideFleeChance = 0 },
        });
        var killer = Add(world, Criminal);
        var victim = Add(world, Honest);
        Add(world, Honest); // testemunha
        var sink = new Sink();
        Violence.Fight(world, Ctx(world, sink), killer, victim, "ofensa", world.Rng.OneShot("k"));
        Justice.Daily(world, Ctx(world, sink), 1);
        Justice.Daily(world, Ctx(world, sink), world.CrimeRules!.Justice.CourtEveryDays);

        Assert.False(killer.IsAlive);
        Assert.Contains(Assert.Single(sink.Of(WorldEventKind.Verdict)).Payload.Split('|')[5], new[] { "forca", "banimento" });
        Assert.True(sink.Of(WorldEventKind.Executed).Any() || sink.Of(WorldEventKind.Banished).Any());
    }

    [Fact]
    public void A_recent_punishment_in_the_city_deters_whoever_thinks_of_stealing()
    {
        var world = New(TheftRules.Default, Rules());
        var borderline = Add(world, Personality.Create(50, 50, 40, 50, 50, 50, 50, 40, 60, 40).Value!); // peso 1,4
        Home(world, Here, borderline);
        double before = CrimeSystem.PlannedTheftPerYear(world, borderline, CrimeSystem.DeterrenceOf(world, borderline, 0), 0, out _);
        borderline.SetHunger(20, 0);
        var neighbour = Add(world, Honest, at: new CellCoord(3, 1));
        Home(world, new CellCoord(3, 1), neighbour).Deposit(new ResourceType(world.EconomyRules.FoodResourceId), 20);
        Assert.True(TheftResolver.Wants(world, borderline, 0));

        // três condenações na cidade: o exemplo pesa
        var deterrence = world.Crime!.EnsureDeterrence(borderline.City.Value);
        deterrence.Level = 0.9;
        deterrence.Tick = 0;

        double after = CrimeSystem.PlannedTheftPerYear(world, borderline, CrimeSystem.DeterrenceOf(world, borderline, 0), 0, out _);
        Assert.True(after < 0.5 * before, $"antes {before:F4}, depois {after:F4}");
        Assert.False(TheftResolver.Wants(world, borderline, 0));
    }
}
