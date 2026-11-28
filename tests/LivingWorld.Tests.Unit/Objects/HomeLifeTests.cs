using System.Text.Json.Nodes;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Objects;
using LivingWorld.Domain.Population;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Objects;
using LivingWorld.Simulation.Players;
using LivingWorld.Simulation.Scenarios;
using static LivingWorld.Tests.Unit.Players.PlayerAbsenceTests;

namespace LivingWorld.Tests.Unit.Objects;

/// <summary>#255 (S2 do sandbox, épico #246): necessidades e objetos como dado. Higiene, diversão e bexiga vêm do catálogo do cenário; cada objeto declara os usos
/// (o que se faz, quanto leva, o que entrega); o corpo anda até o lugar de uso e só então a ação começa; interromper mantém o que subiu; há fila; quem
/// ninguém conduz cuida de si pelos mesmos objetos.</summary>
public class HomeLifeTests
{
    private const int Fine = 250; // ms de mundo por tick fino

    private static string RepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "LivingWorld.sln"))) dir = Directory.GetParent(dir)?.FullName;
        return dir ?? throw new InvalidOperationException("LivingWorld.sln não encontrado");
    }

    private static (WorldState World, WorldClock Clock, PlayerService Service) Sandbox(Action<JsonObject>? edit = null, ulong seed = 255)
    {
        var root = JsonNode.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "scenarios", "periods", "sandbox-casal.json")))!.AsObject();
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

    /// <summary>Entra em Marina, dentro de casa, com o tempo fino (50 ms por tick: 1:1).</summary>
    private static (WorldState World, WorldClock Clock, PlayerService Service, Npc Body) Playing(ulong seed = 255)
    {
        var (world, clock, service) = Sandbox(seed: seed);
        Assert.True(service.SetTickDelta(world, "alan", Fine).IsSuccess);
        var body = Marina(world);
        Assert.True(service.Incarnate(world, body.Id, "alan", PossessionMode.Full).IsSuccess);
        var home = world.Buildings.Single();
        var door = PlayerGround.DoorOf(home)!.Value;
        body.MoveTo(door.Door, 0);
        body.SetFine(new FinePose(door.Door.X * FinePose.PerCell + 500, door.Door.Y * FinePose.PerCell + 500, 0, 0, world.CurrentDate.TotalMilliseconds));
        return (world, clock, service, body);
    }

    private static InteractionOffer Offer(WorldState world, string label) =>
        PlayerService.Reach(world, "alan")!.Single(o => o.Kind == "object" && o.Label == label);

    private static void Run(WorldState world, WorldClock clock, long ms)
    {
        long until = world.CurrentDate.TotalMilliseconds + ms;
        while (world.CurrentDate.TotalMilliseconds < until) clock.Tick(world, Fine);
    }

    private static double Hygiene(WorldState world, Npc npc) => HomeLife.Value(world, npc, "hygiene", world.CurrentDate.TotalMilliseconds)!.Value;

    [Fact]
    public void Needs_and_objects_come_from_scenario_data_and_the_default_catalog_is_an_embedded_file()
    {
        var (world, _, _) = Sandbox();

        var needs = world.ObjectCatalog!.Needs!;
        Assert.Equal(new[] { "hygiene", "fun", "bladder" }, needs.Needs.Select(n => n.Id).ToArray());
        Assert.Equal(2.5, needs.Find("hygiene")!.DecayPerHour);
        var shower = world.ObjectCatalog.Find("shower")!;
        var use = Assert.Single(shower.Uses!);
        Assert.Contains(use.Effects, e => e.Need == "hygiene" && e.PerHour > 0);
        Assert.Equal(new[] { "banheiro" }, shower.Rooms);

        var root = new JsonObject();
        DefaultObjects.AddTo(root); // o catálogo padrão não está no código: é o arquivo scenarios/catalog/default-objects.json
        Assert.NotNull(HomeLifeLoader.Embedded("default-objects"));
        Assert.Equal(11, root["Objects"]!.AsArray().Count);
        Assert.Contains("table", root["Furnishings"]!["default"]!.AsArray().Select(n => n!.ToString()));
        Assert.Contains("oficina", root["Rooms"]!["kinds"]!.AsArray().Select(n => n!["id"]!.ToString()));
    }

    [Fact]
    public void Scenario_overrides_a_catalog_object_by_id_and_a_bad_use_names_the_field()
    {
        var (world, _, _) = Sandbox(r =>
        {
            var objects = new JsonArray(new JsonObject
            {
                ["id"] = "shower",
                ["name"] = "Ducha",
                ["category"] = "appliance",
                ["variationSeed"] = 0,
                ["shape"] = new JsonArray(new JsonObject { ["shape"] = "box", ["size"] = new JsonArray(1.0, 2.0, 1.0), ["offset"] = new JsonArray(0, 1.0, 0), ["color"] = "cfe3ee" }),
                ["usePoints"] = new JsonArray(),
            });
            r["objectsFormat"] = 1;
            r["Objects"] = objects;
        });
        Assert.Equal("Ducha", world.ObjectCatalog!.Find("shower")!.Name);
        Assert.Null(world.ObjectCatalog.Find("shower")!.Uses); // o do cenário vence por inteiro
        Assert.NotNull(world.ObjectCatalog.Find("toilet")!.Uses); // o resto vem do catálogo estendido

        var bad = ScenarioLoaderV2.LoadWorld(JsonNode.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "scenarios", "periods", "sandbox-casal.json")))!.AsObject().Also(r =>
        {
            r["Needs"] = new JsonArray(new JsonObject { ["id"] = "hygiene", ["name"] = "X", ["start"] = 500, ["decayPerHour"] = 1, ["urgentBelow"] = 10 });
        }).ToJsonString());
        Assert.False(bad.IsSuccess);
        Assert.Contains("Needs[0].start", bad.Error);
    }

    [Fact]
    public void Catalog_needs_fall_by_the_declared_rate_and_start_at_the_declared_level()
    {
        var (world, clock, _) = Sandbox();
        var marina = Marina(world);
        Assert.Equal(85, Hygiene(world, marina), 3);

        clock.Run(world, 10);

        Assert.Equal(85 - 10 * 2.5, Hygiene(world, marina), 0);
        Assert.Equal(70 - 10 * 3.0, HomeLife.Value(world, marina, "bladder", world.CurrentDate.TotalMilliseconds)!.Value, 0);
        Assert.Equal(new[] { "Higiene", "Diversão", "Bexiga" }, HomeLife.Readings(world, marina, world.CurrentDate.TotalMilliseconds).Select(r => r.Name).ToArray());
    }

    [Fact]
    public void The_body_walks_to_the_slot_and_the_action_only_starts_there_then_the_need_rises_until_the_end()
    {
        var (world, clock, service, body) = Playing();
        var shower = Offer(world, "Chuveiro");
        long ground = world.CurrentDate.TotalMilliseconds;
        HomeLife.Add(world, body, "hygiene", -50, ground); // suja
        double before = Hygiene(world, body);
        Assert.True(service.Interact(world, "alan", "object", shower.Id, "use:shower").IsSuccess);

        var target = ObjectUsage.Resolve(world, shower.Id / 1000, (int)(shower.Id % 1000), "shower").Value!;
        bool sawWalking = false;
        for (int i = 0; i < 4000 && body.BodyTask is { Doing: false }; i++)
        {
            clock.Tick(world, Fine);
            if (body.BodyTask is { Doing: false })
            {
                sawWalking = true;
                Assert.InRange(Hygiene(world, body), before - 0.3, before + 0.001); // andando: nada subiu (só o desgaste)
            }
        }

        Assert.True(sawWalking, "o corpo teve de andar até o chuveiro");
        var started = body.BodyTask;
        Assert.True(started is { Doing: true });
        long dx = body.Fine!.X - target.FineX, dy = body.Fine.Y - target.FineY;
        Assert.True(dx * dx + dy * dy < 100 * 100, $"a ação só começa no lugar: a {Math.Sqrt(dx * dx + dy * dy):0} milésimos");
        Run(world, clock, 120_000);
        double mid = Hygiene(world, body);
        Assert.InRange(mid, before + 3, 100); // subindo durante, em fatias

        Run(world, clock, 20 * 60_000);
        Assert.Null(body.BodyTask);
        Assert.InRange(Hygiene(world, body), 99, 100); // untilFull: parou ao encher
    }

    [Fact]
    public void Stopping_keeps_what_already_rose_and_walking_interrupts_the_use()
    {
        var (world, clock, service, body) = Playing();
        var tv = Offer(world, "Televisão");
        HomeLife.Add(world, body, "fun", -60, world.CurrentDate.TotalMilliseconds);
        double before = HomeLife.Value(world, body, "fun", world.CurrentDate.TotalMilliseconds)!.Value;
        Assert.True(service.Interact(world, "alan", "object", tv.Id, "use:watch").IsSuccess);
        while (body.BodyTask is { Doing: false }) clock.Tick(world, Fine);
        Run(world, clock, 5 * 60_000);

        Assert.True(service.Interrupt(world, "alan").IsSuccess);

        double after = HomeLife.Value(world, body, "fun", world.CurrentDate.TotalMilliseconds)!.Value;
        Assert.InRange(after, before + 5, before + 12); // 5 min a 90 por hora ≈ +7,5 (o que subiu fica; o resto não existiu)
        Assert.Null(body.BodyTask);
        Run(world, clock, 10 * 60_000);
        Assert.InRange(HomeLife.Value(world, body, "fun", world.CurrentDate.TotalMilliseconds)!.Value, after - 6, after); // nada mais sobe (só decai)

        var again = service.Interact(world, "alan", "object", tv.Id, "use:watch");
        Assert.True(again.IsSuccess, again.Error);
        Assert.True(service.Walk(world, "alan", 1000, 0).IsSuccess); // WASD também interrompe, mesmo a caminho
        Assert.Null(body.BodyTask);
    }

    [Fact]
    public void An_occupied_piece_refuses_and_the_pantry_pays_for_what_the_use_consumes()
    {
        var (world, clock, service, body) = Playing();
        var rafael = Rafael(world);
        var shower = Offer(world, "Chuveiro");
        var house = world.Buildings.Single();
        var household = world.Households.Single();
        var water = new LivingWorld.Domain.Geography.ResourceType(world.EconomyRules.WaterResourceId);
        long pantry = household.Stock.GetValueOrDefault(water);
        Assert.True(ObjectUsage.Begin(world, new LivingWorld.Simulation.Scheduling.TickContext(world, world.Rng, world.Scheduler, new AllEvents()),
            rafael, house.Id.Value, (int)(shower.Id % 1000), "shower").IsSuccess);

        var refused = service.Interact(world, "alan", "object", shower.Id, "use:shower");

        Assert.False(refused.IsSuccess);
        Assert.Contains("Rafael", refused.Error);
        Assert.Null(body.BodyTask);

        Run(world, clock, 30 * 60_000); // Rafael acaba o banho (2 de água, gastos na chegada)
        Assert.True(pantry - household.Stock.GetValueOrDefault(water) >= 2);
        Assert.True(service.Interact(world, "alan", "object", shower.Id, "use:shower").IsSuccess); // livre de novo
    }

    [Fact]
    public void The_queue_holds_four_actions_runs_them_in_order_and_each_can_be_cancelled()
    {
        var (world, clock, service, body) = Playing();
        HomeLife.Add(world, body, "bladder", -60, world.CurrentDate.TotalMilliseconds);
        HomeLife.Add(world, body, "hygiene", -60, world.CurrentDate.TotalMilliseconds);
        var toilet = Offer(world, "Vaso");
        var shower = Offer(world, "Chuveiro");
        var tv = Offer(world, "Televisão");
        var fridge = Offer(world, "Geladeira");
        var first = service.Interact(world, "alan", "object", toilet.Id, "queue:toilet"); // corpo livre: começa já
        Assert.True(first.IsSuccess, first.Error);
        Assert.NotNull(body.BodyTask);
        Assert.True(service.Interact(world, "alan", "object", shower.Id, "queue:shower").IsSuccess);
        Assert.True(service.Interact(world, "alan", "object", tv.Id, "queue:watch").IsSuccess);
        Assert.True(service.Interact(world, "alan", "object", fridge.Id, "queue:snack").IsSuccess);
        Assert.True(service.Interact(world, "alan", "object", tv.Id, "queue:watch").IsSuccess); // quatro na fila (a primeira já corre)
        Assert.Equal(4, body.UseQueue!.Count);
        var sixth = service.Interact(world, "alan", "object", tv.Id, "queue:watch");
        Assert.False(sixth.IsSuccess);
        Assert.Contains("fila", sixth.Error);

        Assert.True(service.Interact(world, "alan", "queue", 1, "cancel").IsSuccess); // tira a TV do meio (a 2ª da fila)
        Assert.Equal(new[] { "shower", "snack", "watch" }, body.UseQueue!.Select(q => q.Use).ToArray());

        var seen = new List<string>();
        for (int i = 0; i < 40 * 60 * 20 && (body.BodyTask is not null || body.UseQueue is { Count: > 0 }); i++)
        {
            clock.Tick(world, Fine);
            if (body.BodyTask is { Doing: true, Use: { } use } && (seen.Count == 0 || seen[^1] != use)) seen.Add(use);
        }

        Assert.Equal(new[] { "toilet", "shower", "snack", "watch" }, seen); // a ordem de pedido
        Assert.Null(body.UseQueue);
    }

    [Fact]
    public void The_partner_nobody_controls_takes_care_of_bladder_hygiene_and_fun_alone_in_a_day()
    {
        var (world, clock, _) = Sandbox();
        var marina = Marina(world);
        var used = new HashSet<string>();
        double lowest = 100;
        var timeline = new List<string>();
        for (int h = 0; h < 72; h++)
        {
            clock.Tick(world);
            foreach (var npc in world.Npcs)
            {
                if (npc.BodyTask is { Use: { } use }) used.Add(use);
                var readings = HomeLife.Readings(world, npc, world.CurrentDate.TotalMilliseconds);
                foreach (var r in readings) lowest = Math.Min(lowest, r.Value);
                timeline.Add($"h{h + 1} {npc.Name[0]} {npc.CurrentAction}/{npc.Interior?.LocalCell}/{npc.BodyTask?.Use} " + string.Join(" ", readings.Select(r => $"{r.Id}={r.Value}")));
            }
        }

        Assert.Contains("toilet", used);
        Assert.Contains("shower", used);
        Assert.True(used.Contains("watch") || used.Contains("relax"), $"ninguém se divertiu: {string.Join(",", used)}");
        Assert.True(lowest > 0, $"alguma barra zerou: {lowest}\n{string.Join("\n", timeline)}\nusos: {string.Join(",", used)}");
        Assert.True(marina.IsAlive && Rafael(world).IsAlive);
    }

    [Fact]
    public void A_low_catalog_need_hurts_health_and_a_dirty_speaker_weighs_on_the_bond()
    {
        var (world, clock, _) = Sandbox();
        var marina = Marina(world);
        long now = world.CurrentDate.TotalMilliseconds;
        HomeLife.Add(world, marina, "hygiene", -70, now);
        Assert.True(HomeLife.BondPenalty(world, marina, now) < 0);
        int health = marina.Health;
        HomeLife.Add(world, marina, "bladder", -100, now);
        for (int h = 0; h < 3; h++) HomeLife.Hourly(world, marina, now);
        Assert.True(marina.Health < health);
        var hint = HomeLife.Hint(world, marina, now)!;
        Assert.Equal("bladder", hint.Need);
        Assert.Equal("Você está com a bexiga cheia", hint.Alert);
        Assert.Equal("Vaso", hint.Object);
        _ = clock;
    }

    [Fact]
    public void Same_commands_same_world_and_a_snapshot_mid_use_continues_the_same()
    {
        string Play(bool cut)
        {
            var (world, clock, service, body) = Playing(77);
            HomeLife.Add(world, body, "hygiene", -50, world.CurrentDate.TotalMilliseconds);
            var shower = Offer(world, "Chuveiro");
            Assert.True(service.Interact(world, "alan", "object", shower.Id, "use:shower").IsSuccess);
            Assert.True(service.Interact(world, "alan", "object", Offer(world, "Televisão").Id, "queue:watch").IsSuccess);
            Run(world, clock, 6_000);
            if (cut)
            {
                var loaded = WorldSnapshot.Deserialize(WorldSnapshot.Serialize(world));
                Assert.Equal(WorldSnapshot.CanonicalHash(world), WorldSnapshot.CanonicalHash(loaded));
                Assert.Equal(body.BodyTask, loaded.FindNpc(body.Id)!.BodyTask);
                Assert.Equal(body.UseQueue!.Count, loaded.FindNpc(body.Id)!.UseQueue!.Count);
                Assert.Equal(HomeLife.Value(world, body, "hygiene", world.CurrentDate.TotalMilliseconds), HomeLife.Value(loaded, loaded.FindNpc(body.Id)!, "hygiene", loaded.CurrentDate.TotalMilliseconds));
            }

            Run(world, clock, 25 * 60_000);
            return WorldSnapshot.CanonicalHash(world);
        }

        Assert.Equal(Play(false), Play(false));
        Assert.Equal(Play(false), Play(true));
    }

    [Fact]
    public void The_interior_shows_who_uses_a_piece_with_the_object_and_the_catalog_use_so_the_client_picks_pose_and_place()
    {
        var (world, _, _) = Sandbox();
        var rafael = Rafael(world);
        var house = world.Buildings.Single();
        var pieces = HomeFurniture.Of(house, world.ObjectCatalog!);
        int shower = pieces.ToList().FindIndex(p => p.Id == "shower");
        var ctx = new LivingWorld.Simulation.Scheduling.TickContext(world, world.Rng, world.Scheduler, new AllEvents());
        Assert.True(ObjectUsage.Begin(world, ctx, rafael, house.Id.Value, shower, "shower").IsSuccess);

        var snapshot = LivingWorld.Api.Visual.Projection.InteriorProjector.Build(world, house.Id).Value!;

        var occupant = snapshot.Occupants.Single(o => o.Npc == rafael.Id);
        Assert.Equal("shower", occupant.UsingObject);
        Assert.Equal("shower", occupant.UsingUse);
        Assert.Contains(snapshot.Objects!.Catalog.Single(d => d.Id == "shower").Uses!, u => u.Id == "shower" && u.Pose == "shower" && u.PoseFallback == "work");
        Assert.Contains(snapshot.Objects.Catalog.Single(d => d.Id == "shower").Uses![0].PoseFallback!, ObjectPoses.Known); // o fallback é uma pose que o cliente sabe
    }

    /// <summary>Achado do S1: "Chão firme −78%" dentro de casa. O corpo não pode andar mais devagar no piso da casa, na porta ou na soleira do que na rua.</summary>
    [Fact]
    public void The_body_keeps_its_speed_on_the_floor_of_the_house_on_the_door_and_on_the_doorstep()
    {
        var (world, _, _, body) = Playing();
        var house = world.Buildings.Single();
        var (door, step) = PlayerGround.DoorOf(house)!.Value;
        var inside = new CellCoord(door.X + 1, door.Y);
        var street = new CellCoord(step.X - 1, step.Y);
        double walk = world.Players.WalkMps;
        foreach (var (a, b) in new[] { (door, inside), (inside, door), (door, step), (step, door), (step, street), (inside, new CellCoord(inside.X + 1, inside.Y)) })
        {
            double mps = PlayerWalk.SpeedMps(world, body, a, b);
            Assert.True(mps >= walk * 0.75, $"{a} -> {b}: {mps:0.00} m/s contra {walk:0.00}");
            Assert.True(PlayerWalk.DragOf(world, body, a, b).SlowPct < 10, $"{a} -> {b}: o chão atrasa {PlayerWalk.DragOf(world, body, a, b).SlowPct}%");
        }
    }

    /// <summary>A causa do "Chão firme −78%": o rótulo e o passo usavam a célula À FRENTE do rumo, e com o corpo junto da parede essa célula é do outro lado dela (a rota
    /// contornava a casa até a porta). O motor não deixa passar ali: o atraso daquele par não é do corpo.</summary>
    [Fact]
    public void Facing_a_wall_does_not_slow_the_body_the_cell_behind_the_wall_is_not_where_it_walks()
    {
        var (world, _, _, body) = Playing();
        var house = world.Buildings.Single();
        var origin = house.Position!.Value;
        var corner = new CellCoord(origin.X + 5, origin.Y); // o canto de nordeste da casa (6 × 7 células)
        var inside = new CellCoord(origin.X + 4, origin.Y);
        double walk = world.Players.WalkMps;

        foreach (var outside in new[] { new CellCoord(corner.X + 1, corner.Y), new CellCoord(corner.X, corner.Y - 1), new CellCoord(corner.X + 1, corner.Y - 1) }) // para fora, em três rumos
        {
            double wallSpeed = PlayerWalk.SpeedMps(world, body, corner, outside);
            var wallDrag = PlayerWalk.DragOf(world, body, corner, outside);
            Assert.True(wallSpeed >= walk * 0.75, $"{corner} -> {outside}: o passo caiu para {wallSpeed:0.00} m/s de {walk:0.00} ({wallDrag})");
            Assert.True(wallDrag.SlowPct < 10, $"{corner} -> {outside}: o atraso dito ao jogador é {wallDrag.SlowPct}% ({wallDrag.Cause})");
        }

        double toWall = PlayerWalk.SpeedMps(world, body, corner, inside);
        var drag = PlayerWalk.DragOf(world, body, corner, inside);

        Assert.True(toWall >= walk * 0.75, $"junto da parede o passo caiu para {toWall:0.00} m/s de {walk:0.00} ({drag})");
        Assert.True(drag.SlowPct < 10, $"o atraso dito ao jogador é {drag.SlowPct}% ({drag.Cause})");
        _ = inside;
    }

    [Fact]
    public void Snow_and_mud_never_slow_the_step_under_a_roof_over_forty_winter_days()
    {
        var (world, clock, _, body) = Playing();
        var house = world.Buildings.Single();
        var (door, step) = PlayerGround.DoorOf(house)!.Value;
        var inside = new CellCoord(door.X + 1, door.Y);
        double worstSheltered = 1, worstOpen = 1;
        int weatherHours = 0;
        for (int hour = 0; hour < 40 * 24; hour++)
        {
            clock.Tick(world);
            long tick = world.CurrentDate.TotalHours;
            body.SetHunger(100, tick); // corpo em dia: só o tempo pode atrasar o passo
            body.SetThirst(100, tick);
            body.SetSleep(100, tick);
            body.SetHealth(100);
            worstSheltered = Math.Min(worstSheltered, PlayerWalk.SpeedMps(world, body, door, inside) / world.Players.WalkMps);
            long open = LivingWorld.Simulation.Emergence.Mobility.TravelMs(world, body, door, inside, 1.0);
            long roof = LivingWorld.Simulation.Emergence.Mobility.TravelMs(world, body, door, inside, 1.0, sheltered: true);
            if (open > roof) weatherHours++;
            worstOpen = Math.Min(worstOpen, (double)roof / open);
        }

        var drag = PlayerWalk.DragOf(world, body, door, inside);
        Assert.True(worstSheltered > 0.75, $"debaixo de teto o corpo andou a {worstSheltered:P0} do passo (nas horas de tempo ruim: {weatherHours}; ao relento chegaria a {worstOpen:P0}); último atraso: {drag}");
    }

    [Fact]
    public void The_scenario_start_hour_puts_the_world_in_the_morning_and_a_bad_hour_is_refused()
    {
        var (world, _, _) = Sandbox();
        Assert.Equal(7, world.CurrentDate.Hour);
        Assert.Equal(85, Hygiene(world, Marina(world)), 0); // as necessidades nascem na hora de partida

        var (night, _, _) = Sandbox(r => ((JsonObject)r).Remove("StartHour"));
        Assert.Equal(0, night.CurrentDate.Hour);

        var bad = ScenarioLoaderV2.LoadWorld(JsonNode.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "scenarios", "periods", "sandbox-casal.json")))!.AsObject().Also(r => r["StartHour"] = 99).ToJsonString());
        Assert.False(bad.IsSuccess);
        Assert.Contains("StartHour", bad.Error);
    }

    [Fact]
    public void A_world_without_catalog_needs_has_no_extra_state()
    {
        var (arm, npc, _) = LivingWorld.Tests.Unit.Players.PlayerControlTests.Body(1);

        Assert.Null(npc.ExtraNeeds);
        Assert.Null(npc.UseQueue);
        Assert.Null(arm.World.ObjectCatalog?.Needs);
        Assert.Empty(HomeLife.Readings(arm.World, npc, arm.World.CurrentDate.TotalMilliseconds));
    }
}

internal static class JsonObjectExtensions
{
    public static JsonObject Also(this JsonObject obj, Action<JsonObject> edit)
    {
        edit(obj);
        return obj;
    }
}
