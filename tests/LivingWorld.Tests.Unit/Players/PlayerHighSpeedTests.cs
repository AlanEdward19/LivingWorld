using LivingWorld.Domain.Population;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Hosting;
using LivingWorld.Simulation.Players;
using LivingWorld.Simulation.Scenarios;
using static LivingWorld.Tests.Unit.Players.PlayerAbsenceTests;

namespace LivingWorld.Tests.Unit.Players;

/// <summary>#261: a lista de velocidades vem do cenário (1× é tempo real; 3×, 10×, 60×, 300× pulam a rotina). Em velocidade alta o Δ do tick fica limitado
/// (<see cref="TimeRules.MaxStepMs"/>) e o motor roda mais ticks por segundo em vez de Δ maior; o corpo só anda por ordem do motor; o replay reproduz o mesmo mundo.</summary>
public class PlayerHighSpeedTests
{
    private sealed record Host(Arm Arm, SimulationHost Simulation, PlayerService Players);

    private static Host HostOf(Arm arm) => new(arm, new SimulationHost(new WorldHost(arm.World, arm.Clock)), new PlayerService(arm.Sink));

    private static PlayerRules Rules() => PlayerRules.Default with { Time = TimeRules.Default with { Speeds = [1, 3, 10, 60, 300] } };

    [Fact]
    public void The_scenario_lists_its_speeds_and_the_old_ceiling_still_works()
    {
        Assert.Equal([1, 3, 10, 60, 300], Rules().Time.SpeedList);
        Assert.Equal(300, Rules().Time.MaxPlayerSpeed);
        Assert.Equal([1, 2, 3], TimeRules.Default.SpeedList); // sem lista: 1..3 como na #260
        Assert.Equal([1, 2], (TimeRules.Default with { MaxPlayerSpeedValue = 2 }).SpeedList);
        Assert.True(Rules().Time.IsValidSpeed(0) && Rules().Time.IsValidSpeed(60) && !Rules().Time.IsValidSpeed(2) && !Rules().Time.IsValidSpeed(301));
    }

    [Fact]
    public void The_sandbox_period_declares_the_list_in_data_and_walking_stops_above_ten_times()
    {
        var root = LivingWorld.Tests.Unit.Cities.SandboxFixtures.CoupleOnly(LivingWorld.Tests.Unit.Cities.SandboxFixtures.Parse());
        var loaded = ScenarioLoaderV2.LoadWorld(root.ToJsonString());
        Assert.True(loaded.IsSuccess, loaded.Error);
        var time = loaded.Value.World.Players.Time;
        Assert.Equal([1, 3, 10, 60, 300], time.SpeedList);
        Assert.Equal(1.0, time.TimeScale); // 1× continua tempo real
        Assert.Equal(10, time.WalkMaxSpeed); // até 10× o corpo anda por WASD; acima só por ordem do motor
    }

    [Fact]
    public void The_engine_validates_only_the_speeds_of_the_list_and_runs_more_ticks_per_second_instead_of_a_huge_delta()
    {
        var host = HostOf(Village(2611, initialPopulation: 20, rules: Rules()));
        var world = host.Arm.World;
        Assert.True(host.Players.Incarnate(world, Pick(world).Id, "ana").IsSuccess);

        Assert.False(host.Players.SetSpeed(world, host.Simulation, "ana", 2).IsSuccess); // fora da lista do cenário
        var seen = new List<(int Speed, int Delta, double Tps)>();
        foreach (int speed in new[] { 1, 3, 10, 60, 300 })
        {
            Assert.True(host.Players.SetSpeed(world, host.Simulation, "ana", speed).IsSuccess);
            host.Simulation.Cycle(host.Players);
            var plan = host.Simulation.LastPlan;
            seen.Add((speed, plan.DeltaMs, plan.TicksPerSecond));
            // o mundo anda `speed` segundos por segundo real: Δ × ticks por segundo
            Assert.Equal(1000.0 * speed, plan.DeltaMs * plan.TicksPerSecond, 3);
        }

        Assert.Equal((50, 20.0), (seen[0].Delta, seen[0].Tps)); // 1×: tempo real, como sempre
        Assert.Equal((150, 20.0), (seen[1].Delta, seen[1].Tps));
        Assert.All(seen, s => Assert.True(s.Delta <= world.Players.Time.MaxStepMs));
        Assert.Equal(2000, seen[4].Delta);
        Assert.Equal(150.0, seen[4].Tps);
    }

