using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Scheduling;

public class EventSchedulerTests
{
    [Fact]
    public void Event_scheduled_for_tick_T_is_due_exactly_at_tick_T()
    {
        var scheduler = new EventScheduler();
        scheduler.Schedule(new ScheduledEvent(1, TargetTick: 10, SystemName: "sys"));

        Assert.False(scheduler.HasDue(9));
        Assert.True(scheduler.HasDue(10));
        Assert.False(scheduler.HasDue(11));
    }

    [Fact]
    public void Two_events_same_tick_pop_in_ascending_id_order_regardless_of_insertion_order()
    {
        var scheduler = new EventScheduler();
        scheduler.Schedule(new ScheduledEvent(Id: 5, TargetTick: 10, SystemName: "sys"));
        scheduler.Schedule(new ScheduledEvent(Id: 2, TargetTick: 10, SystemName: "sys"));
        scheduler.Schedule(new ScheduledEvent(Id: 8, TargetTick: 10, SystemName: "sys"));

        var due = scheduler.PopDue(10);

        Assert.Equal([2, 5, 8], due.Select(e => e.Id));
    }

    [Fact]
    public void Cancel_removes_pending_event_before_it_fires()
    {
        var scheduler = new EventScheduler();
        scheduler.Schedule(new ScheduledEvent(1, TargetTick: 10, SystemName: "sys"));

        var cancelled = scheduler.Cancel(1);

        Assert.True(cancelled);
        Assert.False(scheduler.HasDue(10));
    }

    [Fact]
    public void Cancel_unknown_id_returns_false()
    {
        var scheduler = new EventScheduler();
        Assert.False(scheduler.Cancel(999));
    }

    [Fact]
    public void PopDue_removes_events_so_they_do_not_fire_twice()
    {
        var scheduler = new EventScheduler();
        scheduler.Schedule(new ScheduledEvent(1, TargetTick: 10, SystemName: "sys"));

        scheduler.PopDue(10);

        Assert.False(scheduler.HasDue(10));
        Assert.Empty(scheduler.PopDue(10));
    }

    [Fact]
    public void Snapshot_orders_by_tick_then_id()
    {
        var scheduler = new EventScheduler();
        scheduler.Schedule(new ScheduledEvent(Id: 3, TargetTick: 20, SystemName: "sys"));
        scheduler.Schedule(new ScheduledEvent(Id: 1, TargetTick: 10, SystemName: "sys"));
        scheduler.Schedule(new ScheduledEvent(Id: 2, TargetTick: 10, SystemName: "sys"));

        var snapshot = scheduler.Snapshot();

        Assert.Equal([1, 2, 3], snapshot.Select(e => e.Id));
    }

    [Fact]
    public void Cancel_and_insertion_stay_ordered_and_correct_with_a_hundred_thousand_events_over_as_many_ticks()
    {
        // um Cancel que varresse os baldes (PERF-14) faria 50.000 x 100.000 buscas, bem acima do teto; por índice de id termina em ms
        const int n = 100_000;
        var scheduler = new EventScheduler();
        for (int id = n; id >= 1; id--) // ids em ordem inversa: a inserção ordena
            scheduler.Schedule(new ScheduledEvent(id, TargetTick: id, SystemName: "sys"));

        var watch = System.Diagnostics.Stopwatch.StartNew();
        int cancelled = 0;
        for (int id = 1; id <= n; id += 2)
            if (scheduler.Cancel(id)) cancelled++;
        watch.Stop();

        Assert.Equal(n / 2, cancelled);
        Assert.False(scheduler.Cancel(1)); // já cancelado
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(2), $"{cancelled} cancelamentos levaram {watch.Elapsed}");
        var remaining = scheduler.Snapshot();
        Assert.Equal(n / 2, remaining.Count);
        Assert.Equal(remaining.OrderBy(e => e.TargetTick).ThenBy(e => e.Id).Select(e => e.Id), remaining.Select(e => e.Id));
    }
}
