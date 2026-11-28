using System.Text.Json.Nodes;
using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Careers;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Objects;
using LivingWorld.Simulation.Players;
using LivingWorld.Simulation.Scenarios;
using static LivingWorld.Tests.Unit.Players.PlayerAbsenceTests;

namespace LivingWorld.Tests.Unit.Cities;

/// <summary>#269 (épico #246): a rodada 6 do sandbox. Pressão do dia (ritmo das necessidades em dado), parceiro que vive sozinho (dorme na cama, come, vai ao trabalho),
/// crítica por sono e higiene.</summary>
public class Playtest6Tests
{
    private const int Fine = 250;

    private static (WorldState World, WorldClock Clock, PlayerService Service, Npc Marina, Npc Rafael) Playing(int startHour = 7, ulong seed = 269, bool fine = true, Action<JsonObject>? edit = null)
    {
        var root = SandboxFixtures.Parse();
        root["Seed"] = seed;
        root["StartHour"] = startHour;
        edit?.Invoke(root);
        var result = ScenarioLoaderV2.LoadWorld(root.ToJsonString());
        Assert.True(result.IsSuccess, result.Error);
        var (world, clock) = result.Value;
        world.UsePlayers(null);
        var service = new PlayerService(new AllEvents());
        if (fine) Assert.True(service.SetTickDelta(world, "alan", Fine).IsSuccess);
        var marina = world.Npcs.Single(n => n.Name == "Marina");
        var rafael = world.Npcs.Single(n => n.Name == "Rafael");
        Assert.True(service.Incarnate(world, marina.Id, "alan", PossessionMode.Full).IsSuccess);
        return (world, clock, service, marina, rafael);
    }

    private static long Now(WorldState world) => world.CurrentDate.TotalMilliseconds;

    // --- pressão do dia: ritmo em dado ---

    [Fact]
    public void The_pace_of_the_needs_is_the_scenario_data_hunger_asks_for_food_every_4_to_6_hours_and_sleep_lasts_about_16_hours_awake()
    {
        var (world, _, _, _, _) = Playing(fine: false);
        var rules = world.NeedsRules;

        double hoursToMeal = rules.MealTimeDeficit / rules.HungerDecayPerHour; // quando a fome chega à hora da refeição
        double hoursToTired = (100 - (100 - rules.UrgencyThreshold)) / rules.SleepDecayPerHour; // quando o sono vira urgente
        Assert.InRange(hoursToMeal, 4, 6);
        Assert.InRange(hoursToTired, 14, 18);
        var bladder = HomeLife.Catalog(world)!.Find("bladder")!;
        Assert.InRange((100 - bladder.SeekBelow!.Value) / bladder.DecayPerHour, 6, 12); // do vazio até procurar o vaso: horas, não um dia
        Assert.True(HomeLife.Catalog(world)!.Critical!.Below.ContainsKey("sleep") && HomeLife.Catalog(world)!.Critical!.Below.ContainsKey("hygiene")); // crítica por dado também para sono e higiene
    }

    // --- o parceiro que ninguém conduz vive o dia ---

    [Fact]
    public void The_partner_nobody_drives_goes_to_work_eats_and_sleeps_in_the_bed_at_night_over_a_simulated_day()
    {
        var (world, clock, _, marina, rafael) = Playing(startHour: 7);
        bool slept = false, ate = false, worked = false, usedToilet = false;
        long bedNights = 0;
        for (long i = 0; i < 25L * 3600 * 4; i++)
        {
            clock.Tick(world, Fine);
            if (i % 40 != 0) continue;
            long ms = Now(world);
            int hour = (int)(ms / WorldMillis.PerHour % 24);
            if (rafael.CurrentAction == ActionType.Sleep && (hour >= 22 || hour < 5)) { slept = true; bedNights++; }
            if (rafael.CurrentAction == ActionType.Eat) ate = true;
            if (rafael.CurrentAction == ActionType.Work) worked = true;
            if (rafael.BodyTask is { Action: ObjectUsage.Action, Use: "toilet" }) usedToilet = true;
            Assert.Null(rafael.ControlledAction); // quem ninguém conduz não herda "ordem do jogador" (ficava à toa para sempre)
        }

        Assert.True(worked, "foi ao trabalho sozinho");
        Assert.True(ate, "comeu sozinho");
        Assert.True(slept && bedNights > 100, "dormiu à noite (horas seguidas, não só uma soneca)");
        Assert.True(usedToilet || HomeLife.Value(world, rafael, "bladder", Now(world)) > 5, "usou o vaso ou não deixou a bexiga a zero");
        Assert.True(rafael.SleepAt(world.CurrentDate.TotalHours) > 60, "acordou descansado, não a zero"); // dormir de verdade recupera o sono
        Assert.NotNull(marina);
    }

