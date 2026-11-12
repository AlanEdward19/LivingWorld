using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Population.Family;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Population.Family;

/// <summary>AD-136: a união que pode acabar — parentesco, luto, dote, desgaste, caso, separação e quem fica com quem.</summary>
public class UnionLifeTests
{
    private static readonly WorldCalendar Calendar = ScenarioRunner.DefaultCalendar;
    private static readonly Personality Mid = Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    private sealed class Sink : IWorldEventSink
    {
        public List<WorldEvent> Events { get; } = [];
        public void Record(WorldEvent evt) => Events.Add(evt);
    }

    private static WorldState World(UnionRules? unions = null)
    {
        var family = ScenarioRunner.DefaultFamilyRules with { Unions = unions ?? UnionRules.Medieval };
        var world = new WorldState(
            Calendar, 7, ScenarioRunner.DefaultMap(7), ScenarioRunner.DefaultPopulationCatalog,
            ScenarioRunner.DefaultPopulationRules, ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules, economyRules: ScenarioRunner.DefaultEconomyRules, familyRules: family);
        world.CurrentDate = WorldDate.Epoch(Calendar).AddYears(80);
        return world;
    }

    private static long _next = 1;

    private static Npc Person(WorldState world, Sex sex, int age, Npc? mother = null, Npc? father = null, Personality? p = null)
    {
        var id = new NpcId(_next++);
        var npc = new Npc(id, $"n{id.Value}", sex, world.CurrentDate.AddYears(-age), new CultureId(1), new CellCoord(0, 0),
            mother?.Id, father?.Id, household: null, health: 100, personality: p ?? Mid, profession: new ProfessionType(1),
            currentLocation: new CellCoord(0, 0));
        world.AddNpc(npc);
        return npc;
    }

    private static Household Home(WorldState world, params Npc[] members)
    {
        var home = new Household(world.NextHouseholdIdAndAdvance(), new CellCoord(0, 0), members[0].Id, members.Select(m => m.Id).ToList(),
            new Dictionary<ResourceType, long> { [new ResourceType(1)] = 120, [new ResourceType(2)] = 60 });
        world.AddHousehold(home);
        foreach (var m in members) m.JoinHousehold(home.Id);
        return home;
    }

    private static void Wed(Npc a, Npc b)
    {
        a.Marry(b.Id);
        b.Marry(a.Id);
        a.SetUnion(new UnionState(Unions: 1));
        b.SetUnion(new UnionState(Unions: 1));
    }

    private static TickContext Ctx(WorldState world, Sink? sink = null) => new(world, world.Rng, world.Scheduler, sink);

    [Fact]
    public void Relatedness_follows_the_genealogy_from_siblings_to_cousins()
    {
        var world = World();
        var gm = Person(world, Sex.Female, 70);
        var gf = Person(world, Sex.Male, 72);
        var auntMother = Person(world, Sex.Female, 45, gm, gf);
        var uncleFather = Person(world, Sex.Male, 47, gm, gf);
        var inLawA = Person(world, Sex.Male, 46);
        var inLawB = Person(world, Sex.Female, 44);
        var cousin1 = Person(world, Sex.Female, 20, auntMother, inLawA);
        var cousin2 = Person(world, Sex.Male, 21, inLawB, uncleFather);
        var sibling = Person(world, Sex.Male, 18, auntMother, inLawA);
        var halfSibling = Person(world, Sex.Male, 16, auntMother, Person(world, Sex.Male, 50));

        Assert.Equal(
            [0.5, 0.5, 0.25, 0.25, 0.125, 0.0],
            new[]
            {
                Kinship.Relatedness(world, cousin1, auntMother, 3), Kinship.Relatedness(world, cousin1, sibling, 3),
                Kinship.Relatedness(world, cousin1, halfSibling, 3), Kinship.Relatedness(world, cousin1, uncleFather, 3),
                Kinship.Relatedness(world, cousin1, cousin2, 3), Kinship.Relatedness(world, cousin1, inLawB, 3),
            });
    }

    [Fact]
    public void Courtship_rejects_first_cousins_under_the_medieval_taboo_but_not_under_the_old_rule()
    {
        bool Rejected(UnionRules rules)
        {
            var world = World(rules);
            var gm = Person(world, Sex.Female, 70);
            var gf = Person(world, Sex.Male, 72);
            var m = Person(world, Sex.Female, 45, gm, gf);
            var f = Person(world, Sex.Male, 47, gm, gf);
            var a = Person(world, Sex.Female, 20, m, Person(world, Sex.Male, 46));
            var b = Person(world, Sex.Male, 22, Person(world, Sex.Female, 44), f);
            return CourtshipSystem.Reject(world, a, b, world.CurrentDate, world.PopulationRules, null) == CourtshipRejectionReason.Incesto;
        }

        Assert.Equal((true, false), (Rejected(UnionRules.Medieval), Rejected(UnionRules.Disabled)));
    }

