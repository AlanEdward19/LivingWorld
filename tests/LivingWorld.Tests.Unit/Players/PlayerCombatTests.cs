using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Infrastructure.EventLog;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Players;
using LivingWorld.Simulation.Scenarios;
using static LivingWorld.Tests.Unit.Players.PlayerAbsenceTests;
using static LivingWorld.Tests.Unit.Players.PlayerControlTests;
using static LivingWorld.Tests.Unit.Players.PlayerWalkTests;

namespace LivingWorld.Tests.Unit.Players;

/// <summary>#234: combate com tempo: alcance em metros, recarga do golpe, esquiva e bloqueio, e a reação de quem apanha decidida
/// pelo motor (recuar, fugir, revidar, pedir ajuda), com o revide pelo mesmo caminho de dano.</summary>
public class PlayerCombatTests
{
    private const int Fine = 50;

    private static readonly PlayerRules Rules = NoPace;

    /// <summary>O corpo do jogador (em Full, para a recusa dos valores não atrapalhar) e um desconhecido ao lado, em chão aberto.</summary>
    private static (Arm Arm, Npc Body, Npc Target, PlayerService Service) Scene(ulong seed, PlayerRules? rules = null, int dx = 1, bool housemate = false)
    {
        var arm = Village(seed, rules ?? Rules);
        var w = arm.World;
        var body = Pick(w);
        var start = OpenRun(w, body, 4);
        Place(w, body, start);
        var target = w.AliveNpcIndex.Alive.First(n => n.Id != body.Id && n.City == body.City && n.ControllerId is null
            && n.AgeYears(w.CurrentDate) >= 18 && n.Household != body.Household && n.Spouse != body.Id
            && !w.Relationships.ContainsKey(new RelationshipKey(body.Id, n.Id))
            && (!housemate || w.Npcs.Any(m => m.IsAlive && m.Id != n.Id && m.Household == n.Household && m.AgeYears(w.CurrentDate) >= 18)));
        Place(w, target, new CellCoord(start.X + dx, start.Y));
        body.SetHealth(100);
        target.SetHealth(100);
        var service = new PlayerService(arm.Sink);
        Assert.True(service.Incarnate(w, body.Id, "alan", PossessionMode.Full).IsSuccess);
        return (arm, body, target, service);
    }

    private static void Temper(Npc npc, int risk, int stability, int impulsivity, int extroversion = 50) =>
        npc.ImprintPersonality(npc.Personality with
        {
            RiskAversion = risk,
            EmotionalStability = stability,
            Impulsivity = impulsivity,
            Extroversion = extroversion,
            Loyalty = 50,
        });

    private static void Fast(Arm arm, PlayerService service, int ticks)
    {
        if (arm.World.TickDeltaMs != Fine) Assert.True(service.SetTickDelta(arm.World, "admin", Fine).IsSuccess);
        for (int i = 0; i < ticks; i++) arm.Clock.Tick(arm.World, Fine);
    }

    [Fact]
    public void The_strike_reaches_in_meters_and_a_far_target_is_refused_without_changing_the_world()
    {
        var (arm, _, target, service) = Scene(3, dx: 1);
        var w = arm.World;
        Assert.True(service.Strike(w, "alan", target.Id).IsSuccess); // vizinho: 1,5 m

        var (arm2, body2, far, service2) = Scene(3, dx: 3); // 4,5 m
        string hash = WorldSnapshot.CanonicalHash(arm2.World);
        var refused = service2.Strike(arm2.World, "alan", far.Id);
        Assert.False(refused.IsSuccess);
        Assert.Contains("fora do alcance", refused.Error);
        Assert.Contains("m", refused.Error);
        Assert.Equal(hash, WorldSnapshot.CanonicalHash(arm2.World));
        Assert.Null(body2.Combat);
    }

