using LivingWorld.Domain.History;
using LivingWorld.Domain.Shared;
using LivingWorld.Infrastructure.EventLog;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Tests.Integration.Infrastructure;

/// <summary>event-log-size-budget: o ruído vira agregado por mês e tipo, o resto passa intacto e na mesma ordem.</summary>
public class EventLogCompactorTests
{
    private static readonly WorldCalendar Calendar = new(24, 30, 12); // mês = 720 ticks

    [Fact]
    public void Noise_events_of_a_month_become_one_aggregate_with_count_and_sum()
    {
        var events = new List<WorldEvent>
        {
            new(10, WorldEventKind.WageUnpaid, "1|2|30", 1),
            new(20, WorldEventKind.WageUnpaid, "3|2|45", 2),
            new(30, WorldEventKind.ResourceLost, "9|1|7", 3),
        };

        var compacted = EventLogCompactor.Compact(events, Calendar);

        Assert.Equal(2, compacted.Count);
        var wage = Assert.Single(compacted, e => e.Kind == WorldEventKind.WageUnpaid);
        Assert.Equal("2|75", wage.Payload);
        Assert.Equal(10, wage.Tick);
        Assert.Equal(EventLogCompactor.AggregateSource, wage.SourceSystem);
        Assert.Equal("1|7", Assert.Single(compacted, e => e.Kind == WorldEventKind.ResourceLost).Payload);
    }

    [Fact]
    public void Different_months_stay_separate_aggregates()
    {
        var events = new List<WorldEvent>
        {
            new(100, WorldEventKind.ResourceLost, "1|1|5", 1),
            new(800, WorldEventKind.ResourceLost, "1|1|6", 2), // mês seguinte
        };

        var compacted = EventLogCompactor.Compact(events, Calendar);

        Assert.Equal(["1|5", "1|6"], compacted.Select(e => e.Payload).ToArray());
    }

    [Fact]
    public void History_and_death_events_pass_through_untouched_and_in_order_before_the_aggregates()
    {
        var birth = new WorldEvent(5, WorldEventKind.Birth, "1|2|3|4", 1);
        var death = new WorldEvent(6, WorldEventKind.Death, "9", 2, CauseEventId: 1, SourceSystem: "NpcDeath");
        var noise = new WorldEvent(7, WorldEventKind.PlantMatured, "x|y|1", 3);
        var estate = new WorldEvent(8, WorldEventKind.EstateSettled, "9|1|50|0|0", 4);

        var compacted = EventLogCompactor.Compact([birth, noise, death, estate], Calendar);

        Assert.Same(birth, compacted[0]);
        Assert.Same(death, compacted[1]);
        Assert.Same(estate, compacted[2]);
        Assert.Equal(WorldEventKind.PlantMatured, compacted[3].Kind);
    }

    [Fact]
    public void A_batch_without_noise_is_returned_as_is_and_the_result_is_deterministic()
    {
        var plain = new List<WorldEvent> { new(1, WorldEventKind.Birth, "a", 1), new(2, WorldEventKind.Death, "b", 2) };
        Assert.Same(plain, EventLogCompactor.Compact(plain, Calendar));

        var mixed = new List<WorldEvent>
        {
            new(3, WorldEventKind.WageUnpaid, "1|2|3", 3), new(4, WorldEventKind.ResourceLost, "1|1|4", 4), new(5, WorldEventKind.WageUnpaid, "1|2|5", 5),
        };
        Assert.Equal(
            EventLogCompactor.Compact(mixed, Calendar).Select(e => (e.Tick, e.Kind, e.Payload)).ToArray(),
            EventLogCompactor.Compact(mixed, Calendar).Select(e => (e.Tick, e.Kind, e.Payload)).ToArray());
    }
}
