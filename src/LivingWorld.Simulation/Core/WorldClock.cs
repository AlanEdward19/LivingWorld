using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Needs;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Core;

/// <summary>Avança o mundo em ticks de Δ milissegundos (ADR-0050; sem argumento, Δ = 1 hora, o de produção sem jogador). Os
/// sistemas rodam quando o tick cruza a fronteira do calendário (hora, dia, meio do turno, mês, ano), nunca "a cada tick": Δ
/// divide a hora e o tick nunca a atravessa, então toda hora cheia é um tick e a hora inteira roda uma vez só, com qualquer Δ.
/// Ordem de execução dos sistemas é a ordem declarada na lista recebida no construtor — nunca ordem de registro acidental ou
/// de dicionário.</summary>
public sealed class WorldClock(IReadOnlyList<ISimulationSystem> systems, int maxIterationsPerTick = 1000, IWorldEventSink? sink = null)
{
    private readonly Dictionary<string, ISimulationSystem> _byName = systems.ToDictionary(s => s.Name);
    private readonly IRunsBetweenHours[] _betweenHours = systems.OfType<IRunsBetweenHours>().ToArray();

    public IReadOnlyList<ISimulationSystem> Systems => systems;
    public int MaxIterationsPerTick => maxIterationsPerTick;

    /// <summary>Roda <paramref name="ticks"/> ticks de 1 hora.</summary>
    public void Run(WorldState world, long ticks)
    {
        for (long i = 0; i < ticks; i++)
            Tick(world);
    }

    /// <summary>Roda <paramref name="ticks"/> ticks de <paramref name="deltaMs"/>.</summary>
    public void Run(WorldState world, long ticks, int deltaMs)
    {
        for (long i = 0; i < ticks; i++)
            Tick(world, deltaMs);
    }

    public void Tick(WorldState world) => Tick(world, WorldMillis.HourTick);

    /// <summary>Avança Δ milissegundos, sem passar da próxima hora cheia (o tick que chegaria depois dela para nela). Δ divide a hora
    /// (<see cref="WorldMillis.IsValidTickDelta"/>). Todo tick despacha os eventos agendados que venceram (instante ≤ agora);
    /// os sistemas rodam no tick que chega à hora cheia.</summary>
    public void Tick(WorldState world, int deltaMs)
    {
        if (!WorldMillis.IsValidTickDelta(deltaMs))
            throw new ArgumentOutOfRangeException(nameof(deltaMs), deltaMs, "Δ precisa dividir a hora (1 a 3.600.000 ms)");
        int intoHour = world.CurrentDate.MillisecondOfHour;
        world.TickDeltaMs = deltaMs;
        world.CurrentDate = world.CurrentDate.AddMilliseconds(Math.Min(deltaMs, WorldMillis.PerHour - intoHour));
        if (world.CurrentDate.MillisecondOfHour != 0)
        {
            TickInsideHour(world);
            return;
        }

        NpcWakeScheduler.PrepareWakeBatch(world, world.CurrentDate.TotalMilliseconds);
        var ctx = new TickContext(world, world.Rng, world.Scheduler, sink);

        bool isDayBoundary = world.CurrentDate.Hour == 0;
        bool isMonthBoundary = isDayBoundary && world.CurrentDate.Day == 0;
        bool isYearBoundary = isMonthBoundary && world.CurrentDate.Month == 0;
        bool isMidshift = world.CurrentDate.Hour == world.CurrentDate.Calendar.HoursPerDay * 10 / 24;

        foreach (var system in systems)
        {
            bool runsThisTick = system.Frequency switch
            {
                TickFrequency.Hourly => true,
                TickFrequency.Daily => isDayBoundary,
                TickFrequency.Monthly => isMonthBoundary,
                TickFrequency.Yearly => isYearBoundary,
                TickFrequency.Midshift => isMidshift,
                _ => throw new ArgumentOutOfRangeException(nameof(system.Frequency)),
            };
            if (runsThisTick)
                system.Tick(world, ctx);
        }

        DispatchDueEvents(world, ctx);
    }

    /// <summary>Tick dentro da hora: despacha os eventos vencidos e, com o passo fino (AD-197c), acorda quem tem despertar vencido
    /// agora e roda para ele os sistemas de <see cref="IRunsBetweenHours"/> (a decisão). Sem nada vencido, só o relógio anda.</summary>
    private void TickInsideHour(WorldState world)
    {
        long nowMs = world.CurrentDate.TotalMilliseconds;
        TickContext? ctx = null;
        if (world.FineTime && _betweenHours.Length > 0)
        {
            NpcWakeScheduler.PrepareWakeBatch(world, nowMs);
            if (world.NpcWakeBatch.Count > 0)
            {
                ctx = new TickContext(world, world.Rng, world.Scheduler, sink);
                foreach (var system in _betweenHours)
                    system.Tick(world, ctx);
            }
        }
        if (world.Scheduler.HasDue(nowMs))
            DispatchDueEvents(world, ctx ?? new TickContext(world, world.Rng, world.Scheduler, sink));
    }

    private void DispatchDueEvents(WorldState world, TickContext ctx)
    {
        long tick = world.CurrentDate.TotalMilliseconds;
        int iterations = 0;
        string? lastSystem = null;

        while (world.Scheduler.HasDue(tick))
        {
            if (++iterations > maxIterationsPerTick)
                throw new TickBudgetExceededException(lastSystem ?? "(desconhecido)", maxIterationsPerTick);

            var due = world.Scheduler.PopDue(tick);
            foreach (var evt in due)
            {
                lastSystem = evt.SystemName;
                if (_byName.TryGetValue(evt.SystemName, out var system))
                    system.HandleEvent(world, ctx, evt);
            }

            world.Scheduler.Release(due);
        }
    }
}
