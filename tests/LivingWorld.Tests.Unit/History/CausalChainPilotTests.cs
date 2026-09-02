using LivingWorld.Domain;
using LivingWorld.Simulation;
using LivingWorld.Simulation.History;

namespace LivingWorld.Tests.History;

/// <summary>COH-05: call sites piloto com CauseEventId/SourceSystem reais + determinismo.</summary>
public class CausalChainPilotTests
{
    [Fact]
    public void Extraordinary_use_cost_effect_chain_resolves_to_attempt_root()
    {
        var (world, carrier, target, _) = WorldWithPower();
        var sink = new RecordingSink();
        var ctx = new TickContext(world, world.Rng, world.Scheduler, sink);

        var result = ExtraordinaryInvocationEngine.Invoke(
            world, ctx, new ExtraordinaryInvocation(41, carrier.Id, "test-power", target.Id));

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(3, sink.Events.Count);
        Assert.Equal("ExtraordinaryInvocationEngine", sink.Events[0].SourceSystem);
        Assert.Null(sink.Events[0].CauseEventId);
        Assert.Equal(sink.Events[0].EventId, sink.Events[1].CauseEventId);
        Assert.Equal(sink.Events[1].EventId, sink.Events[2].CauseEventId);
        Assert.Equal(
            sink.Events[0].EventId,
            CausalProvenance.ResolveRootCauseEventId(sink.Events, sink.Events[2].EventId, CausalRules.Default));
    }

    [Fact]
    public void Same_seed_produces_identical_causal_chain()
    {
        static List<(long EventId, long? CauseEventId, string Source, WorldEventKind Kind)> Run()
        {
            var (world, carrier, target, _) = WorldWithPower();
            var sink = new RecordingSink();
            var ctx = new TickContext(world, world.Rng, world.Scheduler, sink);
            ExtraordinaryInvocationEngine.Invoke(
                world, ctx, new ExtraordinaryInvocation(41, carrier.Id, "test-power", target.Id));
            return sink.Events
                .Select(e => (e.EventId, e.CauseEventId, e.SourceSystem, e.Kind))
                .ToList();
        }

        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void BookRediscovery_logs_real_SourceSystem()
    {
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, seed: 3, ScenarioRunner.DefaultMap(3),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules,
            historyRules: HistoryRules.Default);
        var book = new Book(
            world.NextBookIdAndAdvance(),
            CarriesReportId: new ReportId(1),
            CopyOfBookId: null,
            Lost: true,
            LostAtTick: 2,
            RediscoveredAtTick: null);
        world.AddBook(book);
        var sink = new RecordingSink();
        var ctx = new TickContext(world, world.Rng, world.Scheduler, sink);

        var result = BookRediscoverySystem.OnRediscovered(world, ctx, book.Id);

        Assert.True(result.IsSuccess, result.Error);
        var evt = Assert.Single(sink.Events);
        Assert.Equal(WorldEventKind.BookRediscovered, evt.Kind);
        Assert.Equal("BookRediscoverySystem", evt.SourceSystem);
        Assert.Null(evt.CauseEventId);
    }