    [Fact]
    public void A_reach_rule_in_meters_tighter_than_the_neighbour_cell_refuses_the_strike()
    {
        var short_ = Scene(3, Rules with { StrikeReachMeters = 1.0 }, dx: 1); // 1,5 m já não alcança
        var refused = short_.Service.Strike(short_.Arm.World, "alan", short_.Target.Id);
        Assert.False(refused.IsSuccess);
        Assert.Contains("fora do alcance", refused.Error);
    }

    [Fact]
    public void The_strike_has_a_cooldown_that_ends_with_world_time_and_a_refusal_is_not_logged()
    {
        var (arm, body, target, service) = Scene(5);
        var w = arm.World;
        var first = service.Strike(w, "alan", target.Id);
        Assert.True(first.IsSuccess);
        Assert.True(first.Value!.ReadyInMs > 0);
        Place(w, target, new CellCoord(body.CurrentLocation.X + 1, body.CurrentLocation.Y)); // quem apanhou pode ter recuado
        int logged = service.Log.Records.Count;
        string hash = WorldSnapshot.CanonicalHash(w);

        var again = service.Strike(w, "alan", target.Id);
        Assert.False(again.IsSuccess);
        Assert.StartsWith(PlayerCommands.Conflict, again.Error);
        Assert.Contains("ainda se recupera", again.Error);
        Assert.Equal(logged, service.Log.Records.Count);
        Assert.Equal(hash, WorldSnapshot.CanonicalHash(w));

        Fast(arm, service, 30); // 1,5 s de mundo > 900 ms (quem apanhou se afastou: volta para perto)
        Place(w, target, new CellCoord(body.CurrentLocation.X + 1, body.CurrentLocation.Y));
        Assert.True(service.Strike(w, "alan", target.Id).IsSuccess);
    }

    [Fact]
    public void Combat_timing_off_keeps_the_instant_strike_and_no_reaction()
    {
        var (arm, body, target, service) = Scene(5, Rules with { CombatTiming = false });
        Assert.True(service.Strike(arm.World, "alan", target.Id).IsSuccess);
        Assert.True(service.Strike(arm.World, "alan", target.Id).IsSuccess);
        Assert.Null(body.Combat);
        Assert.Null(target.Combat);
        Assert.False(service.Guard(arm.World, "alan", CombatGuard.Dodge).IsSuccess);
    }

    [Fact]
    public void A_guard_has_a_short_window_then_a_cooldown_and_an_exhausted_body_cannot_dodge()
    {
        var (arm, body, _, service) = Scene(7);
        var w = arm.World;
        Assert.True(service.Guard(w, "alan", CombatGuard.Block).IsSuccess);
        var view = PlayerCombat.View(w, body)!;
        Assert.Equal("Block", view.Guard);
        Assert.InRange(view.GuardMs, 1, w.Players.BlockWindowMs);
        var again = service.Guard(w, "alan", CombatGuard.Dodge);
        Assert.False(again.IsSuccess);
        Assert.Contains("a guarda ainda se recupera", again.Error);

        Fast(arm, service, 30);
        Assert.Null(PlayerCombat.View(w, body)!.Guard); // a janela passou
        body.SetSleep(0, w.CurrentDate.TotalHours);
        var tired = service.Guard(w, "alan", CombatGuard.Dodge);
        Assert.False(tired.IsSuccess);
        Assert.Contains("exausto", tired.Error);
        Assert.True(service.Guard(w, "alan", CombatGuard.Block).IsSuccess); // bloquear não exige fôlego de esquiva
    }

