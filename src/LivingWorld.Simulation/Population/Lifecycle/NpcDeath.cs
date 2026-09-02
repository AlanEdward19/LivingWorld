using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Simulation.Behavior.Needs;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Extraordinary.Mechanics;
using LivingWorld.Simulation.History.Distortion;
using LivingWorld.Simulation.Population.Family;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Population.Lifecycle;

/// <summary>Mata um NPC e limpa a referência de household — único ponto que faz as duas coisas
/// juntas, reusado por todo sistema que pode matar um NPC (<see cref="MortalitySystem"/>,
/// <see cref="NeedsDecaySystem"/>). Antes da Fase 4 só a mortalidade por idade matava NPC;
/// morte por fome sustentada chamava <c>Npc.Die</c> direto e pulava a limpeza de household —
/// achado pelo mesmo tipo de invariante que o sweep referencial (task 12, Fase 3) já cobria
/// para dissolução. Corrigido na raiz (função compartilhada) em vez de duplicar a limpeza em
/// cada sistema que mata NPC.</summary>
public static class NpcDeath
{
    public static void Apply(WorldState world, TickContext ctx, Npc npc, WorldEventKind kind)
    {
        NpcInstantiationMechanic.OnCarrierDeath(world, ctx, npc);
        npc.Die(world.CurrentDate);
        SoulMechanic.OnCarrierDeath(world, npc);
        world.AliveNpcIndex.OnDied(npc);
        ctx.LogEvent(kind, npc.Id.Value.ToString(), sourceSystem: "NpcDeath");
        FactToReportConversionScheduler.OnWitnessDied(npc.Id, world, ctx);

        if (npc.Household is not { } householdId) return;

        var household = world.FindHousehold(householdId);
        if (household is null) return;

        household.RemoveMember(npc.Id);

        if (!household.IsEmpty
            && !HouseholdRedistribution.HasLivingAdultOrElder(
                world, household, world.LifeStageRules, world.CurrentDate))
        {
            HouseholdRedistribution.HandleOrphaned(
                world, ctx, household, world.LifeStageRules, world.CurrentDate);
            return;
        }

        HouseholdCleanup.DissolveIfEmpty(world, ctx, household);
    }
}
