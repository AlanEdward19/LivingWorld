using LivingWorld.Domain.Extraordinary;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Extraordinary.Engine;
using LivingWorld.Simulation.Extraordinary.Mechanics;
using LivingWorld.Simulation.Players;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;
using static LivingWorld.Tests.Unit.Players.PlayerAbsenceTests;

namespace LivingWorld.Tests.Unit.Players;

/// <summary>AD-197g: a posse por poder entra na mesma disputa de vontade da posse do jogador.</summary>
public class PowerPossessionContestTests
{
    private const string PowerId = "possess-power";

    private sealed record Scene(WorldState World, WorldClock Clock, AllEvents Sink, Npc Possessor, Npc Target);

    private static Scene Build(ulong seed, int stability, int conscientiousness, int impulsivity, PossessionRules? rules = null,
        IReadOnlyList<string>? costs = null, string reliability = "Guaranteed")
    {
        var descriptor = new PowerDescriptor(PowerId, "test", ["control.possess"], "Active", costs ?? [], reliability, [], [], [], []);
        var (world, _) = ScenarioRunner.Create(seed, extraordinary: new ExtraordinaryScenarioData(true, [descriptor]));
        if (rules is not null) world.UsePlayers(PlayerRules.Default with { Possession = rules });
        var sink = new AllEvents();
        var clock = new WorldClock(ScenarioRunner.DefaultSystems(extraordinary: world.Extraordinary), sink: sink);
        clock.Run(world, 24);
        var target = Pick(world);
        var possessor = world.Npcs.First(n => n.IsAlive && n.Id != target.Id && n.Household != target.Household
            && world.LifeStageRules.LifeStageOf(n.AgeYears(world.CurrentDate)) == LifeStage.Adult);
        world.UpsertExtraordinaryCarrier(new ExtraordinaryCarrierState(possessor.Id, [PowerId], true, "active",
            new ExtraordinaryAppearanceState(1, "", ""), null, world.CurrentDate.TotalHours));
        target.ImprintPersonality(target.Personality with
        {
            EmotionalStability = stability, Conscientiousness = conscientiousness, Impulsivity = impulsivity,
        });
        target.SetHealth(100);
        sink.Events.Clear();
        return new Scene(world, clock, sink, possessor, target);
    }

    private static void Possess(Scene s)
    {
        var result = ExtraordinaryInvocationEngine.Invoke(s.World, new TickContext(s.World, s.World.Rng, s.World.Scheduler, s.Sink),
            new ExtraordinaryInvocation(s.World.NextEventId, s.Possessor.Id, PowerId, s.Target.Id));
        Assert.True(result.IsSuccess, result.Error);
    }

    private static bool Freed(Scene s) =>
        s.Sink.Events.Any(e => e.Kind == WorldEventKind.PossessionResisted && e.Payload.StartsWith($"{s.Target.Id.Value}|", StringComparison.Ordinal));

    [Fact]
    public void A_high_will_resists_or_expels_a_power_possession_and_a_low_one_does_not_in_at_least_18_of_20_seeds()
    {
        int high = 0, low = 0;
        for (ulong seed = 1; seed <= 20; seed++)
        {
            var strong = Build(seed, 95, 95, 5);
            Possess(strong);
            strong.Clock.Run(strong.World, 48);
            if (Freed(strong) && !ControlMechanic.IsPossessed(strong.World, strong.Target)) high++;

            var weak = Build(seed, 35, 35, 65);
            Possess(weak);
            weak.Clock.Run(weak.World, 48);
            if (!Freed(weak) && ControlMechanic.IsPossessed(weak.World, weak.Target)) low++;
        }
        Assert.True(high >= 18 && low >= 18, $"vontade alta livre {high}, baixa possuída {low}");
    }

    [Fact]
    public void The_control_comes_from_the_power_reliability_and_cost()
    {
        double Control(IReadOnlyList<string> costs, string reliability)
        {
            var s = Build(3, 35, 35, 65, costs: costs, reliability: reliability);
            Possess(s);
            return PossessionContest.PowerControl(s.World, s.Target, 0)!.Value;
        }

        double guaranteed = Control([], "Guaranteed"), costly = Control(["carrier.hunger:2"], "Guaranteed"), unreliable = Control([], "ResolutionCheck");
        Assert.True(costly > guaranteed && guaranteed > unreliable, $"{costly} > {guaranteed} > {unreliable}");
    }

    /// <summary>O mesmo NPC, sendo personagem de jogador ou não, resiste ao mesmo poder no mesmo tick.</summary>
    private static (long? FreedAt, string? Refusal) Resist(ulong seed, bool player)
    {
        var s = Build(seed, 76, 76, 24); // vontade que expulsa sem impedir a entrada
        var ctx = new TickContext(s.World, s.World.Rng, s.World.Scheduler, s.Sink);
        if (player) Assert.True(PlayerCommands.Incarnate(s.World, ctx, s.Target.Id, "alan", PossessionMode.Full).IsSuccess);
        Possess(s);
        string? refusal = player ? PlayerCommands.Move(s.World, ctx, s.Target.Id, "alan", 1, 0).Error : null;
        s.Clock.Run(s.World, 48);
        return (s.Sink.Events.FirstOrDefault(e => e.Kind == WorldEventKind.PossessionResisted)?.Tick, refusal);
    }

    [Fact]
    public void A_player_character_is_possessed_and_resists_by_the_same_rule_as_any_npc()
    {
        for (ulong seed = 1; seed <= 5; seed++)
        {
            var npc = Resist(seed, player: false);
            var player = Resist(seed, player: true);
            Assert.NotNull(npc.FreedAt);
            Assert.Equal(npc.FreedAt, player.FreedAt);
            Assert.StartsWith(PlayerCommands.Refused, player.Refusal); // possuído, o corpo não obedece ao jogador
        }
    }

    [Fact]
    public void With_the_will_contest_off_the_old_vigor_roll_decides_and_no_contest_state_is_kept()
    {
        var s = Build(4, 95, 95, 5, PossessionRules.Default with { WillContest = false });
        Possess(s);
        s.Clock.Run(s.World, 12);
        Assert.Null(s.Target.Will);
    }

    [Fact]
    public void Power_possession_with_the_contest_is_deterministic()
    {
        string Run()
        {
            var s = Build(6, 80, 80, 20);
            Possess(s);
            s.Clock.Run(s.World, 72);
            return WorldSnapshot.CanonicalHash(s.World);
        }

        Assert.Equal(Run(), Run());
    }
}