    [Fact]
    public void A_block_lets_only_a_fraction_of_the_blow_through_and_a_blow_outside_the_window_lands_whole()
    {
        var blocked = Scene(9);
        var open = Scene(9);
        Assert.True(blocked.Service.Guard(blocked.Arm.World, "alan", CombatGuard.Block).IsSuccess);
        long at = blocked.Arm.World.CurrentDate.TotalMilliseconds + 100;
        var guardedHit = PlayerCombat.Incoming(blocked.Arm.World, blocked.Arm.Ctx, blocked.Target, blocked.Body, at, "revide");
        var plainHit = PlayerCombat.Incoming(open.Arm.World, open.Arm.Ctx, open.Target, open.Body, at, "revide");
        Assert.Equal(CombatOutcome.Blocked, guardedHit.Outcome);
        Assert.Equal(CombatOutcome.Hit, plainHit.Outcome);
        Assert.InRange(guardedHit.Damage, 1, plainHit.Damage - 1);
        Assert.Equal(CombatOutcome.Blocked.ToString(), PlayerCombat.View(blocked.Arm.World, blocked.Body)!.LastIncoming!.Outcome);

        long late = blocked.Arm.World.CurrentDate.TotalMilliseconds + blocked.Arm.World.Players.BlockWindowMs + 50;
        blocked.Body.SetHealth(100);
        var lateHit = PlayerCombat.Incoming(blocked.Arm.World, blocked.Arm.Ctx, blocked.Target, blocked.Body, late, "revide");
        Assert.Equal(CombatOutcome.Hit, lateHit.Outcome);
    }

    [Fact]
    public void A_dodge_avoids_most_blows_inside_the_window_and_never_outside_it()
    {
        var (arm, body, target, service) = Scene(11, Rules with { DodgeChanceValue = 2.0 }); // limitado a 0,95
        var w = arm.World;
        Assert.True(service.Guard(w, "alan", CombatGuard.Dodge).IsSuccess);
        long from = w.CurrentDate.TotalMilliseconds;
        int dodged = 0, outside = 0;
        for (int i = 1; i <= 20; i++)
        {
            body.SetHealth(100);
            if (PlayerCombat.Incoming(w, arm.Ctx, target, body, from + i, "revide").Outcome == CombatOutcome.Dodged) dodged++;
            body.SetHealth(100);
            if (PlayerCombat.Incoming(w, arm.Ctx, target, body, from + w.Players.DodgeWindowMs + 10 + i, "revide").Outcome == CombatOutcome.Dodged) outside++;
        }
        Assert.InRange(dodged, 15, 20);
        Assert.Equal(0, outside);
    }

    [Fact]
    public void The_reaction_comes_from_who_the_person_is_the_brave_strong_fights_back_the_weak_frightened_flees_the_fond_hesitates()
    {
        var (arm, body, target, _) = Scene(13);
        var w = arm.World;
        long now = w.CurrentDate.TotalMilliseconds;
        body.SetBody(1.70, 68, 10); // agressor franzino
        Temper(target, risk: 0, stability: 100, impulsivity: 100);
        target.SetBody(1.85, 90, 45);
        target.SetHealth(100);
        Assert.Equal(CombatReaction.Retaliate, PlayerCombat.Decide(w, arm.Ctx, target, body, now));

        Temper(target, risk: 100, stability: 10, impulsivity: 10);
        target.SetBody(1.55, 50, 12);
        body.SetBody(1.85, 95, 45);
        target.SetHealth(30);
        Assert.Equal(CombatReaction.Flee, PlayerCombat.Decide(w, arm.Ctx, target, body, now));

        var kin = w.Npcs.First(n => n.IsAlive && n.Household == target.Household && n.Id != target.Id); // parente: o cuidado vem da casa
        Temper(target, risk: 60, stability: 50, impulsivity: 20);
        target.SetBody(1.75, 70, 28);
        kin.SetBody(1.75, 70, 28);
        target.SetHealth(100);
        Assert.Equal(CombatReaction.Recoil, PlayerCombat.Decide(w, arm.Ctx, target, kin, now));
    }

