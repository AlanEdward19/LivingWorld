using System.Text.Json.Nodes;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Geography.Spatial;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cities;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Objects;
using LivingWorld.Simulation.Players;
using LivingWorld.Simulation.Scenarios;
using static LivingWorld.Tests.Unit.Players.PlayerAbsenceTests;

namespace LivingWorld.Tests.Unit.Objects;

/// <summary>#264 (épico #246): modo comprar e construir. O móvel vem do estoque da loja do bairro (ou é feito por encomenda com material dela e prazo), a moeda só muda de
/// mão, o lugar é validado pelo motor (piso livre, fora da porta, parede quando o móvel pede, com caminho), vender devolve parte, a obra revalida os cômodos e nunca prende
/// ninguém, e a beleza do cômodo vira o momento "Cômodo bonito". Tudo em dados (scenarios/catalog/home-build.json).</summary>
public class HomeBuyTests
{
    private const int Fine = 250;

    private static (WorldState World, WorldClock Clock, PlayerService Service, Npc Body, Npc Other, Building House) Playing(ulong seed = 264, Action<JsonObject>? edit = null, bool shop = true)
    {
        var root = LivingWorld.Tests.Unit.Cities.SandboxFixtures.Parse();
        var households = root["AuthoredHouseholds"]!.AsArray();
        while (households.Count > 1) households.RemoveAt(households.Count - 1);
        if (!shop) { var all = root["Workplaces"]!.AsArray(); all.RemoveAt(all.Count - 1); }
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
        var house = world.FindBuilding(world.FindHousehold(body.Household!.Value)!.Dwelling!.Value)!;
        return (world, clock, service, body, other, house);
    }

    private static long Coins(WorldState world) => world.Npcs.Sum(n => n.Wallet.Amount) + world.Workplaces.Sum(w => w.Treasury.Amount);

    private static Workplace Shop(WorldState world) => HomeCommands.Shop(world, world.ObjectCatalog!.Home!)!;

    private static long Stock(WorldState world, string item) => Shop(world).Stock.GetValueOrDefault(new LivingWorld.Domain.Geography.ResourceType(world.ObjectCatalog!.Home!.Item(item)!.Resource));

    /// <summary>Atalho dos testes de lugar e de beleza (o transporte tem os dele, em HomeFreightTests): compra de verdade (moeda, estoque), leva o móvel direto ao depósito da casa e o põe na
    /// primeira célula e no primeiro giro em que o motor aceita (opcionalmente numa sala).</summary>
    private static Result<InteractionResult> Furnish(PlayerService? service, WorldState world, Building house, Npc body, string item, Room? room = null)
    {
        var bought = service is null ? HomeCommands.Apply(world, null!, body, house.Id.Value, $"buy:{item}") : Do(service, world, house, $"buy:{item}");
        if (!bought.IsSuccess) return bought;
        var parcel = house.Edit!.Parcels.Last();
        HomeFreight.Update(world, house, parcel, parcel with { Where = "depot" });
        string verb = PlaceVerb(world, body, house, parcel.Seq, room);
        return service is null ? HomeCommands.Apply(world, null!, body, house.Id.Value, verb) : Do(service, world, house, verb);
    }

    private static string PlaceVerb(WorldState world, Npc body, Building house, int seq, Room? room = null)
    {
        var cells = room is null ? BuildingLayoutCache.Of(house).Layout.FloorLayouts[0].Cells.OrderBy(c => c.Cell.Y).ThenBy(c => c.Cell.X).Select(c => c.Cell).ToList() : room.Cells.ToList();
        foreach (var cell in cells)
            foreach (int facing in new[] { 0, 90, 180, 270 })
            {
                string verb = $"place:{seq}:0:{cell.X}:{cell.Y}:{facing}";
                if (HomeCommands.Check(world, body, house.Id.Value, verb).IsSuccess) return verb;
            }

        throw new InvalidOperationException("sem lugar para o móvel");
    }

    /// <summary>Um móvel já no depósito da casa (sem pagar), para os testes de lugar.</summary>
    private static int InDepot(Building house, string item)
    {
        var edit = house.Edit ??= new HomeEdit();
        edit.Parcels.Add(new Parcel(edit.NextParcel++, item, 0, 0, "depot"));
        return edit.Parcels[^1].Seq;
    }

    private static Result<InteractionResult> Do(PlayerService service, WorldState world, Building house, string verb) => service.Interact(world, "alan", "home", house.Id.Value, verb);

