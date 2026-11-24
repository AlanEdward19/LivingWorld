using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Decision;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Hosting;
using LivingWorld.Simulation.Players;
using LivingWorld.Simulation.Scenarios;
using static LivingWorld.Tests.Unit.Players.PlayerAbsenceTests;

namespace LivingWorld.Tests.Unit.Players;

/// <summary>AD-197c (ADR-0050): com jogador o mundo roda em tempo real (Δ fino na escala do mundo), os NPCs decidem, agem e andam
/// dentro da hora, o admin e o pulo de tempo de quem joga sozinho entram no registro, e o sono acelerado é interrompido pelo motor.</summary>
public class FineTimeTests
{
    private const int Minute = 60_000;

    private sealed record Host(Arm Arm, SimulationHost Simulation, PlayerService Players);

    private static Host HostOf(Arm arm) => new(arm, new SimulationHost(new WorldHost(arm.World, arm.Clock)), new PlayerService(arm.Sink));

    [Fact]
    public void A_walker_passes_through_intermediate_cells_inside_the_hour_with_a_fine_delta()
    {
        var arm = Village(91, initialPopulation: 60);
        Npc? walker = null;
        for (int i = 0; i < 24 * 60 && walker is null; i++)
        {
            arm.Clock.Tick(arm.World, Minute);
            long now = arm.World.CurrentDate.TotalMilliseconds;
            walker = arm.World.Npcs.FirstOrDefault(n => n.IsAlive && n.CurrentAction == ActionType.Travel && n.ActionStartedAtMs == now
                && BehaviorDecisionSystem.TravelDestinationFor(arm.World, n, LivingWorld.Simulation.Economy.Market.MarketIndex.BuildForTick(arm.World)) is { } to
                && LivingWorld.Simulation.Cities.Streets.StreetRoutes.PathOf(arm.World, n.CurrentLocation, to).Count >= 4);
        }
        Assert.NotNull(walker);
        var origin = walker!.CurrentLocation;
        var poses = new List<(long Ms, LivingWorld.Domain.Geography.CellCoord Cell)>();
        while (walker.CurrentAction == ActionType.Travel && walker.CurrentLocation == origin && poses.Count < 6 * 60)
        {
            poses.Add((arm.World.CurrentDate.TotalMilliseconds, TravelPose.CellAt(arm.World, walker, arm.World.CurrentDate.TotalMilliseconds)));
            arm.Clock.Tick(arm.World, Minute);
        }

        var cells = poses.Select(p => p.Cell).Distinct().ToList();
        bool movedInsideHour = poses.Zip(poses.Skip(1)).Any(p => p.First.Cell != p.Second.Cell && p.Second.Ms % WorldMillis.PerHour != 0);
        Assert.True(cells.Count >= 3 && movedInsideHour, $"poses distintas {cells.Count}, dentro da hora {movedInsideHour}");
    }

    [Fact]
    public void With_a_fine_delta_actions_start_inside_the_hour_and_by_the_hour_they_never_do()
    {
        var fine = Village(92, initialPopulation: 40);
        var hourly = Village(92, initialPopulation: 40);

        fine.Clock.Run(fine.World, 6 * 60, Minute);
        hourly.Clock.Run(hourly.World, 6);

        Assert.Equal((true, 0), (fine.World.Npcs.Any(n => n.ActionPhaseMs != 0), hourly.World.Npcs.Count(n => n.ActionPhaseMs != 0)));
    }

    [Fact]
    public void The_body_a_player_conducts_obeys_on_the_next_fine_tick_not_at_the_full_hour()
    {
        var host = HostOf(Village(93, initialPopulation: 30));
        var world = host.Arm.World;
        var npc = Pick(world);
        Assert.True(host.Players.Incarnate(world, npc.Id, "ana").IsSuccess);
        host.Simulation.Cycle(host.Players); // jogador presente: tempo real
        host.Simulation.Cycle(host.Players);

        Assert.True(host.Players.Act(world, "ana", npc.CurrentAction == ActionType.Socialize ? ActionType.Idle : ActionType.Socialize).IsSuccess);
        long asked = world.CurrentDate.TotalMilliseconds;
        host.Simulation.Cycle(host.Players);

        Assert.Equal((RhythmMode.RealTime, asked + 50, true),
            (host.Simulation.LastPlan.Mode, npc.ActionStartedAtMs, npc.ActionStartedAtMs % WorldMillis.PerHour != 0));
    }

