using LivingWorld.Domain;

namespace LivingWorld.Simulation;

/// <summary>Morte por idade como evento agendado (task 4) — nenhuma varredura por tick. A
/// idade de morte é resolvida por antecipação (<see cref="MortalityPlanner"/>) no nascimento;
/// este sistema só executa o que já foi decidido, no tick certo.</summary>
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

    /// <summary>Rola a idade de morte do NPC e agenda o evento único (task 4). Nunca agenda no
    /// passado ou no tick corrente já processado — o scheduler só dispara eventos futuros.</summary>
    public static void SchedulePlannedDeath(WorldState world, TickContext ctx, Npc npc)
    {
        var rng = ctx.Rng($"mortality-{npc.Id.Value}");
        int deathAge = MortalityPlanner.RollDeathAge(rng, world.PopulationRules.LifeTable, npc.Health);
        long deathTick = npc.BirthDate.AddYears(deathAge).TotalHours;
        if (deathTick <= world.CurrentDate.TotalHours)
            deathTick = world.CurrentDate.TotalHours + 1;

        ctx.ScheduleEvent(deathTick, SystemName, npc.Id.Value.ToString());
    }
}
