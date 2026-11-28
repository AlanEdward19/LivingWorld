using System.Text.Json.Nodes;
using LivingWorld.Domain.Cognition;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Geography.Spatial;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Objects;
using LivingWorld.Simulation.Players;
using LivingWorld.Simulation.Scenarios;
using static LivingWorld.Tests.Unit.Players.PlayerAbsenceTests;

namespace LivingWorld.Tests.Unit.Objects;

/// <summary>#259 (S3 do sandbox, épico #246): interações sociais como dado. O catálogo (scenarios/catalog/social.json) declara requisitos, aceitação por termos, efeitos e
/// gestos; o motor decide a aceitação pelo estado do alvo com o RNG do mundo, ocupa os dois corpos, aplica os efeitos ao fim e deixa o par lembrar.</summary>
public class SocialInteractionsTests
{
    private const int Fine = 250;

    private static string RepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "LivingWorld.sln"))) dir = Directory.GetParent(dir)?.FullName;
        return dir ?? throw new InvalidOperationException("LivingWorld.sln não encontrado");
    }

    private static (WorldState World, WorldClock Clock, PlayerService Service) Sandbox(Action<JsonObject>? edit = null, ulong seed = 259)
    {
        var root = LivingWorld.Tests.Unit.Cities.SandboxFixtures.CoupleOnly(LivingWorld.Tests.Unit.Cities.SandboxFixtures.Parse()); // #258: só o casal
        root["Seed"] = seed;
        edit?.Invoke(root);
        var result = ScenarioLoaderV2.LoadWorld(root.ToJsonString());
        Assert.True(result.IsSuccess, result.Error);
        var (world, clock) = result.Value;
        world.UsePlayers(null);
        return (world, clock, new PlayerService(new AllEvents()));
    }

    private static Npc Marina(WorldState world) => world.Npcs.Single(n => n.Name == "Marina");

    private static Npc Rafael(WorldState world) => world.Npcs.Single(n => n.Name == "Rafael");

    /// <summary>O jogador é Marina, na calçada da casa; Rafael (ninguém o conduz) está do lado dela.</summary>
    private static (WorldState World, WorldClock Clock, PlayerService Service, Npc Body, Npc Other) Playing(ulong seed = 259, Action<JsonObject>? edit = null)
    {
        var (world, clock, service) = Sandbox(edit, seed);
        Assert.True(service.SetTickDelta(world, "alan", Fine).IsSuccess);
        var body = Marina(world);
        var other = Rafael(world);
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

    private static Relationship Feeling(WorldState world, Npc from, Npc to) => world.GetOrCreateRelationship(new RelationshipKey(from.Id, to.Id), 0);

    private static void Set(WorldState world, Npc from, Npc to, double affection, double trust)
    {
        var rel = Feeling(world, from, to);
        rel.Shift(RelationshipAxis.Affection, affection - rel.Affection);
        rel.Shift(RelationshipAxis.Trust, trust - rel.Trust);
    }

    private static double Need(WorldState world, Npc npc, string id) => HomeLife.Value(world, npc, id, world.CurrentDate.TotalMilliseconds)!.Value;

    [Fact]
    public void The_catalog_comes_from_data_and_a_bad_term_names_the_field()
    {
        var (world, _, _) = Sandbox();
        var social = world.ObjectCatalog!.Social!;
        Assert.True(social.Interactions.Count >= 10);
        Assert.Equal(new[] { "friendly", "romantic", "funny", "mean", "apology", "invite" }, social.Categories.Select(c => c.Id).ToArray());
        Assert.Equal("Beijar", social.Find("kiss")!.Name);
        Assert.NotNull(social.Find("tv-together")!.Joint);
        Assert.Contains("spouse", social.Find("kiss")!.Requires.States!);
        Assert.NotNull(HomeLifeLoader.Embedded("social")); // o catálogo padrão é o arquivo embutido, não o código

        var bad = ScenarioLoaderV2.LoadWorld(JsonNode.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "scenarios", "periods", "sandbox-casal.json")))!.AsObject().Also(r =>
        {
            r["Social"] = JsonNode.Parse("""{"interactions":[{"id":"x","name":"X","category":"friendly","accept":{"terms":[{"of":"target.need.nada","weight":1}]}}]}""");
        }).ToJsonString());
        Assert.False(bad.IsSuccess);
        Assert.Contains("Social.", bad.Error);
    }

    [Fact]
    public void A_person_in_reach_lists_every_interaction_with_the_unavailable_ones_dimmed_and_the_reason()
    {
        var (world, _, service, body, other) = Playing();
        Set(world, other, body, 10, 5); // pouca intimidade
        var offer = PlayerService.Reach(world, "alan")!.Single(o => o.Kind == "person" && o.Id == other.Id.Value);

        var options = offer.Social!;
        Assert.True(options.Single(o => o.Id == "chat").Available);
        var hug = options.Single(o => o.Id == "hug");
        Assert.False(hug.Available);
        Assert.Contains("intimidade", hug.Reason);
        Assert.True(options.Single(o => o.Id == "kiss").Available || options.Single(o => o.Id == "kiss").Reason is not null);
        Assert.Equal("Casados", offer.Relation!.StateLabel);
        Assert.InRange(offer.Relation.Affection, 0, 100);
        Assert.False(string.IsNullOrEmpty(offer.Relation.Mood));

        var refused = service.Interact(world, "alan", "person", other.Id.Value, "social:hug");
        Assert.False(refused.IsSuccess); // não vale: 409 e o mundo não muda
        Assert.Null(body.BodyTask);
        Assert.Null(other.BodyTask);
    }

    [Fact]
    public void A_compliment_occupies_both_bodies_then_the_effects_land_at_the_end_and_the_pair_remembers()
    {
        var (world, clock, service, body, other) = Playing();
        Set(world, other, body, 50, 40);
        double affection = Feeling(world, other, body).Affection;
        long ms = world.CurrentDate.TotalMilliseconds;
        HomeLife.Add(world, other, "social", -40, ms);
        double lonely = Need(world, other, "social");

        var result = service.Interact(world, "alan", "person", other.Id.Value, "social:compliment");

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal("done", result.Value!.Outcome);
        for (int i = 0; i < 400 && body.BodyTask is { Doing: false }; i++) clock.Tick(world, Fine);
        Assert.True(body.BodyTask is { Doing: true, Initiator: true });
        Assert.True(other.BodyTask is { Doing: true, Initiator: false });
        Assert.NotNull(PlayerTasks.Of(world, other)); // os dois ocupados
        var view = PlayerTasks.Of(world, body)!;
        Assert.Equal("social", view.Action);
        Assert.Equal(other.Id.Value, view.With);
        Assert.Equal(affection, Feeling(world, other, body).Affection, 3); // nada ainda: os efeitos chegam ao fim

        Run(world, clock, 8_000);

        Assert.Null(body.BodyTask);
        Assert.Null(other.BodyTask);
        Assert.InRange(Feeling(world, other, body).Affection, affection + 4.5, affection + 5.5);
        Assert.True(Need(world, other, "social") > lonely + 3);
        Assert.Equal("compliment", Feeling(world, other, body).LastSocial);
        Assert.Equal("compliment", Feeling(world, body, other).LastSocial);
        Assert.True(Feeling(world, other, body).LastSocialAccepted);
        Assert.Contains(other.Psyche!.Memories, m => m.Kind == LifeEventKind.Social && m.Other == body.Id.Value);
        var last = SocialInteractions.LastFor(world, body, 60_000)!;
        Assert.Contains("gostou do elogio", last.Text);
        Assert.Equal(5, last.Delta);
    }

    [Fact]
    public void The_target_state_decides_a_dirty_or_sour_partner_refuses_a_kiss_with_a_readable_reason()
    {
        var (world, clock, service, body, other) = Playing();
        Set(world, other, body, 60, 50);
        var catalog = world.ObjectCatalog!.Social!;
        var kiss = catalog.Find("kiss")!;
        long ms = world.CurrentDate.TotalMilliseconds;
        double fresh = SocialInteractions.Evaluate(world, catalog, kiss, body, other, ms).Chance;

        HomeLife.Add(world, body, "hygiene", -90, ms); // quem pede está sujo
        var (dirty, why) = SocialInteractions.Evaluate(world, catalog, kiss, body, other, ms);
        Assert.True(dirty < fresh - 25, $"{dirty} contra {fresh}");
        Assert.Contains("banho", why);

        HomeLife.Add(world, body, "hygiene", 90, ms);
        HomeLife.Add(world, other, "fun", -80, ms);
        HomeLife.Add(world, other, "sleep", -80, ms);
        HomeLife.Add(world, other, "hunger", -80, ms);
        double sour = SocialInteractions.Evaluate(world, catalog, kiss, body, other, ms).Chance;
        Assert.True(sour < fresh - 10);

        HomeLife.Add(world, body, "hygiene", -90, ms);
        HomeLife.Add(world, other, "bladder", -90, ms);
        var refused = service.Interact(world, "alan", "person", other.Id.Value, "social:kiss"); // sem chance nenhuma: recusa
        Assert.True(refused.IsSuccess, refused.Error);
        Assert.Equal("refused", refused.Value!.Outcome);
        Assert.Contains("recusou o beijo", refused.Value.Note);
        Assert.Null(body.BodyTask); // recusa não ocupa ninguém
        Assert.False(Feeling(world, other, body).LastSocialAccepted);
        Assert.Equal("kiss", Feeling(world, other, body).LastSocial);
        _ = clock;
    }

    [Fact]
    public void Interrupting_keeps_the_part_lived_and_a_bit_under_a_quarter_keeps_nothing()
    {
        var (world, clock, service, body, other) = Playing();
        Set(world, other, body, 60, 50);
        double start = Feeling(world, other, body).Affection;
        Assert.True(service.Interact(world, "alan", "person", other.Id.Value, "social:hug").IsSuccess);
        while (body.BodyTask is { Doing: false }) clock.Tick(world, Fine);
        Run(world, clock, 1_000); // 1 s de 8
        Assert.True(service.Interrupt(world, "alan").IsSuccess);
        Assert.Null(body.BodyTask);
        Assert.Null(other.BodyTask); // o outro corpo é solto junto
        Assert.Equal(start, Feeling(world, other, body).Affection, 3);

        Assert.True(service.Interact(world, "alan", "person", other.Id.Value, "social:hug").IsSuccess);
        while (body.BodyTask is { Doing: false }) clock.Tick(world, Fine);
        Run(world, clock, 4_000); // metade
        Assert.True(service.Interrupt(world, "alan").IsSuccess);
        double gained = Feeling(world, other, body).Affection - start;
        Assert.InRange(gained, 2.0, 3.2); // metade de 5, com o rendimento do que já tinha
    }

    [Fact]
    public void Recent_meanness_makes_the_partner_refuse_and_an_apology_clears_it()
    {
        var (world, clock, service, body, other) = Playing();
        Set(world, other, body, 60, 50);
        Set(world, body, other, 60, 50);
        var catalog = world.ObjectCatalog!.Social!;
        long ms = world.CurrentDate.TotalMilliseconds;
        double calm = SocialInteractions.Evaluate(world, catalog, catalog.Find("hug")!, body, other, ms).Chance;

        Assert.True(service.Interact(world, "alan", "person", other.Id.Value, "social:argue").IsSuccess);
        while (body.BodyTask is { Doing: false }) clock.Tick(world, Fine);
        Run(world, clock, 30_000);
        Assert.Null(body.BodyTask);
        Assert.True(Feeling(world, other, body).Affection < 60 - 4);
        ms = world.CurrentDate.TotalMilliseconds;
        double tense = SocialInteractions.Evaluate(world, catalog, catalog.Find("hug")!, body, other, ms).Chance;
        Assert.True(tense < calm - 25, $"{tense} contra {calm}");

        Set(world, other, body, 60, 50);
        var sorry = service.Interact(world, "alan", "person", other.Id.Value, "social:apologize");
        Assert.True(sorry.IsSuccess, sorry.Error);
        Assert.Equal("done", sorry.Value!.Outcome);
        while (body.BodyTask is { Doing: false }) clock.Tick(world, Fine);
        Run(world, clock, 12_000);
        Assert.Equal("apology", catalog.Find(Feeling(world, other, body).LastSocial!)!.Category);
        Assert.True(SocialInteractions.Evaluate(world, catalog, catalog.Find("hug")!, body, other, world.CurrentDate.TotalMilliseconds).Chance > tense + 20); // a briga passou
    }

    [Fact]
    public void An_invitation_puts_both_bodies_on_their_own_use_of_the_same_sofa_and_both_needs_rise()
    {
        var (world, clock, service, body, other) = Playing(edit: r => r["Social"] = JsonNode.Parse(
            """{"interactions":[{"id":"tv-together","name":"Ver TV","category":"invite","joint":{"use":"together-left","partnerUse":"together-right"},"accept":{"base":100}}]}"""));
        Set(world, other, body, 70, 60);
        var ms = world.CurrentDate.TotalMilliseconds;
        HomeLife.Add(world, other, "fun", -50, ms);
        HomeLife.Add(world, body, "fun", -50, ms);
        // ficam dentro de casa, na sala (a atividade conjunta usa as peças da casa)
        var house = world.Buildings.Single();
        var layout = HomeFurniture.Of(house, world.ObjectCatalog!);
        var sofa = layout.ToList().FindIndex(p => p.Id == "sofa");
        var spot = layout[sofa].Spot!.Value;
        var cell = new CellCoord(house.Position!.Value.X + spot.Cell.X, house.Position!.Value.Y + spot.Cell.Y);
        body.MoveTo(cell, 0);
        body.SetFine(new FinePose(cell.X * FinePose.PerCell + 500, cell.Y * FinePose.PerCell + 500, 0, 0, ms));
        other.EnterBuilding(house.Id, new FloorLevel(spot.Floor), spot.Cell);
        double funMine = Need(world, body, "fun"), funTheirs = Need(world, other, "fun");

        var result = service.Interact(world, "alan", "person", other.Id.Value, "social:tv-together");

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal("done", result.Value!.Outcome);
        Assert.Equal("together-left", body.BodyTask!.Use);
        Assert.Equal("together-right", other.BodyTask!.Use);
        Assert.Equal(body.BodyTask.Piece, other.BodyTask.Piece); // a mesma peça, dois corpos
        while (body.BodyTask is { Doing: false }) clock.Tick(world, Fine);
        Run(world, clock, 10 * 60_000);
        Assert.True(Need(world, body, "fun") > funMine + 5);
        Assert.True(Need(world, other, "fun") > funTheirs + 5);
    }

    [Fact]
    public void A_lonely_npc_starts_an_interaction_on_its_own_and_the_same_seed_gives_the_same_world()
    {
        string Day(ulong seed)
        {
            var (world, clock, _) = Sandbox(seed: seed);
            var marina = Marina(world);
            var rafael = Rafael(world);
            var ms = world.CurrentDate.TotalMilliseconds;
            HomeLife.Add(world, marina, "social", -60, ms);
            HomeLife.Add(world, rafael, "social", -60, ms);
            Assert.NotNull(world.ObjectCatalog!.Social);
            clock.Run(world, 24);
            var kinds = new[] { Feeling(world, marina, rafael), Feeling(world, rafael, marina) };
            Assert.Contains(kinds, r => r.LastSocial is not null); // alguém procurou companhia e o par ficou com a lembrança
            return WorldSnapshot.CanonicalHash(world);
        }

        Assert.Equal(Day(259), Day(259));
    }

    [Fact]
    public void A_snapshot_in_the_middle_of_an_interaction_continues_to_the_same_world()
    {
        string Play(bool cut)
        {
            var (world, clock, service, body, other) = Playing(77);
            Set(world, other, body, 60, 50);
            Assert.True(service.Interact(world, "alan", "person", other.Id.Value, "social:hug").IsSuccess);
            while (body.BodyTask is { Doing: false }) clock.Tick(world, Fine);
            Run(world, clock, 3_000);
            if (cut)
            {
                var loaded = WorldSnapshot.Deserialize(WorldSnapshot.Serialize(world));
                Assert.Equal(WorldSnapshot.CanonicalHash(world), WorldSnapshot.CanonicalHash(loaded));
                Assert.Equal(other.BodyTask, loaded.FindNpc(other.Id)!.BodyTask);
                Assert.Equal(body.BodyTask, loaded.FindNpc(body.Id)!.BodyTask);
            }

            Run(world, clock, 20_000);
            return WorldSnapshot.CanonicalHash(world);
        }

        Assert.Equal(Play(false), Play(false));
        Assert.Equal(Play(false), Play(true));
    }

    [Fact]
    public void The_interior_projection_shows_the_pair_with_the_gesture_and_who_they_face()
    {
        var (world, clock, service, body, other) = Playing();
        Set(world, other, body, 60, 50);
        Assert.True(service.Interact(world, "alan", "person", other.Id.Value, "social:hug").IsSuccess);
        while (body.BodyTask is { Doing: false }) clock.Tick(world, Fine);
        var home = world.Buildings.Single();
        other.EnterBuilding(home.Id, new FloorLevel(0), new CellCoord(2, 2));

        var snapshot = LivingWorld.Api.Visual.Projection.InteriorProjector.Build(world, home.Id).Value!;

        var occupant = snapshot.Occupants.SingleOrDefault(o => o.Npc == other.Id);
        Assert.NotNull(occupant);
        Assert.Equal("hug", occupant!.Social!.Pose);
        Assert.Equal(body.Id.Value, occupant.Social.With);
        Assert.False(occupant.Social.Initiator);
    }
}
