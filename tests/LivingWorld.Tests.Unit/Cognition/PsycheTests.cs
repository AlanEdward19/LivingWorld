using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Cognition;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Decision;
using LivingWorld.Simulation.Cognition;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Market;
using LivingWorld.Simulation.Population.Lifecycle;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Cognition;

/// <summary>AD-138: memória da própria vida, emoção com duração e os seus efeitos. Cada teste falha no comportamento antigo
/// (o NPC não guardava nada do que vivia, não sentia nada, e nada disso mudava o que fazia).</summary>
public class PsycheTests
{
    private static readonly WorldCalendar Calendar = ScenarioRunner.DefaultCalendar;
    private static readonly Personality Mid = Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;
    private const long Day = 24;

    private static WorldState World()
    {
        var world = new WorldState(
            Calendar, 7, ScenarioRunner.DefaultMap(7), ScenarioRunner.DefaultPopulationCatalog,
            ScenarioRunner.DefaultPopulationRules, ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules, economyRules: ScenarioRunner.DefaultEconomyRules,
            familyRules: ScenarioRunner.DefaultFamilyRules);
        world.CurrentDate = WorldDate.Epoch(Calendar).AddYears(80);
        return world;
    }

    private static long _next = 1;

    private static Npc Person(WorldState world, Sex sex, int age, Npc? mother = null, Npc? father = null, Personality? p = null,
        CellCoord? at = null)
    {
        var id = new NpcId(_next++);
        var npc = new Npc(id, $"n{id.Value}", sex, world.CurrentDate.AddYears(-age), new CultureId(1), at ?? new CellCoord(0, 0),
            mother?.Id, father?.Id, household: null, health: 100, personality: p ?? Mid, profession: new ProfessionType(1),
            currentLocation: at ?? new CellCoord(0, 0));
        world.AddNpc(npc);
        return npc;
    }

    private static TickContext Ctx(WorldState world) => new(world, world.Rng, world.Scheduler);

    private static void Wed(Npc a, Npc b)
    {
        a.Marry(b.Id);
        b.Marry(a.Id);
    }

    [Fact]
    public void Death_of_a_child_and_of_a_spouse_leaves_strong_memories_and_grief_in_the_family()
    {
        var world = World();
        var mother = Person(world, Sex.Female, 40);
        var father = Person(world, Sex.Male, 42);
        Wed(mother, father);
        var son = Person(world, Sex.Male, 12, mother, father);
        var daughter = Person(world, Sex.Female, 15, mother, father);

        NpcDeath.Apply(world, Ctx(world), son, WorldEventKind.Death);
        long now = world.CurrentDate.TotalHours;

        foreach (var parent in new[] { mother, father })
        {
            var memory = Assert.Single(parent.Psyche!.Memories, m => m.Kind == LifeEventKind.ChildDied);
            Assert.Equal(son.Id.Value, memory.Other);
            Assert.True(memory.Salience > 60, $"saliência {memory.Salience}");
            Assert.True(PsycheMechanic.EmotionOf(world, parent, Emotion.Grief, now) > 0.5);
        }

        Assert.Contains(daughter.Psyche!.Memories, m => m.Kind == LifeEventKind.SiblingDied && m.Other == son.Id.Value);

        NpcDeath.Apply(world, Ctx(world), father, WorldEventKind.Death);
        Assert.Contains(mother.Psyche!.Memories, m => m.Kind == LifeEventKind.SpouseDied && m.Other == father.Id.Value);
        Assert.Contains(daughter.Psyche!.Memories, m => m.Kind == LifeEventKind.ParentDied);
    }