    [Fact]
    public void The_host_runs_fast_without_players_real_time_with_one_and_back_when_they_leave()
    {
        var host = HostOf(Village(94, initialPopulation: 20, rules: PlayerRules.Default with { Time = TimeRules.Default with { TimeScale = 12 } }));
        var world = host.Arm.World;
        var modes = new List<(RhythmMode, int)>();
        host.Simulation.Cycle(host.Players);
        modes.Add((host.Simulation.LastPlan.Mode, host.Simulation.LastPlan.DeltaMs));
        Assert.True(host.Players.Incarnate(world, Pick(world).Id, "ana").IsSuccess);
        host.Simulation.Cycle(host.Players);
        modes.Add((host.Simulation.LastPlan.Mode, host.Simulation.LastPlan.DeltaMs));
        Assert.True(host.Players.Logout(world, "ana").IsSuccess);
        host.Simulation.Cycle(host.Players);
        modes.Add((host.Simulation.LastPlan.Mode, host.Simulation.LastPlan.DeltaMs));

        Assert.Equal([(RhythmMode.Fast, WorldMillis.HourTick), (RhythmMode.RealTime, 600), (RhythmMode.Fast, WorldMillis.HourTick)], modes);
    }

    [Fact]
    public void Replay_with_delta_changes_an_admin_step_and_an_accelerated_sleep_reproduces_the_hash()
    {
        var arm = Village(95, initialPopulation: 40);
        string snapshot = WorldSnapshot.Serialize(arm.World);
        var host = HostOf(arm);
        var world = arm.World;
        for (int i = 0; i < 3; i++) host.Simulation.Cycle(host.Players);
        var npc = Pick(world);
        Assert.True(host.Players.Incarnate(world, npc.Id, "ana").IsSuccess);
        for (int i = 0; i < 1200; i++) host.Simulation.Cycle(host.Players); // um minuto real a 50 ms
        Assert.True(host.Players.Advance(world, arm.Clock, "admin", 1, WorldMillis.HourTick).IsSuccess); // step do admin
        for (int i = 0; i < 300; i++) host.Simulation.Cycle(host.Players);
        Assert.True(host.Players.Sleep(world, "ana", null).IsSuccess);
        for (int i = 0; i < 48 && host.Players.Skip is not null; i++) host.Simulation.Cycle(host.Players);
        if (host.Players.Skip is not null) Assert.True(host.Players.EndSkip(world, "jogador").IsSuccess);
        for (int i = 0; i < 300; i++) host.Simulation.Cycle(host.Players);
        Assert.True(host.Players.Logout(world, "ana").IsSuccess);
        for (int i = 0; i < 5; i++) host.Simulation.Cycle(host.Players);
        long end = world.CurrentDate.TotalMilliseconds;

        var replayed = PlayerReplay.Run(snapshot, host.Players.Log.Records, new WorldClock(ScenarioRunner.DefaultSystems(), sink: new AllEvents()),
            new AllEvents(), end);

        var kinds = host.Players.Log.Records.Select(r => r.Kind).ToHashSet();
        Assert.Equal(
            (true, WorldSnapshot.CanonicalHash(world)),
            (kinds.IsSupersetOf([PlayerCommandKind.TickDelta, PlayerCommandKind.Advance, PlayerCommandKind.Sleep, PlayerCommandKind.Wake]),
                WorldSnapshot.CanonicalHash(replayed)));
    }

    public enum Cause { Golpe, Fogo, Fome, Sede, Chamado }

    [Theory]
    [InlineData(Cause.Golpe, "golpe")]
    [InlineData(Cause.Fogo, "fogo")]
    [InlineData(Cause.Fome, "fome")]
    [InlineData(Cause.Sede, "sede")]
    [InlineData(Cause.Chamado, "chamado")]
    public void An_accelerated_sleep_is_interrupted_by_what_happens_in_the_engine(Cause cause, string reason)
    {
        var host = HostOf(Village(96, initialPopulation: 40));
        var world = host.Arm.World;
        var sleeper = Sleeper(world, cause);
        Assert.True(host.Players.Incarnate(world, sleeper.Id, "ana").IsSuccess);
        long hour = world.CurrentDate.TotalHours;
        sleeper.SetHunger(100, hour);
        sleeper.SetThirst(100, hour);
        Assert.True(host.Players.Sleep(world, "ana", null).IsSuccess);
        host.Simulation.Cycle(host.Players);
        Assert.Equal(RhythmMode.Accelerated, host.Simulation.LastPlan.Mode);
        string? before = host.Players.SkipEnding(world);

        Cause_(host.Arm, sleeper, cause);
        host.Simulation.Cycle(host.Players);

        Assert.Equal((null, false, reason, ActionType.Idle), (before, host.Players.Skip is not null, host.Players.LastWakeReason, sleeper.ControlledAction));
    }

