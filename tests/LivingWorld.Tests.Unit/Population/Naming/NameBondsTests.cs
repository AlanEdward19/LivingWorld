using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cities;
using LivingWorld.Simulation.Cognition;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Crime;
using LivingWorld.Simulation.Population.Family;
using LivingWorld.Simulation.Population.Lifecycle;
using LivingWorld.Simulation.Population.Naming;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Population.Naming;

/// <summary>AD-178: o nome pesa — xará e linhagem na ajuda, no luto, no legado, no namoro e na fama; e a cidade fala a língua
/// da cultura que a funda.</summary>
public class NameBondsTests
{
    private static readonly Personality Kind = Personality.Create(50, 50, 50, 50, 100, 50, 50, 50, 50, 50).Value!;

    private static WorldState Empty(ulong seed = 1, bool bonds = true) => ScenarioRunner.Create(seed, initialPopulation: 0,
        familyRules: bonds ? null : ScenarioRunner.DefaultFamilyRules with { Names = NameBondRules.Off }).World;

    private static Npc Person(WorldState world, long id, string name, Sex sex, int age, NpcId? mother = null, NpcId? father = null,
        long wallet = 0)
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

    /// <summary>Avó Elara, filho Tor, neta Elara (a xará).</summary>
    private static (WorldState World, Npc Grandma, Npc Son, Npc Granddaughter) Family(bool bonds = true, long grandmaWallet = 0)
    {
        var world = Empty(bonds: bonds);
        var grandma = Person(world, 1, "Elara Moren", Sex.Female, 70, wallet: grandmaWallet);
        var son = Person(world, 2, "Tor Moren", Sex.Male, 45, mother: grandma.Id);
        var granddaughter = Person(world, 3, "Elara Moren", Sex.Female, 12, father: son.Id);
        return (world, grandma, son, granddaughter);
    }

    [Fact]
    public void Grandchild_with_the_grandparent_given_name_is_a_namesake()
    {
        var (world, grandma, _, granddaughter) = Family();

        Assert.True(NameBonds.IsNamesake(world, granddaughter, grandma));
    }

    [Fact]
    public void Same_given_name_without_kinship_is_no_namesake()
    {
        var (world, grandma, _, _) = Family();
        var stranger = Person(world, 9, "Elara Dunbar", Sex.Female, 12);

        Assert.False(NameBonds.IsNamesake(world, stranger, grandma));
    }

    [Fact]
    public void Namesake_mourns_the_grandparent_when_the_rule_is_on()
    {
        var (world, grandma, _, granddaughter) = Family();
        grandma.Die(world.CurrentDate);

        LifeEventIntake.OnEvent(world, WorldEventKind.Death, $"{grandma.Id.Value}", "NpcDeath", world.CurrentDate.TotalHours);

        Assert.True(granddaughter.Psyche?.Grief > 0);
    }

    [Fact]
    public void Namesake_does_not_mourn_when_the_rule_is_off()
    {
        var (world, grandma, _, granddaughter) = Family(bonds: false);
        grandma.Die(world.CurrentDate);

        LifeEventIntake.OnEvent(world, WorldEventKind.Death, $"{grandma.Id.Value}", "NpcDeath", world.CurrentDate.TotalHours);

        Assert.False(granddaughter.Psyche?.Grief > 0);
    }

    [Fact]
    public void Grandparent_leaves_a_legacy_to_the_namesake_before_the_heirs_share()
    {
        var (world, grandma, son, granddaughter) = Family(grandmaWallet: 1000);
        Home(world, 1, grandma);
        grandma.Die(world.CurrentDate);

        EstateSystem.Settle(world, new TickContext(world, world.Rng, world.Scheduler), grandma);

        Assert.Equal((100L, 900L), (granddaughter.Wallet.Amount, son.Wallet.Amount));
    }

    [Fact]
    public void Same_family_name_without_known_ancestor_halves_courtship_preference()
    {
        var world = Empty();
        var a = Person(world, 1, "Ana Galmor", Sex.Female, 20);
        var b = Person(world, 2, "Tor Galmor", Sex.Male, 22);

        Assert.Equal(0.5, CourtshipSystem.NameSignal(world, a, b, world.FamilyRules, null));
    }

    [Fact]
    public void Same_family_name_with_a_known_common_ancestor_leaves_courtship_to_the_kinship_rule()
    {
        var world = Empty();
        var elder = Person(world, 1, "Dun Galmor", Sex.Male, 80);
        var a = Person(world, 2, "Ana Galmor", Sex.Female, 20, father: elder.Id);
        var b = Person(world, 3, "Tor Galmor", Sex.Male, 22, father: elder.Id);

        Assert.Equal(1.0, CourtshipSystem.NameSignal(world, a, b, world.FamilyRules, null));
    }

