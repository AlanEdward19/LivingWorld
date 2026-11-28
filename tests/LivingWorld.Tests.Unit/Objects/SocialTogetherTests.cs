using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Geography.Spatial;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Objects;
using LivingWorld.Simulation.Players;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;
using static LivingWorld.Tests.Unit.Players.PlayerAbsenceTests;

namespace LivingWorld.Tests.Unit.Objects;

/// <summary>#261 (UX do sandbox, épico #246): a interação social aparece de longe (com linha de visão) e o corpo anda até a pessoa; os dois corpos ficam juntos até o fim
/// do gesto; a conversa leve vale com um móvel em uso (declarado no catálogo) e a que pede corpo livre levanta quem começou ou explica o motivo.</summary>
public class SocialTogetherTests
{
    private const int Fine = 250;

    private static (WorldState World, WorldClock Clock, PlayerService Service, Npc Body, Npc Other, long House) Playing(ulong seed = 261)
    {
        var root = LivingWorld.Tests.Unit.Cities.SandboxFixtures.CoupleOnly(LivingWorld.Tests.Unit.Cities.SandboxFixtures.Parse());
        root["Seed"] = seed;
        var loaded = ScenarioLoaderV2.LoadWorld(root.ToJsonString());
        Assert.True(loaded.IsSuccess, loaded.Error);
        var (world, clock) = loaded.Value;
        world.UsePlayers(null);
        var service = new PlayerService(new AllEvents());
        Assert.True(service.SetTickDelta(world, "alan", Fine).IsSuccess);
        var body = world.Npcs.Single(n => n.Name == "Marina");
        var other = world.Npcs.Single(n => n.Name == "Rafael");
        Assert.True(service.Incarnate(world, body.Id, "alan", PossessionMode.Full).IsSuccess);
        var house = world.Buildings.Single();
        var door = PlayerGround.DoorOf(house)!.Value;
        body.MoveTo(door.Door, 0);
        body.SetFine(new FinePose(door.Door.X * FinePose.PerCell + 500, door.Door.Y * FinePose.PerCell + 500, 0, 0, world.CurrentDate.TotalMilliseconds));
        return (world, clock, service, body, other, house.Id.Value);
    }

    private static void Bond(WorldState world, Npc a, Npc b, double affection, double trust)
    {
        foreach (var (x, y) in new[] { (a, b), (b, a) })
        {
            var rel = world.GetOrCreateRelationship(new RelationshipKey(x.Id, y.Id), 0);
            rel.Shift(RelationshipAxis.Affection, affection - rel.Affection);
            rel.Shift(RelationshipAxis.Trust, trust - rel.Trust);
        }
    }

    private static void Run(WorldState world, WorldClock clock, long ms)
    {
        long until = world.CurrentDate.TotalMilliseconds + ms;
        while (world.CurrentDate.TotalMilliseconds < until) clock.Tick(world, Fine);
    }

    private static (double X, double Y) Metres(WorldState world, Npc npc)
    {
        var cell = PlayerGround.PoseOf(world, npc);
        bool fine = npc.Fine is { } f && f.CellX == cell.X && f.CellY == cell.Y;
        double x = fine ? npc.Fine!.X / (double)FinePose.PerCell : cell.X + 0.5;
        double y = fine ? npc.Fine!.Y / (double)FinePose.PerCell : cell.Y + 0.5;
        return (x * world.Players.CellM, y * world.Players.CellM);
    }

