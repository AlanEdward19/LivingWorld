using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Infrastructure.EventLog;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Players;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;
using static LivingWorld.Tests.Unit.Players.PlayerAbsenceTests;

namespace LivingWorld.Tests.Unit.Players;

/// <summary>AD-197f: a posse comum como disputa de vontade, o estranhamento de quem convive e a reação.</summary>
public class PlayerWillTests
{
    /// <summary>Um NPC com o temperamento pedido e um desconhecido na mesma célula.</summary>
    private static (Arm Arm, Npc Npc, Npc Stranger) Scene(ulong seed, int stability, int conscientiousness, int impulsivity, int altruism,
        PossessionRules? rules = null)
    {
        var arm = Village(seed, rules is null ? null : PlayerRules.Default with { Possession = rules });
        var w = arm.World;
        var npc = Pick(w);
        npc.ImprintPersonality(npc.Personality with
        {
            EmotionalStability = stability,
            Conscientiousness = conscientiousness,
            Impulsivity = impulsivity,
            Altruism = altruism,
            Loyalty = 50,
        });
        npc.SetHealth(100);
        var stranger = w.Npcs.First(n => n.IsAlive && n.Household != npc.Household && n.Spouse != npc.Id && n.MotherId != npc.Id
            && n.FatherId != npc.Id && npc.MotherId != n.Id && npc.FatherId != n.Id
            && !w.Relationships.ContainsKey(new RelationshipKey(npc.Id, n.Id)));
        stranger.MoveTo(npc.CurrentLocation, w.CurrentDate.TotalHours);
        return (arm, npc, stranger);
    }

    private static bool StrikeStranger(ulong seed, int altruism)
    {
        var (arm, npc, stranger) = Scene(seed, 70, 70, 30, altruism);
        Assert.True(PlayerCommands.Incarnate(arm.World, arm.Ctx, npc.Id, "alan").IsSuccess);
        return PlayerCommands.Strike(arm.World, arm.Ctx, npc.Id, "alan", stranger.Id).IsSuccess;
    }

    [Fact]
    public void A_very_altruistic_person_refuses_to_hurt_a_stranger_and_a_selfish_one_obeys_in_at_least_18_of_20_seeds()
    {
        int wins = 0;
        for (ulong seed = 1; seed <= 20; seed++)
            if (!StrikeStranger(seed, altruism: 95) && StrikeStranger(seed, altruism: 10)) wins++;
        Assert.True(wins >= 18, $"{wins}/20");
    }

    [Fact]
    public void The_refusal_wears_off_under_long_control()
    {
        var (arm, npc, stranger) = Scene(3, 70, 70, 30, 95);
        var w = arm.World;
        Assert.True(PlayerCommands.Incarnate(w, arm.Ctx, npc.Id, "alan").IsSuccess);
        Assert.StartsWith(PlayerCommands.Refused, PlayerCommands.Strike(w, arm.Ctx, npc.Id, "alan", stranger.Id).Error);
        double fresh = PossessionContest.Resistance(w, npc, w.CurrentDate.TotalHours);

        arm.Clock.Run(w, 120);
        if (npc.ControllerId is null) return; // expulsou antes: outra prova
        stranger.MoveTo(npc.CurrentLocation, w.CurrentDate.TotalHours);
        var ctx = new TickContext(w, w.Rng, w.Scheduler, arm.Sink);

        Assert.True(PossessionContest.Resistance(w, npc, w.CurrentDate.TotalHours) < fresh);
        Assert.True(PlayerCommands.Strike(w, ctx, npc.Id, "alan", stranger.Id).IsSuccess);
    }

    [Fact]
    public void Persuasion_lowers_the_resistance_once_an_hour()
    {
        var (arm, npc, _) = Scene(4, 70, 70, 30, 50);
        var w = arm.World;
        Assert.True(PlayerCommands.Incarnate(w, arm.Ctx, npc.Id, "alan").IsSuccess);
        double before = PossessionContest.Resistance(w, npc, w.CurrentDate.TotalHours);

        Assert.True(PlayerCommands.Persuade(w, arm.Ctx, npc.Id, "alan").IsSuccess);
        Assert.False(PlayerCommands.Persuade(w, arm.Ctx, npc.Id, "alan").IsSuccess);

        Assert.True(PossessionContest.Resistance(w, npc, w.CurrentDate.TotalHours) < before);
    }

