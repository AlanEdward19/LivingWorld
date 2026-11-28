using System.Text.Json.Nodes;
using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Objects;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cities;
using LivingWorld.Simulation.Cities.Streets;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Objects;
using LivingWorld.Simulation.Players;
using LivingWorld.Simulation.Scenarios;
using static LivingWorld.Tests.Unit.Players.PlayerAbsenceTests;

namespace LivingWorld.Tests.Unit.Cities;

/// <summary>#258 (sobras do sandbox, épico #246): o mercado do bairro que repõe comida sem criá-la do nada, a refeição que o fogão faz, a rua, a calçada e o quintal
/// limpo como parâmetros do cenário, o parceiro que volta para casa para se cuidar, dormir quando se quer, o abajur que liga e desliga, e o peso da higiene entre NPCs.
/// Nada disto está escrito no código: é dado do período <c>sandbox-casal.json</c> e do catálogo <c>home-life.json</c>.</summary>
public class Sandbox258Tests
{
    private const int Fine = 250;
    private static readonly ResourceType Food = new(1);
    private static readonly ResourceType Water = new(2);
    private static readonly ResourceType Meal = new(3);

    private static (WorldState World, WorldClock Clock) Load(Action<JsonObject>? edit = null, bool full = true, ulong seed = 258)
    {
        var root = full ? SandboxFixtures.WithoutFurnitureShop(SandboxFixtures.Parse()) : SandboxFixtures.CoupleOnly(SandboxFixtures.Parse());
        root["Seed"] = seed;
        edit?.Invoke(root);
        var result = ScenarioLoaderV2.LoadWorld(root.ToJsonString());
        Assert.True(result.IsSuccess, result.Error);
        return result.Value;
    }

    private static Npc Marina(WorldState world) => world.Npcs.Single(n => n.Name == "Marina");

    private static Npc Rafael(WorldState world) => world.Npcs.Single(n => n.Name == "Rafael");

    private static Npc Joaquim(WorldState world) => world.Npcs.Single(n => n.Name == "Seu Joaquim");

    private static Workplace Market(WorldState world) => world.Workplaces.First(); // #263: o mercado é o primeiro; o escritório e a escola vêm depois

    private static (WorldState World, WorldClock Clock, PlayerService Service, Npc Body) Playing(bool full = false, ulong seed = 258)
    {
        var (world, clock) = Load(full: full, seed: seed);
        world.UsePlayers(null);
        var service = new PlayerService(new AllEvents());
        Assert.True(service.SetTickDelta(world, "alan", Fine).IsSuccess);
        var body = Marina(world);
        Assert.True(service.Incarnate(world, body.Id, "alan", PossessionMode.Full).IsSuccess);
        var home = world.FindBuilding(world.Households.First().Dwelling!.Value)!;
        var door = PlayerGround.DoorOf(home)!.Value;
        body.MoveTo(door.Door, 0);
        body.SetFine(new FinePose(door.Door.X * FinePose.PerCell + 500, door.Door.Y * FinePose.PerCell + 500, 0, 0, world.CurrentDate.TotalMilliseconds));
        return (world, clock, service, body);
    }

    private static void Run(WorldState world, WorldClock clock, long ms)
    {
        long until = world.CurrentDate.TotalMilliseconds + ms;
        while (world.CurrentDate.TotalMilliseconds < until) clock.Tick(world, Fine);
    }

    private static InteractionOffer Offer(WorldState world, string label) =>
        PlayerService.Reach(world, "alan")!.Single(o => o.Kind == "object" && o.Label == label);

    private static long Total(WorldState world, ResourceType resource) =>
        world.Households.Sum(h => h.Stock.GetValueOrDefault(resource)) + world.Workplaces.Sum(w => w.Stock.GetValueOrDefault(resource));

    private static long Coins(WorldState world) =>
        world.Npcs.Sum(n => n.Wallet.Amount) + world.Workplaces.Sum(w => w.Treasury.Amount) + world.ActiveCities().Sum(c => c.Treasury.Amount); // #263: a conta vai ao tesouro da cidade

    // --- 1. o mercado: prédio com estoque, preço e caixa, de porta para a rua, abastecido por quem trabalha nele ---