    [Fact]
    public void Preference_is_individual_the_sick_want_the_healthy_more_than_the_reverse()
    {
        var world = World();
        var healthy = Person(world, Sex.Female, 22);
        var sick = Person(world, Sex.Male, 24);
        sick.SetHealth(20);
        var rules = world.FamilyRules;
        Assert.True(CourtshipSystem.Preference(sick, healthy, null, rules, world.PopulationRules, world.CurrentDate)
            > CourtshipSystem.Preference(healthy, sick, null, rules, world.PopulationRules, world.CurrentDate));
    }

    [Fact]
    public void Marriage_creates_no_food_the_couple_brings_what_each_one_had()
    {
        var world = World();
        var bride = Person(world, Sex.Female, 20);
        var groom = Person(world, Sex.Male, 23);
        var brideParent = Person(world, Sex.Female, 45);
        Home(world, bride, brideParent);
        Home(world, groom);
        long Total() => world.Households.Sum(h => h.Stock.Values.Sum());
        long before = Total();

        MarriageSystem.Marry(world, Ctx(world), bride, groom);

        Assert.Equal(before, Total());
    }

    [Fact]
    public void A_widow_waits_out_the_mourning_before_courting_again()
    {
        bool CourtsAfter(int months)
        {
            var world = World();
            var widow = Person(world, Sex.Female, 30);
            var late = Person(world, Sex.Male, 33);
            var suitor = Person(world, Sex.Male, 31);
            Home(world, widow);
            Home(world, suitor);
            Wed(widow, late);
            late.Die(world.CurrentDate);
            world.AliveNpcIndex.OnDied(late);
            world.CurrentDate = world.CurrentDate.AddHours((long)months * Calendar.HoursPerMonth);
            var sink = new Sink();
            new CourtshipSystem().Tick(world, Ctx(world, sink));
            return widow.CourtingWith is not null || sink.Events.Any(e => e.Kind == WorldEventKind.CourtshipRejected);
        }

        Assert.Equal((false, true), (CourtsAfter(2), CourtsAfter(UnionRules.Medieval.WidowMourningMonths + 1)));
    }

    [Fact]
    public void Separation_splits_the_children_by_age_and_sends_the_leaver_to_his_parents()
    {
        var world = World(UnionRules.Medieval with { OlderChildrenToFather = 1.0 });
        var grandma = Person(world, Sex.Female, 60);
        var husband = Person(world, Sex.Male, 35, grandma);
        var wife = Person(world, Sex.Female, 32);
        var toddler = Person(world, Sex.Female, 3, wife, husband);
        var boy = Person(world, Sex.Male, 10, wife, husband);
        var grandmaHome = Home(world, grandma);
        var home = Home(world, wife, husband, toddler, boy);
        Wed(husband, wife);
        var sink = new Sink();

        UnionDissolution.Separate(world, Ctx(world, sink), husband, wife, UnionEndMode.Separation);

        Assert.Equal(
            (grandmaHome.Id, grandmaHome.Id, home.Id, (NpcId?)null, (NpcId?)null, true),
            (husband.Household, boy.Household, toddler.Household, husband.Spouse, wife.Spouse,
                sink.Events.Any(e => e.Kind == WorldEventKind.UnionEnded)));
    }

    [Fact]
    public void After_a_separation_the_person_courts_again_only_when_the_norm_allows()
    {
        bool CourtsAYearLater(int waitYears)
        {
            var world = World(UnionRules.Medieval with { RemarryYearsAfterSeparation = waitYears });
            var husband = Person(world, Sex.Male, 35);
            var wife = Person(world, Sex.Female, 32);
            Home(world, wife, husband);
            Wed(husband, wife);
            UnionDissolution.Separate(world, Ctx(world), husband, wife, UnionEndMode.Separation);
            var suitor = Person(world, Sex.Male, 30);
            Home(world, suitor);
            world.CurrentDate = world.CurrentDate.AddYears(1);
            var sink = new Sink();
            new CourtshipSystem().Tick(world, Ctx(world, sink));
            return wife.CourtingWith is not null
                || sink.Events.Any(e => e.Kind == WorldEventKind.CourtshipRejected && e.Payload.EndsWith($"|{wife.Id.Value}"));
        }

        Assert.Equal((false, true), (CourtsAYearLater(5), CourtsAYearLater(0)));
    }

