using LivingWorld.AI;
using LivingWorld.Domain;
using LivingWorld.Simulation.History;

namespace LivingWorld.Simulation;

/// <summary>Monta o <see cref="LlmContext"/> de um turno de conversa (Fase 11, LLM-05/06) — só a
/// partir do conhecimento do próprio NPC: <see cref="NpcBeliefQuery"/> para crença (nunca
/// <c>HistoryTruthQuery</c>/<c>WorldState</c> global). Quem monta o resumo/ações permitidas fora
/// da crença (personalidade, profissão, `AllowedActions(npc, ctx)`) é responsabilidade do
/// chamador — este assembler só liga a peça de crença ao transporte (<see cref="LlmContext"/>)
/// sem reimplementar nada que já exista.</summary>
public static class LlmContextAssembler
{
    public static LlmContext Assemble(
        WorldState world, Npc npc, ConversationSession session, string playerUtterance,
        IReadOnlyList<string> allowedIntents, IReadOnlyList<string> allowedActions)
    {
        var beliefFacts = NpcBeliefQuery.BeliefsOf(world, npc.Id);
        string summary = $"{npc.Name}, {npc.Profession}, {npc.City}";

        return new LlmContext(
            NpcKnowledgeSummary: summary,
            PlayerUtterance: playerUtterance,
            AllowedIntents: allowedIntents,
            BeliefFacts: beliefFacts,
            RelevantMemories: null,
            AllowedActions: allowedActions,
            SessionId: session.SessionId,
            SessionOpenedAtTick: session.OpenedAtTick);
    }
}
