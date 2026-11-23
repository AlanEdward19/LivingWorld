using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Extraordinary;
using LivingWorld.Domain.Population;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Extraordinary.Opportunity;

namespace LivingWorld.Simulation.Behavior.Decision;

// Parte de BehaviorDecisionSystem — o poder escolhido pelo controlador externo (AD-196, Fase 25).
public sealed partial class BehaviorDecisionSystem
{
    /// <summary>O jogador pediu <see cref="ActionType.UsePower"/> com um poder: a invocação é a da oportunidade que o motor
    /// ofereceria ao NPC neste tick para esse poder (mesmo mecanismo, alvo sugerido, custo e confiabilidade do descritor). Sem
    /// oportunidade (o poder não se aplica agora), o NPC fica parado, como o motor, que nunca escolhe poder sem oportunidade.</summary>
    private static (ActionType Candidate, PendingPowerInvocation? Pending) ControlledPower(WorldState world, Npc npc, long now)
    {
        if (npc.ControlledPowerId is { } powerId)
            foreach (var opp in PowerOpportunityProvider.ApplicableTo(world, npc, now))
                if (opp.PowerId == powerId)
                    return (ActionType.UsePower, new PendingPowerInvocation(opp.PowerId, opp.MechanicToken, opp.SuggestedTarget));
        return (ActionType.Idle, null);
    }
}
