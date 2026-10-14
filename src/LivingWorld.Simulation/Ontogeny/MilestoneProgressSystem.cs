using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Population.Lifecycle;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Ontogeny;

/// <summary>Faz o progresso de desenvolvimento acompanhar a idade efetiva (Fase 21, <c>Monthly</c>). Na 21a a curva só depende da idade
/// e do <c>RateGene</c>; a 21b a troca por exposição. Ao completar todos os eixos o estado vira <c>null</c> e o NPC deixa de custar
/// qualquer coisa neste sistema. Consumidor causal: o filtro de elegibilidade de ações da decisão.</summary>
[LivingCapability("ONTOGENY", Consumers = ["decision.eligibility"])]
[RunsAfter(typeof(NatalitySystem))]
public sealed class MilestoneProgressSystem : ISimulationSystem
{
    public const string SystemName = "ontogeny-milestone-progress";

    public string Name => SystemName;
    public Domain.Shared.TickFrequency Frequency => Domain.Shared.TickFrequency.Monthly;

    public void Tick(WorldState world, TickContext ctx)
    {
        if (!world.OntogenyRules.Enabled) return;

        foreach (var npc in world.Npcs.OrderBy(n => n.Id.Value))
        {
            if (npc.Development is null || !npc.IsAlive) continue;
            npc.SetDevelopment(OntogenyMechanic.StateFor(world, npc));
        }
    }
}
