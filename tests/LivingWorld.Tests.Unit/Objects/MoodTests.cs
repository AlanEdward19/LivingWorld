using System.Text.Json.Nodes;
using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Objects;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Objects;
using LivingWorld.Simulation.Players;
using LivingWorld.Simulation.Scenarios;
using static LivingWorld.Tests.Unit.Players.PlayerAbsenceTests;

namespace LivingWorld.Tests.Unit.Objects;

/// <summary>#262 (épico #246): momentos (moodlets), emoções, aspirações e desejos como catálogo em dados (scenarios/catalog/moods.json). O motor aplica e expira os momentos
/// (tempo de mundo, no snapshot), a nota de humor pesa na aceitação social, os momentos mudam o rendimento dos usos e a vontade de iniciar, e os desejos nascem do estado.</summary>
public class MoodTests
{
    private const int Fine = 250;

    private static (WorldState World, WorldClock Clock, PlayerService Service, Npc Body, Npc Other) Playing(ulong seed = 262, Action<JsonObject>? edit = null)
    {
        var root = LivingWorld.Tests.Unit.Cities.SandboxFixtures.CoupleOnly(LivingWorld.Tests.Unit.Cities.SandboxFixtures.Parse());
        root["Seed"] = seed;
        edit?.Invoke(root);
        var result = ScenarioLoaderV2.LoadWorld(root.ToJsonString());
        Assert.True(result.IsSuccess, result.Error);
        var (world, clock) = result.Value;
        world.UsePlayers(null);
        var service = new PlayerService(new AllEvents());
        Assert.True(service.SetTickDelta(world, "alan", Fine).IsSuccess);
        var body = world.Npcs.Single(n => n.Name == "Marina");
        var other = world.Npcs.Single(n => n.Name == "Rafael");
        Assert.True(service.Incarnate(world, body.Id, "alan", PossessionMode.Full).IsSuccess);
        var door = PlayerGround.DoorOf(world.Buildings.Single())!.Value;
        body.MoveTo(door.Door, 0);
        body.SetFine(new FinePose(door.Door.X * FinePose.PerCell + 500, door.Door.Y * FinePose.PerCell + 500, 0, 0, world.CurrentDate.TotalMilliseconds));
        if (other.Interior is not null) other.ExitBuilding();
        other.MoveTo(door.Door, 0);
        return (world, clock, service, body, other);
    }

    private static void Run(WorldState world, WorldClock clock, long ms)
    {
        long until = world.CurrentDate.TotalMilliseconds + ms;
        while (world.CurrentDate.TotalMilliseconds < until) clock.Tick(world, Fine);
    }

    private static long Now(WorldState world) => world.CurrentDate.TotalMilliseconds;

    private static IReadOnlyList<MomentView> Moments(WorldState world, Npc npc) => MoodMechanic.Moments(world, npc, Now(world));

    private static void Quarrel(WorldState world, Npc a, Npc b) =>
        MoodMechanic.OnSocial(world, world.ObjectCatalog!.Social!.Find("argue")!, a, b, accepted: true, Now(world));

    [Fact]
    public void Moments_emotions_aspirations_and_wishes_come_from_the_data_catalog_and_a_bad_reference_names_the_field()
    {
        var (world, _, _, _, _) = Playing();
        var moods = world.ObjectCatalog!.Moods!;
        Assert.True(moods.Moments.Count >= 25 && moods.Wishes.Count >= 10 && moods.Aspirations.Count >= 4);
        Assert.Equal("Banho tomado", moods.Moment("clean")!.Name);
        Assert.NotNull(HomeLifeLoader.Embedded("moods")); // o catálogo padrão é o arquivo embutido, não o código

        var bad = ScenarioLoaderV2.LoadWorld(LivingWorld.Tests.Unit.Cities.SandboxFixtures.Parse().Also(r =>
            r["Moods"] = JsonNode.Parse("""{"emotions":[{"id":"a","name":"A","icon":"x"}],"moments":[{"id":"m","name":"M","emotion":"nada","weight":1,"durationMin":5,"trigger":{"kind":"use","use":"shower"}}]}""")).ToJsonString());
        Assert.False(bad.IsSuccess);
        Assert.Contains("Moods.moments[0].emotion", bad.Error);
    }

