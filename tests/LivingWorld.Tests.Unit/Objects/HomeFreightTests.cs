using System.Text.Json.Nodes;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Objects;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cities;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Objects;
using LivingWorld.Simulation.Players;
using LivingWorld.Simulation.Scenarios;
using static LivingWorld.Tests.Unit.Players.PlayerAbsenceTests;

namespace LivingWorld.Tests.Unit.Objects;

/// <summary>#265 (épico #246): o móvel pago não aparece em casa. Fica na loja em nome da casa e vem nas mãos (mais devagar, só o que cabe), de carro (porta-malas com capacidade) ou por entrega
/// (um empregado da loja, no horário, a pé até a porta); em casa vai para o depósito e de lá para o lugar. Tudo em dado (<c>Home.haul</c>), moeda conservada, determinístico.</summary>
public class HomeFreightTests
{
    private const int Fine = 250;

    private static (WorldState World, WorldClock Clock, PlayerService Service, Npc Body, Npc Partner, Building House) Playing(ulong seed = 265, bool maker = true, Action<JsonObject>? edit = null)
    {
        var root = LivingWorld.Tests.Unit.Cities.SandboxFixtures.Parse();
        var households = root["AuthoredHouseholds"]!.AsArray();
        households.RemoveAt(1); // sem o dono do mercado; ficam o casal e o marceneiro (quem entrega)
        if (!maker) households.RemoveAt(households.Count - 1);
        root["Seed"] = seed;
        edit?.Invoke(root);
        var result = ScenarioLoaderV2.LoadWorld(root.ToJsonString());
        Assert.True(result.IsSuccess, result.Error);
        var (world, clock) = result.Value;
        world.UsePlayers(null);
        var service = new PlayerService(new AllEvents());
        Assert.True(service.SetTickDelta(world, "alan", Fine).IsSuccess);
        var body = world.Npcs.Single(n => n.Name == "Marina");
        var partner = world.Npcs.Single(n => n.Name == "Rafael");
        Assert.True(service.Incarnate(world, body.Id, "alan", PossessionMode.Full).IsSuccess);
        body.CreditWallet(new Money(500)); // o casal começa com pouco: o que se compra aqui é logística, não orçamento
        var house = world.FindBuilding(world.FindHousehold(body.Household!.Value)!.Dwelling!.Value)!;
        return (world, clock, service, body, partner, house);
    }

    private static Workplace Shop(WorldState world) => HomeCommands.Shop(world, world.ObjectCatalog!.Home!)!;

    private static HomeHaul Haul(WorldState world) => world.ObjectCatalog!.Home!.Haul!;

    private static long Coins(WorldState world) => world.Npcs.Sum(n => n.Wallet.Amount) + world.Workplaces.Sum(w => w.Treasury.Amount);

    private static long Stock(WorldState world, string item) => Shop(world).Stock.GetValueOrDefault(new ResourceType(world.ObjectCatalog!.Home!.Item(item)!.Resource));

    private static Result<InteractionResult> Do(PlayerService service, WorldState world, Building house, string verb) => service.Interact(world, "alan", "home", house.Id.Value, verb);

    private static Result<HomeOutcome> Check(WorldState world, Npc body, Building house, string verb) => HomeCommands.Check(world, body, house.Id.Value, verb);

    private static void Stand(WorldState world, Npc npc, CellCoord cell)
    {
        npc.ExitBuilding();
        npc.MoveTo(cell, world.CurrentDate.TotalHours);
        npc.SetFine(new FinePose(cell.X * FinePose.PerCell + 500, cell.Y * FinePose.PerCell + 500, 0, 0, world.CurrentDate.TotalMilliseconds));
    }

    private static CellCoord ShopFront(WorldState world) => HomeFreight.ShopFront(world, Shop(world)).Front;

    private static CellCoord HomeFront(WorldState world, Building house) => HomeFreight.FrontOf(world, house)!.Value.Front;

    private static int Buy(PlayerService service, WorldState world, Building house, string item)
    {
        var bought = Do(service, world, house, $"buy:{item}");
        Assert.True(bought.IsSuccess, bought.Error);
        return house.Edit!.Parcels[^1].Seq;
    }