    [Fact]
    public void The_partner_who_leaves_for_the_shift_does_not_get_the_player_order_flag_and_decides_by_himself_when_he_comes_back()
    {
        var (world, clock, _, _, rafael) = Playing(startHour: 7);

        for (int i = 0; i < 14 * 3600 * 4; i++) clock.Tick(world, Fine); // do turno inteiro até a volta

        Assert.Null(rafael.ControlledAction);
        Assert.Null(rafael.ControllerId);
        Assert.True(world.Careers!.JobOf(rafael.Id.Value)!.Shifts >= 1);
    }

    [Fact]
    public void After_the_shift_the_needs_are_not_zero_without_a_reason_the_job_gives_the_lunch_and_the_toilet_break()
    {
        var (world, clock, _, _, rafael) = Playing(startHour: 7);
        for (int i = 0; i < 11 * 3600 * 4 && world.Careers!.JobOf(rafael.Id.Value)!.Shifts < 1; i++) clock.Tick(world, Fine);
        Assert.True(world.Careers!.JobOf(rafael.Id.Value)!.Shifts >= 1);

        long now = Now(world);
        Assert.True(HomeLife.Value(world, rafael, "bladder", now) > 20, "a pausa do banheiro está no dado do cargo");
        Assert.True(HomeLife.Value(world, rafael, "hunger", now) > 25, "o almoço do turno está no dado do cargo");
    }

    // --- crítica por sono, higiene e fome: o corpo sem ordem se cuida ---

    [Theory]
    [InlineData("hygiene", "shower")]
    [InlineData("bladder", "toilet")]
    [InlineData("hunger", null)]
    [InlineData("sleep", null)]
    public void A_critical_need_makes_the_free_will_body_take_care_of_it_with_the_furniture_that_serves(string need, string? use)
    {
        var (world, clock, _, marina, _) = Playing(startHour: 14);
        long now = Now(world);
        if (need == "sleep") marina.SetSleep(4, world.CurrentDate.TotalHours);
        else HomeLife.Add(world, marina, need, -100, now);
        Assert.Contains(CriticalNeeds.Now(world, marina, Now(world)), c => c.Id == need);

        bool started = false;
        for (int i = 0; i < 6000 && !started; i++)
        {
            clock.Tick(world, Fine);
            started = marina.BodyTask is { Action: ObjectUsage.Action } t && (use is null || t.Use == use) || need == "sleep" && marina.CurrentAction == ActionType.Sleep;
        }

        Assert.True(started, $"o corpo foi cuidar de {need} sozinho");
        Assert.Contains(world.Careers!.Notices, n => n.Kind == "need-critical" && n.Npc == marina.Id.Value);
        Assert.Contains(world.Careers.Notices, n => n.Kind == "need-auto" && n.Npc == marina.Id.Value);
    }

    // --- comprar até o fim: o motor diz onde o móvel cabe ---

    private static (LivingWorld.Domain.Cities.Buildings.Building House, int Seq) WithSofaInTheDepot(WorldState world, string id = "sofa")
    {
        var marina = world.Npcs.Single(n => n.Name == "Marina");
        var house = world.FindBuilding(world.FindHousehold(marina.Household!.Value)!.Dwelling!.Value)!;
        var edit = house.Edit ??= new LivingWorld.Domain.Cities.Buildings.HomeEdit();
        int seq = edit.NextParcel++;
        edit.Parcels.Add(new LivingWorld.Domain.Cities.Buildings.Parcel(seq, id, 0, 30, "depot"));
        return (house, seq);
    }

    [Fact]
    public void The_engine_says_where_the_sofa_in_the_depot_fits_and_autoplace_puts_it_there_valid()
    {
        var (world, _, service, marina, _) = Playing(fine: false);
        var (house, seq) = WithSofaInTheDepot(world);

        var fit = HomeCommands.Check(world, marina, house.Id.Value, $"fit:{seq}");

        Assert.True(fit.IsSuccess, fit.Error);
        var spot = fit.Value!.Fit!;
        var asked = HomeCommands.Check(world, marina, house.Id.Value, $"place:{seq}:{spot.Floor}:{spot.X}:{spot.Y}:{spot.Facing}");
        Assert.True(asked.IsSuccess, asked.Error); // o que o motor sugere o próprio motor aceita em place
        int before = HomeFurniture.Of(house, world.ObjectCatalog!).Count;

        var done = service.Interact(world, "alan", "home", house.Id.Value, $"autoplace:{seq}");

        Assert.True(done.IsSuccess, done.Error);
        Assert.DoesNotContain(house.Edit!.Parcels, p => p.Seq == seq); // saiu do depósito
        var pieces = HomeFurniture.Of(house, world.ObjectCatalog!);
        Assert.Equal(before + 1, pieces.Count);
        Assert.Contains(pieces, p => p.Id == "sofa" && p.Spot is { } at && at.Cell.X == spot.X && at.Cell.Y == spot.Y && at.Facing == spot.Facing);
    }

