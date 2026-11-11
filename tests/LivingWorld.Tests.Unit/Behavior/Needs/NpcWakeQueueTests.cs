using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Needs;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Behavior.Needs;

/// <summary>Fila de despertar dedicada (AD-129, #152): um despertar por NPC, lote em ordem declarada, fora do
/// <see cref="EventScheduler"/>.</summary>
public class NpcWakeQueueTests
{
    private static readonly WorldCalendar Calendar = new(24, 30, 12);
    private static readonly Personality Neutral =
        Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    private static (WorldState World, TickContext Ctx) Build(params long[] npcIds)
    {
        var world = new WorldState(
            Calendar, 3, ScenarioRunner.DefaultMap(3),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules);
        foreach (var id in npcIds)
        {
            var loc = new CellCoord(0, 0);
            world.AddNpc(new Npc(
                new NpcId(id), $"n{id}", Sex.Male, WorldDate.Epoch(Calendar).AddYears(-25),
                new CultureId(1), loc, null, null, null, 100, Neutral, ProfessionType.None, loc));
        }
        return (world, new TickContext(world, world.Rng, world.Scheduler));
    }

    [Fact]
    public void Rescheduling_moves_the_wake_so_the_old_tick_no_longer_wakes_the_npc()
    {
        var (world, ctx) = Build(1);
        NpcWakeScheduler.ScheduleWake(world, ctx, 1, 5);
        NpcWakeScheduler.ScheduleWake(world, ctx, 1, 7);

        Assert.Equal(("", "1"), (string.Join(',', world.PeekDueNpcWakes(5)), string.Join(',', world.PeekDueNpcWakes(7))));
    }

    [Fact]
    public void Rescheduling_to_the_same_tick_wakes_the_npc_once()
    {
        var (world, ctx) = Build(1);
        NpcWakeScheduler.ScheduleWake(world, ctx, 1, 5);
        NpcWakeScheduler.ScheduleWake(world, ctx, 1, 5);

        NpcWakeScheduler.PrepareWakeBatch(world, 5);

        Assert.Equal([1L], world.NpcWakeBatch.Select(n => n.Id.Value));
    }

    [Fact]
    public void The_batch_of_a_tick_wakes_the_last_scheduled_first()
    {
        var (world, ctx) = Build(1, 2, 3);
        NpcWakeScheduler.ScheduleWake(world, ctx, 1, 5);
        NpcWakeScheduler.ScheduleWake(world, ctx, 2, 5);
        NpcWakeScheduler.ScheduleWake(world, ctx, 3, 5);
        NpcWakeScheduler.ScheduleWake(world, ctx, 1, 5);

        NpcWakeScheduler.PrepareWakeBatch(world, 5);

        Assert.Equal([1L, 3L, 2L], world.NpcWakeBatch.Select(n => n.Id.Value));
    }

    [Fact]
    public void The_wake_of_a_dead_npc_is_consumed_without_waking_anyone()
    {
        var (world, ctx) = Build(1);
        NpcWakeScheduler.ScheduleWake(world, ctx, 1, 5);
        world.FindNpc(new NpcId(1))!.Die(world.CurrentDate);

        NpcWakeScheduler.PrepareWakeBatch(world, 5);

        Assert.Equal((0, 0), (world.NpcWakeBatch.Count, world.PendingNpcWakes.Count));
    }

    [Fact]
    public void A_wake_takes_a_sequence_from_the_event_id_counter_and_no_scheduled_event()
    {
        var (world, ctx) = Build(1);
        long before = world.NextEventId;

        NpcWakeScheduler.ScheduleWake(world, ctx, 1, 5);

        Assert.Equal(
            (before + 1, 0, new PendingNpcWake(1, 5, before)),
            (world.NextEventId, world.PendingEvents.Count, world.PendingNpcWakes.Single()));
    }

    [Fact]
    public void Pending_wakes_are_listed_one_per_npc_in_id_order()
    {
        var (world, ctx) = Build(1, 2, 3);
        NpcWakeScheduler.ScheduleWake(world, ctx, 3, 9);
        NpcWakeScheduler.ScheduleWake(world, ctx, 1, 6);
        NpcWakeScheduler.ScheduleWake(world, ctx, 2, 4);
        NpcWakeScheduler.ScheduleWake(world, ctx, 3, 8);

        Assert.Equal([(1L, 6L), (2L, 4L), (3L, 8L)], world.PendingNpcWakes.Select(w => (w.NpcId, w.TargetTick)));
    }

    [Fact]
    public void The_pending_wake_tick_is_part_of_the_canonical_hash()
    {
        var (early, earlyCtx) = Build(1);
        var (late, lateCtx) = Build(1);
        NpcWakeScheduler.ScheduleWake(early, earlyCtx, 1, 5);
        NpcWakeScheduler.ScheduleWake(late, lateCtx, 1, 6);

        Assert.NotEqual(WorldSnapshot.CanonicalHash(early), WorldSnapshot.CanonicalHash(late));
    }
}
