using LivingWorld.Domain.Ontogeny;
using LivingWorld.Domain.Population;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Population.Lifecycle;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Ontogeny;

/// <summary>Avança o desenvolvimento de quem ainda está crescendo (Fase 21, <c>Monthly</c>): a curva da idade (escalada pelo <c>RateGene</c>) só
/// rende com exposição ao cuidador (21b), e janela de marco perdida reduz o teto do eixo. Ao completar todos os eixos o estado vira
/// <c>null</c>. Consumidor causal: o filtro de elegibilidade de ações da decisão.</summary>
[LivingCapability("ONTOGENY", Events = [LivingWorld.Domain.History.WorldEventKind.MilestoneRegressed], Consumers = ["decision.eligibility", "timeline.development"])]
[RunsAfter(typeof(NatalitySystem))]
public sealed class MilestoneProgressSystem : ISimulationSystem
{
    public const string SystemName = "ontogeny-milestone-progress";

    public string Name => SystemName;
    public Domain.Shared.TickFrequency Frequency => Domain.Shared.TickFrequency.Monthly;

    public void Tick(WorldState world, TickContext ctx)
    {
        if (!world.OntogenyRules.Enabled) return;

        List<Npc>? growing = null;
        foreach (var candidate in world.Npcs)
            if (candidate.Development is not null && candidate.IsAlive) (growing ??= []).Add(candidate);
        if (growing is null) return;
        growing.Sort((a, b) => a.Id.Value.CompareTo(b.Id.Value)); // ordem por id só entre quem cresce

        foreach (var npc in growing)
        {
            if (npc.Development is not { } state) continue;
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
