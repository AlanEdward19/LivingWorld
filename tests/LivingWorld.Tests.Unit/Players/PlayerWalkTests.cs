using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Infrastructure.EventLog;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Players;
using LivingWorld.Simulation.Scenarios;
using static LivingWorld.Tests.Unit.Players.PlayerAbsenceTests;
using static LivingWorld.Tests.Unit.Players.PlayerControlTests;

namespace LivingWorld.Tests.Unit.Players;

/// <summary>AD-197k: o passo solto (posição contínua em frações de célula, à velocidade do corpo), a mobília pelo catálogo de objetos
/// e o furto de oficina sem preço.</summary>
public class PlayerWalkTests
{
    private const int Fine = 50; // ms de mundo por tick fino (1:1)

    /// <summary>Uma célula aberta com a vizinha de leste aberta (fora de prédio, sem ninguém, sem água).</summary>
    internal static CellCoord OpenRun(WorldState w, Npc npc, int cells)
    {
        npc.ExitBuilding();
        var homes = w.Households.Select(h => h.Location).ToHashSet();
        foreach (var cell in w.Map.Cells.Select(c => c.Coord).OrderBy(c => Math.Abs(c.X - npc.CurrentLocation.X) + Math.Abs(c.Y - npc.CurrentLocation.Y)))
        {
            bool ok = true;
            for (int i = 0; i <= cells && ok; i++)
            {
                var c = new CellCoord(cell.X + i, cell.Y);
                ok = w.Map.TryGetCell(c, out var m) && !m.HasWater && !homes.Contains(c) && PlayerGround.At(w, c) is null
                    && PlayerGround.PersonAt(w, npc, c) is null;
            }
            if (ok) return cell;
        }
        throw new InvalidOperationException("sem chão aberto");
    }

    internal static void Wait(Arm arm, long ms)
    {
        long until = arm.World.CurrentDate.TotalMilliseconds + ms;
        while (arm.World.CurrentDate.TotalMilliseconds < until) arm.Clock.Tick(arm.World, Fine);
    }

    [Fact]
    public void Walking_moves_the_body_in_fractions_of_a_cell_at_the_body_speed_and_crosses_cells_by_the_grid()
    {
        var (arm, npc, service) = Body(1);
        var w = arm.World;
        var start = OpenRun(w, npc, 3);
        Place(w, npc, start);
        double mps = PlayerWalk.SpeedMps(w, npc, start, new CellCoord(start.X + 1, start.Y));
        Assert.InRange(mps, 0.05, 3.0); // o chão deste par pode ser pesado (mata, morro): a medida de gente fica no teste da mediana

        Assert.True(service.Walk(w, "alan", 1000, 0).IsSuccess);
        long x0 = npc.Fine!.X;
        Wait(arm, 200);
        Assert.True(service.Walk(w, "alan", 1000, 0).IsSuccess);
        long expected = (long)Math.Round(mps / w.Players.CellM * 0.2 * FinePose.PerCell);
        Assert.InRange(npc.Fine!.X - x0, expected - 3, expected + 3); // fração de célula na velocidade do corpo
        Assert.Equal(start, npc.CurrentLocation);

        for (int i = 0; i < 40 && npc.CurrentLocation == start; i++)
        {
            Wait(arm, 200);
            Assert.True(service.Walk(w, "alan", 1000, 0).IsSuccess);
        }
        Assert.Equal(new CellCoord(start.X + 1, start.Y), npc.CurrentLocation); // a célula segue a posição fina
        Assert.Equal(npc.CurrentLocation.X, npc.Fine!.CellX);
    }