    [Fact]
    public void A_struck_person_who_flees_leaves_the_aggressor_and_the_world_remembers_the_grudge()
    {
        var (arm, body, target, service) = Scene(15, dx: 1);
        var w = arm.World;
        Temper(target, risk: 100, stability: 10, impulsivity: 10);
        target.SetBody(1.55, 50, 12);
        body.SetBody(1.85, 95, 45);
        double before = MetersApart(w, body, target);
        var hit = service.Strike(w, "alan", target.Id);
        Assert.True(hit.IsSuccess, hit.Error);
        Assert.Equal(CombatOutcome.Hit, hit.Value!.Outcome);
        Assert.True(hit.Value.Damage > 0);
        Assert.Equal(CombatReaction.Flee, hit.Value.Reaction);
        Assert.True(MetersApart(w, body, target) > before, "quem foge se afasta do agressor");
        Assert.Equal(CombatReaction.Flee, target.Combat!.Reaction);
        Assert.NotNull(w.Crime!.GrudgeOf(target.Id.Value, body.Id.Value)); // a mágoa vem do mesmo caminho da briga
        Assert.Equal(CombatOutcome.Hit, target.Combat.LastIncoming);
    }

    [Fact]
    public void A_struck_person_who_retaliates_hits_back_after_a_windup_by_the_same_damage_path_unless_the_body_dodges()
    {
        var (arm, body, target, service) = Scene(17, Rules with { DodgeChanceValue = 2.0 });
        var w = arm.World;
        Temper(target, risk: 0, stability: 100, impulsivity: 100);
        target.SetBody(1.85, 90, 45);
        body.SetBody(1.70, 68, 10);
        Fast(arm, service, 1);
        var hit = service.Strike(w, "alan", target.Id);
        Assert.True(hit.IsSuccess, hit.Error);
        Assert.Equal(CombatReaction.Retaliate, hit.Value!.Reaction);
        long due = target.Combat!.RetaliateAtMs;
        Assert.InRange(due - w.CurrentDate.TotalMilliseconds, 1, 1000);
        int healthBefore = body.Health;

        Fast(arm, service, 40); // 2 s de mundo: a hora marcada passa
        Assert.Equal(0, target.Combat!.RetaliateAtMs); // resolvido
        Assert.True(body.Combat!.LastIncoming is CombatOutcome.Hit or CombatOutcome.Missed or CombatOutcome.Dodged, body.Combat.LastIncoming.ToString());
        if (body.Combat.LastIncoming == CombatOutcome.Hit) Assert.True(body.Health < healthBefore);
    }

    [Fact]
    public void A_retaliation_that_the_player_dodges_in_time_does_no_damage()
    {
        int saved = 0;
        for (ulong seed = 20; seed < 26; seed++)
        {
            var (arm, body, target, service) = Scene(seed, Rules with { DodgeChanceValue = 2.0 });
            var w = arm.World;
            Temper(target, risk: 0, stability: 100, impulsivity: 100);
            target.SetBody(1.85, 90, 45);
            body.SetBody(1.70, 68, 10);
            Fast(arm, service, 1);
            var hit = service.Strike(w, "alan", target.Id);
            if (hit.Value?.Reaction != CombatReaction.Retaliate) continue;
            long due = target.Combat!.RetaliateAtMs;
            // levanta a guarda de forma que o golpe marcado caia dentro da janela
            while (w.CurrentDate.TotalMilliseconds + w.Players.DodgeWindowMs / 2 < due) arm.Clock.Tick(w, Fine);
            if (!service.Guard(w, "alan", CombatGuard.Dodge).IsSuccess) continue;
            int health = body.Health;
            Fast(arm, service, 40);
            if (body.Combat!.LastIncoming == CombatOutcome.Dodged && body.Health == health) saved++;
        }
        Assert.True(saved >= 3, $"{saved}/6 revides esquivados a tempo");
    }

    [Fact]
    public void Someone_who_cares_answers_the_call_for_help_and_goes_against_the_aggressor()
    {
        var (arm, body, target, service) = Scene(19, housemate: true);
        var w = arm.World;
        Temper(target, risk: 100, stability: 10, impulsivity: 10, extroversion: 90);
        target.SetBody(1.55, 50, 12);
        body.SetBody(1.85, 95, 45);
        var friend = w.AliveNpcIndex.Alive.First(n => n.Id != body.Id && n.Id != target.Id && n.Household == target.Household
            && n.AgeYears(w.CurrentDate) >= 18);
        Place(w, friend, new CellCoord(target.CurrentLocation.X, target.CurrentLocation.Y + 1));
        Temper(friend, risk: 0, stability: 0, impulsivity: 100);
        friend.ImprintPersonality(friend.Personality with { Loyalty = 100 });
        friend.SetBody(1.80, 85, 40);
        Fast(arm, service, 1);

        var hit = service.Strike(w, "alan", target.Id);
        Assert.True(hit.IsSuccess, hit.Error);
        Assert.Equal(CombatReaction.CallHelp, hit.Value!.Reaction);
        Assert.Equal(body.Id.Value, friend.Combat!.RetaliateTarget);
        Assert.True(friend.Combat.RetaliateAtMs > 0);
    }

