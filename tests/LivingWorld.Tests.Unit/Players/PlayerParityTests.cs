using System.Reflection;
using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Extraordinary;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Llm;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Decision;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Players;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;
using LivingWorld.Tests.Shared;
using static LivingWorld.Tests.Unit.Players.PlayerAbsenceTests;

namespace LivingWorld.Tests.Unit.Players;

/// <summary>AD-196 (Fase 25, #63): poder pela mesma cadeia, espectador sem escrita, registro de vários jogadores reproduzível e
/// o subsistema de jogador na conta do hash.</summary>
public class PlayerParityTests
{
    private static readonly WorldCalendar Calendar = new(24, 30, 12);

    private static (WorldState World, TickContext Ctx, Npc Npc, AllEvents Sink) PowerWorld()
    {
        var rules = NeedsRules.Create(0, 0, 0, 0, 70, maxActionSelectionSteps: 10, hysteresisEnabled: false, continuityBonus: 0,
            homelessSleepEfficiency: 0.5).Value!;
        var catalog = ActionCatalog.Create(
            new Dictionary<ActionType, int>
            {
                [ActionType.Eat] = 2,
                [ActionType.Sleep] = 8,
                [ActionType.Work] = 8,
                [ActionType.Socialize] = 3,
                [ActionType.Travel] = 4,
                [ActionType.Idle] = 2,
                [ActionType.Buy] = 2,
                [ActionType.UsePower] = 1,
            },
            [new RoutineSlot(ProfessionId: null, Stage: LifeStage.Adult, HourStart: 0, HourEnd: 23, Action: ActionType.Work)],
            ActionType.Idle).Value!;
        var descriptor = new PowerDescriptor("teleport-power", "test", ["npc.teleport:elsewhere"], "Active", [], "Guaranteed", [], [], [], []);
        var carrier = new ExtraordinaryCarrierState(new NpcId(1), [descriptor.Id], true, "active", new ExtraordinaryAppearanceState(1, "", ""), null, 1);
        var world = new WorldState(
            Calendar, 11, ScenarioRunner.DefaultMap(11), ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            rules, catalog, LifeStageRules.Create(childMaxAge: 14, adultMaxAge: 64).Value!,
            extraordinary: new ExtraordinaryScenarioData(true, [descriptor]), extraordinaryCarriers: [carrier]);
        var location = new CellCoord(1, 1);
        var npc = new Npc(new NpcId(1), "npc", Sex.Male, WorldDate.Epoch(Calendar).AddYears(-30), new CultureId(1), location,
            motherId: null, fatherId: null, household: null, health: 100, personality: Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!,
            profession: ProfessionType.None, currentLocation: location, hunger: 20, thirst: 100, sleep: 100, social: 100);
        world.AddNpc(npc);
        world.AdvanceNpcIdTo(2);
        world.AddNpcMemory(npc.Id, MemoryCategory.Social, "foi traído por X na colheita", importance: 90, originTick: 1,
            participants: [npc.Id], location: npc.CurrentLocation, canonicalImportanceThreshold: LlmRules.Default.CanonicalMemoryImportanceThreshold);
        var sink = new AllEvents();
        return (world, new TickContext(world, world.Rng, world.Scheduler, sink), npc, sink);
    }

    /// <summary>Hash do mundo sem o controlador (o único campo que difere por construção entre os braços).</summary>
    private static string HashWithoutController(WorldState world)
    {
        var copy = WorldSnapshot.Deserialize(WorldSnapshot.Serialize(world));
        var npc = copy.FindNpc(new NpcId(1))!;
        npc.Disappear(0);
        npc.Reappear();
        return WorldSnapshot.CanonicalHash(copy);
    }

    private static (string Hash, List<string> Resolution, PendingPowerInvocation? Pending) RunPower(bool player)
    {
        var (world, ctx, npc, sink) = PowerWorld();
        if (player)
        {
            Assert.True(PlayerCommands.Incarnate(world, ctx, npc.Id, "alan").IsSuccess);
            Assert.True(PlayerCommands.Act(world, npc.Id, "alan", ActionType.UsePower, "teleport-power").IsSuccess);
        }
        SimulationWakeTestHelper.Wake(world, npc);
        new BehaviorDecisionSystem().Tick(world, ctx);
        Assert.Equal(ActionType.UsePower, npc.CurrentAction);
        var pending = npc.PendingPowerInvocation;
        var clock = new WorldClock([new BehaviorDecisionSystem()], sink: sink);
        clock.Run(world, 4); // a ação conclui e a invocação resolve
        var resolution = sink.Events.Where(e => e.Kind.ToString().StartsWith("Extraordinary", StringComparison.Ordinal) || e.Kind == WorldEventKind.PowerInvoked)
            .Select(e => $"{e.Tick}|{e.Kind}|{e.Payload}").ToList();
        return (HashWithoutController(world), resolution, pending);
    }

    [Fact]
    public void Engine_and_player_choosing_the_same_invocation_resolve_identically()
    {
        var engine = RunPower(player: false);
        var player = RunPower(player: true);
        Assert.NotEmpty(engine.Resolution);
        Assert.Equal(engine.Pending, player.Pending);
        Assert.Equal(engine.Resolution, player.Resolution);
        Assert.Equal(engine.Hash, player.Hash);
    }