    [Fact]
    public void When_the_asked_spot_is_taken_the_engine_gives_the_nearest_valid_one_and_never_the_taken_cell()
    {
        var (world, _, _, marina, _) = Playing(fine: false);
        var (house, seq) = WithSofaInTheDepot(world);
        var catalog = world.ObjectCatalog!;
        var bed = HomeFurniture.Of(house, catalog).First(p => p.Id == "bed" && p.Spot is not null).Spot!.Value;
        var taken = HomeCommands.Check(world, marina, house.Id.Value, $"place:{seq}:{bed.Floor}:{bed.Cell.X}:{bed.Cell.Y}:0");
        Assert.False(taken.IsSuccess); // onde a cama está não cabe

        var near = HomeCommands.Check(world, marina, house.Id.Value, $"fit:{seq}:{bed.Floor}:{bed.Cell.X}:{bed.Cell.Y}:0");

        Assert.True(near.IsSuccess, near.Error);
        var spot = near.Value!.Fit!;
        Assert.NotEqual((bed.Cell.X, bed.Cell.Y), (spot.X, spot.Y));
        Assert.True(Math.Max(Math.Abs(spot.X - bed.Cell.X), Math.Abs(spot.Y - bed.Cell.Y)) <= 4, "perto de onde se pediu");
        Assert.True(HomeCommands.Check(world, marina, house.Id.Value, $"place:{seq}:{spot.Floor}:{spot.X}:{spot.Y}:{spot.Facing}").IsSuccess);
    }

    [Fact]
    public void Asking_for_a_room_by_its_id_puts_the_piece_in_that_room_and_the_depot_actions_offer_autoplace_with_the_spot()
    {
        var (world, _, _, marina, _) = Playing(fine: false);
        var (house, seq) = WithSofaInTheDepot(world, "plant");
        var rooms = LivingWorld.Simulation.Cities.BuildingRooms.Of(house, world.ObjectCatalog);

        var inLiving = HomeCommands.Check(world, marina, house.Id.Value, $"fit:{seq}:sala");

        Assert.True(inLiving.IsSuccess, inLiving.Error);
        var spot = inLiving.Value!.Fit!;
        Assert.Equal("sala", rooms.RoomAt(spot.Floor, new LivingWorld.Domain.Geography.CellCoord(spot.X, spot.Y))!.Kind);
        var view = HomeView.Of(world, marina)!;
        var parcel = view.Haul!.Parcels.Single(p => p.Seq == seq);
        var action = parcel.Actions.Single(a => a.Verb == $"autoplace:{seq}");
        Assert.True(action.Ok);
        Assert.NotNull(action.Fit);
    }

    [Fact]
    public void A_house_with_no_room_left_refuses_saying_what_to_do()
    {
        var (world, _, _, marina, _) = Playing(fine: false);
        var (house, seq) = WithSofaInTheDepot(world);
        var catalog = world.ObjectCatalog!;
        // enche a casa de sofás até não caber mais um (cada um posto pelo próprio motor)
        for (int i = 0; i < 80; i++)
        {
            var next = house.Edit!.NextParcel++;
            house.Edit.Parcels.Add(new LivingWorld.Domain.Cities.Buildings.Parcel(next, "sofa", 0, 1, "depot"));
            var put = HomeCommands.Check(world, marina, house.Id.Value, $"fit:{next}");
            if (!put.IsSuccess)
            {
                Assert.Contains("não há onde pôr", put.Error);
                Assert.NotNull(catalog);
                return;
            }

            var (_, ctx) = (0, (LivingWorld.Simulation.Scheduling.TickContext?)null);
            var done = HomeCommands.Apply(world, null!, marina, house.Id.Value, $"autoplace:{next}");
            Assert.True(done.IsSuccess, done.Error);
            Assert.Null(ctx);
        }

        Assert.Fail("a casa nunca encheu");
        Assert.True(seq >= 0);
    }
}