    [Fact]
    public void The_neighborhood_has_a_market_building_facing_the_street_with_stock_price_and_cash()
    {
        var (world, _) = Load();

        var market = Market(world);
        Assert.Contains(market.LocationType.Id, world.EconomyCatalog.MarketLocationTypeIds);
        Assert.NotNull(market.Building);
        Assert.True(market.Stock.GetValueOrDefault(Food) > 0 && market.Prices.GetValueOrDefault(Food) > 0 && market.Treasury.Amount > 0);
        var building = world.FindBuilding(market.Building!.Value)!;
        Assert.Equal(180, building.Orientation); // o cenário diz para onde a porta olha (Workplaces[].Orientation)
        var city = world.ActiveCities().Single();
        var (placed, origin, croft) = CityStreets.Placed(world, city).Single(p => p.Building.Id == building.Id);
        Assert.Equal("street", CityStreets.LotOf(world, city, placed, origin, croft).Access); // a porta dá para a rua
        Assert.Equal(5, world.Buildings.Count); // o mercado, a casa do casal, a do comerciante e (#263) o escritório e a escola
    }

    [Fact]
    public void The_shopkeeper_works_there_and_the_market_restocks_without_a_new_person_or_new_cash()
    {
        var (world, clock) = Load();
        long cash = Coins(world);
        long initial = Market(world).Stock.GetValueOrDefault(Food);

        for (int h = 0; h < 24 * 3; h++) clock.Tick(world);

        Assert.Equal(Market(world).Id, Joaquim(world).Employer); // o comerciante é o empregado do mercado
        Assert.True(Market(world).Stock.GetValueOrDefault(Food) > initial, "o mercado não repôs comida");
        Assert.Equal(3, world.Npcs.Count);
        Assert.Equal(cash, Coins(world)); // a moeda só muda de mão (salário e compra), nunca nasce
    }

    [Fact]
    public void The_shopkeeper_starts_on_the_payroll_by_the_scenario_and_the_couple_keeps_looking_for_nothing_else()
    {
        var (world, clock) = Load();

        Assert.Equal(Market(world).Id, Joaquim(world).Employer); // empregado desde o primeiro instante (AuthoredHouseholds[].Members[].Workplace)
        Assert.Single(Market(world).Employees);
        for (int h = 0; h < 24 * 5; h++) clock.Tick(world);
        Assert.Null(Marina(world).Employer); // a vaga é uma só: o casal segue sem emprego (a renda é a próxima etapa, ver docs/domain/sandbox.md)
        Assert.Null(Rafael(world).Employer); // #263: o emprego do Rafael é carreira do livro, não a folha de ofício
        Assert.Equal(1, Marina(world).Profession.Id);
    }

    [Fact]
    public void The_scenario_refuses_a_bad_door_direction_or_a_job_that_does_not_exist_naming_the_field()
    {
        var turned = ScenarioLoaderV2.LoadWorld(Edited(r => r["Workplaces"]![0]!["Orientation"] = 45));
        Assert.False(turned.IsSuccess);
        Assert.Contains("Workplaces[].Orientation", turned.Error);

        var missing = ScenarioLoaderV2.LoadWorld(Edited(r => r["AuthoredHouseholds"]![1]!["Members"]![0]!["Workplace"] = 7));
        Assert.False(missing.IsSuccess);
        Assert.Contains("Workplace 7", missing.Error);
    }

    private static string Edited(Action<JsonObject> edit)
    {
        var root = SandboxFixtures.Parse();
        edit(root);
        return root.ToJsonString();
    }

    [Fact]
    public void Food_is_not_created_from_nothing_when_nobody_works_the_market()
    {
        var (world, clock) = Load(r =>
        {
            r["AuthoredHouseholds"]!.AsArray().RemoveAt(1);
            r["Workplaces"]![0]!["MaxVacancies"] = 0; // o mercado sem quem o abasteça: nem o comerciante, nem o casal pegam a vaga
        });
        long food = Total(world, Food) + Total(world, Meal), water = Total(world, Water);

        for (int h = 0; h < 24 * 4; h++)
        {
            clock.Tick(world);
            Assert.True(Total(world, Food) + Total(world, Meal) <= food, $"a comida cresceu sozinha na hora {h}");
            Assert.True(Total(world, Water) <= water, $"a água cresceu sozinha na hora {h}: {water} -> {Total(world, Water)} casas={string.Join(",", world.Households.Select(x => x.Stock.GetValueOrDefault(Water)))} mercado={Market(world).Stock.GetValueOrDefault(Water)}");
        }
    }

