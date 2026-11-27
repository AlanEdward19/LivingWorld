using LivingWorld.Domain.Llm;
using LivingWorld.Domain.Population;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.History.Queries;

namespace LivingWorld.Simulation.Llm;

/// <summary>Monta o <see cref="LlmContext"/> de um turno de conversa — só a
/// partir do conhecimento do próprio NPC: <see cref="NpcBeliefQuery"/> para crença (nunca
/// <c>HistoryTruthQuery</c>/<c>WorldState</c> global) e <see cref="MemoryRecall"/>
/// para memória recuperada. Quem monta o resumo/ações permitidas fora da crença
/// (personalidade, profissão, `AllowedActions(npc, ctx)`) é responsabilidade do chamador — este
/// assembler só liga crença + memória ao transporte (<see cref="LlmContext"/>) sem reimplementar
/// nada que já exista.</summary>
public static class LlmContextAssembler
{
    public static LlmContext Assemble(
        WorldState world, Npc npc, ConversationSession session, string playerUtterance,
        IReadOnlyList<string> allowedIntents, IReadOnlyList<string> allowedActions,
        LlmRules? llmRules = null, int recallCount = 5, long? tick = null)
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
            SessionOpenedAtTick: session.OpenedAtTick,
            State: StateOf(world, npc, tick ?? session.OpenedAtTick));
    }

    /// <summary>Necessidade mais apertada a partir da qual o NPC "fala disso" (satisfação ≤ 35 de 100).</summary>
    private const int UrgentSatisfaction = 35;

    /// <summary>#237: estado do próprio NPC para o provider sem LLM — só leitura (não cria relação nem muda nada).</summary>
    private static NpcDialogueState StateOf(WorldState world, Npc npc, long tick)
    {
        var needs = new (string Name, int Level)[]
        {
            ("hunger", npc.HungerAt(tick)), ("thirst", npc.ThirstAt(tick)), ("sleep", npc.SleepAt(tick)), ("social", npc.SocialAt(tick)),
        };
        var worst = needs[0];
        foreach (var need in needs)
            if (need.Level < worst.Level) worst = need;

        double? affection = null, trust = null;
        var targets = world.RelationshipTargetsOf(npc.Id);
        var relations = world.RelationshipsFrom(npc.Id);
        for (int i = 0; relations is not null && i < targets.Count && i < relations.Count; i++)
        {
            if (targets[i] != ConversationEffectsApplier.PlayerNpcId) continue;
            affection = relations[i].Affection;
            trust = relations[i].Trust;
            break;
        }

        var p = npc.Personality;
        return new NpcDialogueState(
            npc.Health, worst.Level <= UrgentSatisfaction ? worst.Name : null,
            affection, trust, p.Agreeableness, p.Extroversion, p.Altruism);
    }

    /// <summary>Mesmo funil, para o caso de uso de narrativa — a LLM só
    /// reescreve a prosa dos claims já aprovados (ancorados em evento), sem sessão de NPC nem
    /// crença/memória envolvidas.</summary>
    public static LlmContext AssembleForNarrative(IReadOnlyList<string> approvedClaimTexts) =>
        new(
            NpcKnowledgeSummary: string.Join(" ", approvedClaimTexts),
            PlayerUtterance: "narrar",
            AllowedIntents: []);
}
