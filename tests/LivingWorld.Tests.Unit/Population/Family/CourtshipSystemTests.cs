using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Population;

/// <summary>Fase 7, T16 (FAM-06..11): <see cref="CourtshipSystem"/> — gates, score, cortejo
/// agendado e conclusão via <see cref="MarriageSystem"/>.</summary>
public class CourtshipSystemTests
{
    private static readonly WorldCalendar Calendar = new(24, 30, 12);

    private static readonly Personality SomePersonality =
        Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    private static readonly PopulationRules PopulationRules = ScenarioRunner.DefaultPopulationRules;

    private sealed class EventSink : IWorldEventSink
    {
        public List<(WorldEventKind Kind, string Payload)> Events { get; } = [];

        public void Record(WorldEvent evt) => Events.Add((evt.Kind, evt.Payload!));
    }

    private static FamilyRules RulesWith(
        double courtshipThreshold = 0.5,
        bool neutralDriftEnabled = false,
        IReadOnlyDictionary<AttractionFactor, double>? attractionWeights = null) =>
        FamilyRules.Create(
            relationshipDeltas: ZeroDeltas(),
            decayPerDay: 0,
            contactLossThresholdDays: 30,
            neutralAxisValue: 50,
            attractionWeights: attractionWeights
                ?? Enum.GetValues<AttractionFactor>().ToDictionary(f => f, _ => 1.0),
            courtshipThreshold: courtshipThreshold,
            courtshipDurationDays: 10,
            marriageInitialStock: new Dictionary<int, long> { [1] = 100 },
            conceptionHealthFloor: 40,
            conceptionRelationshipFloor: 40,
            conceptionResourceFloor: new Dictionary<int, long> { [1] = 10 },
            maternalDeathRisk: 0,
            infantDeathRisk: 0,
            vitalityMotherWeight: 0.5,
            vitalityFatherWeight: 0.5,
            vitalityMutationStdDev: 0,
            vitalityMortalityWeight: 0,
            upbringingWealthWeight: 0,
            environmentalWealthChannelEnabled: false,
            neutralDriftEnabled: neutralDriftEnabled,
            vitalityMortalitySelectionEnabled: true).Value!;

    private static Dictionary<(RelationshipEventType, RelationshipAxis), double> ZeroDeltas()
    {
        var deltas = new Dictionary<(RelationshipEventType, RelationshipAxis), double>();
        foreach (var type in Enum.GetValues<RelationshipEventType>())
            foreach (var axis in Enum.GetValues<RelationshipAxis>())
                deltas[(type, axis)] = 0;
        return deltas;
    }

    private static WorldState BuildWorld(FamilyRules familyRules, ulong seed = 1)
    {
        var world = new WorldState(
            Calendar, seed, ScenarioRunner.DefaultMap(seed), ScenarioRunner.DefaultPopulationCatalog,
            PopulationRules, ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules, familyRules: familyRules);
        world.CurrentDate = WorldDate.Epoch(Calendar).AddYears(30);
        return world;
    }

    private static Npc MakeAdult(
        WorldState world,
        Sex sex,
        NpcId id,
        NpcId? motherId = null,
        NpcId? fatherId = null,
        int ageYears = 25)
    {
        var birth = world.CurrentDate.AddYears(-ageYears);
        var npc = new Npc(
            id, $"npc-{id.Value}", sex, birth, new CultureId(1), new CellCoord(0, 0),
            motherId, fatherId, household: null, health: 100, personality: SomePersonality,
            profession: new ProfessionType(1), currentLocation: new CellCoord(0, 0));
        world.AddNpc(npc);
        return npc;
    }

    private static void LinkRelationship(WorldState world, Npc a, Npc b, double trustDelta = 80)
    {
        long now = world.CurrentDate.TotalHours;
        var deltas = ZeroDeltas();
        deltas[(RelationshipEventType.Cohabitation, RelationshipAxis.Trust)] = trustDelta;
        var linkRules = world.FamilyRules with { RelationshipDeltas = deltas };

        var ab = world.GetOrCreateRelationship(new RelationshipKey(a.Id, b.Id), now);
        ab.ApplyEvent(RelationshipEventType.Cohabitation, linkRules);
        var ba = world.GetOrCreateRelationship(new RelationshipKey(b.Id, a.Id), now);
        ba.ApplyEvent(RelationshipEventType.Cohabitation, linkRules);
    }