    [Fact]
    public void Buying_at_the_market_moves_coin_and_food_between_the_two_and_creates_neither()
    {
        var (world, _, service, body) = Playing(full: true);
        var market = Market(world);
        var doorstep = PlayerGround.DoorOf(world.FindBuilding(market.Building!.Value)!)!.Value.Doorstep;
        body.MoveTo(doorstep, 0);
        body.SetFine(new FinePose(doorstep.X * FinePose.PerCell + 500, doorstep.Y * FinePose.PerCell + 500, 0, 0, world.CurrentDate.TotalMilliseconds));
        var home = world.Households.First();
        long cash = Coins(world), food = Total(world, Food), price = market.Prices[Food];
        long wallet = body.Wallet.Amount, pantry = home.Stock.GetValueOrDefault(Food), stock = market.Stock.GetValueOrDefault(Food), till = market.Treasury.Amount;

        var stall = PlayerService.Reach(world, "alan")!.Single(o => o.Kind == "stall");
        Assert.Contains("buy", stall.Verbs);
        var bought = service.Interact(world, "alan", "stall", stall.Id, "buy", Food.Id, 3);

        Assert.True(bought.IsSuccess, bought.Error);
        Assert.Equal(wallet - 3 * price, body.Wallet.Amount);
        Assert.Equal(till + 3 * price, market.Treasury.Amount);
        Assert.Equal(pantry + 3, home.Stock.GetValueOrDefault(Food)); // vai para a geladeira (a despensa da casa)
        Assert.Equal(stock - 3, market.Stock.GetValueOrDefault(Food));
        Assert.Equal(cash, Coins(world));
        Assert.Equal(food, Total(world, Food));
    }

    [Fact]
    public void A_buyer_without_the_coin_is_refused_and_nothing_changes()
    {
        var (world, _, service, body) = Playing(full: true);
        var market = Market(world);
        var doorstep = PlayerGround.DoorOf(world.FindBuilding(market.Building!.Value)!)!.Value.Doorstep;
        body.MoveTo(doorstep, 0);
        body.SetFine(new FinePose(doorstep.X * FinePose.PerCell + 500, doorstep.Y * FinePose.PerCell + 500, 0, 0, world.CurrentDate.TotalMilliseconds));
        body.TryDebitWallet(new LivingWorld.Domain.Shared.Money(body.Wallet.Amount));
        long food = Total(world, Food);
        var stall = PlayerService.Reach(world, "alan")!.Single(o => o.Kind == "stall");

        var refused = service.Interact(world, "alan", "stall", stall.Id, "buy", Food.Id, 1);

        Assert.False(refused.IsSuccess);
        Assert.Equal(food, Total(world, Food));
    }

    [Fact]
    public void The_full_sandbox_is_deterministic_and_the_seed_changes_the_world()
    {
        string Run(ulong seed)
        {
            var (world, clock) = Load(seed: seed);
            for (int h = 0; h < 48; h++) clock.Tick(world);
            return WorldSnapshot.CanonicalHash(world);
        }

        Assert.Equal(Run(258), Run(258));
        Assert.NotEqual(Run(258), Run(259));
    }

    // --- 2. a refeição: o fogão transforma o que sai da geladeira; comer à mesa ---

    [Fact]
    public void The_stove_turns_ingredients_into_meals_and_the_table_serves_them_with_a_better_effect_than_a_snack()
    {
        var (world, clock, service, body) = Playing();
        var home = world.Households.Single();
        body.SetHunger(30, world.CurrentDate.TotalHours);
        long food = home.Stock.GetValueOrDefault(Food);

        var refusedMeal = service.Interact(world, "alan", "object", Offer(world, "Mesa").Id, "use:meal"); // sem refeição pronta, nada para servir
        Assert.False(refusedMeal.IsSuccess);

        Assert.True(service.Interact(world, "alan", "object", Offer(world, "Fogão").Id, "use:cook").IsSuccess);
        Run(world, clock, 15 * 60_000); // 10 min de fogão

        Assert.Equal(food - 2, home.Stock.GetValueOrDefault(Food)); // gasta a comida crua
        Assert.Equal(2, home.Stock.GetValueOrDefault(Meal)); // e a faz refeição, uma por uma (nada vira dois)
        double before = body.HungerAt(world.CurrentDate.TotalHours);
        Assert.True(service.Interact(world, "alan", "object", Offer(world, "Mesa").Id, "use:meal").IsSuccess);
        Run(world, clock, 40 * 60_000);
        Assert.True(body.HungerAt(world.CurrentDate.TotalHours) > before + 40);
        Assert.Equal(1, home.Stock.GetValueOrDefault(Meal));
    }

