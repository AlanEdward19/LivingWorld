using LivingWorld.Domain.History;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Narrative;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Narrative;

/// <summary>Fase 12, T5: <see cref="NpcBiographyQuery"/> (NARR-16..18) — linha do tempo de
/// participação do NPC, ordem cronológica, sem eventos após a morte.</summary>
public class NpcBiographyQueryTests
{
    [Fact]
    public void Timeline_orders_participating_facts_chronologically()
    {
        var (world, _) = ScenarioRunner.Create(1, historyRules: HistoryRules.Default);
        var npc = world.Npcs[0];
        world.AddFact(new Fact(world.NextFactIdAndAdvance(), 30, WorldEventKind.Marriage, [npc.Id], npc.City, 0.5, "third"));
        world.AddFact(new Fact(world.NextFactIdAndAdvance(), 10, WorldEventKind.Death, [npc.Id], npc.City, 0.5, "first"));
        world.AddFact(new Fact(world.NextFactIdAndAdvance(), 20, WorldEventKind.CourtshipRejected, [npc.Id], npc.City, 0.5, "second"));

        var result = NpcBiographyQuery.Timeline(world, npc.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { "first", "second", "third" }, result.Value!.Select(f => f.Payload));
    }

    [Fact]
    public void Timeline_excludes_facts_where_the_npc_does_not_participate()
    {
        var (world, _) = ScenarioRunner.Create(1, historyRules: HistoryRules.Default);
        var npc = world.Npcs[0];
        var other = world.Npcs[1];
        world.AddFact(new Fact(world.NextFactIdAndAdvance(), 10, WorldEventKind.Death, [npc.Id], npc.City, 0.5, "mine"));
        world.AddFact(new Fact(world.NextFactIdAndAdvance(), 20, WorldEventKind.Death, [other.Id], other.City, 0.5, "not-mine"));

        var result = NpcBiographyQuery.Timeline(world, npc.Id);

        Assert.Single(result.Value!);
        Assert.Equal("mine", result.Value![0].Payload);
    }

    [Fact]
    public void Timeline_excludes_events_strictly_after_the_death_tick()
    {
        var (world, _) = ScenarioRunner.Create(1, historyRules: HistoryRules.Default);
        var npc = world.Npcs[0];
        npc.Die(new WorldDate(world.Calendar, 50));
        world.AddFact(new Fact(world.NextFactIdAndAdvance(), 40, WorldEventKind.Marriage, [npc.Id], npc.City, 0.5, "before-death"));
        world.AddFact(new Fact(world.NextFactIdAndAdvance(), 50, WorldEventKind.Death, [npc.Id], npc.City, 0.9, "death-itself"));
        world.AddFact(new Fact(world.NextFactIdAndAdvance(), 60, WorldEventKind.Marriage, [npc.Id], npc.City, 0.5, "after-death"));

        var result = NpcBiographyQuery.Timeline(world, npc.Id);

        Assert.Equal(new[] { "before-death", "death-itself" }, result.Value!.Select(f => f.Payload));
        Assert.DoesNotContain(result.Value!, f => f.Payload == "after-death");
    }

    [Fact]
    public void Timeline_fails_for_an_unknown_npc()
    {
        var (world, _) = ScenarioRunner.Create(1, historyRules: HistoryRules.Default);

        var result = NpcBiographyQuery.Timeline(world, new NpcId(999_999));

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Timeline_breaks_ties_at_the_same_tick_by_fact_id_for_determinism()
    {
        var (world, _) = ScenarioRunner.Create(1, historyRules: HistoryRules.Default);
        var npc = world.Npcs[0];
        var lowerId = new Fact(world.NextFactIdAndAdvance(), 10, WorldEventKind.Marriage, [npc.Id], npc.City, 0.5, "b");
        var higherId = new Fact(world.NextFactIdAndAdvance(), 10, WorldEventKind.Death, [npc.Id], npc.City, 0.5, "a");
        world.AddFact(lowerId);
        world.AddFact(higherId);

        var result = NpcBiographyQuery.Timeline(world, npc.Id);

        Assert.Equal(new[] { "b", "a" }, result.Value!.Select(f => f.Payload));
    }
}
