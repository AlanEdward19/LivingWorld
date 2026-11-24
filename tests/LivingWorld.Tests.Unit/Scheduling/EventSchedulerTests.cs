using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Scheduling;

public class EventSchedulerTests
{
    [Fact]
    public void Event_scheduled_for_instant_T_is_due_from_T_until_the_end_of_its_hour()
    {
        var scheduler = new EventScheduler();
        scheduler.Schedule(new ScheduledEvent(1, TargetMs: 10, SystemName: "sys"));

        // ADR-0050: vence no primeiro tick que chega a ele (com Δ grande, o tick pode passar dele dentro da mesma hora); uma hora
        // depois, o evento esquecido continua sem disparar, como antes.
        Assert.Equal((false, true, true, false),
            (scheduler.HasDue(9), scheduler.HasDue(10), scheduler.HasDue(11), scheduler.HasDue(3_600_001)));
    }

    [Fact]
    public void Due_events_inside_the_hour_pop_in_instant_order_and_leave_the_later_ones()
    {
        var scheduler = new EventScheduler();
        scheduler.Schedule(new ScheduledEvent(Id: 1, TargetMs: 900, SystemName: "sys"));
        scheduler.Schedule(new ScheduledEvent(Id: 2, TargetMs: 60, SystemName: "sys"));
        scheduler.Schedule(new ScheduledEvent(Id: 3, TargetMs: 3_600_000, SystemName: "sys"));

        string first = string.Join(',', scheduler.PopDue(1200).Select(e => e.Id));
        string rest = string.Join(',', scheduler.PopDue(3_600_000).Select(e => e.Id));

        Assert.Equal("2,1|3", $"{first}|{rest}");
    }

    [Fact]
    public void Two_events_same_tick_pop_in_ascending_id_order_regardless_of_insertion_order()
    {
        var scheduler = new EventScheduler();
        scheduler.Schedule(new ScheduledEvent(Id: 5, TargetMs: 10, SystemName: "sys"));
        scheduler.Schedule(new ScheduledEvent(Id: 2, TargetMs: 10, SystemName: "sys"));
        scheduler.Schedule(new ScheduledEvent(Id: 8, TargetMs: 10, SystemName: "sys"));

        var due = scheduler.PopDue(10);

        Assert.Equal([2, 5, 8], due.Select(e => e.Id));
    }

    [Fact]
    public void Cancel_removes_pending_event_before_it_fires()
    {
        var scheduler = new EventScheduler();
        scheduler.Schedule(new ScheduledEvent(1, TargetMs: 10, SystemName: "sys"));

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
        scheduler.Schedule(new ScheduledEvent(1, TargetMs: 10, SystemName: "sys"));

        scheduler.PopDue(10);

        Assert.False(scheduler.HasDue(10));
        Assert.Empty(scheduler.PopDue(10));
    }

    [Fact]
    public void Snapshot_orders_by_tick_then_id()
    {
        var scheduler = new EventScheduler();
        scheduler.Schedule(new ScheduledEvent(Id: 3, TargetMs: 20, SystemName: "sys"));
        scheduler.Schedule(new ScheduledEvent(Id: 1, TargetMs: 10, SystemName: "sys"));
        scheduler.Schedule(new ScheduledEvent(Id: 2, TargetMs: 10, SystemName: "sys"));

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
            scheduler.Schedule(new ScheduledEvent(id, TargetMs: id, SystemName: "sys"));

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
        Assert.Equal(remaining.OrderBy(e => e.TargetMs).ThenBy(e => e.Id).Select(e => e.Id), remaining.Select(e => e.Id));
    }

    /// <summary>AD-197c: eventos de dentro da hora (passo fino) e da hora cheia no mesmo balde saem na ordem (instante, Id), com
    /// cancelamento dos dois tipos, vencendo em ticks de 50 ms; 100 mil dentro da mesma hora cabem bem abaixo do teto.</summary>
    [Fact]
    public void Events_inside_the_hour_and_on_the_full_hour_leave_in_instant_then_id_order_and_a_hundred_thousand_fit()
    {
        const int n = 100_000;
        var scheduler = new EventScheduler();
        var watch = System.Diagnostics.Stopwatch.StartNew();
        for (int id = n; id >= 1; id--)
            scheduler.Schedule(new ScheduledEvent(id, TargetMs: id % 10 == 0 ? 3_600_000 : 1 + (id * 37L % 3_599_998), SystemName: "sys"));
        for (int id = 1; id <= n; id += 3) Assert.True(scheduler.Cancel(id));
        var expected = scheduler.Snapshot().Select(e => e.Id).ToList();
        var popped = new List<long>();
        for (long now = 50; now <= 3_600_000; now += 50)
            while (scheduler.HasDue(now)) { var due = scheduler.PopDue(now); popped.AddRange(due.Select(e => e.Id)); scheduler.Release(due); }
        watch.Stop();

        Assert.Equal(expected, popped);
        Assert.Empty(scheduler.Snapshot());
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(2), $"levou {watch.Elapsed}");
    }
}