    private static string FirstPlace(WorldState world, Npc body, Building house, int seq)
    {
        foreach (var cell in BuildingLayoutCache.Of(house).Layout.FloorLayouts[0].Cells.OrderBy(c => c.Cell.Y).ThenBy(c => c.Cell.X))
            foreach (int facing in new[] { 0, 90, 180, 270 })
            {
                string verb = $"place:{seq}:0:{cell.Cell.X}:{cell.Cell.Y}:{facing}";
                if (Check(world, body, house, verb).IsSuccess) return verb;
            }

        throw new InvalidOperationException("sem lugar");
    }

    private static void Run(WorldState world, WorldClock clock, long ms)
    {
        long until = world.CurrentDate.TotalMilliseconds + ms;
        while (world.CurrentDate.TotalMilliseconds < until) clock.Tick(world, Fine);
    }

    [Fact]
    public void Paying_reserves_the_piece_in_the_shop_in_the_house_name_and_nothing_appears_at_home_or_comes_from_nowhere()
    {
        var (world, _, service, body, _, house) = Playing();
        long total = Coins(world), wallet = body.Wallet.Amount, stock = Stock(world, "sofa"), till = Shop(world).Treasury.Amount;
        int before = HomeFurniture.Of(house, world.ObjectCatalog!).Count(p => p.Spot is not null);
        int seq = Buy(service, world, house, "sofa");
        long price = world.ObjectCatalog!.Home!.Item("sofa")!.Price;
        Assert.Equal(wallet - price, body.Wallet.Amount);
        Assert.Equal(till + price, Shop(world).Treasury.Amount);
        Assert.Equal(stock - 1, Stock(world, "sofa")); // reservado: sai do estoque à venda
        Assert.Equal(total, Coins(world));
        Assert.Equal(before, HomeFurniture.Of(house, world.ObjectCatalog).Count(p => p.Spot is not null)); // ainda não está em casa
        var parcel = HomeFreight.Find(house, seq)!;
        Assert.Equal("shop", parcel.Where);
        Assert.False(Check(world, body, house, FirstPlaceOrNone(seq)).IsSuccess); // o móvel não está no depósito: não se põe

        var view = HomeView.Of(world, body)!;
        Assert.Contains(view.Haul!.Parcels, p => p.Seq == seq && p.Where == "shop" && p.Kg == 45 && p.Carry == "vehicle");
        Assert.Equal("hands", view.Haul.Items["plant"].Carry);
        Assert.Equal("helper", view.Haul.Items["tv"].Carry);
        Assert.Equal("vehicle", view.Haul.Items["sofa"].Carry);
    }

    private static string FirstPlaceOrNone(int seq) => $"place:{seq}:0:1:1:0";

    [Fact]
    public void A_plant_goes_in_the_hands_from_the_shop_to_the_house_slower_and_only_near_each_end()
    {
        var (world, _, service, body, _, house) = Playing();
        int seq = Buy(service, world, house, "plant");
        var far = Check(world, body, house, $"fetch:{seq}");
        Assert.False(far.IsSuccess);
        Assert.Contains("loja", far.Error);

        Stand(world, body, ShopFront(world));
        double free = PlayerWalk.SpeedMps(world, body, ShopFront(world), new CellCoord(ShopFront(world).X, ShopFront(world).Y + 1));
        Assert.True(Do(service, world, house, $"fetch:{seq}").IsSuccess);
        Assert.Equal(("hands", body.Id.Value), (HomeFreight.Find(house, seq)!.Where, HomeFreight.Find(house, seq)!.Carrier));
        Assert.False(Check(world, body, house, $"fetch:{buyAnother(service, world, house)}").IsSuccess); // as mãos estão ocupadas

        Assert.True(HomeFreightViews.SheetOf(world, body) is { Carry.Item: "Planta" });
        double loaded = PlayerWalk.SpeedMps(world, body, ShopFront(world), new CellCoord(ShopFront(world).X, ShopFront(world).Y + 1));
        Assert.True(loaded < free, $"{loaded} < {free}: levar nas mãos atrasa o passo");
        Assert.True(HomeFreight.LoadFactor(world, body) > 1.0);

        Assert.False(Check(world, body, house, $"drop:{seq}").IsSuccess); // ainda não chegou em casa
        Stand(world, body, HomeFront(world, house));
        Assert.True(Do(service, world, house, $"drop:{seq}").IsSuccess);
        Assert.Equal("depot", HomeFreight.Find(house, seq)!.Where);
        Assert.Equal(1.0, HomeFreight.LoadFactor(world, body)); // largou a carga: o passo volta

        var placed = Do(service, world, house, FirstPlace(world, body, house, seq));
        Assert.True(placed.IsSuccess, placed.Error);
        Assert.Contains(HomeFurniture.Of(house, world.ObjectCatalog!), p => p.Id == "plant" && p.Spot is not null);
        Assert.Null(HomeFreight.Find(house, seq));

        static int buyAnother(PlayerService s, WorldState w, Building h) => Buy(s, w, h, "painting");
    }

