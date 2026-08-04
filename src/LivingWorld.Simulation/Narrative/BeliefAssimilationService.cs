using LivingWorld.Domain;
using LivingWorld.Domain.Llm;
using LivingWorld.Simulation.History;

namespace LivingWorld.Simulation.Narrative;

/// <summary>Aplica limiar de confiança à assimilação de um relato por um NPC ouvinte (Fase 12,
/// NARR-13..15) — só entra em memória semântica (<see cref="MemoryCategory.Semantic"/>) o relato
/// cuja confiança (derivada da distância crença-fato de <see cref="HistoryBeliefQuery"/>) alcança
/// o limiar; abaixo dele, a exposição é apenas registrada no retorno, sem mutar a memória do
/// ouvinte (spec.md Edge Cases). Único ponto de acesso é <see cref="HistoryBeliefQuery"/> — nunca
/// <see cref="HistoryTruthQuery"/> — então o fato canônico de origem nunca é lido nem alterado
/// aqui (NARR-14/15, mesma fronteira Verdade/Crença de <c>rules/llm-boundary.md</c>).</summary>
public static class BeliefAssimilationService
{
    public sealed record AssimilationOutcome(bool Accepted, double Confidence, string Reason);

    public const string BelowThresholdReason = "confiança abaixo do limiar: exposição registrada sem mutar memória semântica do ouvinte";
    public const string AssimilatedReason = "crença assimilada na memória semântica do ouvinte";

    /// <summary>Resolve a crença do ouvinte sobre <paramref name="originFactId"/> e decide se ela
    /// entra na memória semântica de <paramref name="listenerId"/>, contra
    /// <paramref name="confidenceThreshold"/> (0-1). Confiança é <c>1 - DistanceFromFact</c>
    /// (mesma métrica de distância já materializada por <see cref="HistoryBeliefQuery"/>),
    /// limitada a <c>[0,1]</c>.</summary>
    // ponytail: 50 replica LlmRules.CanonicalMemoryImportanceThreshold default — literal em vez
    // de referenciar LlmRules.Default aqui para não acoplar este serviço a uma dependência de
    // cenário inteira só pelo valor default de um parâmetro opcional (chamador de cenário real
    // passa o valor de LlmRules explicitamente, como ConversationEffectsApplier já faz).
    public static AssimilationOutcome Assimilate(
        WorldState world, NpcId listenerId, FactId originFactId, long tick,
        double confidenceThreshold, int canonicalImportanceThreshold = 50)
    {
        var belief = HistoryBeliefQuery.BeliefOf(world, listenerId, originFactId);
        if (!belief.IsSuccess)
            return new AssimilationOutcome(false, 0, belief.Error!);

        double confidence = Math.Clamp(1.0 - belief.Value!.DistanceFromFact, 0.0, 1.0);
        if (confidence < confidenceThreshold)
            return new AssimilationOutcome(false, confidence, BelowThresholdReason);

        var listener = world.FindNpc(listenerId);
        world.AddNpcMemory(
            ownerId: listenerId,
            category: MemoryCategory.Semantic,
            content: belief.Value.MoralizedNarrativeSeed,
            importance: (int)Math.Round(confidence * 100),
            originTick: tick,
            participants: belief.Value.AttributedParticipants,
            location: listener?.CurrentLocation ?? default,
            canonicalImportanceThreshold: canonicalImportanceThreshold);

        return new AssimilationOutcome(true, confidence, AssimilatedReason);
    }
}