    [Fact]
    public void A_player_cannot_choose_a_power_the_npc_does_not_carry_or_under_AD196_off()
    {
        var (world, ctx, npc, _) = PowerWorld();
        Assert.True(PlayerCommands.Incarnate(world, ctx, npc.Id, "alan").IsSuccess);
        Assert.False(PlayerCommands.Act(world, npc.Id, "alan", ActionType.UsePower, "fly").IsSuccess);
        Assert.False(PlayerCommands.Act(world, npc.Id, "alan", ActionType.UsePower).IsSuccess);
        world.UsePlayers(PlayerRules.Off);
        Assert.False(PlayerCommands.Act(world, npc.Id, "alan", ActionType.UsePower, "teleport-power").IsSuccess);
    }

    [Fact]
    public void A_spectator_browsing_for_a_thousand_ticks_leaves_the_hash_untouched()
    {
        var watched = Village(21, initialPopulation: 40);
        var alone = Village(21, initialPopulation: 40);
        var spectator = new SpectatorSession(watched.World);
        var browse = new Random(5);
        for (int tick = 0; tick < 1000; tick++)
        {
            var ids = watched.World.Npcs.Select(n => n.Id).ToList();
            var who = ids[browse.Next(ids.Count)];
            var seen = spectator.Follow(who)!;
            spectator.Around(new CellCoord(seen.X, seen.Y), 3);
            spectator.LookCity(watched.World.FindNpc(who)!.City);
            watched.Clock.Run(watched.World, 1);
            alone.Clock.Run(alone.World, 1);
        }

        Assert.Equal(WorldSnapshot.CanonicalHash(alone.World), WorldSnapshot.CanonicalHash(watched.World));
    }

    [Fact]
    public void The_spectator_session_has_no_write_surface()
    {
        var leaks = typeof(SpectatorSession).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName)
            .Where(m => !IsCopy(m.ReturnType) || m.GetParameters().Any(p => !IsCopy(p.ParameterType)))
            .Select(m => m.Name).ToList();
        Assert.Empty(leaks);

        static bool IsCopy(Type t) =>
            t.IsValueType || t == typeof(string) || t == typeof(SpectatorNpcView) || t == typeof(SpectatorCityView)
            || (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IReadOnlyList<>) && IsCopy(t.GetGenericArguments()[0]))
            || (Nullable.GetUnderlyingType(t) is { } inner && IsCopy(inner));
    }

    [Fact]
    public void Interleaved_actions_of_two_players_replayed_from_the_snapshot_reproduce_the_hash()
    {
        var live = Village(13, initialPopulation: 40);
        string snapshot = WorldSnapshot.Serialize(live.World);
        long start = live.World.CurrentDate.TotalSeconds;
        var players = new PlayerService(live.Sink);
        var adults = live.World.Npcs.Where(n => n.IsAlive && n.Employer is not null).Take(2).ToList();

        Assert.True(players.Incarnate(live.World, adults[0].Id, "ana").IsSuccess);
        live.Clock.Run(live.World, 3);
        Assert.True(players.Incarnate(live.World, adults[1].Id, "bia").IsSuccess);
        Assert.True(players.Act(live.World, "ana", ActionType.Idle).IsSuccess);
        live.Clock.Run(live.World, 5);
        Assert.True(players.Act(live.World, "bia", ActionType.Socialize).IsSuccess);
        Assert.False(players.Act(live.World, "ana", ActionType.Travel).IsSuccess); // recusado não entra no registro
        live.Clock.Run(live.World, 7);
        Assert.True(players.Act(live.World, "ana", null).IsSuccess);
        Assert.True(players.Logout(live.World, "bia").IsSuccess);
        live.Clock.Run(live.World, 48);
        Assert.True(players.Logout(live.World, "ana").IsSuccess);
        live.Clock.Run(live.World, 24);
        long end = live.World.CurrentDate.TotalSeconds;

        var replayed = PlayerReplay.Run(snapshot, players.Log.Records, new WorldClock(ScenarioRunner.DefaultSystems(), sink: new AllEvents()),
            new AllEvents(), end);

        Assert.Equal(7, players.Log.Records.Count);
        Assert.Equal(start, players.Log.Records[0].Second);
        Assert.Equal(WorldSnapshot.CanonicalHash(live.World), WorldSnapshot.CanonicalHash(replayed));
    }

    [Fact]
    [Trait("Category", "Scenario")]
    public void Switching_the_player_subsystem_off_changes_the_hash_in_ten_years()
    {
        string Run(bool withSubsystem)
        {
            var systems = ScenarioRunner.DefaultSystems().Where(s => withSubsystem || s is not PlayerSearchSystem).ToList();
            var arm = Village(17, systems: systems);
            var npc = Pick(arm.World);
            Assert.True(PlayerCommands.Incarnate(arm.World, arm.Ctx, npc.Id, "alan").IsSuccess);
            Assert.True(PlayerCommands.Act(arm.World, npc.Id, "alan", ActionType.Idle).IsSuccess);
            arm.Clock.Run(arm.World, 48);
            Assert.True(PlayerCommands.Logout(arm.World, arm.Ctx, "alan").IsSuccess);
            arm.Clock.Run(arm.World, 10L * arm.World.Calendar.HoursPerYear);
            return WorldSnapshot.CanonicalHash(arm.World);
        }

        Assert.NotEqual(Run(withSubsystem: false), Run(withSubsystem: true));
    }
}