    [Fact]
    public void The_slowdown_follows_the_weight_in_the_data_a_chair_slows_less_than_a_tv_and_two_carriers_split_the_load()
    {
        var (world, _, service, body, partner, house) = Playing();
        var haul = Haul(world);
        int chair = Buy(service, world, house, "chair");
        int tv = Buy(service, world, house, "tv");
        Stand(world, body, ShopFront(world));
        Stand(world, partner, ShopFront(world));
        var step = new CellCoord(ShopFront(world).X, ShopFront(world).Y + 1);
        double free = PlayerWalk.SpeedMps(world, body, ShopFront(world), step);
        Assert.True(Do(service, world, house, $"fetch:{chair}").IsSuccess);
        double chairSpeed = PlayerWalk.SpeedMps(world, body, ShopFront(world), step);
        double expected = 1 + haul.Hands.SlowPercent / 100.0 * haul.HeftOf("chair")!.Kg / haul.Hands.MaxKg;
        Assert.Equal(free / expected, chairSpeed, 6);
        Assert.Equal(free, PlayerWalk.SpeedMps(world, partner, ShopFront(world), step), 6); // quem não leva nada anda como antes

        var carried = HomeFreight.Find(house, chair)!;
        HomeFreight.Update(world, house, carried, carried with { Where = "shop", Carrier = 0 });
        Assert.True(Do(service, world, house, $"fetchhelp:{tv}").IsSuccess);
        double shared = 1 + haul.Hands.SlowPercent / 100.0 * (haul.HeftOf("tv")!.Kg / 2) / haul.Hands.MaxKg;
        Assert.Equal(free / shared, PlayerWalk.SpeedMps(world, body, ShopFront(world), step), 6);
        Assert.Equal(free / shared, PlayerWalk.SpeedMps(world, partner, ShopFront(world), step), 6);
    }

    [Fact]
    public void Big_pieces_do_not_fit_the_hands_and_the_refusal_says_the_ways_and_help_doubles_the_hands_only_with_someone_there()
    {
        var (world, _, service, body, partner, house) = Playing();
        int sofa = Buy(service, world, house, "sofa");
        int tv = Buy(service, world, house, "tv");
        Stand(world, body, ShopFront(world));
        var refused = Check(world, body, house, $"fetch:{sofa}");
        Assert.False(refused.IsSuccess);
        Assert.Contains("carro", refused.Error);
        Assert.Contains("entrega", refused.Error);
        Assert.False(Check(world, body, house, $"fetchhelp:{sofa}").IsSuccess); // pesado demais até para dois (e sem ninguém ao lado)

        Assert.False(Check(world, body, house, $"fetch:{tv}").IsSuccess); // 14 kg: acima das mãos de um (12)
        var alone = Check(world, body, house, $"fetchhelp:{tv}");
        Assert.False(alone.IsSuccess);
        Assert.Contains("ajudar", alone.Error); // o parceiro está em casa

        Stand(world, partner, ShopFront(world));
        Assert.True(Do(service, world, house, $"fetchhelp:{tv}").IsSuccess);
        var carried = HomeFreight.Find(house, tv)!;
        Assert.Equal((body.Id.Value, partner.Id.Value), (carried.Carrier, carried.Helper));
        Assert.True(HomeFreight.LoadFactor(world, partner) > 1.0 && HomeFreight.LoadFactor(world, body) > 1.0); // os dois carregam

        Stand(world, body, HomeFront(world, house));
        var waiting = Check(world, body, house, $"drop:{tv}");
        Assert.False(waiting.IsSuccess); // o ajudante ainda não chegou
        Stand(world, partner, HomeFront(world, house));
        Assert.True(Do(service, world, house, $"drop:{tv}").IsSuccess);
        Assert.Equal("depot", HomeFreight.Find(house, tv)!.Where);
    }

