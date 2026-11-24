using LivingWorld.Domain.Cognition;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Infrastructure.EventLog;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Players;
using LivingWorld.Simulation.Scenarios;
using static LivingWorld.Tests.Unit.Players.PlayerAbsenceTests;

namespace LivingWorld.Tests.Unit.Players;

/// <summary>AD-197e: posse comum e plena, a recusa causal da pessoa possuída e o passo do jogador pela fila.</summary>
public class PlayerPossessionTests
{
    /// <summary>Um NPC, alguém da casa dele de quem ele gosta muito e um desconhecido, os três na mesma célula.</summary>
    private static (Arm Arm, Npc Npc, Npc Beloved, Npc Stranger) Scene(ulong seed)
    {
        var arm = Village(seed);
        var w = arm.World;
        var npc = Pick(w);
        var beloved = w.FindHousehold(npc.Household!.Value)!.Members.Where(m => m != npc.Id).Select(m => w.FindNpc(m)!).First(m => m.IsAlive);
        var stranger = w.Npcs.First(n => n.IsAlive && n.Household != npc.Household && n.Spouse != npc.Id && n.MotherId != npc.Id
            && n.FatherId != npc.Id && npc.MotherId != n.Id && npc.FatherId != n.Id
            && !w.Relationships.ContainsKey(new RelationshipKey(npc.Id, n.Id)));
        long now = w.CurrentDate.TotalHours;
        // AD-197f: temperamento fixo (vontade média, pouco altruísmo), para o teste medir só o vínculo
        npc.ImprintPersonality(npc.Personality with { EmotionalStability = 60, Conscientiousness = 60, Impulsivity = 40, Altruism = 20, Loyalty = 50 });
        npc.SetHealth(100);
        w.GetOrCreateRelationship(new RelationshipKey(npc.Id, beloved.Id), now).CopyAxesFrom(Relationship.FromAxes(95, 95, 50, 0, now));
        beloved.MoveTo(npc.CurrentLocation, now);
        stranger.MoveTo(npc.CurrentLocation, now);
        return (arm, npc, beloved, stranger);
    }

    private static (bool Ok, string? Error, string Hash, string HashBefore) Strike(ulong seed, PossessionMode mode, bool family)
    {
        var (arm, npc, beloved, stranger) = Scene(seed);
        var service = new PlayerService(arm.Sink);
        Assert.True(service.Incarnate(arm.World, npc.Id, "alan", mode).IsSuccess);
        string before = WorldSnapshot.CanonicalHash(arm.World);
        var result = service.Strike(arm.World, "alan", (family ? beloved : stranger).Id);
        return (result.IsSuccess, result.Error, WorldSnapshot.CanonicalHash(arm.World), before);
    }

    [Fact]
    public void Striking_a_beloved_relative_is_refused_in_common_possession_and_the_world_does_not_change()
    {
        var refused = Strike(3, PossessionMode.Common, family: true);
        Assert.False(refused.Ok);
        Assert.StartsWith(PlayerCommands.Refused, refused.Error);
        Assert.Equal(refused.HashBefore, refused.Hash);
    }

    [Fact]
    public void Full_possession_obeys_the_same_order_and_the_captive_suffers()
    {
        var (arm, npc, beloved, _) = Scene(3);
        var service = new PlayerService(arm.Sink);
        Assert.True(service.Incarnate(arm.World, npc.Id, "alan", PossessionMode.Full).IsSuccess);

        Assert.True(service.Strike(arm.World, "alan", beloved.Id).IsSuccess);

        Assert.Equal((1, beloved.Id.Value), (npc.Captive!.Objections, npc.Captive.LastObjectionTarget));
        Assert.True(npc.Captive.Distress > 0);
        var view = PossessionWill.View(arm.World, npc, arm.World.CurrentDate.TotalHours)!;
        Assert.Equal(beloved.Name, view.LastObjectionTargetName);
    }

    [Fact]
    public void Striking_a_stranger_is_obeyed_in_both_modes()
    {
        Assert.True(Strike(3, PossessionMode.Common, family: false).Ok);
        Assert.True(Strike(3, PossessionMode.Full, family: false).Ok);
    }

    [Fact]
    public void With_AD196_off_the_possessed_never_refuses()
    {
        var (arm, npc, beloved, _) = Scene(3);
        arm.World.UsePlayers(PlayerRules.Off);
        Assert.True(PlayerCommands.Incarnate(arm.World, arm.Ctx, npc.Id, "alan").IsSuccess);
        Assert.True(PlayerCommands.Strike(arm.World, arm.Ctx, npc.Id, "alan", beloved.Id).IsSuccess);
    }

    [Fact]
    public void Leaving_a_full_possession_frees_the_captive_who_remembers_it()
    {
        var (arm, npc, beloved, _) = Scene(4);
        var service = new PlayerService(arm.Sink);
        Assert.True(service.Incarnate(arm.World, npc.Id, "alan", PossessionMode.Full).IsSuccess);
        Assert.True(service.Strike(arm.World, "alan", beloved.Id).IsSuccess);

        Assert.True(service.Logout(arm.World, "alan").IsSuccess);

        Assert.Null(npc.Captive);
        Assert.Contains(npc.Psyche!.Memories, m => m.Kind == LifeEventKind.Possessed);
    }

    [Fact]
    public void A_move_needs_the_controller_and_one_step_and_the_log_with_moves_and_a_refusal_replays_to_the_same_hash()
    {
        var (arm, npc, beloved, stranger) = Scene(5);
        var w = arm.World;
        string snapshot = WorldSnapshot.Serialize(w);
        var service = new PlayerService(arm.Sink);
        string hash = WorldSnapshot.CanonicalHash(w);
        Assert.False(service.Move(w, "alan", 1, 0).IsSuccess); // sem controlador
        Assert.Equal(hash, WorldSnapshot.CanonicalHash(w));

        Assert.True(service.Incarnate(w, npc.Id, "alan").IsSuccess);
        Assert.False(service.Move(w, "alan", 2, 0).IsSuccess); // só um passo
        Assert.False(service.Strike(w, "alan", beloved.Id).IsSuccess); // recusa: vai ao registro
        Assert.True(service.Strike(w, "alan", stranger.Id).IsSuccess);
        arm.Clock.Run(w, 2);
        var start = npc.CurrentLocation;
        bool moved = service.Move(w, "alan", 1, 0).IsSuccess || service.Move(w, "alan", -1, 0).IsSuccess;
        Assert.True(moved);
        Assert.NotEqual(start, npc.CurrentLocation);
        arm.Clock.Run(w, 5);
        long end = w.CurrentDate.TotalHours;

        Assert.Contains(service.Log.Records, r => r.Refused && r.Kind == PlayerCommandKind.Strike);
        var replayed = PlayerReplay.Run(snapshot, service.Log.Records,
            new WorldClock(ScenarioRunner.DefaultSystems(), sink: new BufferingWorldEventSink()), new BufferingWorldEventSink(), end);
        Assert.Equal(WorldSnapshot.CanonicalHash(w), WorldSnapshot.CanonicalHash(replayed));
    }
}
