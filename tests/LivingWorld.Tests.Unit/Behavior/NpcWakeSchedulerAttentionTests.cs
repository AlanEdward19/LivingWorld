using LivingWorld.Domain;
using LivingWorld.Simulation;
using LivingWorld.Simulation.Behavior;

namespace LivingWorld.Tests.Behavior;

/// <summary>Fase 16.3 T28 (COH-43/44): wakes event-driven via AttentionRouter.</summary>
public class NpcWakeSchedulerAttentionTests
{
    private static readonly WorldCalendar Calendar = new(24, 30, 12);
    private static readonly Personality Neutral =
        Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    private static (WorldState World, TickContext Ctx) Build(ulong seed = 1)
    {
        var world = new WorldState(
            Calendar, seed, ScenarioRunner.DefaultMap(seed),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules);
        var ctx = new TickContext(world, world.Rng, world.Scheduler);
        return (world, ctx);
    }

    private static Npc MakeNpc(WorldState world, long id, CellCoord loc)
    {
        var npc = new Npc(
            new NpcId(id), $"n{id}", Sex.Male, WorldDate.Epoch(Calendar).AddYears(-25),
            new CultureId(1), loc, null, null, null, 100, Neutral, ProfessionType.None, loc);
        world.AddNpc(npc);
        return npc;
    }

    [Fact]
    public void ScheduleAttentionWakes_schedules_immediate_wake_for_Active_Intent_npc()
    {
        var (world, ctx) = Build(20);
        long now = world.CurrentDate.TotalHours;
        var dependent = MakeNpc(world, 1, new CellCoord(0, 0));
        dependent.SetIntent(ActionType.Buy, now);
        MakeNpc(world, 2, new CellCoord(0, 0)); // sem intent

        var evt = new WorldEvent(
            now, WorldEventKind.ResourceLost,
            $"{AttentionRouter.PriceChangePrefix}0.01|0|0",
            EventId: 1);

        NpcWakeScheduler.ScheduleAttentionWakes(world, ctx, evt, AttentionRules.Default, now);

        var due = world.Scheduler.PeekDue(now + 1);
        Assert.Contains(due, e => e.SystemName == NpcWakeScheduler.SystemName && e.Payload == "1");
        Assert.DoesNotContain(due, e => e.Payload == "2");
    }

    [Fact]
    public void ScheduleAttentionWakes_dedupes_via_ReplaceNpcWake()
    {
        var (world, ctx) = Build(21);
        long now = world.CurrentDate.TotalHours;
        var npc = MakeNpc(world, 1, new CellCoord(0, 0));
        npc.SetIntent(ActionType.Eat, now);
        var evt = new WorldEvent(
            now, WorldEventKind.ResourceLost,
            $"{AttentionRouter.PriceChangePrefix}0.01|0|0",
            EventId: 1);

        NpcWakeScheduler.ScheduleAttentionWakes(world, ctx, evt, AttentionRules.Default, now);
        NpcWakeScheduler.ScheduleAttentionWakes(world, ctx, evt, AttentionRules.Default, now);

        var due = world.Scheduler.PeekDue(now + 1)
            .Where(e => e.SystemName == NpcWakeScheduler.SystemName && e.Payload == "1")
            .ToList();
        Assert.Single(due);
    }

    [Fact]
    public void ScheduleAttentionWakes_skips_npc_without_Active_Intent()
    {
        var (world, ctx) = Build(22);
        long now = world.CurrentDate.TotalHours;
        var npc = MakeNpc(world, 1, new CellCoord(0, 0));
        // sem SetIntent — AttentionRouter ainda roteia por magnitude baixa só quem tem intent;
        // forçamos um evento de ameaça que roteia por localização, e verificamos o filtro do scheduler.
        var evt = new WorldEvent(
            now, WorldEventKind.CombatResolved,
            $"{AttentionRouter.ThreatPrefix}0|0",
            EventId: 2);

        NpcWakeScheduler.ScheduleAttentionWakes(world, ctx, evt, AttentionRules.Default, now);

        var due = world.Scheduler.PeekDue(now + 1)
            .Where(e => e.SystemName == NpcWakeScheduler.SystemName && e.Payload == "1");
        Assert.Empty(due);
        Assert.Null(npc.IntentStatus);
    }

    [Fact]
    public void ComputeNextWakeTick_honors_eventDrivenWakeTick()
    {
        var (world, _) = Build(23);
        long now = 100;
        var npc = MakeNpc(world, 1, new CellCoord(0, 0));
        // needs sem decay urgente; sem ação → sem eventDriven seria now+1; com eventDriven now+1 também.
        // Forçamos eventDriven mais cedo relativo a um span maior: ação Idle longa.
        npc.SetCurrentAction(ActionType.Idle, now);
        long without = NpcWakeScheduler.ComputeNextWakeTick(
            npc, world.NeedsRules, world.ActionCatalog, now, world);
        long with = NpcWakeScheduler.ComputeNextWakeTick(
            npc, world.NeedsRules, world.ActionCatalog, now, world, eventDrivenWakeTick: now + 1);

        Assert.True(without >= now + 1);
        Assert.Equal(now + 1, with);
        Assert.True(with <= without);
    }
}
