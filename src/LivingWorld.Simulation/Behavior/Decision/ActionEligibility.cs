using LivingWorld.Domain.Behavior;

namespace LivingWorld.Simulation.Behavior.Decision;

/// <summary>Quais ações o NPC já alcançou pelo desenvolvimento (Fase 21), passado como argumento por valor para a escolha por utility:
/// um campo a mais no <see cref="DecisionContext"/> custaria uma alocação por decisão. <c>default</c> = nada restrito (adulto, ontogenia desligada).</summary>
public readonly record struct ActionEligibility(uint BlockedMask, bool IsDeveloping, double Language)
{
    public bool Allows(ActionType action) => (BlockedMask & (1u << (int)action)) == 0;
}
