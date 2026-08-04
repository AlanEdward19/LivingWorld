using LivingWorld.Domain;
using LivingWorld.Domain.Llm;
using LivingWorld.Simulation;
using LivingWorld.Simulation.History;

namespace LivingWorld.Tests.Llm;

/// <summary>Fase 11, T4 (LLM-05/06): <see cref="LlmContextAssembler"/> liga <see
/// cref="NpcBeliefQuery"/> ao transporte <c>LlmContext</c> — nunca usa <c>HistoryTruthQuery</c>
/// nem estado global do mundo para montar crença.</summary>
public class LlmContextAssemblerTests
{
    private static HistoryRules ForcedMoralizationRules => HistoryRules.Create(
        enabled: true,
        skeletonSignificanceThreshold: 0.5,
        canonSizePerCommunity: 10,
        mediumFidelityByType: new Dictionary<TransmissionMediumType, MediumFidelity>
        {
            [TransmissionMediumType.OralTradition] = new(1.0, 10, DeathConditionType.Decay),
        },
        operatorProbability: new Dictionary<DistortionOperator, double> { [DistortionOperator.Moralization] = 1.0 },
        importanceWeight: 1,
        transmissibilityWeight: 0,
        recencyWeight: 0).Value!;

    [Fact]
    public void Assemble_carries_npc_belief_session_and_intents_without_touching_raw_truth()
    {
        var rules = ForcedMoralizationRules;
        var (world, _) = ScenarioRunner.Create(3, historyRules: rules);
        var npc = world.Npcs[0];
        var city = new City(npc.City, ScenarioRunner.DefaultVillageLocation, 0, null, AggregatePopulationPool.Empty);
        world.AddCity(city);

        var fact = new Fact(new FactId(1), 5, WorldEventKind.Marriage, [npc.Id, new NpcId(2)], city.Id, 0.8, "truth-only-payload");
        world.AddFact(fact);
        var report = new ReportState(
            world.NextReportIdAndAdvance(), fact.Id, city.Id, TransmissionMediumType.OralTradition,
            HopCount: 1, Weight: fact.Significance, CreatedAtTick: 10, LastHopTick: 10);
        world.RegisterReport(report);
        CanonSlotManager.Admit(city, report, rules, nowTick: 20);

        var session = new ConversationSession(SessionId: 7, npc.Id, OpenedAtTick: 20, LastTurnTick: 20, IsActive: true);

        var context = LlmContextAssembler.Assemble(
            world, npc, session, playerUtterance: "oi",
            allowedIntents: ["greet"], allowedActions: ["greet"]);

        Assert.Equal("oi", context.PlayerUtterance);
        Assert.Equal(7, context.SessionId);
        Assert.Equal(20, context.SessionOpenedAtTick);
        Assert.NotNull(context.BeliefFacts);
        Assert.Single(context.BeliefFacts!);
        Assert.DoesNotContain(fact.Payload, context.BeliefFacts![0]);
        Assert.DoesNotContain(fact.Payload, context.NpcKnowledgeSummary);
    }

    /// <summary>Fase 11, roadmap item 2: <c>RelevantMemories</c> não é mais sempre nulo — vem de
    /// <see cref="MemoryRecall"/> sobre a memória real do próprio NPC.</summary>
    [Fact]
    public void Assemble_populates_relevant_memories_from_recall_instead_of_always_null()
    {
        var (world, _) = ScenarioRunner.Create(3);
        var npc = world.Npcs[0];
        world.AddNpcMemory(
            npc.Id, MemoryCategory.Episodic, "festa da colheita na vila", importance: 70, originTick: 0,
            participants: [npc.Id], location: npc.CurrentLocation, canonicalImportanceThreshold: 50);
        var session = new ConversationSession(SessionId: 1, npc.Id, OpenedAtTick: 0, LastTurnTick: 0, IsActive: true);

        var context = LlmContextAssembler.Assemble(
            world, npc, session, playerUtterance: "colheita",
            allowedIntents: ["greet"], allowedActions: ["greet"]);

        Assert.NotNull(context.RelevantMemories);
        Assert.Contains(context.RelevantMemories!, m => m.Event == "festa da colheita na vila" && m.Importance == 70);
    }
}
