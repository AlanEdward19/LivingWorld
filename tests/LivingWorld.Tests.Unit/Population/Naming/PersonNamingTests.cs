using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cities.Construction;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Population.Naming;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;
using LivingWorld.Simulation.Snapshot;

namespace LivingWorld.Tests.Unit.Population.Naming;

/// <summary>AD-178: toda pessoa tem nome próprio e de família, da fonologia da própria cultura, herdado por regra.</summary>
public class PersonNamingTests
{
    private static readonly Personality SomePersonality = Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    private static readonly CultureNaming Honouring = new(
        ["Dun", "Gal", "Mor"], ["a"], ["o"], ["en"], Lineage.Patrilineal, SpouseTakesName: true, AncestorNameChance: 1.0);

    private static WorldState Founded(ulong seed, int population = 120) =>
        ScenarioRunner.Create(seed, initialPopulation: population).World;

    [Fact]
    public void Founders_have_given_and_family_name_never_the_legacy_label()
    {
        var world = Founded(7);

        Assert.All(world.Npcs, npc =>
        {
            Assert.DoesNotContain("npc-", npc.Name);
            Assert.NotNull(npc.FamilyName);
        });
    }

    [Fact]
    public void Same_seed_gives_same_names()
    {
        Assert.Equal(Founded(11).Npcs.Select(n => n.Name), Founded(11).Npcs.Select(n => n.Name));
    }

    [Fact]
    public void Different_seed_gives_different_names()
    {
        Assert.NotEqual(Founded(11).Npcs.Select(n => n.Name), Founded(12).Npcs.Select(n => n.Name));
    }

    [Fact]
    public void Children_of_a_founding_house_share_the_house_family_name()
    {
        var world = Founded(5);
        int checkedHouses = 0;
        foreach (var home in world.Households)
        {
            var members = home.Members.Select(id => world.FindNpc(id)!).ToList();
            var children = members.Where(m => m.Spouse is null && !ReferenceEquals(m, members[0])).ToList();
            if (children.Count < 2) continue;
            checkedHouses++;
            Assert.Single(children.Select(c => c.FamilyName).Distinct());
        }
        Assert.True(checkedHouses > 0);
    }

    [Fact]
    public void No_two_living_people_of_a_house_share_the_full_name()
    {
        var world = Founded(9, population: 400);

        Assert.All(world.Households, home =>
        {
            var names = home.Members.Select(id => world.FindNpc(id)!).Where(n => n.IsAlive).Select(n => n.Name).ToList();
            Assert.Equal(names.Count, names.Distinct().Count());
        });
    }

    [Fact]
    public void Cultures_of_the_same_world_get_their_own_phonology()
    {
        var world = Founded(3, population: 0);

        Assert.NotEqual(
            CultureNaming.Of(world, new CultureId(1)).Syllables, CultureNaming.Of(world, new CultureId(2)).Syllables);
    }

    [Fact]
    public void Newborn_of_a_wed_couple_takes_the_lineage_family_name()
    {
        var (world, mother, father, home) = Couple();
        var naming = Honouring with { AncestorNameChance = 0 };

        string name = PersonNaming.NameNewborn(world, naming, mother, father, new NpcId(99), Sex.Female, home);

        Assert.EndsWith(" " + father.FamilyName, name);
    }

    [Fact]
    public void Newborn_of_a_matrilineal_culture_takes_the_mother_family_name()
    {
        var (world, mother, father, home) = Couple();
        var naming = Honouring with { Lineage = Lineage.Matrilineal, AncestorNameChance = 0 };

        string name = PersonNaming.NameNewborn(world, naming, mother, father, new NpcId(99), Sex.Male, home);

        Assert.EndsWith(" " + mother.FamilyName, name);
    }

    [Fact]
    public void Newborn_out_of_wedlock_takes_the_mother_family_name()
    {
        var (world, mother, father, home) = Couple(wed: false);

        string name = PersonNaming.NameNewborn(world, Honouring with { AncestorNameChance = 0 }, mother, father, new NpcId(99), Sex.Male, home);

        Assert.EndsWith(" " + mother.FamilyName, name);
    }

    [Fact]
    public void Custom_names_the_newborn_after_a_dead_grandparent_of_the_same_sex()
    {
        var (world, mother, father, home) = Couple(grandmotherName: "Elara Moren");

        string name = PersonNaming.NameNewborn(world, Honouring, mother, father, new NpcId(99), Sex.Female, home);

        Assert.Equal("Elara " + father.FamilyName, name);
    }

    [Fact]
    public void Honour_name_is_skipped_when_a_living_housemate_already_has_that_full_name()
    {
        var (world, mother, father, home) = Couple(grandmotherName: "Elara Moren");
        var sister = Person(world, 50, "Elara " + father.FamilyName, Sex.Female);
        home.AddMember(sister.Id);

        string name = PersonNaming.NameNewborn(world, Honouring, mother, father, new NpcId(99), Sex.Female, home);

        Assert.NotEqual(sister.Name, name);
    }

    [Fact]
    public void Spouse_outside_the_lineage_adopts_the_family_name_when_the_culture_says_so()
    {
        var (world, mother, father, _) = Couple(wed: false);

        PersonNaming.OnMarriage(world, Honouring, mother, father);

        Assert.Equal("Ana " + father.FamilyName, mother.Name);
    }

    [Fact]
    public void Spouse_keeps_the_name_when_the_culture_has_no_such_custom()
    {
        var (world, mother, father, _) = Couple(wed: false);

        PersonNaming.OnMarriage(world, Honouring with { SpouseTakesName = false }, mother, father);

        Assert.Equal("Ana Silmor", mother.Name);
    }

