using LivingWorld.Domain.Population;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Hosting;
using LivingWorld.Simulation.Players;
using LivingWorld.Simulation.Scenarios;
using static LivingWorld.Tests.Unit.Players.PlayerAbsenceTests;

namespace LivingWorld.Tests.Unit.Players;

/// <summary>#260 (UX do sandbox): a velocidade do tempo do jogador, como o 1×/2×/3× e a pausa do The Sims. Só vale com ele sozinho no mundo, o motor a valida
/// (0 a <see cref="TimeRules.MaxPlayerSpeed"/>), o Δ do tick fino cresce com ela sem quebrar o 1:1 padrão, a pausa não roda tick e o replay reproduz o mesmo mundo.</summary>
public class PlayerSpeedTests
{
    private sealed record Host(Arm Arm, SimulationHost Simulation, PlayerService Players);

    private static Host HostOf(Arm arm) => new(arm, new SimulationHost(new WorldHost(arm.World, arm.Clock)), new PlayerService(arm.Sink));

    [Fact]
    public void The_default_stays_one_to_one_and_each_speed_multiplies_the_fine_delta()
    {
        var host = HostOf(Village(260, initialPopulation: 20));
        var world = host.Arm.World;
        Assert.True(host.Players.Incarnate(world, Pick(world).Id, "ana").IsSuccess);
        host.Simulation.Cycle(host.Players);
        Assert.Equal((RhythmMode.RealTime, 50), (host.Simulation.LastPlan.Mode, host.Simulation.LastPlan.DeltaMs)); // 1:1 padrão, como antes

        var deltas = new List<int>();
        foreach (int speed in new[] { 2, 3, 1 })
        {
            Assert.True(host.Players.SetSpeed(world, host.Simulation, "ana", speed).IsSuccess);
            host.Simulation.Cycle(host.Players);
            deltas.Add(host.Simulation.LastPlan.DeltaMs);
        }

        Assert.Equal([100, 150, 50], deltas);
    }

    [Fact]
    public void The_speed_is_validated_by_the_engine_range_and_only_for_who_plays_alone()
    {
        var host = HostOf(Village(261, initialPopulation: 40));
        var world = host.Arm.World;
        var npc = Pick(world);
        Assert.True(host.Players.Incarnate(world, npc.Id, "ana").IsSuccess);

        Assert.False(host.Players.SetSpeed(world, host.Simulation, "ana", 4).IsSuccess); // acima do máximo do cenário
        Assert.False(host.Players.SetSpeed(world, host.Simulation, "ana", -1).IsSuccess);
        Assert.False(host.Players.SetSpeed(world, host.Simulation, "ninguem", 2).IsSuccess); // não conduz ninguém
        Assert.Equal(1, host.Simulation.PlayerSpeed);

        Assert.True(host.Players.SetSpeed(world, host.Simulation, "ana", 3).IsSuccess);
        var other = world.Npcs.First(n => n.IsAlive && n.Id != npc.Id);
        Assert.True(host.Players.Incarnate(world, other.Id, "bia").IsSuccess);
        Assert.False(host.Players.SetSpeed(world, host.Simulation, "ana", 2).IsSuccess); // com mais de um jogador ninguém muda o tempo
        Assert.Equal(1, host.Simulation.EffectiveSpeed(world)); // e o relógio volta a 1× para todos
        host.Simulation.Cycle(host.Players);
        Assert.Equal(50, host.Simulation.LastPlan.DeltaMs);
    }

    [Fact]
    public void The_scenario_can_cap_the_speed_in_its_rules()
    {
        var rules = PlayerRules.Default with { Time = TimeRules.Default with { MaxPlayerSpeedValue = 2 } };
        var host = HostOf(Village(262, initialPopulation: 20, rules: rules));
        var world = host.Arm.World;
        Assert.True(host.Players.Incarnate(world, Pick(world).Id, "ana").IsSuccess);

        Assert.True(host.Players.SetSpeed(world, host.Simulation, "ana", 2).IsSuccess);
        Assert.False(host.Players.SetSpeed(world, host.Simulation, "ana", 3).IsSuccess);
    }

    [Fact]
    public void Speed_zero_pauses_the_world_for_the_lone_player_and_one_resumes_it()
    {
        var host = HostOf(Village(263, initialPopulation: 20));
        var world = host.Arm.World;
        Assert.True(host.Players.Incarnate(world, Pick(world).Id, "ana").IsSuccess);
        Assert.False(host.Simulation.PlayerPaused(world));

        Assert.True(host.Players.SetSpeed(world, host.Simulation, "ana", 0).IsSuccess);

        Assert.True(host.Simulation.PlayerPaused(world)); // o laço da API não roda tick enquanto isto vale
        Assert.True(host.Players.SetSpeed(world, host.Simulation, "ana", 1).IsSuccess); // os comandos seguem aceitos para voltar
        Assert.False(host.Simulation.PlayerPaused(world));
    }

    [Fact]
    public void The_speed_is_recorded_and_the_replay_reproduces_the_same_world()
    {
        var arm = Village(264, initialPopulation: 40);
        string snapshot = WorldSnapshot.Serialize(arm.World);
        var host = HostOf(arm);
        var world = arm.World;
        Assert.True(host.Players.Incarnate(world, Pick(world).Id, "ana").IsSuccess);
        for (int i = 0; i < 40; i++) host.Simulation.Cycle(host.Players);
        Assert.True(host.Players.SetSpeed(world, host.Simulation, "ana", 3).IsSuccess);
        for (int i = 0; i < 80; i++) host.Simulation.Cycle(host.Players);
        Assert.True(host.Players.SetSpeed(world, host.Simulation, "ana", 2).IsSuccess);
        for (int i = 0; i < 40; i++) host.Simulation.Cycle(host.Players);
        Assert.True(host.Players.SetSpeed(world, host.Simulation, "ana", 1).IsSuccess);
        for (int i = 0; i < 20; i++) host.Simulation.Cycle(host.Players);
        long end = world.CurrentDate.TotalMilliseconds;

        var replayed = PlayerReplay.Run(snapshot, host.Players.Log.Records, new WorldClock(ScenarioRunner.DefaultSystems(), sink: new AllEvents()), new AllEvents(), end);

        var speeds = host.Players.Log.Records.Where(r => r.Kind == PlayerCommandKind.Speed).Select(r => r.Count).ToList();
        Assert.Equal(new int?[] { 3, 2, 1 }, speeds); // a escolha vai para o registro
        Assert.Contains(host.Players.Log.Records, r => r.Kind == PlayerCommandKind.TickDelta && r.DeltaMs == 150); // e o Δ que ela causa também
        Assert.Equal(WorldSnapshot.CanonicalHash(world), WorldSnapshot.CanonicalHash(replayed));
    }

    [Fact]
    public void Creating_or_opening_another_world_drops_the_speed()
    {
        var host = HostOf(Village(265, initialPopulation: 20));
        var world = host.Arm.World;
        Assert.True(host.Players.Incarnate(world, Pick(world).Id, "ana").IsSuccess);
        Assert.True(host.Players.SetSpeed(world, host.Simulation, "ana", 3).IsSuccess);

        host.Simulation.ResetRhythmOverrides();

        Assert.Equal(1, host.Simulation.PlayerSpeed);
    }
}