    [Fact]
    public void A_log_with_strikes_guards_and_retaliations_replays_to_the_same_hash_and_snapshots_keep_the_combat_state()
    {
        var arm = Village(3, Rules);
        var w = arm.World;
        var body = Pick(w);
        var start = OpenRun(w, body, 4);
        Place(w, body, start);
        var target = w.AliveNpcIndex.Alive.First(n => n.Id != body.Id && n.City == body.City && n.ControllerId is null
            && n.AgeYears(w.CurrentDate) >= 18 && n.Household != body.Household && n.Spouse != body.Id
            && !w.Relationships.ContainsKey(new RelationshipKey(body.Id, n.Id)));
        Place(w, target, new CellCoord(start.X + 1, start.Y));
        Temper(target, risk: 0, stability: 100, impulsivity: 100);
        target.SetBody(1.85, 90, 45);
        body.SetBody(1.70, 68, 10);
        string snapshot = WorldSnapshot.Serialize(w);
        var service = new PlayerService(arm.Sink);
        Assert.True(service.Incarnate(w, body.Id, "alan", PossessionMode.Full).IsSuccess);
        Assert.True(service.SetTickDelta(w, "admin", Fine).IsSuccess);
        arm.Clock.Tick(w, Fine);
        Assert.True(service.Strike(w, "alan", target.Id).IsSuccess);
        for (int i = 0; i < 6; i++) arm.Clock.Tick(w, Fine);
        service.Guard(w, "alan", CombatGuard.Block);
        for (int i = 0; i < 40; i++) arm.Clock.Tick(w, Fine);
        service.Guard(w, "alan", CombatGuard.Dodge);
        service.Strike(w, "alan", target.Id);
        for (int i = 0; i < 40; i++) arm.Clock.Tick(w, Fine);
        long end = w.CurrentDate.TotalMilliseconds;
        Assert.Contains(service.Log.Records, r => r.Kind == PlayerCommandKind.Block);
        Assert.Contains(service.Log.Records, r => r.Kind == PlayerCommandKind.Dodge);
        Assert.NotNull(body.Combat);

        var replayed = PlayerReplay.Run(snapshot, service.Log.Records,
            new WorldClock(ScenarioRunner.DefaultSystems(), sink: new BufferingWorldEventSink()), new BufferingWorldEventSink(), end);
        Assert.Equal(WorldSnapshot.CanonicalHash(w), WorldSnapshot.CanonicalHash(replayed));
        Assert.Equal(body.Combat, replayed.FindNpc(body.Id)!.Combat);
        Assert.Equal(target.Combat, replayed.FindNpc(target.Id)!.Combat);

        var restored = WorldSnapshot.Deserialize(WorldSnapshot.Serialize(w));
        Assert.Equal(body.Combat, restored.FindNpc(body.Id)!.Combat);
        Assert.Equal(WorldSnapshot.CanonicalHash(w), WorldSnapshot.CanonicalHash(restored));
    }

    [Fact]
    public void Nobody_has_combat_state_in_a_world_without_a_fight()
    {
        var arm = Village(3, Rules);
        arm.Clock.Run(arm.World, 24 * 30);
        Assert.All(arm.World.Npcs, n => Assert.Null(n.Combat));
    }

    private static double MetersApart(WorldState w, Npc a, Npc b) => PlayerCombat.MetersBetween(w, a, b);
}