    /// <summary>Medida (AD-197k): a velocidade do passo solto à porta de casa (o chão da vila), mediana de 20 sementes, em m/s.
    /// <c>LW_MEASURE_OUT</c> grava o resultado.</summary>
    [Fact]
    [Trait("Category", "Scenario")]
    public void The_free_walk_speed_at_the_doorstep_is_human_median_of_20_seeds()
    {
        var speeds = new List<double>();
        for (ulong seed = 1; seed <= 20; seed++)
        {
            var (arm, npc, _) = Body(seed);
            var w = arm.World;
            npc.SetSleep(90, w.CurrentDate.TotalHours);
            var door = w.Buildings.Where(b => b.Position is not null && b.CompletedAtTick <= w.CurrentDate.TotalHours).OrderBy(b => b.Id.Value)
                .Select(b => PlayerGround.DoorOf(b)!.Value).First(d => PlayerGround.At(w, d.Doorstep) is null && w.Map.TryGetCell(d.Doorstep, out _));
            var away = new CellCoord(2 * door.Doorstep.X - door.Door.X, 2 * door.Doorstep.Y - door.Door.Y);
            if (!w.Map.TryGetCell(away, out _)) continue;
            speeds.Add(PlayerWalk.SpeedMps(w, npc, door.Doorstep, away));
        }
        speeds.Sort();
        double median = speeds[speeds.Count / 2];
        string line = FormattableString.Invariant($"walkMedianMps={median:0.000} walkMinMps={speeds[0]:0.000} walkMaxMps={speeds[^1]:0.000} seeds={speeds.Count}");
        if (Environment.GetEnvironmentVariable("LW_MEASURE_OUT") is { Length: > 0 } path) File.WriteAllText(path, line + "\n");
        Assert.True(median is >= 0.5 and <= 2.0, line);
    }

    [Fact]
    public void A_free_walk_stops_at_a_wall_and_does_not_go_through_it()
    {
        var (arm, npc, service) = Body(2);
        var w = arm.World;
        var house = w.Buildings.Where(b => b.Position is not null && b.CompletedAtTick <= w.CurrentDate.TotalHours).OrderBy(b => b.Id.Value).First();
        var origin = house.Position!.Value;
        var wall = BuildingFootprintGenerator.Generate(house).Where(c => !InteriorWalkability.IsWalkable(c.Material))
            .Select(c => new CellCoord(origin.X + c.Cell.X, origin.Y + c.Cell.Y))
            .Select(c => (Wall: c, Out: new CellCoord(c.X - 1, c.Y)))
            .First(p => PlayerGround.At(w, p.Out) is null && w.Map.TryGetCell(p.Out, out _) && PlayerGround.PersonAt(w, npc, p.Out) is null);
        Place(w, npc, wall.Out);
        Assert.True(service.Walk(w, "alan", 1000, 0).IsSuccess);
        for (int i = 0; i < 40; i++)
        {
            Wait(arm, 200);
            Assert.True(service.Walk(w, "alan", 1000, 0).IsSuccess);
        }
        Assert.Equal(wall.Out, npc.CurrentLocation);
        Assert.True(npc.Fine!.X < (wall.Out.X + 1) * FinePose.PerCell, "a posição fina para antes da parede");
    }

    [Fact]
    public void A_log_with_free_walking_replays_to_the_same_hash()
    {
        var arm = Village(3, NoPace);
        var w = arm.World;
        var npc = Pick(w);
        var start = OpenRun(w, npc, 3);
        Place(w, npc, start);
        string snapshot = WorldSnapshot.Serialize(w);
        var service = new PlayerService(arm.Sink);
        Assert.True(service.Incarnate(w, npc.Id, "alan").IsSuccess);
        Assert.True(service.SetTickDelta(w, "admin", Fine).IsSuccess);
        for (int i = 0; i < 20; i++)
        {
            Assert.True(service.Walk(w, "alan", i < 12 ? 1000 : 700, i < 12 ? 0 : 700).IsSuccess);
            for (int t = 0; t < 2; t++) arm.Clock.Tick(w, Fine);
        }
        Assert.True(service.Walk(w, "alan", 0, 0).IsSuccess);
        for (int t = 0; t < 4; t++) arm.Clock.Tick(w, Fine);
        long end = w.CurrentDate.TotalMilliseconds;

        Assert.Contains(service.Log.Records, r => r.Kind == PlayerCommandKind.Walk);
        var replayed = PlayerReplay.Run(snapshot, service.Log.Records,
            new WorldClock(ScenarioRunner.DefaultSystems(), sink: new BufferingWorldEventSink()), new BufferingWorldEventSink(), end);
        Assert.Equal(WorldSnapshot.CanonicalHash(w), WorldSnapshot.CanonicalHash(replayed));
        Assert.Equal(npc.Fine, replayed.FindNpc(npc.Id)!.Fine);
    }

