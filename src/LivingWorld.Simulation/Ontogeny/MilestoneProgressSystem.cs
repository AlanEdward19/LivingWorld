using LivingWorld.Domain.Ontogeny;
using LivingWorld.Domain.Population;
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
            var advanced = OntogenyMechanic.AdvanceOneMonth(world, npc, state);
            if (advanced is not null && ActiveDisease(world, npc) is { } disease)
                advanced = Regress(world, ctx, npc, advanced, disease);
            npc.SetDevelopment(advanced);
        }
    }

    /// <summary>Doença ativa do NPC (menor id de doença, para o desempate ser determinístico); nula se não há.</summary>
    private static int? ActiveDisease(WorldState world, Npc npc)
    {
        int? disease = null;
        foreach (var infection in world.Infections)
            if (infection.Npc == npc.Id && (disease is null || infection.Disease < disease)) disease = infection.Disease;
        return disease;
    }

    /// <summary>Regressão com causa nomeada: toda queda de marco vem junto de um evento no mesmo tick dizendo a doença.</summary>
    private static DevelopmentState Regress(WorldState world, TickContext ctx, Npc npc, DevelopmentState state, int disease)
    {
        var lowered = world.OntogenyRules.Regress(state, OntogenyMechanic.EffectiveAgeYears(world, npc));
        foreach (var axis in Enum.GetValues<DevelopmentAxis>())
            if (lowered.Get(axis) < state.Get(axis))
                ctx.LogEvent(
                    LivingWorld.Domain.History.WorldEventKind.MilestoneRegressed,
                    $"{npc.Id.Value}|{axis}|{state.Get(axis):R}|{lowered.Get(axis):R}|{disease}", sourceSystem: SystemName);
        return lowered;
    }
}