    [Fact]
    public void Child_of_a_legacy_named_father_still_gets_a_stable_family_name()
    {
        var (world, mother, _, home) = Couple();
        var legacy = Person(world, 60, "npc-1-17", Sex.Male);
        mother.Marry(legacy.Id);

        string first = PersonNaming.NameNewborn(world, Honouring with { AncestorNameChance = 0 }, mother, legacy, new NpcId(98), Sex.Male, home);
        string second = PersonNaming.NameNewborn(world, Honouring with { AncestorNameChance = 0 }, mother, legacy, new NpcId(99), Sex.Male, home);

        Assert.Equal(first[(first.LastIndexOf(' ') + 1)..], second[(second.LastIndexOf(' ') + 1)..]);
    }

    [Fact]
    public void Materialized_person_gets_a_real_name()
    {
        var world = Founded(4, population: 0);
        var pool = new AggregatePopulationPool(2, 200, 160);
        var city = new City(world.NextCityId(), new CellCoord(2, 2), foundedAtTick: 0, foundedFromCityId: null,
            aggregatePool: pool, poolNpcIds: world.ReserveNpcIdBlock(pool.Count));
        world.AddCity(city);
        var ctx = new TickContext(world, world.Rng, world.Scheduler);

        var result = MaterializationSystem.MaterializeOne(world, ctx, city.Id);

        Assert.True(result.IsSuccess, result.Error);
        Assert.NotNull(result.Value!.FamilyName);
    }

    [Fact]
    public void Names_survive_the_snapshot_round_trip()
    {
        var world = Founded(6, population: 40);

        var back = PositionalSnapshot.DecodeFull(PositionalSnapshot.EncodeFull(world, out _));

        Assert.Equal(world.Npcs.Select(n => n.Name), back.Npcs.Select(n => n.Name));
        Assert.Equal(WorldSnapshot.CanonicalHash(world), WorldSnapshot.CanonicalHash(back));
    }

    [Fact]
    public void World_saved_with_legacy_labels_loads_and_keeps_them()
    {
        var world = Founded(6, population: 40);
        foreach (var npc in world.Npcs) npc.Rename($"npc-1-{npc.Id.Value}");

        var back = WorldSnapshot.Deserialize(WorldSnapshot.Serialize(world));

        Assert.All(back.Npcs, npc => Assert.Equal($"npc-1-{npc.Id.Value}", npc.Name));
        Assert.Equal(WorldSnapshot.CanonicalHash(world), WorldSnapshot.CanonicalHash(back));
    }

    [Fact]
    public void Legacy_one_word_name_has_no_family_name()
    {
        var world = Founded(1, population: 0);

        Assert.Null(Person(world, 1, "npc-1-17", Sex.Male).FamilyName);
    }

    private static (WorldState World, Npc Mother, Npc Father, Household Home) Couple(
        bool wed = true, string? grandmotherName = null)
    {
        var world = Founded(1, population: 0);
        NpcId? grandmother = null;
        if (grandmotherName is not null)
            grandmother = Person(world, 10, grandmotherName, Sex.Female, dead: true).Id;
        var father = Person(world, 20, "Tor Galdun", Sex.Male, mother: grandmother);
        var mother = Person(world, 21, "Ana Silmor", Sex.Female);
        if (wed)
        {
            mother.Marry(father.Id);
            father.Marry(mother.Id);
        }
        var home = new Household(new HouseholdId(1), new CellCoord(0, 0), father.Id, [father.Id, mother.Id]);
        world.AddHousehold(home);
        return (world, mother, father, home);
    }

    private static Npc Person(WorldState world, long id, string name, Sex sex, NpcId? mother = null, bool dead = false)
    {
        var birth = world.CurrentDate.AddYears(-60);
        var npc = new Npc(
            new NpcId(id), name, sex, birth, ScenarioRunner.DefaultCulture, new CellCoord(0, 0),
            motherId: mother, fatherId: null, household: null, health: 100, personality: SomePersonality,
            profession: ProfessionType.None, currentLocation: new CellCoord(0, 0),
            deathDate: dead ? birth.AddYears(50) : null);
        world.AddNpc(npc);
        return npc;
    }
}

/// <summary>AD-178 na corrida compartilhada da vila (20 seeds, 10 anos): quem nasce no mundo vivo tem nome de família, e irmãos
/// de pai e mãe dividem o nome de família (exceção declarada: filho nascido antes do casamento leva o da mãe).</summary>
public class PersonNamingVillageTests
{
    [Fact]
    [Trait("Category", "Scenario")]
    public void Nobody_born_in_the_village_decade_keeps_a_legacy_label()
    {
        Assert.All(DefaultVillageDecade.Runs, run => Assert.Equal(0, run.Naming.Legacy));
        Assert.True(DefaultVillageDecade.Runs.Sum(r => r.Naming.Born) > 0);
    }

    [Fact]
    [Trait("Category", "Scenario")]
    public void Full_siblings_share_the_family_name()
    {
        int groups = DefaultVillageDecade.Runs.Sum(r => r.Naming.SiblingGroups);
        int sharing = DefaultVillageDecade.Runs.Sum(r => r.Naming.SiblingGroupsSharingFamily);

        Assert.True(groups > 0);
        Assert.True(sharing >= 0.9 * groups, $"{sharing}/{groups} grupos de irmãos com um só nome de família");
    }
}
