using LivingWorld.Domain.Cognition;
using LivingWorld.Domain.Crime;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Crime;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;
using static LivingWorld.Tests.Unit.Crime.CrimeTestWorld;

namespace LivingWorld.Tests.Unit.Crime;

/// <summary>Violência entre pessoas (AD-150): propensão pelo corpo (homem jovem), impulsividade e instabilidade; a briga fere
/// (saúde) ou mata (homicídio pelo caminho das mortes), deixa mágoa em quem apanhou e pede vingança dos parentes; o ciúme do
/// traído vira mágoa contra o amante; a mágoa decai; a doméstica fica fora desta fatia. Antes do AD-150 nada disso existia.</summary>
public class ViolenceTests
{
    private static CrimeRules Rules(double lethal = 0.0) => CrimeRules.Default with
    {
        Violence = new ViolenceRules { LethalChance = lethal },
        Justice = new JusticeRules { Enabled = false },
    };

    [Fact]
    public void A_young_impulsive_man_is_far_more_violent_than_a_calm_woman_of_the_same_age()
    {
        var world = ScenarioRunner.Create(1, initialPopulation: 0).World;
        var hot = Personality.Create(50, 50, 50, 10, 50, 50, 50, 50, 90, 50).Value!;
        var calm = Personality.Create(50, 50, 50, 90, 50, 50, 50, 50, 20, 50).Value!;
        var youngMan = Add(world, hot, sex: Sex.Male, age: 20);
        var woman = Add(world, calm, sex: Sex.Female, age: 20);
        var sameManCalm = Add(world, calm, sex: Sex.Male, age: 20);
        var rules = world.CrimeRules!.Violence;
        long now = world.CurrentDate.TotalHours;

        double man = Violence.Propensity(world, rules, youngMan, now);
        Assert.True(man > 5 * Violence.Propensity(world, rules, woman, now), "sexo e idade pesam pouco");
        Assert.True(man > 3 * Violence.Propensity(world, rules, sameManCalm, now), "temperamento pesa pouco");
    }

    [Fact]
    public void A_fight_wounds_the_victim_who_remembers_and_holds_a_grudge_and_the_event_names_everyone()
    {
        var world = New(crime: Rules());
        var a = Add(world, Average);
        var b = Add(world, Average);
        var sink = new Sink();

        Violence.Fight(world, Ctx(world, sink), a, b, "ofensa", world.Rng.OneShot("f"));

        Assert.True(b.Health < 100);
        Assert.True(b.IsAlive);
        Assert.Equal("agressao", world.Crime!.GrudgeOf(b.Id.Value, a.Id.Value)!.Motive);
        Assert.Contains(b.Psyche!.Memories, m => m.Kind == LifeEventKind.Violence && m.Other == a.Id.Value);
        var parts = Assert.Single(sink.Of(WorldEventKind.Assault)).Payload.Split('|');
        Assert.Equal([a.Id.Value.ToString(), b.Id.Value.ToString(), "ofensa"], parts[..3]);
        Assert.Equal("ferido", parts[4]);
        Assert.Equal(100 - b.Health, int.Parse(parts[3]));
    }

    [Fact]
    public void A_lethal_fight_is_a_homicide_and_the_victim_s_kin_want_revenge()
    {
        var world = New(crime: Rules(lethal: 1.0));
        var killer = Add(world, Average);
        var mother = Add(world, Average, sex: Sex.Female, age: 50);
        var victim = new Npc(world.NextNpcIdAndAdvance(), "v", Sex.Male, WorldDate.Epoch(Calendar).AddYears(-22), new CultureId(1),
            Here, mother.Id, null, household: null, health: 100, personality: Average, profession: new ProfessionType(1), currentLocation: Here);
        world.AddNpc(victim);
        var sink = new Sink();

        Violence.Fight(world, Ctx(world, sink), killer, victim, "ofensa", world.Rng.OneShot("f"));

        Assert.False(victim.IsAlive);
        Assert.Single(sink.Of(WorldEventKind.Homicide), e => e.Payload == victim.Id.Value.ToString());
        var revenge = world.Crime!.GrudgeOf(mother.Id.Value, killer.Id.Value)!;
        Assert.Equal("vinganca", revenge.Motive);
        Assert.Equal(100, revenge.Intensity);
        Assert.Contains(mother.Psyche!.Memories, m => m.Kind == LifeEventKind.KinMurdered && m.Other == killer.Id.Value);
    }

