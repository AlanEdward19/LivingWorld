using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Population;

/// <summary>Fase 7, T15 (FAM-12): <see cref="MarriageSystem.Marry"/> forma household novo com
/// estoque inicial e dissolve households anteriores vazios.</summary>
public class MarriageSystemTests
{
    private static readonly WorldCalendar Calendar = new(24, 30, 12);

    private static readonly Personality SomePersonality =
        Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    private static FamilyRules RulesWithMarriageStock(long foodAmount = 200) =>
        FamilyRules.Create(
            relationshipDeltas: FullDeltas(),
            decayPerDay: 0.5,
            contactLossThresholdDays: 30,
            neutralAxisValue: 50,
            attractionWeights: Enum.GetValues<AttractionFactor>().ToDictionary(f => f, _ => 1.0),
            courtshipThreshold: 0.6,
            courtshipDurationDays: 90,
            marriageInitialStock: new Dictionary<int, long> { [1] = foodAmount },
            conceptionHealthFloor: 40,
            conceptionRelationshipFloor: 40,
            conceptionResourceFloor: new Dictionary<int, long> { [1] = 10 },
            maternalDeathRisk: 0.02,
            infantDeathRisk: 0.05,
            vitalityMotherWeight: 0.5,
            vitalityFatherWeight: 0.5,
            vitalityMutationStdDev: 5,
            vitalityMortalityWeight: 0.3,
            upbringingWealthWeight: 0.3,
            environmentalWealthChannelEnabled: false,
            neutralDriftEnabled: false,
            vitalityMortalitySelectionEnabled: true).Value!;

    private static Dictionary<(RelationshipEventType, RelationshipAxis), double> FullDeltas()
    {
        var deltas = new Dictionary<(RelationshipEventType, RelationshipAxis), double>();
        foreach (var type in Enum.GetValues<RelationshipEventType>())
            foreach (var axis in Enum.GetValues<RelationshipAxis>())
                deltas[(type, axis)] = 0.0;
        return deltas;
    }

    private static WorldState BuildWorld(FamilyRules rules) => new(
        Calendar, seed: 1, ScenarioRunner.DefaultMap(1), ScenarioRunner.DefaultPopulationCatalog,
        ScenarioRunner.DefaultPopulationRules, ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
        ScenarioRunner.DefaultLifeStageRules, familyRules: rules);

    private static Npc MakeNpc(WorldState world, Sex sex, NpcId? id = null)
    {
        var npcId = id ?? world.NextNpcIdAndAdvance();
        var npc = new Npc(
            npcId, $"npc-{npcId.Value}", sex, WorldDate.Epoch(Calendar), new CultureId(1), new CellCoord(0, 0),
            motherId: null, fatherId: null, household: null, health: 100, personality: SomePersonality,
            profession: new ProfessionType(1), currentLocation: new CellCoord(0, 0));
        world.AddNpc(npc);
        return npc;
    }

    private static Household SoloHousehold(WorldState world, Npc npc)
    {
        var household = new Household(world.NextHouseholdIdAndAdvance(), npc.CurrentLocation, npc.Id, [npc.Id]);
        world.AddHousehold(household);
        npc.JoinHousehold(household.Id);
        return household;
    }

    private sealed class EventSink : IWorldEventSink
    {
        public List<(WorldEventKind Kind, string Payload)> Events { get; } = [];

        public void Record(WorldEvent evt) => Events.Add((evt.Kind, evt.Payload!));
    }

    [Fact]
    public void Marry_creates_new_household_with_marriage_initial_stock()
    {
        var rules = RulesWithMarriageStock(foodAmount: 150);
        var world = BuildWorld(rules);
        var sink = new EventSink();
        var ctx = new TickContext(world, world.Rng, world.Scheduler, sink);
        var a = MakeNpc(world, Sex.Male);
        var b = MakeNpc(world, Sex.Female);

        MarriageSystem.Marry(world, ctx, a, b);

        Assert.NotNull(a.Household);
        Assert.Equal(a.Household, b.Household);
        var home = world.FindHousehold(a.Household!.Value)!;
        Assert.Equal(150, home.Stock.GetValueOrDefault(new ResourceType(1)));
        Assert.Contains(a.Id, home.Members);
        Assert.Contains(b.Id, home.Members);
    }

    [Fact]
    public void Marry_dissolves_previous_household_when_it_becomes_empty()
    {
        var world = BuildWorld(RulesWithMarriageStock());
        var ctx = new TickContext(world, world.Rng, world.Scheduler);
        var a = MakeNpc(world, Sex.Male);
        var b = MakeNpc(world, Sex.Female);
        var oldHomeA = SoloHousehold(world, a);
        var oldHomeB = SoloHousehold(world, b);

        MarriageSystem.Marry(world, ctx, a, b);

        Assert.DoesNotContain(world.Households, h => h.Id == oldHomeA.Id);
        Assert.DoesNotContain(world.Households, h => h.Id == oldHomeB.Id);
    }

    [Fact]
    public void Marry_sets_spouse_pointers_on_both_npcs()
    {
        var world = BuildWorld(RulesWithMarriageStock());
        var ctx = new TickContext(world, world.Rng, world.Scheduler);
        var a = MakeNpc(world, Sex.Male);
        var b = MakeNpc(world, Sex.Female);

        MarriageSystem.Marry(world, ctx, a, b);

        Assert.Equal(b.Id, a.Spouse);
        Assert.Equal(a.Id, b.Spouse);
    }

    [Fact]
    public void Marry_logs_world_event_with_both_ids()
    {
        var world = BuildWorld(RulesWithMarriageStock());
        var sink = new EventSink();
        var ctx = new TickContext(world, world.Rng, world.Scheduler, sink);
        var a = MakeNpc(world, Sex.Male, id: new NpcId(10));
        var b = MakeNpc(world, Sex.Female, id: new NpcId(20));

        MarriageSystem.Marry(world, ctx, a, b);

        Assert.Contains((WorldEventKind.Marriage, "10|20"), sink.Events);
    }

    [Fact]
    public void Marry_leaves_non_empty_previous_household_intact()
    {
        var world = BuildWorld(RulesWithMarriageStock());
        var ctx = new TickContext(world, world.Rng, world.Scheduler);
        var roommate = MakeNpc(world, Sex.Male);
        var a = MakeNpc(world, Sex.Male);
        var b = MakeNpc(world, Sex.Female);
        var shared = new Household(
            world.NextHouseholdIdAndAdvance(), a.CurrentLocation, roommate.Id, [roommate.Id, a.Id]);
        world.AddHousehold(shared);
        roommate.JoinHousehold(shared.Id);
        a.JoinHousehold(shared.Id);
        SoloHousehold(world, b);

        MarriageSystem.Marry(world, ctx, a, b);

        Assert.Contains(world.Households, h => h.Id == shared.Id);
        Assert.Equal(shared.Id, roommate.Household);
        Assert.DoesNotContain(a.Id, world.FindHousehold(shared.Id)!.Members);
    }
}
