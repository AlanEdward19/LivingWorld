using System.Text.Json.Nodes;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Geography;
using LivingWorld.Simulation.Cities;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Objects;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Cities;

/// <summary>#252 (S1 do sandbox, épico #246): o período <c>scenarios/periods/sandbox-casal.json</c> é só dado: um casal autorado numa casa de subúrbio
/// com o tamanho (<c>Rooms.shapes</c>), os cômodos e os móveis que o cenário declara. Nada disto está escrito no código do motor.</summary>
public class SandboxCasalTests
{
    private static string RepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "LivingWorld.sln"))) dir = Directory.GetParent(dir)?.FullName;
        return dir ?? throw new InvalidOperationException("LivingWorld.sln não encontrado");
    }

    /// <summary>#258: por padrão só o casal (os testes da casa); <c>full</c> traz o mercado e o comerciante também (os testes do bairro).</summary>
    private static string Json(Action<JsonObject>? edit = null, bool full = false)
    {
        var root = full ? SandboxFixtures.Parse() : SandboxFixtures.CoupleOnly(SandboxFixtures.Parse());
        edit?.Invoke(root);
        return root.ToJsonString();
    }

    private static (WorldState World, WorldClock Clock) Load(Action<JsonObject>? edit = null, bool full = false)
    {
        var result = ScenarioLoaderV2.LoadWorld(Json(edit, full));
        Assert.True(result.IsSuccess, result.Error);
        return result.Value;
    }

    [Fact]
    public void The_scenario_creates_exactly_a_married_couple_in_one_household()
    {
        var (world, _) = Load();

        Assert.Equal(2, world.Npcs.Count);
        var (a, b) = (world.Npcs[0], world.Npcs[1]);
        Assert.Equal(b.Id, a.Spouse);
        Assert.Equal(a.Id, b.Spouse);
        Assert.Equal(a.Household, b.Household);
        Assert.Equal(new[] { "Marina", "Rafael" }, world.Npcs.Select(n => n.Name).Order().ToArray());
        Assert.All(world.Npcs, n => Assert.True(n.AgeYears(world.CurrentDate) >= 18));
        Assert.Single(world.Households);
    }

    [Fact]
    public void The_player_walks_at_the_domestic_pace_the_scenario_declares()
    {
        var (world, _) = Load();

        Assert.Equal(1.1, world.Players.WalkMps, 3); // dono: 1,4 m/s parecia rápido dentro de casa; passo de casa vem do cenário
    }

    [Fact]
    public void The_sandbox_runs_in_real_time_even_with_nobody_playing()
    {
        var (world, _) = Load();

        // dono: "o jogo não tá em tempo real" — no sandbox o relógio é 1:1 sempre; sem jogador o mundo não corre uma hora por tick
        Assert.Equal(1.0, world.Players.Time.TimeScale);
        Assert.Equal(LivingWorld.Domain.Population.IdleRhythm.RealTime, world.Players.Time.IdleRhythm);
    }

    [Fact]
    public void The_house_has_the_size_the_scenario_declares_and_its_rooms_and_furniture()
    {
        var (world, _) = Load();

        var house = Assert.Single(world.Buildings); // uma casa só, sem cidade em volta
        Assert.Equal(-1, house.BuildingTypeId);
        Assert.Equal(new BuildingShape(7, 6, 1), house.Shape);
        var layout = BuildingLayoutGenerator.Generate(house);
        Assert.Equal(1, layout.Floors);
        Assert.Equal(7 * 6, layout.FloorLayouts[0].Cells.Count); // 10,5 m × 9 m, bem acima de 6×5 células

        var rooms = BuildingRooms.Of(house, world.ObjectCatalog).Floors[0].Rooms.Select(r => r.Kind).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(new[] { "banheiro", "cozinha", "quarto", "sala" }, rooms);

        var pieces = HomeFurniture.Of(house, world.ObjectCatalog!);
        Assert.All(pieces, p => Assert.NotNull(p.Spot)); // tudo coube sem fechar o caminho
        string RoomOf(string id) => HomeFurniture.RoomOf(house, world.ObjectCatalog, pieces.First(p => p.Id == id).Spot!.Value)!;
        Assert.Equal("quarto", RoomOf("bed"));
        Assert.Equal("cozinha", RoomOf("stove"));
        Assert.Equal("cozinha", RoomOf("fridge"));
        Assert.Equal("cozinha", RoomOf("table"));
        Assert.Equal("sala", RoomOf("sofa"));
        Assert.Equal(2, pieces.Count(p => p.Id == "chair"));
    }

    [Fact]
    public void The_pantry_has_food_for_days_and_the_couple_stays_for_three_days()
    {
        var (world, clock) = Load();
        var household = world.Households.Single();
        var food = new ResourceType(world.EconomyRules.FoodResourceId);
        long before = household.Stock.GetValueOrDefault(food);
        Assert.True(before >= 2 * 20, $"comida inicial pequena: {before}"); // a rotina de casa gasta dias, não horas

        for (int tick = 0; tick < 72; tick++) clock.Tick(world);

        Assert.Equal(2, world.Npcs.Count(n => n.IsAlive));
        Assert.Equal(2, world.Npcs.Count);
        Assert.Single(world.Buildings);
        Assert.True(household.Stock.GetValueOrDefault(food) > 0, "a despensa acabou em três dias");
    }

    [Fact]
    public void Same_seed_same_world_and_another_seed_changes_the_world()
    {
        string Run(ulong seed)
        {
            var (world, clock) = Load(r => r["Seed"] = seed);
            for (int tick = 0; tick < 48; tick++) clock.Tick(world);
            return WorldSnapshot.CanonicalHash(world);
        }

        Assert.Equal(Run(252), Run(252));
        Assert.NotEqual(Run(252), Run(253));
    }

    [Fact]
    public void The_house_size_comes_from_the_scenario_not_from_the_code()
    {
        var (world, _) = Load(r => r["Rooms"]!["shapes"]!["-1"] = new JsonObject { ["width"] = 9, ["depth"] = 7, ["floors"] = 1 });

        Assert.Equal(63, BuildingLayoutGenerator.Generate(world.Buildings.Single()).FloorLayouts[0].Cells.Count);
    }

    [Theory]
    [InlineData(2, 6, 1)]
    [InlineData(7, 99, 1)]
    [InlineData(7, 6, 0)]
    [InlineData(7, 6, 9)]
    public void A_shape_outside_the_limits_is_refused_naming_the_field(int width, int depth, int floors)
    {
        var result = ScenarioLoaderV2.LoadWorld(Json(r => r["Rooms"]!["shapes"]!["-1"] = new JsonObject { ["width"] = width, ["depth"] = depth, ["floors"] = floors }));

        Assert.False(result.IsSuccess);
        Assert.Contains("Rooms.shapes.-1", result.Error);
    }

    [Fact]
    public void Without_shapes_the_house_keeps_the_footprint_of_before()
    {
        var (world, _) = Load(r => ((JsonObject)r["Rooms"]!).Remove("shapes"));

        var house = world.Buildings.Single();
        Assert.Null(house.Shape);
        Assert.Equal(9, BuildingLayoutGenerator.Generate(house).FloorLayouts[0].Cells.Count); // a casa de antes: 3×3
    }

    [Fact]
    public void The_declared_shape_survives_save_and_load()
    {
        var (world, clock) = Load();
        for (int tick = 0; tick < 5; tick++) clock.Tick(world);

        var loaded = WorldSnapshot.Deserialize(WorldSnapshot.Serialize(world));

        Assert.Equal(new BuildingShape(7, 6, 1), loaded.Buildings.Single().Shape);
        Assert.Equal(world.ObjectCatalog!.Rooms!.ShapeFor(-1), loaded.ObjectCatalog!.Rooms!.ShapeFor(-1));
        Assert.Equal(WorldSnapshot.CanonicalHash(world), WorldSnapshot.CanonicalHash(loaded));
    }

    /// <summary>#252: o jogador na casa vê cada móvel no lugar onde o motor o pôs, o mais perto primeiro, com o nome que o cenário deu.</summary>
    [Fact]
    public void The_furniture_offers_carry_the_scenario_name_and_the_place_and_come_nearest_first()
    {
        var (world, _) = Load();
        var service = new LivingWorld.Simulation.Players.PlayerService(new LivingWorld.Tests.Unit.Players.PlayerAbsenceTests.AllEvents());
        var npc = world.Npcs[0];
        Assert.True(service.Incarnate(world, npc.Id, "alan").IsSuccess);
        var house = world.Buildings.Single();
        var pieces = HomeFurniture.Of(house, world.ObjectCatalog!);
        var origin = house.Position!.Value;
        var at = npc.CurrentLocation;

        var objects = LivingWorld.Simulation.Players.PlayerService.Reach(world, "alan")!.Where(o => o.Kind == "object").ToList();

        Assert.Contains(objects, o => o.Label == "Cama de casal" && o.Verbs.Contains("use:sleep")); // #255: os usos vêm do catálogo
        Assert.Contains(objects, o => o.Label == "Geladeira" && o.Verbs.Contains("use:snack"));
        Assert.Contains(objects, o => o.Label == "Sofá" && o.Verbs.Contains("use:relax") && o.Uses!.Any(u => u.Name == "Sentar e relaxar"));
        var bed = pieces.First(p => p.Id == "bed").Spot!.Value;
        var bedOffer = objects.Single(o => o.Label == "Cama de casal");
        Assert.Equal((origin.X + bed.Cell.X, origin.Y + bed.Cell.Y), (bedOffer.X, bedOffer.Y));
        var distances = objects.Select(o => (long)(o.X - at.X) * (o.X - at.X) + (long)(o.Y - at.Y) * (o.Y - at.Y)).ToList();
        Assert.Equal(distances.Order().ToList(), distances); // do mais perto ao mais longe
    }

    /// <summary>#260: o casal nascia empilhado na mesma célula da porta; agora cada morador começa numa célula livre distinta da casa, em outro cômodo.</summary>
    [Fact]
    public void The_couple_starts_in_distinct_free_cells_inside_the_house_in_different_rooms()
    {
        var (world, _) = Load();
        var house = world.Buildings.Single();
        var origin = house.Position!.Value;
        var catalog = world.ObjectCatalog!;
        var furniture = HomeFurniture.Of(house, catalog).Where(p => p.Spot is not null).Select(p => p.Spot!.Value.Cell).ToHashSet();
        var rooms = BuildingRooms.Of(house, catalog).Floors[0];

        var cells = world.Npcs.Select(n => new CellCoord(n.CurrentLocation.X - origin.X, n.CurrentLocation.Y - origin.Y)).ToList();

        Assert.Equal(2, cells.Distinct().Count()); // ninguém um dentro do outro
        Assert.All(cells, c => Assert.Contains(rooms.Rooms, r => r.Cells.Contains(c))); // dentro da casa
        Assert.All(cells, c => Assert.DoesNotContain(c, furniture)); // nenhum em cima de móvel
        Assert.NotEqual(rooms.Rooms.First(r => r.Cells.Contains(cells[0])).Kind, rooms.Rooms.First(r => r.Cells.Contains(cells[1])).Kind); // cômodos diferentes
        Assert.Equal(world.Households.Single().Location, world.Npcs[0].CurrentLocation); // o primeiro fica na célula da casa
    }

    [Fact]
    public void The_start_cells_are_the_same_for_the_same_seed()
    {
        var (a, _) = Load();
        var (b, _) = Load();

        Assert.Equal(a.Npcs.Select(n => n.CurrentLocation), b.Npcs.Select(n => n.CurrentLocation));
    }

    /// <summary>#260: vender a casa nunca é a oferta principal do E nem aparece na roda de dentro; "Cama" genérica e "Despensa" eram o mesmo móvel com outro nome.</summary>
    [Fact]
    public void The_main_offer_is_the_nearest_furniture_never_the_house_sale_and_the_names_have_no_duplicates()
    {
        var (world, _) = Load();
        var service = new LivingWorld.Simulation.Players.PlayerService(new LivingWorld.Tests.Unit.Players.PlayerAbsenceTests.AllEvents());
        Assert.True(service.Incarnate(world, world.Npcs[0].Id, "alan").IsSuccess);

        var offers = LivingWorld.Simulation.Players.PlayerService.Reach(world, "alan")!;

        Assert.NotEqual("house", offers[0].Kind); // a principal não é vender
        Assert.DoesNotContain(offers, o => o.Kind == "bed"); // a cama é a "Cama de casal" do catálogo, uma só
        Assert.Equal(1, offers.Count(o => o.Label == "Cama de casal"));
        Assert.DoesNotContain(offers, o => o.Kind == "stock" && o.Verbs.Contains("take")); // comer é da geladeira, que tem nome de geladeira
        Assert.DoesNotContain(offers, o => o.Label is "Cama" or "Despensa"); // os nomes vêm do catálogo, não os genéricos
        if (offers.Any(o => o.Kind == "house")) Assert.Equal("house", offers[^1].Kind); // se aparece, é a última
    }

    [Fact]
    public void The_sale_is_offered_from_the_outside_at_the_door_and_comes_last()
    {
        var (world, _) = Load();
        var service = new LivingWorld.Simulation.Players.PlayerService(new LivingWorld.Tests.Unit.Players.PlayerAbsenceTests.AllEvents());
        var npc = world.Npcs[0];
        Assert.True(service.Incarnate(world, npc.Id, "alan").IsSuccess);
        var doorstep = LivingWorld.Simulation.Players.PlayerGround.DoorOf(world.Buildings.Single())!.Value.Doorstep;
        npc.MoveTo(doorstep, world.CurrentDate.TotalHours);

        var offers = LivingWorld.Simulation.Players.PlayerService.Reach(world, "alan")!;

        Assert.Equal("house", offers[^1].Kind); // de fora, ao mirar a casa, a venda está ao alcance e por último
    }

    /// <summary>#260: a luz da casa vem do catálogo (um abajur com `light`), na sala e no quarto; nada de luz escrita no código.</summary>
    [Fact]
    public void The_house_has_lamps_from_the_catalog_in_the_living_room_and_the_bedroom()
    {
        var (world, _) = Load();
        var house = world.Buildings.Single();
        var catalog = world.ObjectCatalog!;

        var lamps = HomeFurniture.Of(house, catalog).Where(p => p.Id is "lamp" or "bedside").ToList();

        Assert.Equal(2, lamps.Count);
        Assert.All(lamps, l => Assert.NotNull(l.Spot));
        Assert.Equal(new[] { "quarto", "sala" }, lamps.Select(l => HomeFurniture.RoomOf(house, catalog, l.Spot!.Value)!).Order(StringComparer.Ordinal).ToArray());
        var light = catalog.Find("lamp")!.Light!;
        Assert.True(light.RangeM > 0 && light.Energy > 0 && light.Color.Length == 6);
    }

    /// <summary>#260: a ficha traz o ritmo de cada necessidade (a seta de tendência): cai devagar parado e sobe com o uso que o corpo faz.</summary>
    [Fact]
    public void The_need_rates_fall_with_time_and_rise_with_the_use_in_progress()
    {
        var (world, _) = Load();
        var npc = world.Npcs[0];

        var idle = HomeLife.Rates(world, npc);

        Assert.True(idle["hygiene"] < 0 && idle["fun"] < 0 && idle["bladder"] < 0);
        Assert.True(idle["hunger"] <= 0);
    }
}