    private static double Gap(WorldState world, Npc a, Npc b)
    {
        var (ax, ay) = Metres(world, a);
        var (bx, by) = Metres(world, b);
        return Math.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by));
    }

    private static TickContext Ctx(WorldState world) => new(world, world.Rng, world.Scheduler, new AllEvents());

    /// <summary>Põe o corpo (conduzido ou não) na célula da peça do tipo dado, dentro de casa; devolve o índice da peça.</summary>
    private static int PutAt(WorldState world, Npc npc, string pieceId)
    {
        var building = world.Buildings.Single();
        var layout = HomeFurniture.Of(building, world.ObjectCatalog!);
        int index = layout.ToList().FindIndex(p => p.Id == pieceId);
        var spot = layout[index].Spot!.Value;
        var cell = new CellCoord(building.Position!.Value.X + spot.Cell.X, building.Position!.Value.Y + spot.Cell.Y);
        if (npc.ControllerId is not null)
        {
            npc.MoveTo(cell, 0);
            npc.SetFine(new FinePose(cell.X * FinePose.PerCell + 500, cell.Y * FinePose.PerCell + 500, 0, 0, world.CurrentDate.TotalMilliseconds));
        }
        else if (npc.Interior is not null) npc.MoveWithinBuilding(spot.Cell);
        else npc.EnterBuilding(building.Id, new FloorLevel(spot.Floor), spot.Cell);
        return index;
    }

    [Fact]
    public void The_menu_shows_the_person_across_the_room_and_the_body_walks_there_and_the_pair_stays_together_until_the_gesture_ends()
    {
        var (world, clock, service, body, other, _) = Playing();
        Bond(world, body, other, 90, 90);
        PutAt(world, other, "sofa");
        var person = PlayerService.Reach(world, "alan")!.Single(o => o.Kind == "person" && o.Id == other.Id.Value);
        Assert.NotNull(person.Social);
        double far = Gap(world, body, other);
        Assert.True(far > 2.5, $"a pessoa está longe (a {far:0.0} m) e mesmo assim aparece no menu");

        var result = service.Interact(world, "alan", "person", other.Id.Value, "social:hug");

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal("done", result.Value!.Outcome);
        var rest = Metres(world, other);
        for (int i = 0; i < 800 && body.BodyTask is { Doing: false }; i++) clock.Tick(world, Fine);
        Assert.True(body.BodyTask is { Doing: true }, "o corpo chegou e o gesto começou");
        Assert.True(Gap(world, body, other) <= 1.6, $"os dois ficam juntos: {Gap(world, body, other):0.0} m");
        long end = body.BodyTask!.UntilMs;
        while (world.CurrentDate.TotalMilliseconds < end - Fine)
        {
            clock.Tick(world, Fine);
            Assert.True(other.BodyTask is { Action: SocialInteractions.Action, Doing: true }, "o parceiro segue no gesto");
            Assert.True(Gap(world, body, other) <= 1.6);
        }

        Assert.Equal(rest, Metres(world, other)); // o parceiro não saiu do lugar
        Run(world, clock, 2_000);
        Assert.Null(body.BodyTask);
        Assert.Null(other.BodyTask);
    }

    [Fact]
    public void A_light_chat_works_while_sitting_and_keeps_the_use_while_a_hug_makes_the_body_rise_first()
    {
        var (world, clock, service, body, other, _) = Playing();
        Bond(world, body, other, 90, 90);
        PutAt(world, other, "sofa");
        var sofa = PlayerService.Reach(world, "alan")!.First(o => o.Kind == "object" && o.Label == "Sofá");
        var used = service.Interact(world, "alan", "object", sofa.Id, "use:relax");
        Assert.True(used.IsSuccess, used.Error);
        for (int i = 0; i < 800 && body.BodyTask is { Doing: false }; i++) clock.Tick(world, Fine);
        Assert.True(body.BodyTask is { Action: ObjectUsage.Action, Doing: true });

        var options = SocialInteractions.OptionsFor(world, body, other)!;
        Assert.True(options.Single(o => o.Id == "chat").Available, "bater papo vale sentado");
        Assert.True(options.Single(o => o.Id == "hug").Available, "abraçar levanta o corpo antes (como no Sims)");

        var chat = service.Interact(world, "alan", "person", other.Id.Value, "social:chat");
        Assert.True(chat.IsSuccess, chat.Error);
        Assert.Equal("done", chat.Value!.Outcome);
        Assert.True(body.BodyTask is { Action: ObjectUsage.Action }, "o uso do sofá não foi cancelado");

        for (int attempt = 0; attempt < 12 && body.BodyTask is not { Action: SocialInteractions.Action }; attempt++) // #268: o abraço nem sempre é aceito: tenta de novo, com tempo entre uma e outra
        {
            var hug = service.Interact(world, "alan", "person", other.Id.Value, "social:hug");
            Assert.True(hug.IsSuccess, hug.Error);
            if (hug.Value!.Outcome == "done") break;
            Run(world, clock, 5 * 60_000);
        }

        Assert.True(body.BodyTask is { Action: SocialInteractions.Action }, "levantou para abraçar");
    }

    [Fact]
    public void A_partner_using_a_piece_blocks_what_needs_a_free_body_with_the_reason_and_still_takes_a_light_chat()
    {
        var (world, clock, service, body, other, house) = Playing();
        Bond(world, body, other, 90, 90);
        int chair = PutAt(world, other, "chair");
        PutAt(world, body, "table"); // na mesma cozinha: a linha de visão não tem parede
        Assert.True(ObjectUsage.Begin(world, Ctx(world), other, house, chair, "meal").IsSuccess);
        for (int i = 0; i < 800 && other.BodyTask is { Doing: false }; i++) clock.Tick(world, Fine);
        Assert.True(other.BodyTask is { Action: ObjectUsage.Action, Doing: true });

        var options = SocialInteractions.OptionsFor(world, body, other)!;
        var hug = options.Single(o => o.Id == "hug");
        Assert.False(hug.Available);
        Assert.Contains("móvel", hug.Reason);
        Assert.True(options.Single(o => o.Id == "chat").Available);

        var chat = service.Interact(world, "alan", "person", other.Id.Value, "social:chat");
        Assert.True(chat.IsSuccess, chat.Error);
        for (int i = 0; i < 800 && body.BodyTask is { Doing: false }; i++) clock.Tick(world, Fine);
        Assert.True(other.BodyTask is { Action: ObjectUsage.Action }, "o parceiro segue no móvel dele");
        Run(world, clock, 60_000);
        Assert.Null(body.BodyTask);
    }

    [Fact]
    public void In_real_time_with_the_client_polling_the_pose_the_body_still_walks_to_the_person()
    {
        var (world, clock, service, body, other, _) = Playing();
        Assert.True(service.SetTickDelta(world, "alan", 50).IsSuccess); // tempo real, como no jogo (o cliente lê a pose a cada instante)
        Bond(world, body, other, 90, 90);
        PutAt(world, other, "sofa");
        var start = Metres(world, body);

        var result = service.Interact(world, "alan", "person", other.Id.Value, "social:hug");
        Assert.True(result.IsSuccess, result.Error);
        double first = Gap(world, body, other);
        for (int i = 0; i < 1200 && body.BodyTask is { Doing: false }; i++)
        {
            clock.Tick(world, 50);
            if (i % 2 == 0) Assert.True(service.Walk(world, "alan", 0, 0).IsSuccess); // o cliente pergunta a pose parado
        }

        Assert.NotEqual(start, Metres(world, body));
        Assert.True(Gap(world, body, other) < first, $"se aproximou de {first:0.0} m para {Gap(world, body, other):0.0} m");
        Assert.True(body.BodyTask is { Doing: true }, "chegou e o gesto começou");
    }

    [Fact]
    public void Calling_a_partner_busy_with_a_piece_is_offered_and_the_engine_decides_whether_they_get_up()
    {
        var (world, clock, service, body, other, house) = Playing();
        Bond(world, body, other, 95, 95);
        int chair = PutAt(world, other, "chair");
        PutAt(world, body, "table");
        Assert.True(ObjectUsage.Begin(world, Ctx(world), other, house, chair, "meal").IsSuccess);
        for (int i = 0; i < 800 && other.BodyTask is { Doing: false }; i++) clock.Tick(world, Fine);
        Assert.True(other.BodyTask is { Action: ObjectUsage.Action, Doing: true });

        var hug = SocialInteractions.OptionsFor(world, body, other)!.Single(o => o.Id == "hug");
        Assert.False(hug.Available);
        Assert.True(hug.CanCall); // o menu oferece "Chamar"
        Assert.Contains("às", hug.FreeAtText); // e diz quando o outro termina (hora do jogo)
        Assert.False(SocialInteractions.OptionsFor(world, body, other)!.Single(o => o.Id == "chat").CanCall); // quem já pode conversar não precisa chamar

        // com a briga viva ele nunca levanta, e diz por quê; sem ela, com afeição alta, levanta numa das tentativas (decisão sorteada pelo mundo)
        MoodMechanic.OnSocial(world, world.ObjectCatalog!.Social!.Find("argue")!, body, other, accepted: true, world.CurrentDate.TotalMilliseconds);
        var refused = service.Interact(world, "alan", "person", other.Id.Value, "call");
        Assert.True(refused.IsSuccess, refused.Error);
        Assert.Equal("refused", refused.Value!.Outcome);
        Assert.Contains("magoado", refused.Value.Note);
        Assert.True(other.BodyTask is { Action: ObjectUsage.Action }, "segue no móvel");

        other.SetMoods(null);
        bool rose = false;
        for (int i = 0; i < 40 && !rose; i++)
        {
            Run(world, clock, 60_000);
            if (other.BodyTask is not { Action: ObjectUsage.Action }) break;
            var called = service.Interact(world, "alan", "person", other.Id.Value, "call");
            Assert.True(called.IsSuccess, called.Error);
            rose = called.Value!.Outcome == "done" && other.BodyTask is not { Action: ObjectUsage.Action, Doing: true };
        }

        Assert.True(rose || other.BodyTask is not { Action: ObjectUsage.Action }, "chamado com carinho, o outro levanta");
    }
}
