using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Population.Lifecycle;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Behavior.Needs;

/// <summary>Decai as 4 necessidades por tick Hourly, dispara objetivo em 0 (via
/// <see cref="Npc.HasUrgentNeed"/>) e mata por fome sustentada (Fase 4, task 10 —
/// NEEDS-01/02/03). Com <see cref="LazyNeed"/> (Fase 9), decaimento é pull-only — este sistema
/// só trata fome zero sustentada.</summary>
public sealed class NeedsDecaySystem : ISimulationSystem
{
    public const string SystemName = "needs-decay";

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Hourly;

    public void Tick(WorldState world, TickContext ctx)
    {
        var rules = world.NeedsRules;
        long now = ctx.CurrentTick;

        foreach (var npc in world.Npcs.Where(n => n.IsAlive))
            HandleStarvation(world, ctx, npc, rules, now);
    }

    /// <summary>NEEDS-03: fome em 0 por <c>X = ceil(100 / HungerDecayPerHour)</c> ticks
    /// consecutivos mata o NPC. <c>HungerDecayPerHour == 0</c> é o edge case declarado no spec
    /// ("fome nunca decai, NPC nunca morre de fome") — sem taxa não há prazo de sobrevivência
    /// para derivar, então a checagem é pulada.</summary>
    private static void HandleStarvation(WorldState world, TickContext ctx, Npc npc, NeedsRules rules, long now)
    {
        if (npc.HungerAt(now) > 0)
        {
            if (npc.HungerZeroSinceTick is not null)
                npc.ClearHungerZeroSince();
            return;
        }

        if (npc.HungerZeroSinceTick is null)
        {
            npc.MarkHungerZeroSince(now);
            return;
        }

        if (rules.HungerDecayPerHour <= 0) return;

        long survivalTicks = (long)Math.Ceiling(100.0 / rules.HungerDecayPerHour);
        if (now - npc.HungerZeroSinceTick.Value >= survivalTicks)
            NpcDeath.Apply(world, ctx, npc, WorldEventKind.Starvation);
    }
}