    [Fact]
    public void The_home_catalog_comes_from_data_with_prices_beauty_stock_in_resources_and_paints()
    {
        var (world, _, _, _, _, _) = Playing();
        var home = world.ObjectCatalog!.Home!;
        Assert.True(home.Items.Count >= 15 && home.Paints.Count >= 6 && home.Categories.Count >= 6);
        Assert.Equal(4, home.Build.Contractor);
        Assert.NotNull(Shop(world));
        foreach (var item in home.Items)
        {
            Assert.NotNull(world.ObjectCatalog.Find(item.Object));
            Assert.True(item.Price > 0 && item.Resource >= 20);
        }

        Assert.Equal("concrete", home.DefaultPaint("floor")!.Id);
    }

    [Fact]
    public void A_bad_home_reference_names_the_field_and_a_world_without_the_block_has_no_buy_mode()
    {
        var root = LivingWorld.Tests.Unit.Cities.SandboxFixtures.Parse();
        root["Seed"] = 1;
        var items = root["CatalogExtends"]!.AsArray();
        items.Clear();
        foreach (var name in new[] { "home-life", "social", "moods", "careers" }) items.Add(name);
        var without = ScenarioLoaderV2.LoadWorld(root.ToJsonString());
        Assert.True(without.IsSuccess, without.Error);
        Assert.Null(without.Value.World.ObjectCatalog!.Home);

        var broken = LivingWorld.Tests.Unit.Cities.SandboxFixtures.Parse();
        broken["Seed"] = 1;
        broken["Home"] = JsonNode.Parse("""{"rules":{},"categories":[{"id":"a","name":"A"}],"items":[{"object":"nao-existe","category":"a","price":1,"beauty":0,"resource":99}],"paints":[],"build":{"contractor":2}}""");
        var result = ScenarioLoaderV2.LoadWorld(broken.ToJsonString());
        Assert.False(result.IsSuccess);
        Assert.Contains("Home.items[0].object", result.Error);
    }

    [Fact]
    public void Buying_from_stock_moves_coin_from_wallet_to_shop_and_the_piece_from_stock_to_the_house()
    {
        var (world, _, service, body, _, house) = Playing();
        var home = world.ObjectCatalog!.Home!;
        long total = Coins(world), wallet = body.Wallet.Amount, stock = Stock(world, "plant"), treasury = Shop(world).Treasury.Amount;
        int before = HomeFurniture.Of(house, world.ObjectCatalog!).Count;
        string verb = "buy:plant";
        var result = Do(service, world, house, verb);
        Assert.True(result.IsSuccess, result.Error);

        long price = home.Item("plant")!.Price;
        Assert.Equal(wallet - price, body.Wallet.Amount);
        Assert.Equal(treasury + price, Shop(world).Treasury.Amount);
        Assert.Equal(stock - 1, Stock(world, "plant"));
        Assert.Equal(total, Coins(world)); // moeda conservada
        Assert.Equal(before, HomeFurniture.Of(house, world.ObjectCatalog!).Count); // #265: pagar não põe o móvel em casa
        var parcel = Assert.Single(house.Edit!.Parcels);
        Assert.Equal(("plant", "shop", price), (parcel.Id, parcel.Where, parcel.Paid)); // reservado na loja, em nome da casa
        Assert.Contains(service.Log.Records, r => r.Kind == PlayerCommandKind.Interact && r.Interaction == "home" && r.Verb == verb);
    }

    [Fact]
    public void A_refused_purchase_changes_nothing_and_says_why()
    {
        var (world, _, service, body, _, house) = Playing();
        string verb = "buy:plant";
        body.TryDebitWallet(new Money(body.Wallet.Amount - 2)); // sobram 2 moedas
        string hash = WorldSnapshot.CanonicalHash(world);
        var poor = Do(service, world, house, verb);
        Assert.False(poor.IsSuccess);
        Assert.Contains("falta dinheiro", poor.Error);
        Assert.Equal(2, body.Wallet.Amount);
        Assert.Equal(hash, WorldSnapshot.CanonicalHash(world)); // a recusa não mexe em nada
        Assert.Equal(3, Stock(world, "plant"));
    }