    [Fact]
    public void Grief_for_a_spouse_lasts_months_fades_within_the_year_and_the_memory_stays_for_decades()
    {
        var world = World();
        var widow = Person(world, Sex.Female, 50);
        var husband = Person(world, Sex.Male, 55);
        Wed(widow, husband);
        NpcDeath.Apply(world, Ctx(world), husband, WorldEventKind.Death);
        long t0 = world.CurrentDate.TotalHours;

        double g0 = PsycheMechanic.EmotionOf(world, widow, Emotion.Grief, t0);
        double month = PsycheMechanic.EmotionOf(world, widow, Emotion.Grief, t0 + 30 * Day);
        double year = PsycheMechanic.EmotionOf(world, widow, Emotion.Grief, t0 + 360 * Day);
        Assert.True(month > 0.8 * g0, $"um mês depois {month:0.00} de {g0:0.00}");
        Assert.InRange(year, 0.01, 0.3 * g0);

        var memory = Assert.Single(widow.Psyche!.Memories, m => m.Kind == LifeEventKind.SpouseDied);
        double twentyYears = PsycheMechanic.StrengthOf(world.NeedsRules.Psyche, memory, t0 + 20 * 360 * Day, 24);
        Assert.True(twentyYears > 10, $"força depois de 20 anos {twentyYears:0.0}");
        Assert.True(PsycheMechanic.AffectOf(world, widow, t0 + 3 * 360 * Day).IsCalm || year > 0);
    }

    [Fact]
    public void Low_emotional_stability_grieves_longer()
    {
        var world = World();
        var fragile = Person(world, Sex.Female, 40, p: Mid with { EmotionalStability = 5 });
        var steady = Person(world, Sex.Female, 40, p: Mid with { EmotionalStability = 95 });
        long t = world.CurrentDate.TotalHours;
        PsycheMechanic.Experience(world, fragile, LifeEventKind.SpouseDied, 99, t);
        PsycheMechanic.Experience(world, steady, LifeEventKind.SpouseDied, 98, t);

        long later = t + 180 * Day;
        Assert.True(PsycheMechanic.EmotionOf(world, fragile, Emotion.Grief, later)
            > 2 * PsycheMechanic.EmotionOf(world, steady, Emotion.Grief, later));
    }

    [Fact]
    public void A_devastating_loss_leaves_a_mark_on_emotional_stability_and_marriage_steadies()
    {
        var world = World();
        var npc = Person(world, Sex.Female, 30);
        long t = world.CurrentDate.TotalHours;
        PsycheMechanic.Experience(world, npc, LifeEventKind.ChildDied, 77, t);
        Assert.Equal(48, npc.Personality.EmotionalStability);
        PsycheMechanic.Experience(world, npc, LifeEventKind.Married, 78, t);
        Assert.Equal(49, npc.Personality.EmotionalStability);
        PsycheMechanic.Experience(world, npc, LifeEventKind.Hired, 0, t);
        Assert.Equal(49, npc.Personality.EmotionalStability);
    }

    [Fact]
    public void Memory_is_bounded_weak_memories_give_way_and_related_events_reinforce()
    {
        var world = World();
        var npc = Person(world, Sex.Male, 30);
        var rules = world.NeedsRules.Psyche;
        long t = world.CurrentDate.TotalHours;
        PsycheMechanic.Experience(world, npc, LifeEventKind.ChildDied, 500, t);
        for (int i = 0; i < 30; i++)
            PsycheMechanic.Experience(world, npc, LifeEventKind.AidGiven, 1000 + i, t + i * Day);

        Assert.Equal(rules.MemoryCapacity, npc.Psyche!.Memories.Count);
        Assert.Contains(npc.Psyche.Memories, m => m.Kind == LifeEventKind.ChildDied); // o marcante não cede ao trivial

        long later = t + 5 * 360 * Day;
        var before = npc.Psyche.Memories.First(m => m.Kind == LifeEventKind.ChildDied);
        double unrehearsed = PsycheMechanic.StrengthOf(rules, before, later, 24);
        PsycheMechanic.Experience(world, npc, LifeEventKind.ParentDied, 500, later); // a mesma pessoa de novo: reaviva
        var after = npc.Psyche.Memories.First(m => m.Kind == LifeEventKind.ChildDied);
        Assert.True(PsycheMechanic.StrengthOf(rules, after, later, 24) > unrehearsed);
    }

