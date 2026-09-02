using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Llm;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Simulation.Behavior.Decision;

namespace LivingWorld.Simulation.Llm;

/// <summary>Decide se um NPC aceita ou recusa `StartConversation` (Fase 11, LLM-01/02) — modelo
/// oportunista (design.md): o NPC nunca é forçado a parar automaticamente; se aceitar, a ação
/// corrente segue rodando quando compatível, e só uma ação incompatível é candidata a pausa (a
/// pausa em si é responsabilidade do chamador — esta política só informa a compatibilidade).</summary>
public static class ConversationAvailabilityPolicy
{
    public readonly record struct Decision(ConversationStartDecision Result, ConversationCompatibility Compatibility);

    /// <summary>Fonte da ação atual é <see cref="BehaviorDecisionSystem"/> via <see
    /// cref="Npc.CurrentAction"/> — esta política nunca recalcula a decisão de comportamento,
    /// só lê o que já foi decidido. <paramref name="relationshipToInitiator"/> nulo (nunca se
    /// encontraram) nunca é hostil por si só.</summary>
    public static Decision Evaluate(
        Npc npc, NeedsRules needsRules, LlmRules llmRules, Relationship? relationshipToInitiator, long now)
    {
        if (relationshipToInitiator is { } relationship
            && relationship.Get(RelationshipAxis.Trust) < llmRules.HostileTrustThreshold)
            return new Decision(ConversationStartDecision.RejectedHostile, ConversationCompatibility.Forbidden);

        var compatibility = npc.CurrentAction is { } action
            ? llmRules.ActionCompatibility[action]
            : ConversationCompatibility.Compatible;

        if (compatibility != ConversationCompatibility.Forbidden)
            return new Decision(ConversationStartDecision.Accepted, compatibility);

        var decision = npc.HasUrgentNeed(needsRules, now)
            ? ConversationStartDecision.RejectedBusy
            : ConversationStartDecision.RejectedUnavailable;
        return new Decision(decision, compatibility);
    }
}