    [Fact]
    public void Interrupting_the_cooking_loses_the_meal_and_the_ingredients_are_not_returned()
    {
        var (world, clock, service, body) = Playing();
        var home = world.Households.Single();
        long food = home.Stock.GetValueOrDefault(Food);
        Assert.True(service.Interact(world, "alan", "object", Offer(world, "Fogão").Id, "use:cook").IsSuccess);
        Run(world, clock, 4 * 60_000);

        Assert.True(service.Interrupt(world, "alan").IsSuccess);

        Assert.Equal(0, home.Stock.GetValueOrDefault(Meal));
        Assert.Equal(food - 2, home.Stock.GetValueOrDefault(Food));
    }

    [Fact]
    public void The_one_who_nobody_controls_cooks_and_eats_the_meal_before_the_raw_food()
    {
        var (world, clock) = Load(full: false);
        var home = world.Households.Single();
        long raw = home.Stock.GetValueOrDefault(Food);
        long meals = 0;

        for (int h = 0; h < 24 * 4; h++)
        {
            clock.Tick(world);
            meals = Math.Max(meals, home.Stock.GetValueOrDefault(Meal));
        }

        Assert.True(meals >= 1, "ninguém cozinhou");
        Assert.True(home.Stock.GetValueOrDefault(Food) < raw);
        Assert.Equal(Meal, LivingWorld.Simulation.Behavior.Resolvers.FoodResolver.ResolveMeal(world, WithMeal(home)));
    }

    private static LivingWorld.Domain.Population.Family.Household WithMeal(LivingWorld.Domain.Population.Family.Household home)
    {
        if (home.Stock.GetValueOrDefault(Meal) < 1) home.Deposit(Meal, 1);
        return home;
    }

    // --- 3. rua, calçada e quintal limpo: parâmetros de cenário ---

    [Fact]
    public void The_scenario_paves_the_street_and_the_sidewalk_and_a_world_without_the_parameter_has_neither()
    {
        var (world, _) = Load();
        var network = CityStreets.Of(world, world.ActiveCities().Single());
        var ground = world.Ground!;

        Assert.All(network.Cells.Where(c => !world.Buildings.Any(b => Covers(b, c))), cell => Assert.Equal(GroundSurface.Stone, ground.PavingAt(ground.IndexOf(cell))));
        int sidewalk = 0;
        for (int i = 0; i < ground.Count; i++)
            if (ground.PavingAt(i) == GroundSurface.Gravel) sidewalk++;
        Assert.True(sidewalk >= 20, $"calçada pequena: {sidewalk}");
        var roads = StreetViews.ByCity(world).Values.Single();
        Assert.Contains(roads, r => r.Surface == StreetSurface.Paved && r.Cell.Y == 12);

        var (bare, _) = Load(r => r["CityLayout"]!["Streets"] = new JsonObject());
        var plain = bare.Ground!;
        for (int i = 0; i < plain.Count; i++) Assert.Equal(GroundSurface.None, plain.PavingAt(i));
    }

    private static bool Covers(Building b, LivingWorld.Domain.Geography.CellCoord cell) =>
        b.Position is { } origin && BuildingFootprintGenerator.Generate(b).Any(c => c.Cell.X + origin.X == cell.X && c.Cell.Y + origin.Y == cell.Y);