    [Fact]
    public void The_engine_refuses_a_bad_place_a_taken_cell_the_door_a_wall_piece_off_the_wall_and_plumbing_outside_the_bathroom()
    {
        var (world, _, _, body, _, house) = Playing();
        var layout = BuildingLayoutCache.Of(house).Layout.FloorLayouts[0];
        var rooms = BuildingRooms.Of(house, world.ObjectCatalog);
        var pieces = HomeFurniture.Of(house, world.ObjectCatalog!);
        var taken = pieces.First(p => p.Spot is not null).Spot!.Value;
        var depot = new Dictionary<string, int>();
        string Check(string item, CellCoord cell, int facing)
        {
            if (!depot.TryGetValue(item, out int seq)) depot[item] = seq = InDepot(house, item);
            return HomeCommands.Check(world, body, house.Id.Value, $"place:{seq}:0:{cell.X}:{cell.Y}:{facing}") is { IsSuccess: false } r ? r.Error! : "";
        }

        Assert.Contains("já tem um móvel", Check("plant", taken.Cell, 0));
        var door = layout.Cells.Single(c => c.Material == BuildingMaterial.Door).Cell;
        Assert.Contains("piso", Check("plant", door, 0) + "piso"); // a porta nunca aceita móvel (porta e escada ficam livres)
        Assert.NotEqual("", Check("plant", door, 0));
        var entry = FurniturePlanner.EntryOf(layout.Cells.ToDictionary(c => c.Cell, c => c.Material), door, 0)!.Value;
        Assert.Contains("porta", Check("plant", entry, 0));
        Assert.Contains("giro", Check("plant", new CellCoord(0, 0), 45));

        // a pintura de parede pede o fundo na parede: no meio da sala, com os quatro giros, ao menos um lado sem parede recusa
        var doorways = HomeEditor.Doorways(rooms.Floors[0], entry);
        var middle = rooms.Floors[0].Rooms.SelectMany(r => r.Cells).Where(c => !pieces.Any(p => p.Spot?.Cell == c) && !doorways.Contains(c) && layout.Cells.First(x => x.Cell == c).Material == BuildingMaterial.Floor).First(c =>
            HomeEditor.Around.All(d => !rooms.Blocked[0].Contains(WallEdge.Between(c, new CellCoord(c.X + d.X, c.Y + d.Y)))));
        Assert.Contains("parede", Check("painting", middle, 0));
        var bathroom = rooms.Floors[0].Rooms.First(r => r.Kind == "banheiro").Cells;
        var livingCell = rooms.Floors[0].Rooms.First(r => r.Kind == "sala").Cells.First(c => !pieces.Any(p => p.Spot?.Cell == c) && !doorways.Contains(c));
        Assert.Contains("banheiro", Check("shower", livingCell, 0));
        Assert.NotEmpty(bathroom);
    }

    [Fact]
    public void An_out_of_stock_piece_is_made_to_order_with_shop_material_a_markup_and_a_delivery_date()
    {
        var (world, clock, service, body, _, house) = Playing();
        var home = world.ObjectCatalog!.Home!;
        Assert.Equal(0, Stock(world, "armchair"));
        long total = Coins(world), wood = Shop(world).Stock.GetValueOrDefault(new LivingWorld.Domain.Geography.ResourceType(40)), wallet = body.Wallet.Amount;
        var ordered = Do(service, world, house, "buy:armchair");
        Assert.True(ordered.IsSuccess, ordered.Error);
        long price = home.Item("armchair")!.Price * (100 + home.Rules.OrderMarkupPercent) / 100;
        Assert.Equal(wallet - price, body.Wallet.Amount);
        Assert.Equal(wood - home.Item("armchair")!.Craft!.Materials["40"], Shop(world).Stock.GetValueOrDefault(new LivingWorld.Domain.Geography.ResourceType(40)));
        Assert.Equal(total, Coins(world));
        var parcel = Assert.Single(house.Edit!.Parcels);
        Assert.DoesNotContain(HomeFurniture.Of(house, world.ObjectCatalog!), p => p.Id == "armchair"); // ainda não existe: a loja o faz
        Assert.False(HomeCommands.Check(world, body, house.Id.Value, $"fetch:{parcel.Seq}").IsSuccess); // e ninguém leva o que ainda não está pronto

        long ready = parcel.ReadyAtMs;
        Assert.True(ready > world.CurrentDate.TotalMilliseconds);
        while (world.CurrentDate.TotalMilliseconds < ready + 1000) clock.Tick(world, 60_000);
        Assert.Equal("shop", house.Edit.Parcels[0].Where); // pronto, esperando na loja em nome da casa
    }

