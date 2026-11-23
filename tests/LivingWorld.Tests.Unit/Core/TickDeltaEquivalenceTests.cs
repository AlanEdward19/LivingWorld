using LivingWorld.Domain.Behavior;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Players;
using LivingWorld.Simulation.Scenarios;
using static LivingWorld.Tests.Unit.Players.PlayerAbsenceTests;

namespace LivingWorld.Tests.Unit.Core;

/// <summary>ADR-0050 (AD-197b), prova do Δ menor: o mesmo mundo, sem jogador nem entradas, avançado N horas em ticks de uma hora e
/// em ticks de um minuto (e de um segundo), chega ao mesmo estado. Nesta fatia isso vale para o mundo inteiro, e não por truque:
/// tudo que decide (sistemas de fronteira, decisão por despertar, eventos) ainda calcula em horas e só roda na hora cheia, e o
/// que já é em segundos (relógio, fila de eventos, despertar, necessidades) dá a mesma conta na hora cheia. As divergências
/// esperadas quando a decisão passar a ser fina (AD-197c) estão em docs/domain/time-in-seconds.md.</summary>
public class TickDeltaEquivalenceTests
{
    [Theory]
    [InlineData(60)]
    [InlineData(900)]
    public void A_village_run_by_the_hour_and_by_a_smaller_delta_reaches_the_same_canonical_world(int delta)
    {
        var hourly = Village(71, initialPopulation: 40);
        var fine = Village(71, initialPopulation: 40);

        hourly.Clock.Run(hourly.World, 48);
        fine.Clock.Run(fine.World, 48 * (3600 / delta), delta);

        Assert.Equal(WorldSnapshot.CanonicalHash(hourly.World), WorldSnapshot.CanonicalHash(fine.World));
    }

    [Fact]
    public void Needs_read_inside_the_hour_lie_between_the_two_full_hours()
    {
        var arm = Village(72, initialPopulation: 20);
        var npc = arm.World.Npcs.First(n => n.IsAlive);
        long hour = arm.World.CurrentDate.TotalHours;

        double atHour = npc.HungerNeed.ValueAt(hour + 1), atNext = npc.HungerNeed.ValueAt(hour + 2);
        double inside = npc.HungerNeed.ValueAtSecond((hour + 1) * 3600 + 1800);

        Assert.InRange(inside, Math.Min(atHour, atNext), Math.Max(atHour, atNext));
    }

    [Fact]
    public void Replay_with_the_recorded_sequence_of_deltas_reproduces_the_world()
    {
        var live = Village(73, initialPopulation: 40);
        string snapshot = WorldSnapshot.Serialize(live.World);
        var players = new PlayerService(live.Sink);
        var adult = live.World.Npcs.First(n => n.IsAlive && n.Employer is not null);

        Assert.True(players.Incarnate(live.World, adult.Id, "ana").IsSuccess);
        live.Clock.Run(live.World, 3);
        Assert.True(players.SetTickDelta(live.World, "admin", 60).IsSuccess);
        live.Clock.Run(live.World, 150, players.Log.TickDeltaSeconds); // 2 h 30 min em minutos
        Assert.True(players.Act(live.World, "ana", ActionType.Idle).IsSuccess); // dentro da hora
        live.Clock.Run(live.World, 45, players.Log.TickDeltaSeconds);
        Assert.True(players.SetTickDelta(live.World, "admin", 3600).IsSuccess);
        live.Clock.Run(live.World, 20, players.Log.TickDeltaSeconds); // o primeiro tick para na hora cheia
        Assert.True(players.Logout(live.World, "ana").IsSuccess);
        live.Clock.Run(live.World, 10);
        long end = live.World.CurrentDate.TotalSeconds;

        var replayed = PlayerReplay.Run(snapshot, players.Log.Records, new WorldClock(ScenarioRunner.DefaultSystems(), sink: new AllEvents()),
            new AllEvents(), end);

        Assert.Equal(
            (2, WorldSnapshot.CanonicalHash(live.World)),
            (players.Log.Records.Count(r => r.Kind == PlayerCommandKind.TickDelta), WorldSnapshot.CanonicalHash(replayed)));
    }

    [Fact]
    public void A_delta_that_does_not_divide_the_hour_is_refused_and_not_recorded()
    {
        var arm = Village(74, initialPopulation: 10);
        var players = new PlayerService(arm.Sink);

        bool accepted = players.SetTickDelta(arm.World, "admin", 7).IsSuccess;

        Assert.Equal((false, 0, 3600), (accepted, players.Log.Records.Count, players.Log.TickDeltaSeconds));
    }
}
