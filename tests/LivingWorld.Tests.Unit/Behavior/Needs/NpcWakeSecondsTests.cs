using System.Text.Json;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Needs;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Behavior.Needs;

/// <summary>ADR-0050 (AD-197b): o despertar é agendado em segundos; vence no primeiro lote que chega a ele, e quem acorda mais
/// tarde na mesma hora continua na fila.</summary>
public class NpcWakeSecondsTests
{
    private static readonly WorldCalendar Calendar = new(24, 30, 12);
    private static readonly Personality Neutral = Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    private static WorldState Build(params long[] npcIds)
    {
        var world = new WorldState(
            Calendar, 3, ScenarioRunner.DefaultMap(3), ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog, ScenarioRunner.DefaultLifeStageRules);
        foreach (var id in npcIds)
            world.AddNpc(new Npc(
                new NpcId(id), $"n{id}", Sex.Male, WorldDate.Epoch(Calendar).AddYears(-25),
                new CultureId(1), new CellCoord(0, 0), null, null, null, 100, Neutral, ProfessionType.None, new CellCoord(0, 0)));
        return world;
    }

    [Fact]
    public void A_wake_inside_the_hour_waits_for_its_instant_and_the_later_one_stays_queued()
    {
        var world = Build(1, 2);
        world.ReplaceNpcWakeAtSecond(1, 600);
        world.ReplaceNpcWakeAtSecond(2, 1800);

        NpcWakeScheduler.PrepareWakeBatch(world, 300);
        int atFive = world.NpcWakeBatch.Count;
        NpcWakeScheduler.PrepareWakeBatch(world, 900);
        string atFifteen = string.Join(',', world.NpcWakeBatch.Select(n => n.Id.Value));

        Assert.Equal((0, "1", 1800L), (atFive, atFifteen, world.PendingWakeSecondOf(2)));
    }

    [Fact]
    public void Pending_wake_serializes_hours_on_the_full_hour_and_seconds_inside_it()
    {
        string full = JsonSerializer.Serialize(new PendingNpcWake(1, 5 * 3600, 9));
        string inside = JsonSerializer.Serialize(new PendingNpcWake(1, 5 * 3600 + 30, 9));

        Assert.Equal(
            ("{\"NpcId\":1,\"TargetTick\":5,\"Sequence\":9}", "{\"NpcId\":1,\"TargetTick\":\"18030s\",\"Sequence\":9}"),
            (full, inside));
    }
}