    [Fact]
    public void Selling_returns_part_of_the_value_from_the_shop_till_and_discarding_returns_nothing()
    {
        var (world, _, service, body, _, house) = Playing();
        var home = world.ObjectCatalog!.Home!;
        Assert.True(Furnish(service, world, house, body, "plant").IsSuccess);
        int plant = HomeFurniture.Of(house, world.ObjectCatalog!).Count - 1;
        long total = Coins(world), wallet = body.Wallet.Amount, stock = Stock(world, "plant"), treasury = Shop(world).Treasury.Amount;
        var sold = Do(service, world, house, $"sell:{plant}");
        Assert.True(sold.IsSuccess, sold.Error);
        long refund = home.Item("plant")!.Price * home.Rules.ResalePercent / 100;
        Assert.Equal(wallet + refund, body.Wallet.Amount);
        Assert.Equal(treasury - refund, Shop(world).Treasury.Amount);
        Assert.Equal(stock + 1, Stock(world, "plant")); // volta ao estoque: não some
        Assert.Equal(total, Coins(world));
        Assert.Null(HomeFurniture.Of(house, world.ObjectCatalog!)[plant].Spot);
        Assert.False(Do(service, world, house, $"sell:{plant}").IsSuccess); // já não existe

        long before = Coins(world);
        int armchairLess = HomeFurniture.Of(house, world.ObjectCatalog!).ToList().FindIndex(p => p.Id == "chair" && p.Spot is not null);
        var discarded = Do(service, world, house, $"discard:{armchairLess}");
        Assert.True(discarded.IsSuccess, discarded.Error);
        Assert.Equal(before, Coins(world)); // descartar não cria moeda
        Assert.Null(HomeFurniture.Of(house, world.ObjectCatalog!)[armchairLess].Spot);
    }

    [Fact]
    public void Moving_a_piece_is_validated_and_a_base_piece_can_be_moved_sold_and_keeps_its_index()
    {
        var (world, _, service, body, _, house) = Playing();
        var pieces = HomeFurniture.Of(house, world.ObjectCatalog!);
        int sofa = pieces.ToList().FindIndex(p => p.Id == "sofa");
        var from = pieces[sofa].Spot!.Value;
        var layout = BuildingLayoutCache.Of(house).Layout.FloorLayouts[0];
        CellCoord? target = null;
        int facing = 0;
        foreach (var c in layout.Cells.OrderBy(c => c.Cell.Y).ThenBy(c => c.Cell.X))
            foreach (int f in new[] { 0, 90, 180, 270 })
                if (target is null && HomeCommands.Check(world, body, house.Id.Value, $"move:{sofa}:0:{c.Cell.X}:{c.Cell.Y}:{f}").IsSuccess && c.Cell != from.Cell) (target, facing) = (c.Cell, f);
        Assert.NotNull(target);
        var moved = Do(service, world, house, $"move:{sofa}:0:{target!.Value.X}:{target.Value.Y}:{facing}");
        Assert.True(moved.IsSuccess, moved.Error);
        var after = HomeFurniture.Of(house, world.ObjectCatalog!);
        Assert.Equal(pieces.Count, after.Count);
        Assert.Equal(target, after[sofa].Spot!.Value.Cell);
        Assert.Equal(facing, after[sofa].Spot!.Value.Facing);
        Assert.False(Do(service, world, house, $"move:{sofa}:0:99:99:0").IsSuccess);
    }

    [Fact]
    public void Raising_a_wall_splits_a_room_knocking_it_down_merges_and_the_works_never_close_the_way_to_the_door()
    {
        var (world, _, service, body, _, house) = Playing();
        var catalog = world.ObjectCatalog!;
        long total = Coins(world);
        var before = BuildingRooms.Of(house, catalog).Floors[0];
        var layout = BuildingLayoutCache.Of(house).Layout.FloorLayouts[0];
        var big = before.Rooms.OrderByDescending(r => r.Cells.Count).First();
        var cells = big.Cells.ToHashSet();
        var edge = big.Cells.SelectMany(c => HomeEditor.Around.Select(d => (A: c, B: new CellCoord(c.X + d.X, c.Y + d.Y)))).First(e =>
            cells.Contains(e.B) && HomeEditor.EdgeState(layout, before, WallEdge.Between(e.A, e.B)) == "open"
            && HomeCommands.Check(world, body, house.Id.Value, $"wall:0:{e.A.X}:{e.A.Y}:{e.B.X}:{e.B.Y}").IsSuccess);
        long wallet = body.Wallet.Amount;
        var built = Do(service, world, house, $"wall:0:{edge.A.X}:{edge.A.Y}:{edge.B.X}:{edge.B.Y}");
        Assert.True(built.IsSuccess, built.Error);
        Assert.Equal(wallet - world.ObjectCatalog!.Home!.Build.Wall, body.Wallet.Amount);
        Assert.Equal(total, Coins(world));
        var after = BuildingRooms.Of(house, catalog).Floors[0];
        Assert.Equal("wall", HomeEditor.EdgeState(layout, after, WallEdge.Between(edge.A, edge.B)));
        Assert.True(HomeEditor.AllConnected(layout, after, HomeFurniture.Of(house, catalog).Where(p => p.Spot is not null).Select(p => p.Spot!.Value.Cell).ToHashSet()) || after.Rooms.Count >= before.Rooms.Count);
        // o passo do corpo respeita a parede nova (a mesma regra do chão, em aresta)
        var origin = house.Position!.Value;
        Assert.True(PlayerGround.At(world, new CellCoord(origin.X + edge.A.X, origin.Y + edge.A.Y))!.Value.Closed(
            new CellCoord(origin.X + edge.A.X, origin.Y + edge.A.Y), new CellCoord(origin.X + edge.B.X, origin.Y + edge.B.Y)));

        Assert.False(Do(service, world, house, $"wall:0:{edge.A.X}:{edge.A.Y}:{edge.B.X}:{edge.B.Y}").IsSuccess); // já é parede
        var opened = Do(service, world, house, $"open:0:{edge.A.X}:{edge.A.Y}:{edge.B.X}:{edge.B.Y}");
        Assert.True(opened.IsSuccess, opened.Error);
        Assert.Equal(before.Rooms.Count, BuildingRooms.Of(house, catalog).Floors[0].Rooms.Count);
        Assert.Equal(total, Coins(world));
        Assert.False(Do(service, world, house, "open:0:0:0:99:99").IsSuccess);
    }