    [Fact]
    public void At_300x_the_world_stays_correct_the_clock_the_needs_and_the_body_use()
    {
        var fast = HostOf(Village(2612, initialPopulation: 40, rules: Rules()));
        var slow = HostOf(Village(2612, initialPopulation: 40, rules: Rules()));
        var npcId = Pick(fast.Arm.World).Id;
        Assert.True(fast.Players.Incarnate(fast.Arm.World, npcId, "ana").IsSuccess);
        Assert.True(slow.Players.Incarnate(slow.Arm.World, npcId, "ana").IsSuccess);
        Assert.True(fast.Players.SetSpeed(fast.Arm.World, fast.Simulation, "ana", 300).IsSuccess);
        long start = fast.Arm.World.CurrentDate.TotalMilliseconds;

        // meia hora de mundo a 300× = 900 ticks de 2 s; o mesmo tempo em passos de 500 ms (a mesma regra, mais fina)
        for (int i = 0; i < 900; i++) fast.Simulation.Cycle(fast.Players);
        Assert.True(slow.Players.SetTickDelta(slow.Arm.World, "ana", 500).IsSuccess);
        while (slow.Arm.World.CurrentDate.TotalMilliseconds < start + 1_800_000) slow.Arm.Clock.Tick(slow.Arm.World, 500);

        Assert.Equal(start + 1_800_000, fast.Arm.World.CurrentDate.TotalMilliseconds); // o relógio fecha exato: nenhum tick pulou tempo
        var a = fast.Arm.World.FindNpc(npcId)!;
        var b = slow.Arm.World.FindNpc(npcId)!;
        long end = fast.Arm.World.CurrentDate.TotalMilliseconds;
        // as necessidades são do relógio, não do Δ: a queda é a mesma (nada acelerado além do tempo)
        Assert.Equal(b.HungerNeed.ValueAtMs(end), a.HungerNeed.ValueAtMs(end), 0);
        Assert.Equal(b.SleepNeed.ValueAtMs(end), a.SleepNeed.ValueAtMs(end), 0);
        Assert.True(a.IsAlive && b.IsAlive);
    }

    [Fact]
    public void In_high_speed_the_wasd_walk_is_refused_with_the_reason_and_comes_back_at_normal_speed()
    {
        var host = HostOf(Village(2613, initialPopulation: 20, rules: Rules()));
        var world = host.Arm.World;
        Assert.True(host.Players.Incarnate(world, Pick(world).Id, "ana").IsSuccess);
        Assert.True(host.Players.SetSpeed(world, host.Simulation, "ana", 60).IsSuccess);
        host.Simulation.Cycle(host.Players);

        var refused = host.Players.Walk(world, "ana", 1000, 0);
        Assert.False(refused.IsSuccess);
        Assert.Contains("velocidade alta", refused.Error);

        Assert.True(host.Players.SetSpeed(world, host.Simulation, "ana", 1).IsSuccess);
        host.Simulation.Cycle(host.Players);
        Assert.True(host.Players.Walk(world, "ana", 1000, 0).IsSuccess);
    }

    [Fact]
    public void A_replay_across_high_speeds_reproduces_the_same_world_hash()
    {
        var arm = Village(2614, initialPopulation: 40, rules: Rules());
        string snapshot = WorldSnapshot.Serialize(arm.World);
        var host = HostOf(arm);
        var world = arm.World;
        Assert.True(host.Players.Incarnate(world, Pick(world).Id, "ana").IsSuccess);
        foreach (var (speed, cycles) in new[] { (1, 30), (10, 60), (300, 200), (60, 80), (1, 20) })
        {
            Assert.True(host.Players.SetSpeed(world, host.Simulation, "ana", speed).IsSuccess);
            for (int i = 0; i < cycles; i++) host.Simulation.Cycle(host.Players);
        }

        long end = world.CurrentDate.TotalMilliseconds;
        var replayed = PlayerReplay.Run(snapshot, host.Players.Log.Records, new WorldClock(ScenarioRunner.DefaultSystems(), sink: new AllEvents()), new AllEvents(), end);

        Assert.Equal(new int?[] { 1, 10, 300, 60, 1 }, host.Players.Log.Records.Where(r => r.Kind == PlayerCommandKind.Speed).Select(r => r.Count).ToArray());
        Assert.Contains(host.Players.Log.Records, r => r.Kind == PlayerCommandKind.TickDelta && r.DeltaMs == 2000);
        Assert.Equal(WorldSnapshot.CanonicalHash(world), WorldSnapshot.CanonicalHash(replayed));
    }
}