    [Fact]
    public void Reject_sibling_pair_returns_Incesto()
    {
        var mother = new NpcId(100);
        var father = new NpcId(101);
        var world = BuildWorld(RulesWith());
        var brother = MakeAdult(world, Sex.Male, new NpcId(1), mother, father);
        var sister = MakeAdult(world, Sex.Female, new NpcId(2), mother, father);

        Assert.Equal(CourtshipRejectionReason.Incesto, CourtshipSystem.Reject(brother, sister, world.CurrentDate, PopulationRules));
    }

    [Fact]
    public void Tick_rejects_siblings_with_Incesto_even_when_relationship_is_strong()
    {
        var mother = new NpcId(100);
        var father = new NpcId(101);
        var rules = RulesWith(courtshipThreshold: 0.0);
        var world = BuildWorld(rules);
        var brother = MakeAdult(world, Sex.Male, new NpcId(1), mother, father);
        var sister = MakeAdult(world, Sex.Female, new NpcId(2), mother, father);
        LinkRelationship(world, brother, sister, trustDelta: 50);

        var sink = new EventSink();
        var ctx = new TickContext(world, world.Rng, world.Scheduler, sink);
        new CourtshipSystem().Tick(world, ctx);

        Assert.Contains(
            (WorldEventKind.CourtshipRejected, $"Incesto|{brother.Id.Value}|{sister.Id.Value}"),
            sink.Events);
        Assert.Null(brother.CourtingWith);
    }

    [Fact]
    public void Tick_rejects_pair_outside_fertility_window_with_ForaDaFaixaEtaria()
    {
        var rules = RulesWith(courtshipThreshold: 0.0);
        var world = BuildWorld(rules);
        var man = MakeAdult(world, Sex.Male, new NpcId(1), ageYears: 50);
        var woman = MakeAdult(world, Sex.Female, new NpcId(2), ageYears: 25);
        LinkRelationship(world, man, woman);

        var sink = new EventSink();
        new CourtshipSystem().Tick(world, new TickContext(world, world.Rng, world.Scheduler, sink));

        Assert.Contains(
            (WorldEventKind.CourtshipRejected, $"ForaDaFaixaEtaria|{man.Id.Value}|{woman.Id.Value}"),
            sink.Events);
    }

    [Fact]
    public void Tick_rejects_low_attraction_with_SemAfinidade()
    {
        var rules = RulesWith(
            courtshipThreshold: 0.99,
            attractionWeights: Enum.GetValues<AttractionFactor>().ToDictionary(f => f, _ => 0.0));
        var world = BuildWorld(rules);
        var man = MakeAdult(world, Sex.Male, new NpcId(1));
        var woman = MakeAdult(world, Sex.Female, new NpcId(2));
        LinkRelationship(world, man, woman);

        var sink = new EventSink();
        new CourtshipSystem().Tick(world, new TickContext(world, world.Rng, world.Scheduler, sink));

        Assert.Contains(
            (WorldEventKind.CourtshipRejected, $"SemAfinidade|{man.Id.Value}|{woman.Id.Value}"),
            sink.Events);
    }

    [Fact]
    public void Tick_starts_courtship_and_schedules_completion_when_attraction_passes()
    {
        var rules = RulesWith(courtshipThreshold: 0.0);
        var world = BuildWorld(rules);
        var man = MakeAdult(world, Sex.Male, new NpcId(1));
        var woman = MakeAdult(world, Sex.Female, new NpcId(2));
        LinkRelationship(world, man, woman);

        var sink = new EventSink();
        var scheduler = new EventScheduler();
        var ctx = new TickContext(world, world.Rng, scheduler, sink);
        new CourtshipSystem().Tick(world, ctx);

        Assert.Equal(woman.Id, man.CourtingWith);
        Assert.Equal(man.Id, woman.CourtingWith);
        Assert.Contains((WorldEventKind.CourtshipStarted, "1|2"), sink.Events);
        Assert.NotEmpty(scheduler.Snapshot());
    }