    [Fact]
    public void StillBirth_from_NatalitySystem_logs_real_SourceSystem()
    {
        var calendar = new WorldCalendar(24, 30, 12);
        var table = LifeTable.Create(90, [new LifeTableBracket(0, 89, 0.01)]).Value!;
        var popRules = PopulationRules.Create(
            table, fertilityMinAge: 16, fertilityMaxAge: 45, annualConceptionChance: 1.0, gestationDays: 10).Value!;
        var deltas = new Dictionary<(RelationshipEventType, RelationshipAxis), double>();
        foreach (var type in Enum.GetValues<RelationshipEventType>())
            foreach (var axis in Enum.GetValues<RelationshipAxis>())
                deltas[(type, axis)] = 0;
        var family = FamilyRules.Create(
            relationshipDeltas: deltas, decayPerDay: 0, contactLossThresholdDays: 30, neutralAxisValue: 50,
            attractionWeights: new Dictionary<AttractionFactor, double>(), courtshipThreshold: 0.5,
            courtshipDurationDays: 10, marriageInitialStock: new Dictionary<int, long>(),
            conceptionHealthFloor: 0, conceptionRelationshipFloor: 0,
            conceptionResourceFloor: new Dictionary<int, long>(), maternalDeathRisk: 0, infantDeathRisk: 1.0,
            vitalityMotherWeight: 0.5, vitalityFatherWeight: 0.5, vitalityMutationStdDev: 0,
            vitalityMortalityWeight: 0, upbringingWealthWeight: 0.2, environmentalWealthChannelEnabled: false,
            neutralDriftEnabled: false, vitalityMortalitySelectionEnabled: true).Value!;

        var world = new WorldState(
            calendar, 7, ScenarioRunner.DefaultMap(7),
            ScenarioRunner.DefaultPopulationCatalog, popRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules, familyRules: family);
        var location = new CellCoord(1, 1);
        var personality = Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;
        var mother = new Npc(
            new NpcId(1), "mother", Sex.Female, WorldDate.Epoch(calendar).AddYears(-20),
            ScenarioRunner.DefaultCulture, location, null, null, null, health: 100,
            personality: personality, profession: ProfessionType.None, currentLocation: location);
        var father = new Npc(
            new NpcId(2), "father", Sex.Male, WorldDate.Epoch(calendar).AddYears(-22),
            ScenarioRunner.DefaultCulture, location, null, null, null, health: 100,
            personality: personality, profession: ProfessionType.None, currentLocation: location);
        var household = new Household(new HouseholdId(1), location, mother.Id, [mother.Id, father.Id]);
        mother.JoinHousehold(household.Id);
        father.JoinHousehold(household.Id);
        world.AddNpc(mother);
        world.AddNpc(father);
        world.AddHousehold(household);
        world.AdvanceNpcIdTo(3);
        world.AdvanceHouseholdIdTo(2);
        world.CurrentDate = WorldDate.Epoch(calendar).AddYears(20);

        var sink = new RecordingSink();
        var ctx = new TickContext(world, world.Rng, world.Scheduler, sink);
        var evt = ctx.ScheduleEvent(
            world.CurrentDate.TotalHours, NatalitySystem.SystemName,
            $"{mother.Id.Value}|{father.Id.Value}|{household.Id.Value}|100");
        new NatalitySystem().HandleEvent(world, ctx, evt);

        var stillBirth = Assert.Single(sink.Events, e => e.Kind == WorldEventKind.StillBirth);
        Assert.Equal("NatalitySystem", stillBirth.SourceSystem);
        Assert.NotEqual("Unknown", stillBirth.SourceSystem);
    }

    private static (WorldState World, Npc Carrier, Npc Target, Household Home) WorldWithPower()
    {
        var descriptor = new PowerDescriptor(
            "test-power", "test-source", ["npc.health:15"], "Active",
            ["household.resource.9:2"], "Guaranteed", [], [], [], []);
        var state = new ExtraordinaryCarrierState(
            new NpcId(1), [descriptor.Id], true, "active",
            new ExtraordinaryAppearanceState(1, "", ""), null, 1);
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, 42, ScenarioRunner.DefaultMap(42),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules,
            extraordinary: new ExtraordinaryScenarioData(true, [descriptor]), extraordinaryCarriers: [state]);
        var carrier = new Npc(
            new NpcId(1), "carrier", Sex.Male, WorldDate.Epoch(ScenarioRunner.DefaultCalendar),
            ScenarioRunner.DefaultCulture, new CellCoord(0, 0), null, null, new HouseholdId(1), health: 100,
            personality: Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!,
            profession: ProfessionType.None, currentLocation: new CellCoord(0, 0));
        var target = new Npc(
            new NpcId(2), "target", Sex.Male, WorldDate.Epoch(ScenarioRunner.DefaultCalendar),
            ScenarioRunner.DefaultCulture, new CellCoord(0, 0), null, null, null, health: 50,
            personality: Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!,
            profession: ProfessionType.None, currentLocation: new CellCoord(0, 0));
        var home = new Household(new HouseholdId(1), new CellCoord(0, 0), carrier.Id, [carrier.Id],
            new Dictionary<ResourceType, long> { [new ResourceType(9)] = 5 });
        world.AddNpc(carrier);
        world.AddNpc(target);
        world.AddHousehold(home);
        return (world, carrier, target, home);
    }

    private sealed class RecordingSink : IWorldEventSink
    {
        public List<WorldEvent> Events { get; } = [];
        public void Record(WorldEvent evt) => Events.Add(evt);
    }
}
