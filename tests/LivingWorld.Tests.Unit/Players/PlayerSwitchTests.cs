using LivingWorld.Domain.Population;
using LivingWorld.Infrastructure.EventLog;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Players;
using LivingWorld.Simulation.Scenarios;
using static LivingWorld.Tests.Unit.Players.PlayerAbsenceTests;

namespace LivingWorld.Tests.Unit.Players;

/// <summary>#252 (sandbox do casal): trocar de corpo solta o NPC conduzido SEM fazê-lo sumir (ao contrário de sair do personagem) e conduz o
/// outro, no mesmo modo de posse; uma peça só, sem segundo jogador.</summary>
public class PlayerSwitchTests
{
    private static (Arm Arm, Npc A, Npc B) Pair(ulong seed)
    {
        var arm = Village(seed);
        var a = Pick(arm.World);
        var b = arm.World.Npcs.First(n => n.IsAlive && n.Id != a.Id);
        return (arm, a, b);
    }

    [Theory]
    [InlineData(PossessionMode.Common)]
    [InlineData(PossessionMode.Full)]
    public void Switching_releases_the_old_body_without_it_disappearing_and_takes_the_other_in_the_same_mode(PossessionMode mode)
    {
        var (arm, a, b) = Pair(11);
        var service = new PlayerService(arm.Sink);
        Assert.True(service.Incarnate(arm.World, a.Id, "alan", mode).IsSuccess);
        if (mode == PossessionMode.Common) b.ImprintPersonality(b.Personality with { Conscientiousness = 0, EmotionalStability = 0 }); // sem vontade que impeça

        var result = service.Switch(arm.World, b.Id, "alan");

        Assert.True(result.IsSuccess, result.Error);
        Assert.Null(a.ControllerId);
        Assert.Null(a.DisappearedAtTick); // não sumiu: é a pessoa solta, não o logout
        Assert.True(a.IsAlive);
        Assert.Equal("alan", b.ControllerId);
        Assert.Equal(mode == PossessionMode.Full ? PossessionMode.Full : null, b.ControlMode);
        Assert.Single(arm.World.Npcs, n => n.ControllerId == "alan"); // sem segundo corpo
        Assert.Equal(a.Household, arm.World.FindHousehold(a.Household!.Value)!.Id); // a casa do solto continua com ele
        Assert.Contains(a.Id, arm.World.FindHousehold(a.Household!.Value)!.Members);
    }

    [Fact]
    public void Logout_keeps_home_and_job_when_the_scenario_says_so_and_disappears_otherwise()
    {
        foreach (bool keeps in new[] { true, false })
        {
            var arm = Village(21, new PlayerRules { LogoutKeepsLife = keeps });
            var npc = Pick(arm.World);
            var household = npc.Household;
            var employer = npc.Employer;
            var service = new PlayerService(arm.Sink);
            Assert.True(service.Incarnate(arm.World, npc.Id, "alan", PossessionMode.Full).IsSuccess);

            Assert.True(service.Logout(arm.World, "alan").IsSuccess);

            Assert.Null(npc.ControllerId); // solto nos dois casos
            if (keeps) // #246 (AD-237): sair do jogo não desfaz a vida do personagem
            {
                Assert.Null(npc.DisappearedAtTick);
                Assert.Equal(household, npc.Household);
                Assert.Equal(employer, npc.Employer);
            }
            else
            {
                Assert.NotNull(npc.DisappearedAtTick); // o sumiço de sempre (AD-196)
                Assert.NotEqual(household, npc.Household);
            }
        }
    }

    [Fact]
    public void A_target_already_driven_or_the_same_body_is_refused_and_nothing_changes()
    {
        var (arm, a, b) = Pair(12);
        var service = new PlayerService(arm.Sink);
        Assert.True(service.Incarnate(arm.World, a.Id, "alan", PossessionMode.Full).IsSuccess);
        Assert.True(service.Incarnate(arm.World, b.Id, "bia", PossessionMode.Full).IsSuccess);
        string before = WorldSnapshot.CanonicalHash(arm.World);

        Assert.False(service.Switch(arm.World, b.Id, "alan").IsSuccess); // b é de outro jogador
        Assert.False(service.Switch(arm.World, a.Id, "alan").IsSuccess); // já conduz a
        Assert.False(service.Switch(arm.World, b.Id, "ninguem").IsSuccess); // quem não conduz ninguém não troca

        Assert.Equal(before, WorldSnapshot.CanonicalHash(arm.World));
        Assert.Equal("alan", a.ControllerId);
    }

    [Fact]
    public void The_session_log_replays_a_switch_to_the_same_world()
    {
        var (arm, a, b) = Pair(13);
        string start = WorldSnapshot.Serialize(arm.World);
        var service = new PlayerService(arm.Sink);
        Assert.True(service.Incarnate(arm.World, a.Id, "alan", PossessionMode.Full).IsSuccess);
        Assert.True(service.Switch(arm.World, b.Id, "alan").IsSuccess);
        Assert.Contains(service.Log.Records, r => r.Kind == PlayerCommandKind.Switch && r.NpcId == b.Id.Value);

        arm.Clock.Run(arm.World, 3);
        var replayed = PlayerReplay.Run(start, service.Log.Records,
            new WorldClock(ScenarioRunner.DefaultSystems(), sink: new BufferingWorldEventSink()), new BufferingWorldEventSink(), arm.World.CurrentDate.TotalMilliseconds);

        Assert.Equal(WorldSnapshot.CanonicalHash(arm.World), WorldSnapshot.CanonicalHash(replayed));
    }
}
