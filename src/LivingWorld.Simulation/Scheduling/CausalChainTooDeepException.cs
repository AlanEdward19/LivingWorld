using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Scheduling;

/// <summary>Cadeia de <see cref="WorldEvent.CauseEventId"/> ciclou ou excedeu
/// <c>CausalRules.MaxCauseChainDepth</c> (COH-02). Aborta nomeando o evento culpado —
/// mesmo shape de <see cref="TickBudgetExceededException"/>.</summary>
public sealed class CausalChainTooDeepException(long culpritEventId, int maxDepth)
    : Exception(
        $"Cadeia causal excedeu profundidade {maxDepth} (ciclo ou cadeia longa demais). Evento culpado: {culpritEventId}.")
{
    public long CulpritEventId { get; } = culpritEventId;
    public int MaxDepth { get; } = maxDepth;
}