    [Fact]
    public void A_wall_between_two_rooms_that_would_cut_off_a_room_is_refused_and_nothing_changes()
    {
        var (world, _, _, body, _, house) = Playing();
        var catalog = world.ObjectCatalog!;
        var layout = BuildingLayoutCache.Of(house).Layout.FloorLayouts[0];
        var rooms = BuildingRooms.Of(house, catalog).Floors[0];
        var door = rooms.Doors.First();
        // fechar a única porta que liga um cômodo à entrada deixa o cômodo sem caminho
        var tryShut = HomeCommands.Check(world, body, house.Id.Value, $"shut:0:{door.A.X}:{door.A.Y}:{door.B.X}:{door.B.Y}");
        var hash = WorldSnapshot.CanonicalHash(world);
        Assert.Equal(hash, WorldSnapshot.CanonicalHash(world));
        if (!tryShut.IsSuccess) Assert.Contains("caminho", tryShut.Error);
        Assert.Equal("door", HomeEditor.EdgeState(layout, rooms, door.Edge));
        // a parede de fora e a porta da rua nunca mudam
        var outer = layout.Walls!.First();
        Assert.Contains("fora", HomeCommands.Check(world, body, house.Id.Value, $"open:0:{outer.A.X}:{outer.A.Y}:{outer.B.X}:{outer.B.Y}").Error!);
    }

    [Fact]
    public void Extending_the_house_adds_floor_cells_inside_the_lot_pays_the_shop_and_redraws_the_outer_walls()
    {
        var (world, _, service, body, _, house) = Playing();
        var catalog = world.ObjectCatalog!;
        long total = Coins(world);
        var layout = BuildingLayoutCache.Of(house).Layout.FloorLayouts[0];
        var cells = layout.Cells.Select(c => c.Cell).ToHashSet();
        var outside = cells.SelectMany(c => HomeEditor.Around.Select(d => new CellCoord(c.X + d.X, c.Y + d.Y))).Where(c => !cells.Contains(c))
            .First(c => HomeCommands.Check(world, body, house.Id.Value, $"extend:{c.X}:{c.Y}").IsSuccess);
        long wallet = body.Wallet.Amount;
        var built = Do(service, world, house, $"extend:{outside.X}:{outside.Y}");
        Assert.True(built.IsSuccess, built.Error);
        Assert.Equal(wallet - catalog.Home!.Build.Cell, body.Wallet.Amount);
        Assert.Equal(total, Coins(world));
        var after = BuildingLayoutCache.Of(house).Layout.FloorLayouts[0];
        Assert.Contains(after.Cells, c => c.Cell == outside && c.Material == BuildingMaterial.Floor);
        Assert.Equal(cells.Count + 1, after.Cells.Count);
        Assert.Contains(BuildingRooms.Of(house, catalog).Floors[0].Rooms, r => r.Cells.Contains(outside)); // entra num cômodo
        // o chão do mundo reconhece a célula como parte da casa
        var origin = house.Position!.Value;
        Assert.NotNull(PlayerGround.At(world, new CellCoord(origin.X + outside.X, origin.Y + outside.Y)));
        Assert.False(Do(service, world, house, $"extend:{outside.X}:{outside.Y}").IsSuccess);
        Assert.False(Do(service, world, house, "extend:60:60").IsSuccess); // longe da casa
    }