    [Fact]
    public void A_grudge_halves_in_its_half_life_and_a_scheduled_fight_between_spouses_never_happens()
    {
        var world = New(crime: Rules());
        var rules = world.CrimeRules!.Violence;
        Violence.AddGrudge(world, 1, 2, "ofensa", 80, 0);
        long halfLife = (long)rules.GrudgeHalfLifeDays * Calendar.HoursPerDay;
        Assert.InRange(Violence.Intensity(rules, world.Crime!.GrudgeOf(1, 2)!, halfLife, Calendar.HoursPerDay), 39, 40);

        var husband = Add(world, Average);
        var wife = Add(world, Average, sex: Sex.Female);
        husband.Marry(wife.Id);
        wife.Marry(husband.Id);
        new CrimeSystem().HandleEvent(world, Ctx(world), new ScheduledEvent(1, 1, CrimeSystem.SystemName, $"F|{husband.Id.Value}|{wife.Id.Value}|ofensa"));
        Assert.Equal(100, wife.Health); // violência doméstica é da fatia seguinte
    }

    [Fact]
    public void A_discovered_affair_leaves_the_betrayed_jealous_of_the_lover()
    {
        var world = New(crime: Rules());
        Add(world, Average); Add(world, Average); Add(world, Average);
        Ctx(world).LogEvent(WorldEventKind.AffairDiscovered, "0|1|2|perdao", "UnionSystem");

        var grudge = world.Crime!.GrudgeOf(0, 2)!;
        Assert.Equal("ciume", grudge.Motive);
        Assert.Equal(world.CrimeRules!.Violence.JealousyGrudge, grudge.Intensity);
    }

    [Fact]
    public void The_crime_ledger_survives_a_snapshot_round_trip_and_an_old_snapshot_opens_without_it()
    {
        var world = New(crime: Rules());
        var a = Add(world, Average);
        var b = Add(world, Average);
        Violence.Fight(world, Ctx(world), a, b, "ofensa", world.Rng.OneShot("f"));

        var json = WorldSnapshot.Serialize(world);
        var restored = WorldSnapshot.Deserialize(json);
        Assert.Equal(WorldSnapshot.CanonicalHash(world), WorldSnapshot.CanonicalHash(restored));
        Assert.Equal(world.Crime!.Grudges.Count, restored.Crime!.Grudges.Count);
        Assert.Equal(world.CrimeRules, restored.CrimeRules);

        var old = System.Text.Json.Nodes.JsonNode.Parse(json)!.AsObject();
        old.Remove("Crime");
        old.Remove("CrimeRules");
        var reopened = WorldSnapshot.Deserialize(old.ToJsonString());
        Assert.Null(reopened.CrimeRules);
        Assert.Null(reopened.Crime);
    }

    [Fact]
    [Trait("Category", "Scenario")]
    public void The_same_village_gives_the_same_crimes_and_the_reopened_world_continues_the_same_history()
    {
        static (List<string> Events, WorldState World) Run(ulong seed, int years, WorldState? from = null)
        {
            var world = from ?? ScenarioRunner.Create(seed).World;
            var sink = new Sink();
            new WorldClock(ScenarioRunner.DefaultSystems(), sink: sink).Run(world, years * 12 * 30 * 24);
            var kinds = new[] { WorldEventKind.TheftCommitted, WorldEventKind.Assault, WorldEventKind.Verdict, WorldEventKind.CrimeReported };
            return (sink.Events.Where(e => kinds.Contains(e.Kind)).Select(e => $"{e.Tick}|{e.Kind}|{e.Payload}").ToList(), world);
        }

        var (first, world) = Run(5, 3);
        var (again, _) = Run(5, 3);
        Assert.Equal(first, again);
        Assert.NotEmpty(first);

        var reopened = WorldSnapshot.Deserialize(WorldSnapshot.Serialize(world));
        var (cont, _) = Run(5, 2, world);
        var (contReopened, _) = Run(5, 2, reopened);
        Assert.Equal(cont, contReopened);
    }
}