    [Fact]
    public void HandleEvent_logs_CourtshipSucceeded_before_Marriage()
    {
        var rules = RulesWith(courtshipThreshold: 0.0);
        var world = BuildWorld(rules);
        var man = MakeAdult(world, Sex.Male, new NpcId(1));
        var woman = MakeAdult(world, Sex.Female, new NpcId(2));
        man.StartCourtship(woman.Id);
        woman.StartCourtship(man.Id);

        var sink = new EventSink();
        var evt = new ScheduledEvent(1, world.CurrentDate.TotalHours, CourtshipSystem.SystemName, "1|2");
        new CourtshipSystem().HandleEvent(world, new TickContext(world, world.Rng, world.Scheduler, sink), evt);

        int succeeded = sink.Events.FindIndex(e => e.Kind == WorldEventKind.CourtshipSucceeded);
        int marriage = sink.Events.FindIndex(e => e.Kind == WorldEventKind.Marriage);
        Assert.True(succeeded >= 0);
        Assert.True(marriage >= 0);
        Assert.True(succeeded < marriage);
        Assert.Equal(man.Id, woman.Spouse);
    }

    [Fact]
    public void HandleEvent_is_silent_no_op_when_one_spouse_died_and_clears_survivor_courtship()
    {
        var world = BuildWorld(RulesWith());
        var man = MakeAdult(world, Sex.Male, new NpcId(1));
        var woman = MakeAdult(world, Sex.Female, new NpcId(2));
        man.StartCourtship(woman.Id);
        woman.StartCourtship(man.Id);
        woman.Die(world.CurrentDate);

        var sink = new EventSink();
        var evt = new ScheduledEvent(1, world.CurrentDate.TotalHours, CourtshipSystem.SystemName, "1|2");
        new CourtshipSystem().HandleEvent(world, new TickContext(world, world.Rng, world.Scheduler, sink), evt);

        Assert.Empty(sink.Events);
        Assert.Null(man.CourtingWith);
        Assert.Null(man.Spouse);
    }

    [Fact]
    public void HandleEvent_is_no_op_when_one_npc_married_elsewhere()
    {
        var world = BuildWorld(RulesWith());
        var man = MakeAdult(world, Sex.Male, new NpcId(1));
        var woman = MakeAdult(world, Sex.Female, new NpcId(2));
        var other = MakeAdult(world, Sex.Female, new NpcId(3));
        man.StartCourtship(woman.Id);
        woman.StartCourtship(man.Id);
        woman.Marry(other.Id);

        var sink = new EventSink();
        var evt = new ScheduledEvent(1, world.CurrentDate.TotalHours, CourtshipSystem.SystemName, "1|2");
        new CourtshipSystem().HandleEvent(world, new TickContext(world, world.Rng, world.Scheduler, sink), evt);

        Assert.Empty(sink.Events);
        Assert.Null(man.CourtingWith);
        Assert.Null(man.Spouse);
    }

    [Fact]
    public void NeutralDriftEnabled_forms_pair_without_attraction_threshold()
    {
        var rules = RulesWith(courtshipThreshold: 1.0, neutralDriftEnabled: true);
        var world = BuildWorld(rules);
        var man = MakeAdult(world, Sex.Male, new NpcId(1));
        var woman = MakeAdult(world, Sex.Female, new NpcId(2));
        LinkRelationship(world, man, woman);

        var sink = new EventSink();
        new CourtshipSystem().Tick(world, new TickContext(world, world.Rng, world.Scheduler, sink));

        Assert.Equal(woman.Id, man.CourtingWith);
        Assert.Contains((WorldEventKind.CourtshipStarted, "1|2"), sink.Events);
    }

    [Fact]
    public void Tick_skips_npc_with_no_relationship_candidates_without_error()
    {
        var world = BuildWorld(RulesWith());
        MakeAdult(world, Sex.Male, new NpcId(1));

        var sink = new EventSink();
        new CourtshipSystem().Tick(world, new TickContext(world, world.Rng, world.Scheduler, sink));

        Assert.Empty(sink.Events);
    }

    [Fact]
    public void AttractionScore_is_higher_when_cultures_match()
    {
        var rules = RulesWith();
        var world = BuildWorld(rules);
        var a = MakeAdult(world, Sex.Male, new NpcId(1));
        var sameCulture = MakeAdult(world, Sex.Female, new NpcId(2));
        var otherCulture = new Npc(
            new NpcId(3), "other", Sex.Female, world.CurrentDate.AddYears(-25), new CultureId(2),
            new CellCoord(0, 0), null, null, null, 100, SomePersonality, new ProfessionType(1),
            new CellCoord(0, 0));
        world.AddNpc(otherCulture);

        double same = CourtshipSystem.AttractionScore(
            a, sameCulture, null, null, rules, PopulationRules, world.CurrentDate);
        double diff = CourtshipSystem.AttractionScore(
            a, otherCulture, null, null, rules, PopulationRules, world.CurrentDate);

        Assert.True(same > diff);
    }
}