    [Fact]
    public void Grief_takes_working_days_away_and_the_calm_keep_their_routine()
    {
        var world = World();
        var mourner = Person(world, Sex.Female, 35);
        var calm = Person(world, Sex.Female, 35);
        long t = world.CurrentDate.TotalHours;
        PsycheMechanic.Experience(world, mourner, LifeEventKind.ChildDied, 77, t);

        int mournerWork = 0, calmWork = 0, laterWork = 0;
        for (int d = 0; d < 60; d++)
        {
            long now = t + d * Day + 8;
            if (BehaviorDecisionSystem.MoodShapesRoutine(world, Ctx(world), mourner, ActionType.Work, now) == ActionType.Work) mournerWork++;
            if (BehaviorDecisionSystem.MoodShapesRoutine(world, Ctx(world), calm, ActionType.Work, now) == ActionType.Work) calmWork++;
            long yearLater = now + 2 * 360 * Day;
            if (BehaviorDecisionSystem.MoodShapesRoutine(world, Ctx(world), mourner, ActionType.Work, yearLater) == ActionType.Work) laterWork++;
        }

        Assert.Equal(60, calmWork);
        Assert.InRange(mournerWork, 20, 54);
        Assert.True(laterWork > mournerWork, "o luto passa e o trabalho volta");
    }

    [Fact]
    public void Socializing_alone_does_not_satisfy_and_loneliness_builds_while_company_satisfies()
    {
        var world = World();
        long t = world.CurrentDate.TotalHours;
        var hermit = Person(world, Sex.Male, 40, at: new CellCoord(40, 40));
        hermit.SetSocial(30, t);
        BehaviorDecisionSystem.ApplySocialize(world, hermit, t, Ctx(world));
        Assert.Equal(30, hermit.SocialAt(t));
        Assert.True(PsycheMechanic.EmotionOf(world, hermit, Emotion.Loneliness, t) > 0);

        var a = Person(world, Sex.Male, 40, at: new CellCoord(5, 5));
        _ = Person(world, Sex.Female, 40, at: new CellCoord(5, 6));
        a.SetSocial(30, t);
        BehaviorDecisionSystem.ApplySocialize(world, a, t, Ctx(world));
        Assert.Equal(100, a.SocialAt(t));
    }

    [Fact]
    public void Who_lived_through_famine_wants_a_fuller_pantry()
    {
        var world = World();
        var npc = Person(world, Sex.Male, 40);
        long t = world.CurrentDate.TotalHours;
        Assert.Equal(1.0, PsycheMechanic.HoardFactor(world, npc, t));

        LifeEventIntake.OnHungerEpisodeEnded(world, npc, t - 5 * Day, t);
        double hoard = PsycheMechanic.HoardFactor(world, npc, t);
        Assert.InRange(hoard, 1.5, 2.0);
        Assert.True(PsycheMechanic.HoardFactor(world, npc, t + 10 * 360 * Day) < hoard); // a lembrança esmaece

        var brief = Person(world, Sex.Male, 40);
        LifeEventIntake.OnHungerEpisodeEnded(world, brief, t - 3, t); // três horas de fome não marcam
        Assert.Null(brief.Psyche);
    }

    [Fact]
    public void Who_was_helped_by_a_household_is_more_inclined_to_help_it_back()
    {
        var world = World();
        var helper = Person(world, Sex.Female, 40);
        var neighbour = Person(world, Sex.Male, 40);
        var stranger = Person(world, Sex.Male, 40);
        Ctx(world).LogEvent(WorldEventKind.AidGiven, $"{neighbour.Id.Value}|{helper.Id.Value}|20", sourceSystem: "population-mutual-aid");

        long t = world.CurrentDate.TotalHours;
        Assert.Contains(helper.Psyche!.Memories, m => m.Kind == LifeEventKind.AidReceived && m.Other == neighbour.Id.Value);
        Assert.Contains(neighbour.Psyche!.Memories, m => m.Kind == LifeEventKind.AidGiven);
        double toNeighbour = PsycheMechanic.Gratitude(world, helper, [neighbour], t);
        double toStranger = PsycheMechanic.Gratitude(world, helper, [stranger], t);
        Assert.True(toNeighbour > toStranger && toStranger > 0);
    }