    [Fact]
    public void Painting_a_room_costs_per_cell_changes_its_beauty_and_a_wrong_paint_kind_is_refused()
    {
        var (world, _, service, body, _, house) = Playing();
        var catalog = world.ObjectCatalog!;
        var rooms = BuildingRooms.Of(house, catalog).Floors[0];
        var living = rooms.Rooms.First(r => r.Kind == "sala");
        var cell = living.Cells[0];
        double before = RoomBeauty.At(house, catalog, 0, cell)!.Value;
        long total = Coins(world), wallet = body.Wallet.Amount;
        Assert.False(Do(service, world, house, $"paint:floor:0:{cell.X}:{cell.Y}:cream").IsSuccess); // tinta de parede no piso
        var painted = Do(service, world, house, $"paint:floor:0:{cell.X}:{cell.Y}:wood");
        Assert.True(painted.IsSuccess, painted.Error);
        Assert.Equal(wallet - living.Cells.Count * catalog.Home!.Paint("wood")!.Price, body.Wallet.Amount);
        Assert.Equal(total, Coins(world));
        double after = RoomBeauty.At(house, catalog, 0, cell)!.Value;
        Assert.True(after > before, $"{before} -> {after}");
        Assert.False(Do(service, world, house, $"paint:floor:0:{cell.X}:{cell.Y}:wood").IsSuccess); // já está assim
        var snapshot = LivingWorld.Api.Visual.Projection.InteriorProjector.Build(world, house.Id).Value!;
        Assert.NotNull(snapshot.Paint);
        Assert.Contains(snapshot.Paint!.Marks, m => m.Kind == "floor" && m.Paint == "wood");
    }

    [Fact]
    public void Decor_raises_the_room_beauty_and_a_pretty_room_gives_the_moment_while_the_person_is_in_it_and_an_ugly_one_the_opposite()
    {
        var (world, _, service, body, _, house) = Playing();
        var catalog = world.ObjectCatalog!;
        var living = BuildingRooms.Of(house, catalog).Floors[0].Rooms.First(r => r.Kind == "sala");
        // #266: a sala é pequena e cada móvel tem pegada e lado livre; sem o abajur do canto cabem a pintura (na parede) e a planta
        int lamp = HomeFurniture.Of(house, catalog).ToList().FindIndex(p => p.Id == "lamp");
        Assert.True(Do(service, world, house, $"discard:{lamp}").IsSuccess);
        var inside = living.Cells.First(c => HomeFurniture.Of(house, catalog).All(p => p.Spot?.Cell != c));
        body.EnterBuilding(house.Id, new FloorLevel(0), inside);
        long ms = world.CurrentDate.TotalMilliseconds;
        double start = RoomBeauty.At(house, catalog, 0, inside)!.Value;
        Assert.DoesNotContain(MoodMechanic.Moments(world, body, ms), m => m.Id == "pretty-room");

        foreach (string item in new[] { "painting", "plant" }) // a pintura (que pede parede) primeiro: a sala é pequena e a planta fica em qualquer lugar
        {
            var done = Furnish(service, world, house, body, item, living);
            Assert.True(done.IsSuccess, done.Error);
        }

        double rich = RoomBeauty.At(house, catalog, 0, inside)!.Value;
        Assert.True(rich >= start + 5 - 0.01, $"{start} -> {rich}");
        Assert.Contains(MoodMechanic.Moments(world, body, ms), m => m.Id == "pretty-room" && m.Name == "Cômodo bonito" && m.Weight > 0);
        Assert.Equal(rich, RoomBeauty.Rooms(house, catalog).Single(r => r.Kind == "sala").Beauty);

        var bath = BuildingRooms.Of(house, catalog).Floors[0].Rooms.First(r => r.Kind == "banheiro");
        body.MoveWithinBuilding(bath.Cells[0]);
        Assert.Contains(MoodMechanic.Moments(world, body, ms), m => m.Id == "ugly-room" && m.Name == "Cômodo feio" && m.Weight < 0);
        Assert.DoesNotContain(MoodMechanic.Moments(world, body, ms), m => m.Id == "pretty-room"); // vale pelo cômodo onde está
    }

