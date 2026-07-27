using LivingWorld.Domain;

namespace LivingWorld.Simulation;

/// <summary>Avança o mundo em ticks de 1 hora. Ordem de execução dos sistemas é a ordem
/// declarada na lista recebida no construtor — nunca ordem de registro acidental ou de
/// dicionário.</summary>
public sealed class WorldClock(IReadOnlyList<ISimulationSystem> systems, int maxIterationsPerTick = 1000, IWorldEventSink? sink = null)
{
    private readonly Dictionary<string, ISimulationSystem> _byName = systems.ToDictionary(s => s.Name);

    public int MaxIterationsPerTick => maxIterationsPerTick;

    public void Run(WorldState world, long ticks)
    {
        for (long i = 0; i < ticks; i++)
            Tick(world);
    }

    public void Tick(WorldState world)
    {
        world.CurrentDate = world.CurrentDate.AddHours(1);
        var ctx = new TickContext(world, world.Rng, world.Scheduler, sink);

        bool isDayBoundary = world.CurrentDate.Hour == 0;
        bool isMonthBoundary = isDayBoundary && world.CurrentDate.Day == 0;
        bool isYearBoundary = isMonthBoundary && world.CurrentDate.Month == 0;

        foreach (var system in systems)
        {
            bool runsThisTick = system.Frequency switch
            {
                TickFrequency.Hourly => true,
                TickFrequency.Daily => isDayBoundary,
                TickFrequency.Monthly => isMonthBoundary,
                TickFrequency.Yearly => isYearBoundary,
                _ => throw new ArgumentOutOfRangeException(nameof(system.Frequency)),
            };
            if (runsThisTick)
                system.Tick(world, ctx);
        }

        // Eventos agendados pelos sistemas acima para este mesmo tick disparam antes do
        // próximo tick começar — inclusive reagendamentos em cadeia (teto de iterações abaixo).
        DispatchDueEvents(world, ctx);
    }

    /// <summary>Processa a fila do tick atual em loop: um handler pode agendar outro evento
    /// para o mesmo tick (re-agendamento imediato). Estourar o teto aborta nomeando o
    /// sistema do último evento despachado.</summary>
    private void DispatchDueEvents(WorldState world, TickContext ctx)
    {
        long tick = world.CurrentDate.TotalHours;
        int iterations = 0;
        string? lastSystem = null;

        while (world.Scheduler.HasDue(tick))
        {
            if (++iterations > maxIterationsPerTick)
                throw new TickBudgetExceededException(lastSystem ?? "(desconhecido)", maxIterationsPerTick);

            foreach (var evt in world.Scheduler.PopDue(tick))
            {
                lastSystem = evt.SystemName;
                if (_byName.TryGetValue(evt.SystemName, out var system))
                    system.HandleEvent(world, ctx, evt);
            }
        }
    }
}