    [Fact]
    public void A_finished_shower_gives_a_moment_with_time_that_expires_and_an_interrupted_one_does_not()
    {
        var (world, clock, service, body, _) = Playing();
        HomeLife.Add(world, body, "hygiene", -50, Now(world));
        var shower = PlayerService.Reach(world, "alan")!.Single(o => o.Kind == "object" && o.Label == "Chuveiro");
        Assert.True(service.Interact(world, "alan", "object", shower.Id, "use:shower").IsSuccess);
        Run(world, clock, 60_000);
        Assert.True(service.Interrupt(world, "alan").IsSuccess);
        Assert.DoesNotContain(Moments(world, body), m => m.Id == "clean"); // só o uso inteiro conta

        Assert.True(service.Interact(world, "alan", "object", shower.Id, "use:shower").IsSuccess);
        for (int i = 0; i < 40_000 && (body.BodyTask is not null || !Moments(world, body).Any(m => m.Id == "clean")); i++) clock.Tick(world, Fine);
        var clean = Assert.Single(Moments(world, body), m => m.Id == "clean");
        Assert.Equal("Banho tomado", clean.Name);
        Assert.InRange(clean.RemainingMs!.Value, 170 * 60_000L, 180 * 60_000L);
        Assert.True(MoodMechanic.Sum(world, body, Now(world)) >= 2);

        Run(world, clock, 181 * 60_000L);
        Assert.DoesNotContain(Moments(world, body), m => m.Id == "clean"); // expirou, sem passo por tick
    }

    [Fact]
    public void A_fight_stacks_names_the_cause_and_drives_the_dominant_emotion_and_the_face()
    {
        var (world, _, _, body, other) = Playing();
        Assert.Equal("calm", MoodMechanic.EmotionOf(world, body, Now(world))?.Id);
        Assert.Null(MoodMechanic.FaceOf(world, body, Now(world)));

        Quarrel(world, body, other);
        Quarrel(world, body, other);
        Quarrel(world, body, other);
        Quarrel(world, body, other); // empilha até o limite
        var quarrel = Assert.Single(Moments(world, body), m => m.Id == "quarrel");
        Assert.Equal("Brigou com Rafael", quarrel.Name);
        Assert.Equal("Rafael", quarrel.Cause);
        Assert.Equal(3, quarrel.Count);
        Assert.Equal(-9, quarrel.Weight);
        Assert.Equal("Marina", Assert.Single(Moments(world, other), m => m.Id == "quarrel").Cause); // o id 0 também é alguém: a causa não se perde
        Assert.Equal("angry", MoodMechanic.EmotionOf(world, other, Now(world))!.Id);
        Assert.Equal("anger", MoodMechanic.FaceOf(world, body, Now(world))); // o rosto que o boneco já sabe desenhar
        var view = MoodMechanic.ViewOf(world, body, Now(world))!;
        Assert.Equal("Irritado", view.EmotionName);
        Assert.True(view.Score < 20, $"nota {view.Score}");
    }