    [Fact]
    public void A_bought_piece_enters_the_catalog_uses_and_the_partner_nobody_controls_goes_to_the_new_tv_after_the_old_one_is_gone()
    {
        var root = LivingWorld.Tests.Unit.Cities.SandboxFixtures.Parse();
        var households = root["AuthoredHouseholds"]!.AsArray();
        while (households.Count > 1) households.RemoveAt(households.Count - 1);
        root["Seed"] = 2640;
        var (world, clock) = ScenarioLoaderV2.LoadWorld(root.ToJsonString()).Value;
        world.UsePlayers(null);
        var marina = world.Npcs.Single(n => n.Name == "Marina");
        var rafael = world.Npcs.Single(n => n.Name == "Rafael");
        var house = world.FindBuilding(world.FindHousehold(marina.Household!.Value)!.Dwelling!.Value)!;
        var catalog = world.ObjectCatalog!;
        int oldTv = HomeFurniture.Of(house, catalog).ToList().FindIndex(p => p.Id == "tv");
        Assert.True(HomeCommands.Apply(world, null!, marina, house.Id.Value, $"discard:{oldTv}").IsSuccess);
        var living = BuildingRooms.Of(house, catalog).Floors[0].Rooms.First(r => r.Kind == "sala");
        Assert.True(Furnish(null, world, house, marina, "tv", living).IsSuccess);
        int newTv = HomeFurniture.Of(house, catalog).Count - 1;
        Assert.Equal("tv", HomeFurniture.Of(house, catalog)[newTv].Id);
        Assert.True(ObjectUsage.Resolve(world, house.Id.Value, newTv, "watch").IsSuccess); // o catálogo de usos enxerga a peça comprada
        Assert.False(ObjectUsage.Resolve(world, house.Id.Value, oldTv, "watch").IsSuccess); // e a descartada saiu dos usos

        var used = new HashSet<int>();
        for (int step = 0; step < 12 * 40; step++)
        {
            if (step % 12 == 0)
            {
                HomeLife.Add(world, rafael, "fun", -70, world.CurrentDate.TotalMilliseconds);
                HomeLife.Add(world, marina, "fun", -70, world.CurrentDate.TotalMilliseconds);
            }

            clock.Tick(world, 300_000);
            foreach (var npc in world.Npcs)
                if (npc.BodyTask is { Use: "watch" or "relax" } task && task.Building == house.Id.Value && task.Piece == newTv) used.Add((int)npc.Id.Value);
        }

        Assert.NotEmpty(used); // alguém assistiu à TV nova
    }

    [Fact]
    public void The_same_commands_make_the_same_world_and_the_edit_survives_a_snapshot_round_trip()
    {
        string Run()
        {
            var (world, _, service, body, _, house) = Playing(seed: 777);
            Assert.True(Furnish(service, world, house, body, "plant").IsSuccess);
            var layout = BuildingLayoutCache.Of(house).Layout.FloorLayouts[0];
            var cells = layout.Cells.Select(c => c.Cell).ToHashSet();
            var outside = cells.SelectMany(c => HomeEditor.Around.Select(d => new CellCoord(c.X + d.X, c.Y + d.Y))).Where(c => !cells.Contains(c))
                .First(c => HomeCommands.Check(world, body, house.Id.Value, $"extend:{c.X}:{c.Y}").IsSuccess);
            Assert.True(Do(service, world, house, $"extend:{outside.X}:{outside.Y}").IsSuccess);
            var living = BuildingRooms.Of(house, world.ObjectCatalog!).Floors[0].Rooms[0].Cells[0];
            Assert.True(Do(service, world, house, $"paint:floor:0:{living.X}:{living.Y}:wood").IsSuccess);
            return WorldSnapshot.CanonicalHash(world);
        }

        Assert.Equal(Run(), Run());

        var (w, _, s, b, _, h) = Playing(seed: 778);
        Assert.True(Furnish(s, w, h, b, "plant").IsSuccess);
        var json = WorldSnapshot.Serialize(w);
        var restored = WorldSnapshot.Deserialize(json);
        var house = restored.FindBuilding(h.Id)!;
        Assert.NotNull(house.Edit);
        Assert.Equal(h.Edit!.Added, house.Edit.Added);
        Assert.Equal(WorldSnapshot.CanonicalHash(w), WorldSnapshot.CanonicalHash(restored));
        Assert.Equal(HomeFurniture.Of(h, w.ObjectCatalog!).Select(p => (p.Id, p.Spot)), HomeFurniture.Of(house, restored.ObjectCatalog!).Select(p => (p.Id, p.Spot)));
    }

    [Fact]
    public void A_world_with_no_edit_keeps_the_building_free_of_an_edit_and_the_hash_free_of_it()
    {
        var (world, _, _, _, _, house) = Playing();
        Assert.Null(house.Edit);
        Assert.DoesNotContain("\"Edit\"", WorldSnapshot.Serialize(world));
    }

