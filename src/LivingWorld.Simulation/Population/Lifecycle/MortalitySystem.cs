using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Population.Lifecycle;

/// <summary>Morte por idade como evento agendado — nenhuma varredura por tick. A
/// idade de morte é resolvida por antecipação (<see cref="MortalityPlanner"/>) no nascimento;
/// este sistema só executa o que já foi decidido, no tick certo.</summary>
[LivingCapability("DEATH", Events = [WorldEventKind.Death, WorldEventKind.Starvation, WorldEventKind.Dehydration, WorldEventKind.Exhaustion], Consumers = ["timeline.death"])]
public sealed class MortalitySystem : ISimulationSystem
{
    public const string SystemName = "population-mortality";

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Hourly; // sem trabalho por tick — só HandleEvent

    public void Tick(WorldState world, TickContext ctx)
    {
    }

    public void HandleEvent(WorldState world, TickContext ctx, ScheduledEvent evt)
    {
        var npcId = new NpcId(long.Parse(evt.Payload!));
        var npc = world.FindNpc(npcId);
        if (npc is null || !npc.IsAlive) return; // referência já resolvida/perdida — sem-op, não exceção

        NpcDeath.Apply(world, ctx, npc, WorldEventKind.Death);
    }

    /// <summary>Rola a idade de morte do NPC e agenda o evento único. Nunca agenda no
    /// passado ou no tick corrente já processado — o scheduler só dispara eventos futuros.</summary>
    public static void SchedulePlannedDeath(WorldState world, TickContext ctx, Npc npc)
    {
        var rng = ctx.StreamFor("mortality", npc.Id.Value);
        // VitalityMortalitySelectionEnabled desliga Vitality como fator de
        // seleção na mortalidade — passa 1.0 direto, nunca chama EffectiveVitalityMultiplier
        // (mesmo contrato já documentado no próprio método, FamilyRules.cs). Flag independente de
        // NeutralDriftEnabled (que só controla escolha de parceiro em CourtshipSystem) —
        // o controle de deriva neutra "de verdade" liga as duas.
        double vitalityMultiplier = world.FamilyRules.VitalityMortalitySelectionEnabled
            ? world.FamilyRules.EffectiveVitalityMultiplier(npc.Vitality)
            : 1.0;
        // Lê o multiplicador já agregado (mínimo entre poderes manifestos) no
        // estado do portador. Multiplicador 0 = não envelhece — não agenda morte por idade.
        double senescenceRateMultiplier = 1.0;
        var carrier = world.CarrierOf(npc.Id);
        if (carrier is { IsManifested: true })
        {
            if (carrier.SenescenceRateMultiplier <= 0.0)
                return;
            senescenceRateMultiplier = carrier.SenescenceRateMultiplier;
        }
        int deathAge = MortalityPlanner.RollDeathAge(
            rng, world.PopulationRules.LifeTable, npc.Health, vitalityMultiplier, senescenceRateMultiplier,
            aliveAtAgeYears: world.PopulationRules.ConditionalInitialMortality ? npc.AgeYears(world.CurrentDate) : 0);
        long deathTick = npc.BirthDate.AddYears(deathAge).TotalHours;
        if (deathTick <= world.CurrentDate.TotalHours)
            deathTick = world.CurrentDate.TotalHours + 1;

        ctx.ScheduleEvent(deathTick, SystemName, npc.Id.Value.ToString());
    }
}