    [Fact]
    public void The_furniture_of_the_home_is_offered_from_the_object_catalog_and_using_it_asks_its_action()
    {
        // um catálogo de objetos mínimo (a cama serve dormir), a mobília de todo prédio
        var bed = new LivingWorld.Domain.Objects.ObjectDescriptor("cama", "furniture",
            [new LivingWorld.Domain.Objects.ObjectPart("box", [1, 0.4, 2], [0, 0, 0], "#aa8866")], 0,
            [new LivingWorld.Domain.Objects.ObjectUsePoint("deitar", [0, 0, 0], 0, Action: (int)ActionType.Sleep)]);
        var catalog = new LivingWorld.Domain.Objects.ObjectCatalog(1, [bed],
            new Dictionary<string, IReadOnlyList<string>> { ["default"] = ["cama"] });
        var (world, _) = ScenarioRunner.Create(4, objectCatalog: catalog);
        world.UsePlayers(NoPace);
        var sink = new AllEvents();
        var clock = new WorldClock(ScenarioRunner.DefaultSystems(), sink: sink);
        clock.Run(world, 96);
        var arm = new Arm(world, clock, sink, new LivingWorld.Simulation.Scheduling.TickContext(world, world.Rng, world.Scheduler, sink));
        var w = arm.World;
        var npc = w.Npcs.First(n => n.IsAlive && n.Household is { } h && w.FindHousehold(h) is { Dwelling: { } d }
            && PlayerGround.At(w, w.FindHousehold(h)!.Location)?.Building.Id == d);
        var service = new PlayerService(arm.Sink);
        Assert.True(service.Incarnate(w, npc.Id, "alan").IsSuccess);
        var home = w.FindHousehold(npc.Household!.Value)!;
        Place(w, npc, home.Location);
        var offer = PlayerService.Reach(w, "alan")!.First(o => o.Kind == "object");
        Assert.NotEmpty(offer.Label);
        string verb = offer.Verbs[0];

        Assert.True(service.Interact(w, "alan", "object", offer.Id, verb).IsSuccess);
        Assert.Equal(Enum.Parse<ActionType>(verb, ignoreCase: true), npc.ControlledAction);
    }

    [Fact]
    public void A_workshop_without_prices_can_be_robbed_through_the_theft_of_any_npc()
    {
        int offered = 0, logged = 0;
        for (ulong seed = 1; seed <= 6; seed++)
        {
            var (arm, npc, service) = Body(seed);
            var w = arm.World;
            // uma oficina sem preço (o mundo semeado só tem banca e lavoura): o tipo de uma oficina que não é lavoura, sem tabela de preço
            var kind = w.Workplaces.First(p => p.LocationType.Id != 1).LocationType;
            var at = OpenRun(w, npc, 1);
            var shop = new Workplace(new WorkplaceId(w.Workplaces.Max(p => p.Id.Value) + 1), kind, at, 1, [],
                new Dictionary<ResourceType, long> { [new ResourceType(w.EconomyRules.FoodResourceId)] = 5 }, new Money(0),
                new Dictionary<ResourceType, long>(), npc.City);
            w.AddWorkplace(shop);
            Place(w, npc, shop.Location);
            var offer = PlayerService.Reach(w, "alan")!.FirstOrDefault(o => o.Kind == "workshop" && o.Id == shop.Id.Value);
            if (offer is null || !offer.Verbs.Contains("steal")) continue;
            offered++;
            Assert.True(service.Interact(w, "alan", "workshop", shop.Id.Value, "steal").IsSuccess);
            if (arm.Sink.Events.Any(e => e.Kind == WorldEventKind.TheftCommitted)) logged++;
        }
        Assert.True(offered >= 1, "alguma oficina sem preço com estoque");
        Assert.Equal(offered, logged);
    }
}

internal static class WorkshopTestExtensions
{
    /// <summary>A oficina não é do corpo (nem dono nem empregado).</summary>
    public static bool Outsider(this Workplace shop, Npc npc) => shop.Owner != npc.Id && !shop.Employees.Contains(npc.Id);
}