    [Fact]
    public void A_big_piece_goes_by_the_house_car_only_if_the_trunk_holds_it_and_the_car_is_where_the_work_is()
    {
        var (world, clock, service, body, _, house) = Playing();
        int sofa = Buy(service, world, house, "sofa");
        int bed = Buy(service, world, house, "bed");
        var home = world.ObjectCatalog!.Home!;
        var car = HomeFreight.VehicleOf(home.Haul!)!;
        Assert.True(car.TrunkLiters >= 900 && car.TrunkLiters < 900 + 1200); // o porta-malas leva o sofá, não o sofá e a cama juntos

        // o carro começa em frente à casa: carregar na loja sem ele lá recusa
        Stand(world, body, ShopFront(world));
        Assert.Contains("carro", Check(world, body, house, $"load:{sofa}").Error);

        Stand(world, body, HomeFreight.CarCell(world, house));
        long wallet = body.Wallet.Amount;
        Assert.True(Do(service, world, house, "drive:shop").IsSuccess);
        Assert.True(body.BodyTask is { Action: HomeFreight.DriveAction });
        Assert.Equal(car.Mps, PlayerWalk.SpeedMps(world, body, new CellCoord(5, 5), new CellCoord(5, 6)));
        Assert.False(Check(world, body, house, "drive:home").IsSuccess); // dirigindo, o corpo está ocupado
        long t0 = world.CurrentDate.TotalMilliseconds;
        for (int i = 0; i < 4000 && body.BodyTask is not null; i++) clock.Tick(world, Fine);
        Assert.Null(body.BodyTask);
        long drive = world.CurrentDate.TotalMilliseconds - t0;
        var park = HomeFreight.CarCell(world, house);
        Assert.True(Math.Max(Math.Abs(park.X - ShopFront(world).X), Math.Abs(park.Y - ShopFront(world).Y)) <= HomeFreight.ReachCells(world, home.Haul!));
        int cells = Math.Max(Math.Abs(park.X - HomeFreight.FrontOf(world, house)!.Value.Park.X), Math.Abs(park.Y - HomeFreight.FrontOf(world, house)!.Value.Park.Y));
        Assert.True(drive < cells * world.Players.CellM / world.Players.WalkMps * 1000, $"{drive} ms: o carro é mais rápido que andar");
        Assert.Equal(wallet, body.Wallet.Amount);

        Assert.True(Do(service, world, house, $"load:{sofa}").IsSuccess);
        Assert.Equal("car", HomeFreight.Find(house, sofa)!.Where);
        var full = Check(world, body, house, $"load:{bed}");
        Assert.False(full.IsSuccess);
        Assert.Contains("porta-malas", full.Error);

        Assert.False(Check(world, body, house, $"unload:{sofa}").IsSuccess); // o carro está na loja, não em casa
        Assert.True(Do(service, world, house, "drive:home").IsSuccess);
        for (int i = 0; i < 4000 && body.BodyTask is not null; i++) clock.Tick(world, Fine);
        Assert.True(Do(service, world, house, $"unload:{sofa}").IsSuccess);
        Assert.Equal("depot", HomeFreight.Find(house, sofa)!.Where);
        Assert.Equal("shop", HomeFreight.Find(house, bed)!.Where);
        Assert.Null(house.Edit!.Car); // voltou ao lugar de partida: nada a gravar
    }

