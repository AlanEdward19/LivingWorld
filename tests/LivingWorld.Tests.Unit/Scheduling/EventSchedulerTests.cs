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
}