    [Fact]
    public void A_wife_with_an_affair_conceives_from_the_lover_in_the_paternity_share()
    {
        var world = World(UnionRules.Medieval with { AffairPaternityShare = 1.0 });
        var wife = Person(world, Sex.Female, 28);
        var husband = Person(world, Sex.Male, 30);
        var lover = Person(world, Sex.Male, 29);
        Wed(wife, husband);
        Assert.Same(husband, UnionSystem.BiologicalFather(world, Ctx(world), wife, husband));
        wife.SetUnion(wife.Union! with { AffairWith = lover.Id.Value });
        lover.SetUnion(new UnionState(AffairWith: wife.Id.Value));
        Assert.Same(lover, UnionSystem.BiologicalFather(world, Ctx(world), wife, husband));
    }

    [Fact]
    public void Discovered_affair_is_a_betrayal_the_betrayed_trusts_less_and_resents_more()
    {
        var world = World(UnionRules.Medieval with { DiscoveryPerMonth = 1.0, AdulterySeparation = 0, ViolenceChance = 0 });
        var wife = Person(world, Sex.Female, 28);
        var husband = Person(world, Sex.Male, 30);
        var lover = Person(world, Sex.Female, 27);
        Home(world, wife, husband);
        Home(world, lover);
        Wed(wife, husband);
        husband.SetUnion(husband.Union! with { AffairWith = lover.Id.Value });
        lover.SetUnion(new UnionState(AffairWith: husband.Id.Value));
        var trust = world.GetOrCreateRelationship(new RelationshipKey(wife.Id, husband.Id), 0);
        trust.CopyAxesFrom(Relationship.FromAxes(60, 60, 60, 0, 0));
        var sink = new Sink();

        new UnionSystem().Tick(world, Ctx(world, sink));

        Assert.Equal((true, true, true, (long?)null),
            (trust.Trust < 60, wife.Union!.Grievance >= UnionRules.Medieval.BetrayalGrievance * 0.5,
                sink.Events.Any(e => e.Kind == WorldEventKind.AffairDiscovered && e.Payload.EndsWith("|perdao")),
                husband.Union!.AffairWith));
    }

    [Fact]
    public void Penury_wears_the_couple_down_and_cools_their_affection()
    {
        double AffectionAfter(bool poor)
        {
            var world = World();
            var a = Person(world, Sex.Female, 28);
            var b = Person(world, Sex.Male, 30);
            var home = Home(world, a, b);
            if (poor) home.Withdraw(new ResourceType(1), 120);
            Wed(a, b);
            var rel = world.GetOrCreateRelationship(new RelationshipKey(a.Id, b.Id), 0);
            rel.CopyAxesFrom(Relationship.FromAxes(70, 70, 50, 0, 0));
            var system = new UnionSystem();
            for (int m = 0; m < 24; m++)
            {
                world.CurrentDate = world.CurrentDate.AddHours(Calendar.HoursPerMonth);
                system.Tick(world, Ctx(world));
            }
            return rel.Affection;
        }

        Assert.True(AffectionAfter(poor: true) < AffectionAfter(poor: false));
    }

    [Fact]
    public void Couples_who_stopped_loving_each_other_separate_only_while_unions_can_end()
    {
        int Separations(UnionRules rules)
        {
            var world = World(rules with { SeparationPerYear = 6 });
            var sink = new Sink();
            for (int i = 0; i < 20; i++)
            {
                var a = Person(world, Sex.Female, 28);
                var b = Person(world, Sex.Male, 30);
                Home(world, a, b);
                Wed(a, b);
                world.GetOrCreateRelationship(new RelationshipKey(a.Id, b.Id), 0);
                world.GetOrCreateRelationship(new RelationshipKey(b.Id, a.Id), 0);
            }
            var system = new UnionSystem();
            for (int m = 0; m < 12; m++)
            {
                world.CurrentDate = world.CurrentDate.AddHours(Calendar.HoursPerMonth);
                system.Tick(world, Ctx(world, sink));
            }
            return sink.Events.Count(e => e.Kind == WorldEventKind.UnionEnded);
        }

        Assert.Equal((true, 0), (Separations(UnionRules.Medieval) > 0, Separations(UnionRules.Disabled with { SeparationPerYear = 6 })));
    }
}