    [Fact]
    public void A_strong_will_prevents_the_entry_and_a_middle_one_expels_while_a_weak_one_does_neither_in_at_least_18_of_20_seeds()
    {
        int prevent = 0, expel = 0, weak = 0;
        for (ulong seed = 1; seed <= 20; seed++)
        {
            var (strong, s, _) = Scene(seed, 100, 100, 0, 50);
            string hash = WorldSnapshot.CanonicalHash(strong.World);
            var refused = PlayerCommands.Incarnate(strong.World, strong.Ctx, s.Id, "alan");
            if (!refused.IsSuccess && refused.Error!.StartsWith(PlayerCommands.Refused, StringComparison.Ordinal)
                && hash == WorldSnapshot.CanonicalHash(strong.World))
                prevent++;

            var (middle, m, _) = Scene(seed, 88, 88, 20, 50);
            Assert.True(PlayerCommands.Incarnate(middle.World, middle.Ctx, m.Id, "alan").IsSuccess);
            middle.Clock.Run(middle.World, 48);
            if (m.ControllerId is null && middle.Sink.Events.Any(e => e.Kind == WorldEventKind.PossessionExpelled)) expel++;

            var (low, l, _) = Scene(seed, 40, 40, 60, 50);
            Assert.True(PlayerCommands.Incarnate(low.World, low.Ctx, l.Id, "alan").IsSuccess);
            low.Clock.Run(low.World, 48);
            if (l.ControllerId == "alan" || !l.IsAlive) weak++;
        }
        Assert.True(prevent >= 18 && expel >= 18 && weak >= 18, $"impede {prevent}, expulsa {expel}, fraca segue possuída {weak}");
    }

    /// <summary>O controlador pede ação 16 vezes, uma por hora: a do costume (imitar) ou outra.</summary>
    private static (List<WorldEvent> Events, PossessionLedger? Book) Conduct(ulong seed, bool imitate)
    {
        var (arm, npc, _) = Scene(seed, 40, 40, 60, 50);
        var w = arm.World;
        Assert.True(PlayerCommands.Incarnate(w, arm.Ctx, npc.Id, "alan").IsSuccess);
        var service = new PlayerService(arm.Sink);
        for (int i = 0; i < 16 && npc.ControllerId == "alan"; i++)
        {
            var stage = w.LifeStageRules.LifeStageOf(npc.AgeYears(w.CurrentDate));
            var routine = w.ActionCatalog.RoutineOf(npc.Profession, stage, w.CurrentDate.Hour);
            var other = routine == ActionType.Idle ? ActionType.Socialize : ActionType.Idle;
            var chosen = imitate && PlayerCommands.Choosable.Contains(routine) ? routine : other;
            service.Act(w, "alan", imitate ? routine : chosen);
            arm.Clock.Run(w, 1);
        }
        return (arm.Sink.Events, w.Possession);
    }

    [Fact]
    public void Imitated_conduct_raises_no_strangeness_and_deviant_conduct_leads_someone_to_conclude_possession_in_at_least_18_of_20_seeds()
    {
        int quiet = 0, concluded = 0;
        for (ulong seed = 1; seed <= 20; seed++)
        {
            var (_, book) = Conduct(seed, imitate: true);
            if (book is null || book.Suspicions.All(s => s.Value == 0)) quiet++;
            var (events, _) = Conduct(seed, imitate: false);
            if (events.Any(e => e.Kind == WorldEventKind.PossessionConcluded) && events.Any(e => e.Kind == WorldEventKind.PossessionConfronted))
                concluded++;
        }
        Assert.True(quiet >= 18 && concluded >= 18, $"imitando sem estranhamento {quiet}, desviando e concluindo {concluded}");
    }

    [Fact]
    public void With_AD197f_off_values_do_not_refuse_and_nobody_grows_strange()
    {
        var (arm, npc, stranger) = Scene(3, 70, 70, 30, 95, PossessionRules.Off);
        Assert.True(PlayerCommands.Incarnate(arm.World, arm.Ctx, npc.Id, "alan").IsSuccess);
        Assert.True(PlayerCommands.Strike(arm.World, arm.Ctx, npc.Id, "alan", stranger.Id).IsSuccess);
        Assert.Null(arm.World.Possession);
    }

    [Fact]
    public void A_log_with_refusals_persuasion_and_deviant_acts_replays_to_the_same_hash()
    {
        var (arm, npc, stranger) = Scene(5, 70, 70, 30, 95);
        var w = arm.World;
        string snapshot = WorldSnapshot.Serialize(w);
        var service = new PlayerService(arm.Sink);
        Assert.True(service.Incarnate(w, npc.Id, "alan").IsSuccess);
        Assert.False(service.Strike(w, "alan", stranger.Id).IsSuccess); // recusa
        Assert.True(service.Persuade(w, "alan").IsSuccess);
        for (int i = 0; i < 6; i++)
        {
            service.Act(w, "alan", i % 2 == 0 ? ActionType.Socialize : ActionType.Idle);
            arm.Clock.Run(w, 7);
        }
        long end = w.CurrentDate.TotalMilliseconds;

        var replayed = PlayerReplay.Run(snapshot, service.Log.Records,
            new WorldClock(ScenarioRunner.DefaultSystems(), sink: new BufferingWorldEventSink()), new BufferingWorldEventSink(), end);
        Assert.Equal(WorldSnapshot.CanonicalHash(w), WorldSnapshot.CanonicalHash(replayed));
    }
}
