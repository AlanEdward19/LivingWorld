using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Needs;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Core;

/// <summary>Avança o mundo em ticks de Δ segundos (ADR-0050; sem argumento, Δ = 1 hora, o de produção sem jogador). Os
/// sistemas rodam quando o tick cruza a fronteira do calendário (hora, dia, meio do turno, mês, ano), nunca "a cada tick": Δ
/// divide a hora e o tick nunca a atravessa, então toda hora cheia é um tick e a hora inteira roda uma vez só, com qualquer Δ.
/// Ordem de execução dos sistemas é a ordem declarada na lista recebida no construtor — nunca ordem de registro acidental ou
/// de dicionário.</summary>
public sealed class WorldClock(IReadOnlyList<ISimulationSystem> systems, int maxIterationsPerTick = 1000, IWorldEventSink? sink = null)
{
    private readonly Dictionary<string, ISimulationSystem> _byName = systems.ToDictionary(s => s.Name);

    public IReadOnlyList<ISimulationSystem> Systems => systems;
    public int MaxIterationsPerTick => maxIterationsPerTick;

    /// <summary>Roda <paramref name="ticks"/> ticks de 1 hora.</summary>
    public void Run(WorldState world, long ticks)
    {
        for (long i = 0; i < ticks; i++)
            Tick(world);
    }

    /// <summary>Roda <paramref name="ticks"/> ticks de <paramref name="deltaSeconds"/>.</summary>
    public void Run(WorldState world, long ticks, int deltaSeconds)
    {
        for (long i = 0; i < ticks; i++)
            Tick(world, deltaSeconds);
    }

    public void Tick(WorldState world) => Tick(world, WorldSeconds.HourTick);

    /// <summary>Avança Δ segundos, sem passar da próxima hora cheia (o tick que chegaria depois dela para nela). Δ divide a hora
    /// (<see cref="WorldSeconds.IsValidTickDelta"/>). Todo tick despacha os eventos agendados que venceram (instante ≤ agora);
    /// os sistemas rodam no tick que chega à hora cheia.</summary>
    public void Tick(WorldState world, int deltaSeconds)
    {
        if (!WorldSeconds.IsValidTickDelta(deltaSeconds))
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "Δ precisa dividir a hora (1 a 3600 s)");
        int intoHour = world.CurrentDate.SecondOfHour;
        world.CurrentDate = world.CurrentDate.AddSeconds(Math.Min(deltaSeconds, WorldSeconds.PerHour - intoHour));
        if (world.CurrentDate.SecondOfHour != 0)
        {
            if (world.Scheduler.HasDue(world.CurrentDate.TotalSeconds))
                DispatchDueEvents(world, new TickContext(world, world.Rng, world.Scheduler, sink));
            return;
        }

        NpcWakeScheduler.PrepareWakeBatch(world, world.CurrentDate.TotalHours);
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

    private void DispatchDueEvents(WorldState world, TickContext ctx)
    {
        long tick = world.CurrentDate.TotalSeconds;
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
