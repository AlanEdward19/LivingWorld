using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Population.Lifecycle;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Ontogeny;

/// <summary>Avança o desenvolvimento de quem ainda está crescendo (Fase 21, <c>Monthly</c>): a curva da idade (escalada pelo <c>RateGene</c>) só
/// rende com exposição ao cuidador (21b), e janela de marco perdida reduz o teto do eixo. Ao completar todos os eixos o estado vira
/// <c>null</c>. Consumidor causal: o filtro de elegibilidade de ações da decisão.</summary>
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
            if (npc.Development is not { } state || !npc.IsAlive) continue;
            npc.SetDevelopment(OntogenyMechanic.AdvanceOneMonth(world, npc, state));
        }
    }
}