    [Fact]
    public void Lineage_shame_spills_on_who_carries_the_same_family_name_in_the_city()
    {
        var world = Empty();
        var thief = Person(world, 1, "Os Galmor", Sex.Male, 30);
        var cousin = Person(world, 2, "Tor Galmor", Sex.Male, 30);
        var other = Person(world, 3, "Ren Dunbar", Sex.Male, 30);
        var record = world.Crime!.EnsureRecord(thief.Id.Value);
        (record.Infamy, record.InfamyTick) = (80, world.CurrentDate.TotalHours);
        long now = world.CurrentDate.TotalHours;

        Assert.Equal((20.0, 0.0), (Justice.ReputationOf(world, cousin.Id.Value, now), Justice.ReputationOf(world, other.Id.Value, now)));
    }

    [Fact]
    public void Lineage_member_in_the_city_helps_a_starving_house_when_the_rule_is_on()
    {
        Assert.True(AidFromLineage(bonds: true) > 0);
    }

    [Fact]
    public void Without_the_rule_the_same_family_name_calls_nobody_to_help()
    {
        Assert.Equal(0, AidFromLineage(bonds: false));
    }

    [Fact]
    public void Namesake_grandparent_helps_more_often_than_a_grandparent_with_another_name()
    {
        int namesake = 0, plain = 0;
        for (ulong seed = 1; seed <= 60; seed++)
        {
            namesake += GrandparentAid(seed, sameName: true);
            plain += GrandparentAid(seed, sameName: false);
        }
        Assert.True(namesake > plain, $"xará {namesake} × outro nome {plain}");
    }

    [Fact]
    public void Cities_of_different_cultures_get_different_names()
    {
        var a = Empty(seed: 5);
        var b = Empty(seed: 5);

        Assert.NotEqual(CityNameGenerator.Generate(a, new CultureId(1)), CityNameGenerator.Generate(b, new CultureId(2)));
    }

    [Fact]
    public void City_and_people_of_a_culture_share_the_syllables()
    {
        var world = ScenarioRunner.Create(7).World;
        var naming = CultureNaming.Of(world, ScenarioRunner.DefaultCulture);
        string city = world.ActiveCities().First().Name;

        Assert.Contains(naming.Syllables, s => city.StartsWith(s, StringComparison.Ordinal));
        Assert.Contains(world.Npcs, n => naming.Syllables.Any(s => n.FamilyName!.StartsWith(s, StringComparison.Ordinal)
            && city.StartsWith(s, StringComparison.Ordinal)));
    }

    [Fact]
    public void Daughter_city_takes_the_mother_first_syllable_when_the_culture_says_so()
    {
        var world = Empty();
        var naming = CultureNaming.Of(world, ScenarioRunner.DefaultCulture) with { DaughterTakesMotherName = true };

        Assert.StartsWith("Gal", naming.NewPlaceName(world.Rng.OneShot("t"), "Galmorum"));
    }

    private static int AidFromLineage(bool bonds)
    {
        int aid = 0;
        for (ulong seed = 1; seed <= 20; seed++)
        {
            var world = Empty(seed, bonds);
            var needy = Person(world, 1, "Ana Galmor", Sex.Female, 30);
            Home(world, 1, needy);
            var helper = Person(world, 2, "Tor Galmor", Sex.Male, 40, wallet: 100_000);
            Home(world, 2, helper);
            aid += CountAid(world, helper);
        }
        return aid;
    }

    private static int GrandparentAid(ulong seed, bool sameName)
    {
        var world = Empty(seed);
        var grandma = Person(world, 1, "Elara Moren", Sex.Female, 70, wallet: 100_000);
        Home(world, 1, grandma);
        var son = Person(world, 2, "Tor Moren", Sex.Male, 45, mother: grandma.Id);
        var child = Person(world, 3, sameName ? "Elara Moren" : "Isa Moren", Sex.Female, 20, father: son.Id);
        Home(world, 2, son, child);
        return CountAid(world, grandma);
    }

    /// <summary>Um dia de ajuda mútua (uma rolagem por ajudante e dia).</summary>
    private static int CountAid(WorldState world, Npc helper)
    {
        var sink = new AidSink();
        new MutualAidSystem().Tick(world, new TickContext(world, world.Rng, world.Scheduler, sink));
        return sink.Count(helper.Id.Value);
    }

    private sealed class AidSink : IWorldEventSink
    {
        private readonly List<string> _payloads = [];
        public void Record(WorldEvent evt) { if (evt.Kind == WorldEventKind.AidGiven) _payloads.Add(evt.Payload); }
        public int Count(long helper) => _payloads.Count(p => p.StartsWith($"{helper}|", StringComparison.Ordinal));
    }
}