    [Fact]
    public void The_yard_around_the_houses_and_the_street_have_no_trees_by_the_scenario_not_by_the_fixed_rule()
    {
        var (clean, _) = Load();
        var (wild, _) = Load(r => r["CityLayout"]!["Streets"] = new JsonObject());
        var zones = PlayerNature.Clear(clean);
        Assert.NotEmpty(zones);
        Assert.Empty(PlayerNature.Clear(wild));

        int cleared = 0, kept = 0;
        for (int x = 0; x < clean.Map.Width; x++)
            for (int y = 0; y < clean.Map.Height; y++)
            {
                var cell = new LivingWorld.Domain.Geography.CellCoord(x, y);
                if (!zones.Any(z => z.Contains(cell))) continue;
                Assert.Null(PlayerNature.TreeAt(clean, cell)); // dentro da zona o motor não aceita árvore
                if (PlayerNature.CellHash(cell) < PlayerNature.TreeChance && PlayerNature.TreeAt(wild, cell) is not null) cleared++;
                else kept++;
            }
        Assert.True(cleared > 0, "nenhuma árvore do sorteio caía no quintal: o teste não prova nada");
        Assert.True(kept > 0);
    }

    // --- 4. o parceiro se cuida fora de casa ---

    [Fact]
    public void The_partner_away_from_home_with_a_full_bladder_goes_back_and_uses_the_toilet()
    {
        var (world, clock) = Load(full: false);
        var rafael = Rafael(world);
        var home = world.Households.Single();
        var far = new CellCoord(home.Location.X - 6, home.Location.Y + 3);
        rafael.ExitBuilding();
        rafael.MoveTo(far, world.CurrentDate.TotalHours);
        HomeLife.Add(world, rafael, "bladder", -80, world.CurrentDate.TotalMilliseconds);
        Assert.NotEqual(home.Location, rafael.CurrentLocation);
        bool used = false;
        long cameBack = -1;

        for (int h = 0; h < 12; h++)
        {
            clock.Tick(world);
            if (cameBack < 0 && rafael.CurrentLocation == home.Location) cameBack = h;
            used |= rafael.BodyTask is { Use: "toilet" };
        }

        Assert.True(cameBack >= 0, "o parceiro não voltou para casa");
        Assert.True(used || (HomeLife.Value(world, rafael, "bladder", world.CurrentDate.TotalMilliseconds) ?? 0) > 40, "voltou mas não se cuidou");
    }

    // --- 5. dormir quando se quer ---

    [Fact]
    public void Lying_down_when_rested_is_allowed_and_does_not_start_an_accelerated_skip()
    {
        var (world, clock, service, body) = Playing();
        Assert.True(body.SleepAt(world.CurrentDate.TotalHours) >= SkipWatch.Rested);

        var slept = service.Sleep(world, "alan", null);

        Assert.True(slept.IsSuccess, slept.Error);
        Assert.Null(service.Skip); // o sono acelerado só acelera enquanto há sono a recuperar
        Assert.Equal(LivingWorld.Domain.Behavior.ActionType.Sleep, body.ControlledAction);
        Run(world, clock, 60 * 60_000);
        Assert.Equal(LivingWorld.Domain.Behavior.ActionType.Sleep, body.CurrentAction);
    }

    [Fact]
    public void Sleeping_when_tired_speeds_the_time_up_and_wakes_by_itself_when_rested()
    {
        var (world, clock, service, body) = Playing();
        body.SetSleep(30, world.CurrentDate.TotalHours);
        Rafael(world).SetControlledAction(ActionType.Idle); // #269: o parceiro agora vive sozinho (e chama quem dorme, que é outro fim do pulo: "chamado"); aqui ele fica à toa para o fim ser só o descanso

        Assert.True(service.Sleep(world, "alan", null).IsSuccess);
        Assert.NotNull(service.Skip);
        string? ending = null;
        for (int tick = 0; tick < 24 * 20 && ending is null; tick++)
        {
            clock.Tick(world, 3_600_000);
            ending = service.SkipEnding(world);
        }

        Assert.Equal("descansado", ending);
        Assert.True(body.SleepAt(world.CurrentDate.TotalHours) >= SkipWatch.Rested);
    }

    // --- 6. o abajur liga e desliga, com estado no motor ---