    [Fact]
    public void Nobody_skips_time_with_another_player_in_the_world_and_a_second_arrival_ends_the_sleep()
    {
        var host = HostOf(Village(97, initialPopulation: 40));
        var world = host.Arm.World;
        var ana = Pick(world);
        var bia = world.Npcs.First(n => n.IsAlive && n.Id != ana.Id && n.Employer is not null);
        Assert.True(host.Players.Incarnate(world, ana.Id, "ana").IsSuccess);
        Assert.True(host.Players.Sleep(world, "ana", null).IsSuccess);
        host.Simulation.Cycle(host.Players);
        var whileAlone = host.Simulation.LastPlan.Mode;

        Assert.True(host.Players.Incarnate(world, bia.Id, "bia").IsSuccess);
        host.Simulation.Cycle(host.Players);
        var refused = host.Players.Sleep(world, "bia", null);

        Assert.Equal((RhythmMode.Accelerated, "outro jogador", RhythmMode.RealTime, false),
            (whileAlone, host.Players.LastWakeReason, host.Simulation.LastPlan.Mode, refused.IsSuccess));
    }

    [Fact]
    public void Ablation_without_fine_decisions_a_fine_delta_reaches_the_hourly_world_and_with_them_it_does_not()
    {
        var off = PlayerRules.Default with { Time = TimeRules.Default with { FineDecisions = false } };
        var hourly = Village(98, rules: off, initialPopulation: 40);
        var fineOff = Village(98, rules: off, initialPopulation: 40);
        var fineOnHourly = Village(98, initialPopulation: 40);
        var fineOn = Village(98, initialPopulation: 40);

        hourly.Clock.Run(hourly.World, 12);
        fineOff.Clock.Run(fineOff.World, 12 * 60, Minute);
        fineOnHourly.Clock.Run(fineOnHourly.World, 12);
        fineOn.Clock.Run(fineOn.World, 12 * 60, Minute);

        Assert.Equal((true, false), (
            WorldSnapshot.CanonicalHash(hourly.World) == WorldSnapshot.CanonicalHash(fineOff.World),
            WorldSnapshot.CanonicalHash(fineOnHourly.World) == WorldSnapshot.CanonicalHash(fineOn.World)));
    }

    private static Npc Sleeper(WorldState world, Cause cause) => Pick(world); // Pick: mora com ao menos duas pessoas

    private static void Cause_(Arm arm, Npc sleeper, Cause cause)
    {
        var world = arm.World;
        long hour = world.CurrentDate.TotalHours;
        switch (cause)
        {
            case Cause.Golpe:
                var aggressor = world.Npcs.First(n => n.IsAlive && n.Id != sleeper.Id && n.Employer is not null);
                string key = Enumerable.Range(0, 100).Select(k => $"teste-golpe-{k}").First(k =>
                {
                    var probe = arm.Ctx.OneShot(k);
                    probe.NextDouble();
                    return probe.NextDouble() > 0.2; // o segundo sorteio decide o golpe mortal: este não mata
                });
                LivingWorld.Simulation.Crime.Violence.Fight(world, arm.Ctx, aggressor, sleeper, "teste", arm.Ctx.OneShot(key));
                break;
            case Cause.Fogo:
                var region = world.Map.Regions.First(r => r.Cells.Contains(sleeper.CurrentLocation));
                world.AddEnvironmentTemperatureAdjustment(new(region.Id, 500f, hour + 48));
                break;
            case Cause.Fome:
                sleeper.SetHunger(10, hour);
                break;
            case Cause.Sede:
                sleeper.SetThirst(10, hour);
                break;
            default: // alguém da casa, acordado, vem conversar ao lado
                var friend = world.FindHousehold(sleeper.Household!.Value)!.Members.Select(world.FindNpc)
                    .First(n => n is { IsAlive: true } && n.Id != sleeper.Id)!;
                friend.MoveTo(sleeper.CurrentLocation, hour);
                friend.SetCurrentAction(ActionType.Socialize, hour);
                break;
        }
    }
}
