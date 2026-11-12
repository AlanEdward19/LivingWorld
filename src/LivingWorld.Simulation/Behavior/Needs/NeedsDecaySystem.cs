using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Population.Lifecycle;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Behavior.Needs;

/// <summary>Decai as 4 necessidades por tick Hourly, dispara objetivo em 0 (via
/// <see cref="Npc.HasUrgentNeed"/>) e mata por fome sustentada.
/// Com <see cref="LazyNeed"/>, decaimento é pull-only — este sistema
/// só trata fome zero sustentada.</summary>
[LivingCapability("NEEDS", Consumers = ["inspector.npc.needs"])]
[RunsAfter(typeof(MortalitySystem))]
[RunsAfter(typeof(NatalitySystem))]
public sealed class NeedsDecaySystem : ISimulationSystem
{
    public const string SystemName = "needs-decay";

    private readonly List<Npc> _alive = [];

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Hourly;

    public void Tick(WorldState world, TickContext ctx)
    {
        var rules = world.NeedsRules;
        long now = ctx.CurrentTick;

        // cópia dos vivos: a morte dentro do laço tira o NPC do índice; varrer só os vivos poupa os mortos acumulados em world.Npcs
        _alive.Clear();
        _alive.AddRange(world.AliveNpcIndex.Alive);
        foreach (var npc in _alive)
        {
            if (!npc.IsAlive) continue;
            HandleStarvation(world, ctx, npc, rules, now);
            if (!npc.IsAlive) continue;
            HandleDehydration(world, ctx, npc, rules, now);
            if (!npc.IsAlive) continue;
            HandleExhaustion(world, ctx, npc, rules, now);
        }
    }

    /// <summary>Sede em 0 pelo tempo que ela leva para ir de 100 a 0 mata (mesmo molde da fome, mas
    /// a sede decai mais rápido, então mata antes). Derivado do <see cref="LazyNeed"/>: sem estado novo.</summary>
    private static void HandleDehydration(WorldState world, TickContext ctx, Npc npc, NeedsRules rules, long now)
    {
        if (rules.ThirstDecayPerHour <= 0) return;
        long survivalTicks = (long)Math.Ceiling(100.0 / rules.ThirstDecayPerHour);
        if (npc.ThirstNeed.TicksAtZero(now) >= survivalTicks)
            NpcDeath.Apply(world, ctx, npc, WorldEventKind.Dehydration);
    }

    /// <summary>Sono em 0 por mais que o tempo de 100 a 0 tira 2 de saúde por hora, até o piso da exaustão. O sono restaurado (necessidade &gt; 0) interrompe a perda — a saúde volta pelos meios de sempre.</summary>
    /// <summary>Saúde abaixo da qual a falta de sono, sozinha, não leva ninguém.</summary>
    public const int ExhaustionHealthFloor = 10;

    private static void HandleExhaustion(WorldState world, TickContext ctx, Npc npc, NeedsRules rules, long now)
    {
        if (rules.SleepDecayPerHour <= 0) return;
        long graceTicks = (long)Math.Ceiling(100.0 / rules.SleepDecayPerHour);
        if (npc.SleepNeed.TicksAtZero(now) < graceTicks) return;

        // Não há registro de gente morta só por falta de sono: a privação debilita (e a saúde baixa pesa no risco mensal
        // de morte), mas não mata por si. Antes a saúde ia a zero em ~5 dias (conferência AD-133).
        if (npc.Health > ExhaustionHealthFloor)
            npc.SetHealth(Math.Max(ExhaustionHealthFloor, npc.Health - 2));
    }

    /// <summary>Fome em 0 por <c>X = ceil(100 / HungerDecayPerHour)</c> ticks
    /// consecutivos mata o NPC. <c>HungerDecayPerHour == 0</c> é o edge case declarado no spec
    /// ("fome nunca decai, NPC nunca morre de fome") — sem taxa não há prazo de sobrevivência
    /// para derivar, então a checagem é pulada.</summary>
    private static void HandleStarvation(WorldState world, TickContext ctx, Npc npc, NeedsRules rules, long now)
    {
        if (npc.HungerAt(now) > 0)
        {
            if (npc.HungerZeroSinceTick is { } since)
            {
                npc.ClearHungerZeroSince();
                LivingWorld.Simulation.Cognition.LifeEventIntake.OnHungerEpisodeEnded(world, npc, since, now); // AD-138: fome passada
            }
            return;
        }

        if (npc.HungerZeroSinceTick is null)
        {
            npc.MarkHungerZeroSince(now);
            return;
        }

        if (rules.HungerDecayPerHour <= 0) return;

        if (now - npc.HungerZeroSinceTick.Value >= StarvationTicksFor(world, npc, rules, now))
            NpcDeath.Apply(world, ctx, npc, WorldEventKind.Starvation);
    }

    /// <summary>Jejum que mata: o do cenário vezes o fator da idade (AD-134: criança e idoso aguentam menos). Só calculado
    /// com a fome já em zero.</summary>
    public static long StarvationTicksFor(WorldState world, Npc npc, NeedsRules rules, long now)
    {
        long ticks = rules.StarvationTicks;
        if (!rules.Physiology.Enabled || ticks == long.MaxValue) return ticks;
        double age = Math.Max(0, now - npc.BirthDate.TotalHours) / (double)world.Calendar.HoursPerYear;
        return Math.Max(1, (long)(ticks * rules.Physiology.StarvationSurvivalFactorByAge.At(age)));
    }
}