    [Fact]
    public void Delivery_costs_money_leaves_in_the_delivery_hours_with_a_real_employee_and_arrives_only_when_the_walk_ends()
    {
        var (world, clock, service, body, _, house) = Playing();
        var haul = Haul(world);
        int seq = Buy(service, world, house, "sofa");
        long total = Coins(world), wallet = body.Wallet.Amount;
        var ordered = Do(service, world, house, $"deliver:{seq}");
        Assert.True(ordered.IsSuccess, ordered.Error);
        var parcel = HomeFreight.Find(house, seq)!;
        Assert.Equal(wallet - haul.Delivery.Fee, body.Wallet.Amount);
        Assert.Equal(total, Coins(world)); // a taxa vai ao caixa da loja
        var courier = world.FindNpc(new NpcId(parcel.Courier))!;
        Assert.Equal("Seu Tomás", courier.Name); // uma pessoa do mundo: o empregado da loja
        Assert.Contains(courier.Id, Shop(world).Employees);
        Assert.Equal("courier", parcel.Where);

        long day = (long)world.Calendar.HoursPerDay * WorldMillis.PerHour;
        long hour = parcel.DepartMs % day / WorldMillis.PerHour;
        Assert.InRange(hour, haul.Delivery.OpenHour, haul.Delivery.CloseHour - 1);
        Assert.True(parcel.DepartMs >= world.CurrentDate.TotalMilliseconds + haul.Delivery.PrepMin * 60_000L);
        var trip = HomeView.Of(world, body)!.Haul!.Couriers.Single();
        Assert.True(trip.Path.Count > 2 && trip.ArrivesInMs == parcel.DueMs - world.CurrentDate.TotalMilliseconds);
        Assert.Equal(HomeFreight.TravelMs(world, haul, trip.Path.Count - 1), parcel.DueMs - parcel.DepartMs);

        // não chega antes do prazo, nem some: depois do prazo está no depósito e a ficha avisa
        while (world.CurrentDate.TotalMilliseconds < parcel.DueMs - 60_000) clock.Tick(world, 30_000);
        Assert.Equal("courier", HomeFreight.Find(house, seq)!.Where);
        Assert.Null(HomeFreightViews.SheetOf(world, body)?.Arrived.FirstOrDefault());
        while (world.CurrentDate.TotalMilliseconds < parcel.DueMs + 1000) clock.Tick(world, 30_000);
        Assert.Equal("depot", HomeFreight.Find(house, seq)!.Where);
        var sheet = HomeFreightViews.SheetOf(world, body)!;
        Assert.Equal(("Sofá", "Seu Tomás"), (sheet.Arrived[0].Item, sheet.Arrived[0].By));
        Assert.Equal(0, sheet.Coming);
    }

    [Fact]
    public void Delivery_at_night_waits_for_the_next_opening_and_two_orders_do_not_use_the_same_courier_at_once()
    {
        var (world, clock, service, body, _, house) = Playing();
        var haul = Haul(world);
        long day = (long)world.Calendar.HoursPerDay * WorldMillis.PerHour;
        long night = (world.CurrentDate.TotalMilliseconds / day + 1) * day - WorldMillis.PerHour; // 23h
        Assert.Equal(world.CurrentDate.TotalMilliseconds / day * day + haul.Delivery.OpenHour * (long)WorldMillis.PerHour, HomeFreight.NextDeparture(world, haul, world.CurrentDate.TotalMilliseconds / day * day + 1000));
        Assert.Equal((night / day + 1) * day + haul.Delivery.OpenHour * (long)WorldMillis.PerHour, HomeFreight.NextDeparture(world, haul, night));
        long noon = night / day * day + (haul.Delivery.OpenHour + 1) * (long)WorldMillis.PerHour;
        Assert.Equal(noon, HomeFreight.NextDeparture(world, haul, noon));

        int a = Buy(service, world, house, "sofa");
        int b = Buy(service, world, house, "bookshelf");
        Assert.True(Do(service, world, house, $"deliver:{a}").IsSuccess);
        Assert.True(Do(service, world, house, $"deliver:{b}").IsSuccess);
        var first = HomeFreight.Find(house, a)!;
        var second = HomeFreight.Find(house, b)!;
        Assert.True(second.DepartMs >= first.DueMs + (first.DueMs - first.DepartMs), "o entregador só sai de novo depois de entregar e voltar");
        Assert.Equal(2, HomeFreightViews.SheetOf(world, body)!.Coming);
        clock.Tick(world, Fine);
    }

    [Fact]
    public void Without_a_courier_or_when_the_shop_is_out_of_the_piece_or_money_delivery_is_refused_and_changes_nothing()
    {
        var (world, _, service, body, _, house) = Playing(maker: false);
        int seq = Buy(service, world, house, "sofa");
        string hash = WorldSnapshot.CanonicalHash(world);
        var none = Do(service, world, house, $"deliver:{seq}");
        Assert.False(none.IsSuccess);
        Assert.Contains("entregue", none.Error);
        Assert.Equal(hash, WorldSnapshot.CanonicalHash(world));

        var (w2, _, s2, b2, _, h2) = Playing();
        int seq2 = Buy(s2, w2, h2, "sofa");
        b2.TryDebitWallet(new Money(b2.Wallet.Amount));
        Assert.Contains("falta dinheiro", Do(s2, w2, h2, $"deliver:{seq2}").Error);
        Assert.Equal("shop", HomeFreight.Find(h2, seq2)!.Where);
        Assert.False(Do(s2, w2, h2, "deliver:99").IsSuccess); // não existe
    }