    [Fact]
    public void The_lamp_turns_on_and_off_by_its_catalog_uses_and_the_offer_only_shows_what_changes_something()
    {
        var (world, clock, service, body) = Playing();
        var lamp = Offer(world, "Abajur");
        Assert.Equal(new[] { "use:on" }, lamp.Verbs.Where(v => v.StartsWith("use:")).ToArray()); // apagado: só se oferece ligar
        string hash = WorldSnapshot.CanonicalHash(world);

        Assert.True(service.Interact(world, "alan", "object", lamp.Id, "use:on").IsSuccess);
        Run(world, clock, 20_000);

        int piece = (int)(lamp.Id % 1000);
        Assert.True(world.LightIsOn(lamp.Id / 1000, piece));
        Assert.Equal(new[] { "use:off" }, Offer(world, "Abajur").Verbs.Where(v => v.StartsWith("use:")).ToArray());
        Assert.NotEqual(hash, WorldSnapshot.CanonicalHash(world)); // a luz ligada é estado do mundo
        var loaded = WorldSnapshot.Deserialize(WorldSnapshot.Serialize(world));
        Assert.True(loaded.LightIsOn(lamp.Id / 1000, piece));
        Assert.Equal(WorldSnapshot.CanonicalHash(world), WorldSnapshot.CanonicalHash(loaded));
        Assert.Equal(new[] { piece }, LivingWorld.Api.Visual.Projection.InteriorProjector.LitPieces(world, new BuildingId(lamp.Id / 1000)).ToArray());

        Assert.True(service.Interact(world, "alan", "object", Offer(world, "Abajur").Id, "use:off").IsSuccess);
        Run(world, clock, 20_000);
        Assert.False(world.LightIsOn(lamp.Id / 1000, piece));
        Assert.Null(world.Lights); // nada ligado volta ao mundo de antes (fora do snapshot e do hash)
    }

    [Fact]
    public void The_interior_snapshot_carries_which_pieces_are_lit()
    {
        var (world, _, _, _) = Playing();
        var house = world.Buildings.Single();
        int lamp = HomeFurniture.Of(house, world.ObjectCatalog!).ToList().FindIndex(p => p.Id == "lamp");
        world.SetLight(house.Id.Value, lamp, true);

        var snapshot = LivingWorld.Api.Visual.Projection.InteriorProjector.Build(world, house.Id).Value!;

        var pieces = snapshot.Objects!.Furniture;
        Assert.True(pieces[lamp].Lit);
        Assert.Single(pieces, p => p.Lit == true);
    }

    [Fact]
    public void The_one_nobody_controls_turns_the_light_on_at_dusk_and_off_before_leaving_it_for_the_night()
    {
        var (world, clock) = Load(full: false);
        bool everOn = false, everOffAfterOn = false;
        int litByDay = 0;
        for (int h = 0; h < 24 * 4; h++)
        {
            clock.Tick(world);
            bool lit = world.Lights is not null;
            everOffAfterOn |= everOn && !lit;
            everOn |= lit;
            if (lit && !HomeTending.IsDark(world)) litByDay++; // quando o dia nasce o NPC apaga: só passa uma hora ou duas até ele chegar ao interruptor
        }

        Assert.True(everOn, "ninguém ligou a luz ao escurecer");
        Assert.True(everOffAfterOn, "a luz nunca foi desligada");
        Assert.True(litByDay <= 24, $"a luz ficou ligada de dia por {litByDay} horas");
    }

    // --- 7. o peso da higiene vale entre NPCs ---

    [Fact]
    public void A_dirty_speaker_weighs_on_the_bond_of_the_listener_also_between_two_npcs()
    {
        var (world, _) = Load(full: false);
        var (marina, rafael) = (Marina(world), Rafael(world));
        long now = world.CurrentDate.TotalMilliseconds;
        double Affection() => world.Relationships[new LivingWorld.Domain.Population.Family.RelationshipKey(rafael.Id, marina.Id)].Affection;
        double clean = Affection();

        HomeLife.WeighBond(world, marina, rafael, now); // asseada: nada pesa
        Assert.Equal(clean, Affection());

        HomeLife.Add(world, marina, "hygiene", -80, now);
        HomeLife.WeighBond(world, marina, rafael, now);
        Assert.True(Affection() < clean);

        // e pelas interações sociais do catálogo entre os dois (nenhum deles é o jogador)
        double afterTalk = Affection();
        SocialInteractions.ApplyEffects(world, new SocialEffects(new SocialSide(), new SocialSide()), marina, rafael, 1.0, now, "x");
        Assert.True(Affection() < afterTalk);
    }
}
