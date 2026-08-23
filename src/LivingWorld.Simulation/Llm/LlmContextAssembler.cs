using LivingWorld.Domain;
using LivingWorld.Domain.Llm;
using LivingWorld.Simulation.History;

namespace LivingWorld.Simulation;

/// <summary>Monta o <see cref="LlmContext"/> de um turno de conversa (Fase 11, LLM-05/06) — só a
/// partir do conhecimento do próprio NPC: <see cref="NpcBeliefQuery"/> para crença (nunca
/// <c>HistoryTruthQuery</c>/<c>WorldState</c> global) e <see cref="MemoryRecall"/> (roadmap item
/// 2) para memória recuperada. Quem monta o resumo/ações permitidas fora da crença
/// (personalidade, profissão, `AllowedActions(npc, ctx)`) é responsabilidade do chamador — este
/// assembler só liga crença + memória ao transporte (<see cref="LlmContext"/>) sem reimplementar
/// nada que já exista.</summary>
public static class LlmContextAssembler
{
    public static LlmContext Assemble(
        WorldState world, Npc npc, ConversationSession session, string playerUtterance,
        IReadOnlyList<string> allowedIntents, IReadOnlyList<string> allowedActions,
        LlmRules? llmRules = null, int recallCount = 5)
    {
        var beliefFacts = NpcBeliefQuery.BeliefsOf(world, npc.Id);
        string summary = $"{npc.Name}, {npc.Profession}, {npc.City}";

        var rules = llmRules ?? LlmRules.Default;
        var recalledMemories = MemoryRecall.Recall(world, npc.Id, playerUtterance, recallCount, rules);
        var relevantMemories = recalledMemories.Select(m => new MemoryCandidate(m.Content, m.Importance)).ToList();

        return new LlmContext(
            NpcKnowledgeSummary: summary,
            PlayerUtterance: playerUtterance,
            AllowedIntents: allowedIntents,
            BeliefFacts: beliefFacts,
            RelevantMemories: relevantMemories,
            AllowedActions: allowedActions,
            SessionId: session.SessionId,
            SessionOpenedAtTick: session.OpenedAtTick);
    }

    /// <summary>Mesmo funil, para o caso de uso de narrativa (Fase 12, NARR-12) — a LLM só
    /// reescreve a prosa dos claims já aprovados (ancorados em evento), sem sessão de NPC nem
    /// crença/memória envolvidas.</summary>
    public static LlmContext AssembleForNarrative(IReadOnlyList<string> approvedClaimTexts) =>
        new(
            NpcKnowledgeSummary: string.Join(" ", approvedClaimTexts),
            PlayerUtterance: "narrar",
            AllowedIntents: []);
}
