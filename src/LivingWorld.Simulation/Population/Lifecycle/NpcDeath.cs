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
/// <see cref="NeedsDecaySystem"/>). Antes só a mortalidade por idade matava NPC;
/// morte por fome sustentada chamava <c>Npc.Die</c> direto e pulava a limpeza de household —
/// achado pelo mesmo tipo de invariante que o sweep referencial já cobria
/// para dissolução. Corrigido na raiz (função compartilhada) em vez de duplicar a limpeza em
/// cada sistema que mata NPC.</summary>
public static class NpcDeath
{
    /// <param name="cause">Nome da causa no payload (<c>npcId|nome</c>) quando o kind não a diz (AD-164: a sobra da tábua e o
    /// combate saem como <c>Death</c>); vazio, o payload é só o id.</param>
    public static void Apply(WorldState world, TickContext ctx, Npc npc, WorldEventKind kind, string cause = "")
    {
        NpcInstantiationMechanic.OnCarrierDeath(world, ctx, npc);
        npc.Die(world.CurrentDate);
        EstateSystem.Settle(world, ctx, npc);
        SoulMechanic.OnCarrierDeath(world, npc);
        world.AliveNpcIndex.OnDied(npc);
        string id = npc.Id.Value.ToString();
        ctx.LogEvent(kind, cause.Length == 0 ? id : id + "|" + cause, sourceSystem: "NpcDeath");
        FactToReportConversionScheduler.OnWitnessDied(npc.Id, world, ctx);
        LivingWorld.Simulation.Health.Lactation.OnDeath(world, npc, ctx.CurrentTick);
        world.ForgetHealthOf(npc.Id); // infecção e imunidade do morto saem dos livros (AD-137)

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