    [Fact]
    public void The_depot_holds_what_arrived_places_it_validated_stashes_it_back_and_the_shop_buys_it_back_from_there()
    {
        var (world, _, service, body, _, house) = Playing();
        var home = world.ObjectCatalog!.Home!;
        int seq = Buy(service, world, house, "plant");
        Assert.False(Check(world, body, house, $"sellp:{seq}").IsSuccess); // na loja ainda: só se cancela
        long total = Coins(world), wallet = body.Wallet.Amount, stock = Stock(world, "plant");
        var cancel = Do(service, world, house, $"cancel:{seq}");
        Assert.True(cancel.IsSuccess, cancel.Error);
        Assert.Equal(wallet + home.Item("plant")!.Price, body.Wallet.Amount); // devolve tudo
        Assert.Equal(stock + 1, Stock(world, "plant"));
        Assert.Equal(total, Coins(world));
        Assert.Null(HomeFreight.Find(house, seq));

        int again = Buy(service, world, house, "plant");
        var parcel = HomeFreight.Find(house, again)!;
        HomeFreight.Update(world, house, parcel, parcel with { Where = "depot" });
        Assert.False(Check(world, body, house, $"place:{again}:0:0:0:45").IsSuccess); // lugar inválido (a regra de lugar é a de sempre)
        var placed = Do(service, world, house, FirstPlace(world, body, house, again));
        Assert.True(placed.IsSuccess, placed.Error);

        int piece = HomeFurniture.Of(house, world.ObjectCatalog!).ToList().FindIndex(p => p.Id == "plant" && p.Spot is not null);
        var stashed = Do(service, world, house, $"stash:{piece}");
        Assert.True(stashed.IsSuccess, stashed.Error);
        Assert.Null(HomeFurniture.Of(house, world.ObjectCatalog!)[piece].Spot);
        var inDepot = house.Edit!.Parcels.Single(p => p.Where == "depot");
        Assert.Equal("plant", inDepot.Id);

        long walletNow = body.Wallet.Amount, totalNow = Coins(world), stockNow = Stock(world, "plant");
        Assert.True(Do(service, world, house, $"sellp:{inDepot.Seq}").IsSuccess);
        Assert.Equal(walletNow + home.Item("plant")!.Price * home.Rules.ResalePercent / 100, body.Wallet.Amount);
        Assert.Equal(stockNow + 1, Stock(world, "plant"));
        Assert.Equal(totalNow, Coins(world));
        Assert.Empty(house.Edit.Parcels);
    }

    [Fact]
    public void A_made_to_order_piece_waits_in_the_shop_until_it_is_ready_and_cannot_be_fetched_or_delivered_before()
    {
        var (world, clock, service, body, _, house) = Playing();
        int seq = Buy(service, world, house, "armchair");
        var parcel = HomeFreight.Find(house, seq)!;
        Assert.True(parcel.ReadyAtMs > world.CurrentDate.TotalMilliseconds);
        Stand(world, body, ShopFront(world));
        Assert.Contains("fazendo", Check(world, body, house, $"fetch:{seq}").Error);
        Assert.Contains("fazendo", Check(world, body, house, $"load:{seq}").Error);
        while (world.CurrentDate.TotalMilliseconds < parcel.ReadyAtMs + 1000) clock.Tick(world, 60_000);
        Assert.True(Do(service, world, house, $"deliver:{seq}").IsSuccess); // pronta: a entrega sai (a poltrona é grande demais para as mãos)
        Assert.True(HomeFreight.Find(house, seq)!.DepartMs >= parcel.ReadyAtMs);
    }

