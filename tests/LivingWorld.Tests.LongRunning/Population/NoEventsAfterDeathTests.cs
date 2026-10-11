using LivingWorld.Domain.History;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.LongRunning.Population;

/// <summary>Fase 3: nenhum evento com tick maior que o da morte referencia o NPC morto
/// (emprego, casamento, cortejo). Cobre o cenário default por 1 ano.</summary>
public class NoEventsAfterDeathTests
{
    private const long OneYearInHours = 12 * 30 * 24;

    private sealed class RecordingSink : IWorldEventSink
    {
        public List<WorldEvent> Events { get; } = [];
        public void Record(WorldEvent evt) => Events.Add(evt);
    }

    [Fact]
    public void Dead_npc_is_never_hired_married_or_courted_after_its_death_tick()
    {
        var (world, _) = ScenarioRunner.Create(seed: 42);
        var sink = new RecordingSink();
        var clock = new WorldClock(ScenarioRunner.DefaultSystems(), sink: sink);
        clock.Run(world, OneYearInHours);

        var deathTick = new Dictionary<string, long>();
        foreach (var e in sink.Events.Where(e => e.Kind is WorldEventKind.Death or WorldEventKind.Starvation && e.SourceSystem == "NpcDeath"))
            deathTick[e.Payload] = e.Tick;
        Assert.NotEmpty(deathTick);

        foreach (var e in sink.Events)
        {
            var ids = e.Kind switch
            {
                WorldEventKind.Hired => e.Payload.Split('|').Take(1),
                WorldEventKind.Marriage or WorldEventKind.CourtshipStarted or WorldEventKind.CourtshipSucceeded
                    => e.Payload.Split('|').Take(2),
                _ => [],
            };
            foreach (var id in ids)
                if (deathTick.TryGetValue(id, out var died))
                    Assert.True(e.Tick <= died, $"{e.Kind} de NPC {id} no tick {e.Tick}, morto no tick {died}");
        }
    }
}