    [Fact]
    public void Who_was_betrayed_trusts_less_and_avoids_the_betrayer()
    {
        var world = World();
        var betrayed = Person(world, Sex.Female, 30);
        var unfaithful = Person(world, Sex.Male, 30);
        var friend = Person(world, Sex.Female, 30);
        long t = world.CurrentDate.TotalHours;
        var before = PsycheMechanic.BondCeilings(world, betrayed, friend, t, 80, 80);
        Assert.Equal((80.0, 80.0), before);

        Ctx(world).LogEvent(WorldEventKind.AffairDiscovered, $"{betrayed.Id.Value}|{unfaithful.Id.Value}|{friend.Id.Value}|perdao", sourceSystem: "population-union");

        var withFriend = PsycheMechanic.BondCeilings(world, betrayed, friend, t, 80, 80);
        var withHim = PsycheMechanic.BondCeilings(world, betrayed, unfaithful, t, 80, 80);
        Assert.True(withFriend.Trust < 80 && withFriend.Affection == 80);
        Assert.True(withHim.Trust < withFriend.Trust && withHim.Affection < 80);
        Assert.True(SocialLeaning.Of(world, betrayed, t).AppealToward(world, betrayed, unfaithful, 50, t) < 0);
        Assert.True(PsycheMechanic.EmotionOf(world, betrayed, Emotion.Anger, t) > 0.5);
        Assert.True(PsycheMechanic.EmotionOf(world, unfaithful, Emotion.Shame, t) > 0.3);
    }

    [Fact]
    public void Psyche_survives_the_snapshot_and_an_old_snapshot_opens_without_it()
    {
        var (world, _) = ScenarioRunner.Create(3);
        var npc = world.AliveNpcIndex.Alive.OrderBy(n => n.Id.Value).First();
        PsycheMechanic.Experience(world, npc, LifeEventKind.Married, 42, world.CurrentDate.TotalHours);
        string hash = WorldSnapshot.CanonicalHash(world);

        var restored = WorldSnapshot.Deserialize(WorldSnapshot.Serialize(world));
        Assert.Equal(hash, WorldSnapshot.CanonicalHash(restored));
        var back = restored.FindNpc(npc.Id)!;
        Assert.Equal(npc.Psyche!.Memories, back.Psyche!.Memories);
        Assert.Equal(npc.Psyche.Joy, back.Psyche.Joy);

        var node = System.Text.Json.Nodes.JsonNode.Parse(WorldSnapshot.Serialize(world))!;
        foreach (var n in node["Npcs"]!.AsArray()) n!.AsObject().Remove("Psyche");
        var old = WorldSnapshot.Deserialize(node.ToJsonString());
        Assert.Null(old.FindNpc(npc.Id)!.Psyche);
    }

    [Fact]
    public void Needs_restocking_uses_the_famine_memory()
    {
        var (world, _) = ScenarioRunner.Create(3);
        var economy = world.EconomyRules;
        var market = MarketIndex.BuildForTick(world);
        Npc? probe = null;
        foreach (var npc in world.AliveNpcIndex.Alive.OrderBy(n => n.Id.Value))
            if (npc.Household is { } h && world.FindHousehold(h) is { Members.Count: >= 3 } && npc.AgeYears(world.CurrentDate) >= 18) { probe = npc; break; }
        Assert.NotNull(probe);
        probe!.CreditWallet(new Money(500));
        var home = world.FindHousehold(probe!.Household!.Value)!;
        double target = BehaviorDecisionSystem.ProvisionTargetFor(world, probe, market);
        long level = (long)Math.Ceiling(target * 0.5 * home.Members.Count); // no limiar de reposição: ainda não repõe
        foreach (var id in new[] { economy.FoodResourceId, economy.WaterResourceId })
        {
            var r = new ResourceType(id);
            long have = home.Stock.GetValueOrDefault(r);
            if (have > level) home.Withdraw(r, have - level); else if (have < level) home.Deposit(r, level - have);
        }

        Assert.False(BehaviorDecisionSystem.NeedsRestocking(world, probe, market));
        long t = world.CurrentDate.TotalHours;
        LifeEventIntake.OnHungerEpisodeEnded(world, probe, t - 6 * Day, t);
        Assert.True(level < target * 0.5 * home.Members.Count * PsycheMechanic.HoardFactor(world, probe, t));
        Assert.True(BehaviorDecisionSystem.NeedsRestocking(world, probe, market));
    }
}
