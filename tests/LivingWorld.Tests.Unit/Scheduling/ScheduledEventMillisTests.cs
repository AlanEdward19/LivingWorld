using System.Text.Json;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Scheduling;

/// <summary>ADR-0050 (AD-197b): o agendador guarda o instante em milissegundos; na hora cheia o save e o hash têm o número em horas de
/// antes, e um evento agendado dentro da hora vence no primeiro tick que chega a ele.</summary>
public class ScheduledEventSecondsTests
{
    [Fact]
    public void Full_hour_event_serializes_with_the_legacy_hour_number()
    {
        string json = JsonSerializer.Serialize(new ScheduledEvent(4, 7 * 3_600_000L, "sys", "p"));

        Assert.Equal("{\"Id\":4,\"TargetTick\":7,\"SystemName\":\"sys\",\"Payload\":\"p\"}", json);
    }

    [Fact]
    public void Sub_hour_event_round_trips_in_seconds_and_legacy_hours_load_as_seconds()
    {
        var evt = new ScheduledEvent(4, 7 * 3_600_000L + 90, "sys");
        var legacy = JsonSerializer.Deserialize<ScheduledEvent>("{\"Id\":4,\"TargetTick\":7,\"SystemName\":\"sys\"}")!;
        var fromSeconds = JsonSerializer.Deserialize<ScheduledEvent>("{\"Id\":4,\"TargetTick\":\"25290s\",\"SystemName\":\"sys\"}")!;

        Assert.Equal((evt, 7L * 3_600_000, 25_290_000L), (JsonSerializer.Deserialize<ScheduledEvent>(JsonSerializer.Serialize(evt)), legacy.TargetMs, fromSeconds.TargetMs));
    }

    [Fact]
    public void Never_sentinel_keeps_its_value_through_json()
    {
        var evt = new ScheduledEvent(1, long.MaxValue, "sys");

        Assert.Equal(long.MaxValue, JsonSerializer.Deserialize<ScheduledEvent>(JsonSerializer.Serialize(evt))!.TargetMs);
    }

    [Fact]
    public void Event_scheduled_inside_the_hour_fires_on_the_first_fine_tick_that_reaches_it()
    {
        var (world, _) = ScenarioRunner.Create(seed: 1, initialPopulation: 0);
        var fired = new List<long>();
        var probe = new Probe(fired);
        var clock = new WorldClock([probe]);
        new TickContext(world, world.Rng, world.Scheduler).ScheduleEventAtMs(world.CurrentDate.TotalMilliseconds + 125, probe.Name);

        clock.Run(world, 4, 50);

        Assert.Equal([world.CurrentDate.TotalMilliseconds - 50], fired);
    }

    private sealed class Probe(List<long> fired) : ISimulationSystem
    {
        public string Name => "probe";
        public TickFrequency Frequency => TickFrequency.Yearly;
        public void Tick(WorldState world, TickContext ctx) { }
        public void HandleEvent(WorldState world, TickContext ctx, ScheduledEvent evt) => fired.Add(ctx.CurrentMs);
    }
}