    [Fact]
    public void The_same_commands_make_the_same_world_and_parcels_and_the_car_survive_a_snapshot_round_trip()
    {
        string Run()
        {
            var (world, clock, service, body, _, house) = Playing(seed: 2650);
            int a = Buy(service, world, house, "sofa");
            Assert.True(Do(service, world, house, $"deliver:{a}").IsSuccess);
            int b = Buy(service, world, house, "plant");
            Stand(world, body, ShopFront(world));
            Assert.True(Do(service, world, house, $"fetch:{b}").IsSuccess);
            while (world.CurrentDate.TotalMilliseconds < HomeFreight.Find(house, a)!.DueMs + 1000) clock.Tick(world, 30_000);
            return WorldSnapshot.CanonicalHash(world);
        }

        Assert.Equal(Run(), Run());

        var (w, _, s, bd, _, h) = Playing(seed: 2651);
        Assert.True(Do(s, w, h, $"deliver:{Buy(s, w, h, "sofa")}").IsSuccess);
        Buy(s, w, h, "chair");
        h.Edit!.Car = new CarSpot(3, 4);
        var restored = WorldSnapshot.Deserialize(WorldSnapshot.Serialize(w));
        var house = restored.FindBuilding(h.Id)!;
        Assert.Equal(h.Edit.Parcels, house.Edit!.Parcels);
        Assert.Equal(new CarSpot(3, 4), house.Edit.Car);
        Assert.Equal(WorldSnapshot.CanonicalHash(w), WorldSnapshot.CanonicalHash(restored));
        Assert.Equal(bd.Id, restored.FindNpc(bd.Id)!.Id);
    }

    [Fact]
    public void A_bad_haul_block_is_refused_naming_the_field_and_the_base_catalog_fills_what_the_scenario_leaves_out()
    {
        var bad = LivingWorld.Tests.Unit.Cities.SandboxFixtures.Parse();
        bad["Seed"] = 1;
        bad["Home"] = JsonNode.Parse("""{"haul":{"heft":{"sofa":{"kg":0,"liters":900}}}}""");
        var loaded = ScenarioLoaderV2.LoadWorld(bad.ToJsonString());
        Assert.False(loaded.IsSuccess);
        Assert.Contains("Home.haul.heft.sofa", loaded.Error); // o móvel sem peso e volume é recusado com o campo

        var ghost = LivingWorld.Tests.Unit.Cities.SandboxFixtures.Parse();
        ghost["Seed"] = 1;
        ghost["Home"] = JsonNode.Parse("""{"haul":{"fleet":["fantasma"]}}""");
        var missing = ScenarioLoaderV2.LoadWorld(ghost.ToJsonString());
        Assert.False(missing.IsSuccess);
        Assert.Contains("Home.haul.fleet[0]", missing.Error);

        var tuned = LivingWorld.Tests.Unit.Cities.SandboxFixtures.Parse();
        tuned["Seed"] = 1;
        tuned["Home"] = JsonNode.Parse("""{"haul":{"hands":{"maxKg":50,"maxLiters":900,"slowPercent":10}}}""");
        var loadedTuned = ScenarioLoaderV2.LoadWorld(tuned.ToJsonString());
        Assert.True(loadedTuned.IsSuccess, loadedTuned.Error);
        var haul = loadedTuned.Value.World.ObjectCatalog!.Home!.Haul!;
        Assert.Equal(50, haul.Hands.MaxKg); // o cenário manda
        Assert.Single(haul.Vehicles); // e o que ele não diz vem do catálogo-base
        Assert.Equal(18, haul.Delivery.CloseHour);
    }

    [Fact]
    public void The_interaction_wheel_offers_only_the_freight_actions_the_engine_says_are_valid_where_the_body_is_and_running_one_works()
    {
        var (world, _, service, body, _, house) = Playing();
        int seq = Buy(service, world, house, "plant");
        Stand(world, body, HomeFront(world, house));
        var atHome = PlayerInteractions.Offers(world, body).Single(o => o.Kind == PlayerInteractions.FreightKind);
        Assert.DoesNotContain(atHome.Uses!, u => u.Id.StartsWith("fetch:")); // longe da loja não se leva o móvel
        Assert.Contains(atHome.Uses!, u => u.Id == "drive:shop" && u.Name.Contains("loja"));

        Stand(world, body, ShopFront(world));
        var atShop = PlayerInteractions.Offers(world, body).Single(o => o.Kind == PlayerInteractions.FreightKind);
        Assert.Contains(atShop.Uses!, u => u.Id == $"fetch:{seq}" && u.Name.Contains("Planta"));
        Assert.Contains(atShop.Uses!, u => u.Id == $"deliver:{seq}");
        Assert.DoesNotContain(atShop.Uses!, u => u.Id.StartsWith("load:")); // o carro está em casa

        var done = service.Interact(world, "alan", PlayerInteractions.FreightKind, house.Id.Value, $"fetch:{seq}");
        Assert.True(done.IsSuccess, done.Error);
        Assert.Equal("hands", HomeFreight.Find(house, seq)!.Where);
    }
}