    [Fact]
    public void A_bad_mood_weighs_on_social_acceptance_and_on_what_the_person_picks_to_do()
    {
        var (world, _, _, body, other) = Playing();
        var social = world.ObjectCatalog!.Social!;
        foreach (var (a, b) in new[] { (body, other), (other, body) })
        {
            var rel = world.GetOrCreateRelationship(new RelationshipKey(a.Id, b.Id), 0);
            rel.Shift(RelationshipAxis.Affection, 70 - rel.Affection);
            rel.Shift(RelationshipAxis.Trust, 60 - rel.Trust);
        }

        var hug = social.Find("hug")!;
        double calm = SocialInteractions.Evaluate(world, social, hug, body, other, Now(world)).Chance;
        double biasBefore = MoodMechanic.SocialBias(world, other, "mean", Now(world));
        Quarrel(world, body, other);
        Quarrel(world, body, other);
        Quarrel(world, body, other);
        var (chance, reason) = SocialInteractions.Evaluate(world, social, hug, body, other, Now(world));
        Assert.True(chance < calm - 10, $"{chance} contra {calm}");
        Assert.Contains("bom humor", reason); // o humor é o termo que mais pesa: o motivo da recusa
        Assert.True(MoodMechanic.SocialBias(world, other, "mean", Now(world)) > biasBefore * 3, "quem brigou puxa mais o que é mau");
        Assert.True(MoodMechanic.SocialBias(world, other, "romantic", Now(world)) < 0.2);
    }

    [Fact]
    public void Moments_change_how_much_a_use_delivers()
    {
        var (world, clock, service, body, _) = Playing();
        double Gain(bool rested)
        {
            HomeLife.Add(world, body, "fun", -60, Now(world));
            if (rested) MoodMechanic.OnAction(world, body, ActionType.Sleep, Now(world));
            var tv = PlayerService.Reach(world, "alan")!.Single(o => o.Kind == "object" && o.Label == "Televisão");
            double before = HomeLife.Value(world, body, "fun", Now(world))!.Value;
            Assert.True(service.Interact(world, "alan", "object", tv.Id, "use:watch").IsSuccess);
            while (body.BodyTask is { Doing: false }) clock.Tick(world, Fine);
            Run(world, clock, 600_000);
            Assert.True(service.Interrupt(world, "alan").IsSuccess);
            return HomeLife.Value(world, body, "fun", Now(world))!.Value - before;
        }

        double plain = Gain(false);
        Run(world, clock, 2 * 60 * 60_000L);
        double withMood = Gain(true);
        Assert.True(MoodMechanic.UseRate(world, body, Now(world)) > 1.1);
        Assert.True(withMood > plain * 1.05, $"{withMood} contra {plain}");
    }

    [Fact]
    public void Wishes_come_from_the_state_and_the_aspiration_and_the_hug_wish_is_fulfilled_with_a_reward()
    {
        var (world, _, _, body, other) = Playing();
        HomeLife.Add(world, body, "hygiene", -50, Now(world));
        MoodMechanic.Hourly(world, body, Now(world));
        var wishes = MoodMechanic.Wishes(world, body, Now(world));
        Assert.Equal(3, wishes.Count);
        Assert.All(wishes, w => Assert.False(string.IsNullOrWhiteSpace(w.Text)));
        Assert.Equal("knowledge", MoodMechanic.AspirationOf(world, body)!.Id); // a personalidade escolhe
        Assert.Equal("comfort", MoodMechanic.AspirationOf(world, other)!.Id);

        var rel = world.GetOrCreateRelationship(new RelationshipKey(other.Id, body.Id), 0);
        rel.Shift(RelationshipAxis.Affection, 80 - rel.Affection);
        rel.Shift(RelationshipAxis.Trust, 70 - rel.Trust);
        var state = body.Moods!;
        body.SetMoods(state with { Wishes = [new WishInstance("hug", other.Id.Value + 1, Now(world))] });
        Assert.Equal("Abraçar Rafael", MoodMechanic.Wishes(world, body, Now(world)).Single().Text);
        Assert.Equal(other.Id.Value, MoodMechanic.Wishes(world, body, Now(world)).Single().Other); // o cliente recebe o id real do alvo
        body.SetMoods(body.Moods! with { Wishes = [new WishInstance("shower", 0, Now(world))] });
        Assert.Equal(-1, MoodMechanic.Wishes(world, body, Now(world)).Single().Other); // sem alvo
        body.SetMoods(state with { Wishes = [new WishInstance("hug", other.Id.Value + 1, Now(world))] });

        var goals = PlayerGoals.Of(world, body);
        Assert.Contains(goals, g => g.Kind == "wish.hug" && g.Reason == "Abraçar Rafael"); // liga com as metas do jogador

        MoodMechanic.OnSocial(world, world.ObjectCatalog!.Social!.Find("hug")!, body, other, accepted: true, Now(world)); // o abraço aceito (o gesto vivo é do social)
        Assert.Equal(10, body.Moods!.Points);
        Assert.DoesNotContain(MoodMechanic.Wishes(world, body, Now(world)), w => w.Id == "hug");
        Assert.Equal("Abraçar Rafael", body.Moods.Recent![0].Text);
        Assert.Contains(Moments(world, body), m => m.Id == "wish-done");
        Assert.Contains(Moments(world, body), m => m.Id == "hugged" && m.Cause == "Rafael");
    }

