using LivingWorld.Domain.Cognition;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cities;
using LivingWorld.Simulation.Cognition;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Crime;
using LivingWorld.Simulation.Population.Family;
using LivingWorld.Simulation.Population.Naming;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Population.Naming;

/// <summary>AD-183: a honra respinga na linhagem como a vergonha; o fundador funda; o nome aparece como motivo no evento; o luto
/// mostrado é o do motor.</summary>
public class HonorTests
{
    private static readonly Personality Kind = Personality.Create(50, 50, 50, 50, 100, 50, 50, 50, 50, 50).Value!;

    private static WorldState Empty(ulong seed = 1, NameBondRules? names = null) => ScenarioRunner.Create(seed, initialPopulation: 0,
        familyRules: names is null ? null : ScenarioRunner.DefaultFamilyRules with { Names = names }).World;

    private static Npc Person(WorldState world, long id, string name, Sex sex = Sex.Male, int age = 30, long wallet = 0,
        NpcId? mother = null, NpcId? father = null)
    {
        var city = world.ActiveCities().First();
        var npc = new Npc(new NpcId(id), name, sex, world.CurrentDate.AddYears(-age), ScenarioRunner.DefaultCulture, city.Location,
            motherId: mother, fatherId: father, household: null, health: 100, personality: Kind, profession: ProfessionType.None,
            currentLocation: city.Location, wallet: new Money(wallet), city: city.Id);
        world.AddNpc(npc);
        return npc;
    }

    private static Household Home(WorldState world, long id, params Npc[] members)
    {
        var home = new Household(new HouseholdId(id), members[0].CurrentLocation, members[0].Id, members.Select(m => m.Id).ToList(),
            city: members[0].City);
        world.AddHousehold(home);
        foreach (var m in members) m.JoinHousehold(home.Id);
        return home;
    }

    private static long Now(WorldState world) => world.CurrentDate.TotalHours;

    [Fact]
    public void Aid_given_is_honor_of_the_helper_and_spills_on_the_lineage_in_the_city()
    {
        var world = Empty();
        var helper = Person(world, 1, "Tor Galmor");
        var cousin = Person(world, 2, "Ren Galmor");
        var other = Person(world, 3, "Ren Dunbar");

        for (int i = 0; i < 10; i++)
            Honor.OnEvent(world, WorldEventKind.AidGiven, $"{helper.Id.Value}|{other.Id.Value}|50", Now(world));

        Assert.Equal(20.0, Honor.OwnOf(world, helper.Id.Value, Now(world)), 3);
        Assert.Equal((5.0, 0.0), (Honor.StandingOf(world, cousin, Now(world)), Honor.StandingOf(world, other, Now(world))));
    }

    [Fact]
    public void City_alms_are_no_one_honor()
    {
        var world = Empty();
        Person(world, 1, "Tor Galmor");

        Honor.OnEvent(world, WorldEventKind.AidGiven, "city-x|1|50", Now(world));

        Assert.Null(world.Honor);
    }

    [Fact]
    public void Without_the_rule_no_deed_is_kept()
    {
        var world = Empty(names: NameBondRules.Off);
        var helper = Person(world, 1, "Tor Galmor");

        Honor.OnEvent(world, WorldEventKind.DesignPrototyped, $"{helper.Id.Value}|3", Now(world));

        Assert.Null(world.Honor);
    }

    [Fact]
    public void Honor_fades_with_the_half_life()
    {
        var world = Empty();
        var inventor = Person(world, 1, "Tor Galmor");
        Honor.Credit(world, inventor.Id.Value, 20, Now(world));
        long later = Now(world) + (long)world.FamilyRules.Names.HonorHalfLifeDays * world.Calendar.HoursPerDay;

        Assert.Equal(10.0, Honor.OwnOf(world, inventor.Id.Value, later), 3);
    }

    [Fact]
    public void Lineage_honor_discounts_the_infamy_that_suspicion_and_credit_read()
    {
        var world = Empty();
        var thief = Person(world, 1, "Os Galmor");
        var founder = Person(world, 2, "Tor Galmor");
        var record = world.Crime!.EnsureRecord(thief.Id.Value);
        (record.Infamy, record.InfamyTick) = (40, Now(world));
        double before = Justice.ReputationOf(world, thief.Id.Value, Now(world));

        Honor.Credit(world, founder.Id.Value, 80, Now(world));

        // 40 de má fama − 0,5 × (0,25 × 80) de honra da linhagem
        Assert.Equal((40.0, 30.0), (before, Justice.ReputationOf(world, thief.Id.Value, Now(world))));
    }

    [Fact]
    public void Regard_weighs_honor_against_infamy_in_courtship()
    {
        var world = Empty();
        var honored = Person(world, 1, "Tor Galmor");
        var shamed = Person(world, 2, "Os Dunbar");
        Honor.Credit(world, honored.Id.Value, 50, Now(world));
        var record = world.Crime!.EnsureRecord(shamed.Id.Value);
        (record.Infamy, record.InfamyTick) = (50, Now(world));

        Assert.Equal((1.1, 0.9), (Math.Round(CourtshipSystem.RegardSignal(world, honored, Now(world)), 6),
            Math.Round(CourtshipSystem.RegardSignal(world, shamed, Now(world)), 6)));
    }

