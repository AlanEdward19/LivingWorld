using LivingWorld.Domain.Population;

namespace LivingWorld.Domain.Behavior;

/// <summary>Estado do intent persistente do NPC (Fase 16.3 P2a, COH-41) — paralelo a
/// <see cref="Npc.CurrentAction"/>, mas em nível de plano (não só a ação imediata).</summary>
public enum IntentStatus
{
    /// <summary>Intent ativo; o NPC persiste o plano até completar ou invalidar.</summary>
    Active = 0,

    /// <summary>Intent atingiu o objetivo (ex.: comida adquirida).</summary>
    Completed = 1,

    /// <summary>Todas as alternativas do plano falharam; reconsideração completa necessária.</summary>
    Invalidated = 2,
}