    [Fact]
    public void A_wish_fulfills_by_a_matching_use_by_state_and_expires_by_time()
    {
        var (world, _, _, body, other) = Playing();
        long ms = Now(world);
        body.SetMoods(MoodState.Empty with
        {
            Wishes = [new WishInstance("shower", 0, ms), new WishInstance("fun-high", 0, ms), new WishInstance("hug", other.Id.Value + 1, ms - 20 * 3_600_000L)],
        });
        MoodMechanic.OnUse(world, body, "shower", ms);
        Assert.Equal(1, body.Moods!.Done);
        HomeLife.Add(world, body, "fun", 100, ms);
        MoodMechanic.Hourly(world, body, ms);
        Assert.Equal(2, body.Moods!.Done); // "ficar com a diversão alta" é de estado
        Assert.DoesNotContain(body.Moods.Wishes, w => w.SinceMs < ms - 8 * 3_600_000L); // o que venceu (8 h) saiu
        Assert.True(body.Moods.Points >= 20);
    }

    [Fact]
    public void State_moments_hold_while_the_condition_does_and_snapshot_and_replay_are_deterministic()
    {
        string Play(bool cut)
        {
            var (world, clock, _, body, other) = Playing(77);
            HomeLife.Add(world, body, "hygiene", -80, Now(world));
            Assert.Contains(Moments(world, body), m => m.Id == "smelly" && m.RemainingMs is null);
            Quarrel(world, body, other);
            Run(world, clock, 2 * 60 * 60_000L);
            MoodMechanic.Hourly(world, body, Now(world));
            if (cut)
            {
                var loaded = WorldSnapshot.Deserialize(WorldSnapshot.Serialize(world));
                Assert.Equal(WorldSnapshot.CanonicalHash(world), WorldSnapshot.CanonicalHash(loaded));
                var again = loaded.FindNpc(body.Id)!;
                Assert.Equal(body.Moods!.Moments, again.Moods!.Moments);
                Assert.Equal(body.Moods.Wishes, again.Moods.Wishes);
                Assert.Equal(Moments(world, body).Select(m => m.Id), MoodMechanic.Moments(loaded, again, Now(loaded)).Select(m => m.Id));
            }

            Run(world, clock, 8 * 60 * 60_000L);
            return WorldSnapshot.CanonicalHash(world);
        }

        Assert.Equal(Play(false), Play(true));
        Assert.Equal(Play(false), Play(false));
    }

    [Fact]
    public void A_scenario_without_the_catalog_has_no_mood_state_at_all()
    {
        var (world, clock, _, body, other) = Playing(edit: r => r["CatalogExtends"] = new JsonArray("home-life", "social"));
        Assert.Null(world.ObjectCatalog!.Moods);
        Run(world, clock, 3 * 60 * 60_000L);
        Assert.Null(body.Moods);
        Assert.Null(MoodMechanic.ViewOf(world, body, Now(world)));
        Assert.Null(MoodMechanic.FaceOf(world, body, Now(world)));
        Assert.Equal(1, MoodMechanic.UseRate(world, other, Now(world)));
    }
}