    [Fact]
    public void Honor_survives_the_snapshot_round_trip_and_an_old_snapshot_opens_without_it()
    {
        var world = Empty();
        var helper = Person(world, 1, "Tor Galmor");
        string before = WorldSnapshot.Serialize(world);
        Honor.Credit(world, helper.Id.Value, 30, Now(world));

        var reopened = WorldSnapshot.Deserialize(WorldSnapshot.Serialize(world));
        var old = WorldSnapshot.Deserialize(before);

        Assert.Equal(30.0, Honor.OwnOf(reopened, helper.Id.Value, Now(world)), 3);
        Assert.Equal(WorldSnapshot.Serialize(world), WorldSnapshot.Serialize(reopened));
        Assert.Null(old.Honor);
        Assert.DoesNotContain("\"Honor\"", before);
    }

    [Fact]
    public void Founder_is_the_adult_who_leads_the_most_people()
    {
        var world = Empty();
        var alone = Person(world, 1, "Ana Dunbar", wallet: 5000);
        Home(world, 1, alone);
        var father = Person(world, 2, "Tor Galmor", age: 45);
        var wife = Person(world, 3, "Isa Galmor", Sex.Female, 40);
        var kid = Person(world, 4, "Ren Galmor", age: 8, father: father.Id);
        var homes = new List<Household> { world.FindHousehold(new HouseholdId(1))!, Home(world, 2, father, wife, kid) };

        Assert.Equal(father.Id, Founders.Pick(world, homes, Now(world))!.Id);
    }

    [Fact]
    public void Daughter_city_honors_the_founder_when_the_culture_says_so()
    {
        var world = Empty();
        var founder = Person(world, 1, "Galmor Dunbar");
        var mother = world.ActiveCities().First();
        CultureId? honoring = null;
        for (int c = 1; c <= 60 && honoring is null; c++)
        {
            var naming = CultureNaming.Of(world, new CultureId(c));
            if (naming.DaughterHonorsFounder) honoring = new CultureId(c);
            Assert.False(naming.DaughterHonorsFounder && naming.DaughterTakesMotherName);
        }

        var (name, custom) = CityNameGenerator.GenerateDaughter(world, honoring!.Value, mother, founder);

        Assert.Equal("fundador", custom);
        Assert.StartsWith("Galmor", name);
    }

    [Fact]
    public void Mourning_shown_is_the_grief_the_engine_still_feels()
    {
        Assert.False(PsycheMechanic.Mourns(new Affect(0.019, 0, 0, 0, 0, 0)));
        Assert.True(PsycheMechanic.Mourns(new Affect(0.02, 0, 0, 0, 0, 0)));
        Assert.True(PsycheMechanic.Mourns(new Affect(0.05, 0, 0, 0, 0, 0))); // antes: só ≥ 0,1 aparecia como luto
    }

    [Fact]
    public void Lineage_aid_carries_the_motive_in_the_event()
    {
        var payloads = new List<string>();
        for (ulong seed = 1; seed <= 20 && payloads.Count == 0; seed++)
        {
            var world = Empty(seed);
            var needy = Person(world, 1, "Ana Galmor", Sex.Female);
            Home(world, 1, needy);
            var helper = Person(world, 2, "Tor Galmor", age: 40, wallet: 100_000);
            Home(world, 2, helper);
            var sink = new Sink(WorldEventKind.AidGiven);
            new MutualAidSystem().Tick(world, new TickContext(world, world.Rng, world.Scheduler, sink));
            payloads.AddRange(sink.Payloads.Where(p => p.StartsWith("2|", StringComparison.Ordinal)));
        }

        Assert.NotEmpty(payloads);
        Assert.All(payloads, p => Assert.EndsWith("|linhagem", p));
    }

    [Fact]
    public void Legacy_to_the_namesake_is_in_the_estate_event()
    {
        var world = Empty();
        var grandma = Person(world, 1, "Elara Moren", Sex.Female, 70, wallet: 1000);
        var son = Person(world, 2, "Tor Moren", age: 45, mother: grandma.Id);
        var granddaughter = Person(world, 3, "Elara Moren", Sex.Female, 12, father: son.Id);
        Home(world, 1, grandma);
        grandma.Die(world.CurrentDate);
        var sink = new Sink(WorldEventKind.EstateSettled);

        LivingWorld.Simulation.Population.Lifecycle.EstateSystem.Settle(world, new TickContext(world, world.Rng, world.Scheduler, sink), grandma);

        Assert.EndsWith($"|{granddaughter.Id.Value}|100", Assert.Single(sink.Payloads));
    }

    [Theory]
    [InlineData(WorldEventKind.AidGiven, "1|2|30|food|xara", "xara")]
    [InlineData(WorldEventKind.AidGiven, "1|2|30||linhagem", "linhagem")]
    [InlineData(WorldEventKind.AidGiven, "1|2|30", null)]
    [InlineData(WorldEventKind.CourtshipRejected, "MesmoNome|1|2", "mesmo-nome")]
    [InlineData(WorldEventKind.CourtshipRejected, "SemAfinidade|1|2", null)]
    [InlineData(WorldEventKind.EstateSettled, "1|2|900|0|0|3|100", "xara")]
    [InlineData(WorldEventKind.CrimeReported, "1|2|7|furto|0|fama-da-linhagem", "fama-da-linhagem")]
    public void Event_involvement_exposes_the_name_motive(WorldEventKind kind, string payload, string? motive)
    {
        var world = Empty();

        var involved = LivingWorld.Api.Visual.Observable.EventInvolvementReader.Read(world, new WorldEvent(1, kind, payload, 1, null, "t"));

        Assert.Equal(motive, involved.Motive);
    }

    private sealed class Sink(WorldEventKind kind) : IWorldEventSink
    {
        public List<string> Payloads { get; } = [];
        public void Record(WorldEvent evt) { if (evt.Kind == kind) Payloads.Add(evt.Payload); }
    }
}