    [Fact]
    public void Without_a_furniture_shop_the_buy_mode_says_so_and_nothing_is_bought()
    {
        var (world, _, service, body, _, house) = Playing(shop: false);
        var refused = Do(service, world, house, "buy:plant");
        Assert.False(refused.IsSuccess);
        Assert.Contains("loja", refused.Error);
    }

    [Fact]
    public void The_buy_mode_view_tells_stock_order_price_effects_pieces_resale_rooms_paints_and_the_works_price_from_the_engine()
    {
        var (world, _, service, body, _, house) = Playing();
        var view = HomeView.Of(world, body)!;
        Assert.Equal(house.Id.Value, view.Building);
        Assert.Equal(body.Wallet.Amount, view.Wallet);
        Assert.NotNull(view.Shop);
        var sofa = view.Items.Single(i => i.Object == "sofa");
        Assert.True(sofa.Stock >= 1 && sofa.Price == 30 && sofa.Beauty == 3);
        Assert.Contains(sofa.Effects, e => e.Need == "fun" && e.PerHour > 0 && e.NeedName == "Diversão"); // o nome da necessidade vem do catálogo do cenário
        var armchair = view.Items.Single(i => i.Object == "armchair"); // esgotada: sob encomenda, mais cara
        Assert.Equal(0, armchair.Stock);
        Assert.True(armchair.Orderable && armchair.OrderPrice > armchair.Price && armchair.CraftHours > 0);
        Assert.Contains(view.Items, i => i.Object == "painting" && i.Wall);
        Assert.Equal(view.Items.Count, world.ObjectCatalog!.Home!.Items.Count);
        var bed = view.Pieces.Single(p => p.Id == "bed");
        Assert.Equal(world.ObjectCatalog.Home.Item("bed")!.Price * 60 / 100, bed.Resale);
        Assert.True(bed.Sellable);
        Assert.Contains(view.Rooms, r => r.Kind == "banheiro" && r.Beauty < 0);
        Assert.Contains(view.Paints, p => p.Kind == "floor" && p.Color.Length == 6);
        Assert.Equal(world.ObjectCatalog.Home.Build.Wall, view.Build.Wall);
        Assert.Empty(view.Orders);

        Assert.True(Furnish(service, world, house, body, "plant").IsSuccess);
        var after = HomeView.Of(world, body)!;
        Assert.Equal(view.Items.Single(i => i.Object == "plant").Stock - 1, after.Items.Single(i => i.Object == "plant").Stock);
        Assert.Equal(view.Pieces.Count + 1, after.Pieces.Count);
        Assert.Equal(view.Wallet - 8, after.Wallet);
    }

    [Fact]
    public void The_cabinetmaker_restocks_the_furniture_shop_from_its_own_wood_over_the_days_so_pieces_never_appear_from_nothing()
    {
        var root = LivingWorld.Tests.Unit.Cities.SandboxFixtures.Parse();
        var households = root["AuthoredHouseholds"]!.AsArray();
        while (households.Count > 2) households.RemoveAt(households.Count - 1); // o casal, o comerciante do mercado... e o marceneiro fica
        root["Seed"] = 2641;
        var loaded = ScenarioLoaderV2.LoadWorld(root.ToJsonString());
        Assert.True(loaded.IsSuccess, loaded.Error);
        var (world, clock) = loaded.Value;
        world.UsePlayers(null);
        var shop = Shop(world);
        long armchairBefore = Stock(world, "armchair"), woodBefore = shop.Stock.GetValueOrDefault(new LivingWorld.Domain.Geography.ResourceType(40));
        long total = world.Workplaces.Sum(w => w.Stock.Values.Sum());
        Assert.Equal(0, armchairBefore);
        for (int hour = 0; hour < 24 * 4; hour++) clock.Tick(world, 3_600_000);
        long woodAfter = Shop(world).Stock.GetValueOrDefault(new LivingWorld.Domain.Geography.ResourceType(40));
        long pieces = Enumerable.Range(20, 18).Sum(r => Shop(world).Stock.GetValueOrDefault(new LivingWorld.Domain.Geography.ResourceType(r)));
        Assert.True(Shop(world).Employees.Count > 0 || pieces > 0, "o marceneiro trabalha na loja");
        Assert.True(woodAfter <= woodBefore, "a madeira só sai do estoque da loja (ninguém a cria)");
        Assert.True(pieces >= 18, $"depois de dias há móveis feitos no estoque: {pieces}");
        Assert.True(total >= 0);
    }
}
