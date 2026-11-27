using System.Text.Json;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cities;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Objects;
using LivingWorld.Simulation.Players;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Tests.Unit.Objects;

namespace LivingWorld.Tests.Unit.Cities;

/// <summary>#226: a parede é aresta entre células (não célula inteira) e a casa se divide em cômodos por função, tudo pelo catálogo do cenário.</summary>
public class HouseRoomsTests
{
    private static readonly int[] Types = [-1, 1, 2, 3, 4, 7, 10, 11, 14, 21];
    private static readonly CellCoord[] Four = [new(0, -1), new(-1, 0), new(1, 0), new(0, 1)];

    private static IEnumerable<Building> Houses()
    {
        foreach (var type in Types)
            for (long id = 1; id <= 8; id++)
                foreach (int orientation in new[] { 0, 90, 180, 270 })
                    yield return new Building(new BuildingId(id), default, type, 0, orientation: orientation);
    }

    [Fact]
    public void A_3_by_3_house_has_nine_floor_cells_and_walls_only_on_the_edges()
    {
        foreach (int orientation in new[] { 0, 90, 180, 270 })
        {
            var layout = BuildingLayoutGenerator.Generate(new BuildingId(5), -1, orientation);
            var ground = layout.FloorLayouts[0];

            Assert.Equal(9, ground.Cells.Count);
            Assert.All(ground.Cells, c => Assert.True(InteriorWalkability.IsWalkable(c.Material))); // nenhuma célula é parede
            Assert.Single(ground.Cells, c => c.Material == BuildingMaterial.Door);
            // o contorno de 3x3 tem 12 arestas: 11 de parede e 1 de porta
            Assert.Equal(11, ground.Walls!.Count);
            Assert.Single(ground.Doors!);
            Assert.All(ground.Walls, w => Assert.True(w.Material is BuildingMaterial.StoneWall or BuildingMaterial.WoodWall));
        }
    }

    [Fact]
    public void The_outer_walls_close_the_plan_except_at_the_door_opening()
    {
        foreach (var building in Houses())
        {
            var layout = BuildingLayoutGenerator.Generate(building);
            var ground = layout.FloorLayouts[0];
            var cells = ground.Cells.Select(c => c.Cell).ToHashSet();
            var edges = ground.Walls!.Select(w => w.Edge).Concat(ground.Doors!.Select(d => d.Edge)).ToHashSet();
            int expected = 0;
            foreach (var cell in cells)
                foreach (var d in Four)
                {
                    var next = new CellCoord(cell.X + d.X, cell.Y + d.Y);
                    if (cells.Contains(next)) continue;
                    expected++;
                    Assert.Contains(WallEdge.Between(cell, next), edges); // toda aresta de borda é parede ou a porta
                }

            Assert.Equal(expected, ground.Walls!.Count + ground.Doors!.Count);
            var (door, facing) = BuildingFootprintGenerator.DoorOf(ground.Cells);
            Assert.Equal(WallEdge.Between(door, new CellCoord(door.X + facing.X, door.Y + facing.Y)), ground.Doors.Single().Edge);
            // acima do térreo a porta vira parede
            foreach (var upper in layout.FloorLayouts.Skip(1)) Assert.Empty(upper.Doors!);
        }
    }

    private static HashSet<CellCoord> ReachFromDoor(Building building)
    {
        var catalog = ObjectCatalogTests.SampleCatalog();
        var layout = BuildingLayoutGenerator.Generate(building);
        var blocked = BuildingRooms.Of(building, catalog).Blocked[0];
        var cells = layout.FloorLayouts[0].Cells.Select(c => c.Cell).ToHashSet();
        var door = layout.FloorLayouts[0].Cells.Single(c => c.Material == BuildingMaterial.Door).Cell;
        var seen = new HashSet<CellCoord> { door };
        var queue = new Queue<CellCoord>([door]);
        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            foreach (var d in Four)
            {
                var next = new CellCoord(cell.X + d.X, cell.Y + d.Y);
                if (cells.Contains(next) && !blocked.Contains(WallEdge.Between(cell, next)) && seen.Add(next)) queue.Enqueue(next);
            }
        }

        return seen;
    }

    [Fact]
    public void Every_cell_of_every_house_is_reachable_from_the_street_door_in_all_rotations()
    {
        int divided = 0;
        foreach (var building in Houses())
        {
            var reached = ReachFromDoor(building);
            var all = BuildingLayoutGenerator.Generate(building).FloorLayouts[0].Cells.Select(c => c.Cell).ToHashSet();
            Assert.True(all.SetEquals(reached), $"cômodo preso em {building.Id} tipo {building.BuildingTypeId} giro {building.Orientation}");
            if (BuildingRooms.Of(building, ObjectCatalogTests.SampleCatalog()).Floors[0].Rooms.Count > 1) divided++;
        }

        Assert.True(divided > 50, "o teste não pode passar sem dividir casas");
    }

    [Fact]
    public void A_4_by_4_house_has_at_least_two_rooms_and_every_cell_is_useful_floor()
    {
        var catalog = ObjectCatalogTests.SampleCatalog();
        var house = new Building(new BuildingId(3), default, 3, 0, orientation: 0); // tipo ímpar = 4x4
        var layout = BuildingLayoutGenerator.Generate(house);
        var rooms = BuildingRooms.Of(house, catalog).Floors[0];

        Assert.Equal(16, layout.FloorLayouts[0].Cells.Count);
        Assert.True(rooms.Rooms.Count >= 2);
        Assert.Equal(16, rooms.Rooms.Sum(r => r.Cells.Count)); // cada célula em um cômodo só
        Assert.Equal(16, rooms.Rooms.SelectMany(r => r.Cells).Distinct().Count());
        double useful = layout.FloorLayouts[0].Cells.Count(c => InteriorWalkability.IsWalkable(c.Material)) / 16.0;
        Assert.True(useful >= 0.7, $"área útil {useful:P0}");
        Assert.All(rooms.Rooms, r => Assert.True(r.Cells.Count >= catalog.Rooms!.Kinds.Single(k => k.Id == r.Kind).MinCells));
    }

    [Fact]
    public void A_house_too_small_to_divide_keeps_one_room_and_without_the_catalog_there_is_one_room()
    {
        var catalog = ObjectCatalogTests.SampleCatalog();
        var tiny = new Building(new BuildingId(9), default, 4, 0, orientation: 0); // plano com um cômodo só
        Assert.Single(BuildingRooms.Of(tiny, catalog).Floors[0].Rooms);
        Assert.Empty(BuildingRooms.Of(tiny, catalog).Floors[0].Walls);

        var plain = new Building(new BuildingId(9), default, 3, 0, orientation: 0);
        var none = BuildingRooms.Of(plain, null);
        Assert.Single(none.Floors[0].Rooms);
        Assert.Empty(none.Floors[0].Walls);
    }

    [Fact]
    public void The_room_plan_is_deterministic_and_comes_from_the_catalog_not_from_the_code()
    {
        var catalog = ObjectCatalogTests.SampleCatalog();
        foreach (var building in Houses().Take(60))
        {
            var a = JsonSerializer.Serialize(BuildingRooms.Of(building, catalog).Floors);
            var again = new Building(building.Id, default, building.BuildingTypeId, 0, orientation: building.Orientation);
            Assert.Equal(a, JsonSerializer.Serialize(BuildingRooms.Of(again, catalog).Floors));
        }

        // outro catálogo (cômodos e proporções do autor) dá outra planta, sem mudar uma linha de código
        var custom = catalog with
        {
            Rooms = new RoomCatalog(
                [new RoomKind("salão", [3], 1, 2), new RoomKind("alcova", [1], 1, 2), new RoomKind("despensa", [0], 1, 2)],
                new Dictionary<string, IReadOnlyList<string>> { ["default"] = ["salão", "alcova", "despensa"] }),
        };
        var house = new Building(new BuildingId(3), default, 3, 0, orientation: 0);
        var kinds = BuildingRooms.Of(house, custom).Floors[0].Rooms.Select(r => r.Kind).ToList();
        Assert.Equal(["salão", "alcova", "despensa"], kinds);
    }

    [Fact]
    public void Furniture_goes_to_the_room_that_serves_its_use_and_keeps_a_path_to_the_door()
    {
        var catalog = ObjectCatalogTests.SampleCatalog();
        var kinds = catalog.Rooms!.Kinds.ToDictionary(k => k.Id);
        int beds = 0, tables = 0;
        foreach (var building in Houses())
        {
            var rooms = BuildingRooms.Of(building, catalog);
            if (rooms.Floors[0].Rooms.Count < 2) continue;
            foreach (var piece in HomeFurniture.Of(building, catalog).Where(p => p.Spot is not null))
            {
                var use = catalog.Find(piece.Id)!.UsePoints.Select(p => p.Action).FirstOrDefault(a => a is not null);
                if (piece.Spot!.Value.Floor != 0 || use is null) continue;
                var room = HomeFurniture.RoomOf(building, catalog, piece.Spot.Value)!;
                if (!kinds[room].Uses.Contains(use.Value) && rooms.Floors[0].Rooms.Any(r => kinds[r.Kind!].Uses.Contains(use.Value)))
                    Assert.Fail($"{piece.Id} no cômodo {room} (usa {use}) em {building.Id} tipo {building.BuildingTypeId}");
                if (piece.Id == "bed") beds++;
                if (piece.Id == "table") tables++;
            }

            // o caminho: a célula de cada peça posta não fecha nenhum cômodo (todo piso livre continua ligado à porta)
            var layout = BuildingLayoutGenerator.Generate(building);
            var taken = HomeFurniture.Of(building, catalog).Where(p => p.Spot is { Floor: 0 }).Select(p => p.Spot!.Value.Cell).ToHashSet();
            var walkable = layout.FloorLayouts[0].Cells.Select(c => c.Cell).ToHashSet();
            var door = layout.FloorLayouts[0].Cells.Single(c => c.Material == BuildingMaterial.Door).Cell;
            Assert.True(FurniturePlanner.AllReachable(walkable, taken, door, rooms.Blocked[0]), $"caminho fechado em {building.Id} tipo {building.BuildingTypeId}");
        }

        Assert.True(beds > 10 && tables > 10, $"o teste não pode passar vazio ({beds} camas, {tables} mesas)");
    }

    [Fact]
    public void A_catalog_without_rooms_still_loads_and_the_rooms_block_is_validated_with_its_path()
    {
        var root = new System.Text.Json.Nodes.JsonObject();
        DefaultObjects.AddTo(root);
        root["Rooms"]!["plans"]!["default"] = new System.Text.Json.Nodes.JsonArray("sala", "inexistente");
        var bad = ObjectCatalogScenarioLoader.Load(root);
        Assert.False(bad.IsSuccess);
        Assert.Contains("Rooms.plans.default", bad.Error);

        root.Remove("Rooms");
        var plain = ObjectCatalogScenarioLoader.Load(root);
        Assert.True(plain.IsSuccess);
        Assert.Null(plain.Value!.Rooms);
    }

    // --- o passo do corpo: parede em aresta, no motor e na máscara que o cliente consulta ---

    private static (WorldState World, List<Building> Houses) Village(ulong seed)
    {
        var (world, _) = ScenarioRunner.Create(seed, objectCatalog: ObjectCatalogTests.SampleCatalog());
        var houses = world.Buildings.Where(b => b.Position is not null && b.CompletedAtTick <= world.CurrentDate.TotalHours)
            .OrderBy(b => b.Id.Value).Take(12).ToList();
        return (world, houses);
    }

    [Fact]
    public void A_step_is_blocked_by_the_wall_edge_and_free_through_the_door_the_engine_and_the_mask_agree()
    {
        var (world, houses) = Village(11);
        int closed = 0, open = 0, doorSteps = 0;
        foreach (var house in houses)
        {
            var origin = house.Position!.Value;
            var rooms = BuildingRooms.Of(house, world.ObjectCatalog);
            var cells = BuildingLayoutGenerator.Generate(house).FloorLayouts[0].Cells.Select(c => c.Cell).ToHashSet();
            var (door, doorstep) = PlayerGround.DoorOf(house)!.Value;
            var mask = PlayerGround.Mask(world, 0, null, door, 6);
            int side = 13;
            bool Bit(CellCoord from, CellCoord to) =>
                (mask[(from.Y - (door.Y - 6)) * side + (from.X - (door.X - 6))] & (1 << ((to.Y - from.Y + 1) * 3 + (to.X - from.X + 1)))) != 0;
            foreach (var local in cells)
                foreach (var d in Four)
                {
                    var next = new CellCoord(local.X + d.X, local.Y + d.Y);
                    var from = new CellCoord(origin.X + local.X, origin.Y + local.Y);
                    var to = new CellCoord(origin.X + next.X, origin.Y + next.Y);
                    if (!cells.Contains(next) || Math.Abs(from.X - door.X) > 6 || Math.Abs(from.Y - door.Y) > 6
                        || Math.Abs(to.X - door.X) > 6 || Math.Abs(to.Y - door.Y) > 6) continue;
                    bool wall = rooms.Blocked[0].Contains(WallEdge.Between(local, next));
                    var refusal = PlayerGround.TerrainRefusal(world, 0, null, from, to, c => PlayerGround.At(world, c));
                    Assert.Equal(wall, refusal is not null);
                    Assert.Equal(!wall, Bit(from, to));
                    if (wall) closed++; else open++;
                }

            // fora para dentro: só da soleira para a porta (a abertura), nunca por uma parede
            Assert.Null(PlayerGround.TerrainRefusal(world, 0, null, doorstep, door, c => PlayerGround.At(world, c)));
            Assert.Null(PlayerGround.TerrainRefusal(world, 0, house.Id, door, doorstep, c => PlayerGround.At(world, c)));
            foreach (var local in cells.Where(c => new CellCoord(origin.X + c.X, origin.Y + c.Y) != door))
                foreach (var d in Four)
                {
                    var outside = new CellCoord(local.X + d.X, local.Y + d.Y);
                    if (cells.Contains(outside)) continue;
                    var wout = new CellCoord(origin.X + outside.X, origin.Y + outside.Y);
                    Assert.NotNull(PlayerGround.TerrainRefusal(world, 0, null, wout, new CellCoord(origin.X + local.X, origin.Y + local.Y), c => PlayerGround.At(world, c)));
                }

            doorSteps++;
        }

        Assert.True(closed > 20 && open > 50 && doorSteps > 5, $"fechados {closed}, abertos {open}, casas {doorSteps}");
    }

    [Fact]
    public void The_body_walks_every_room_of_every_house_without_an_invisible_block()
    {
        var (world, houses) = Village(12);
        int rooms = 0;
        foreach (var house in houses)
        {
            var origin = house.Position!.Value;
            var cells = BuildingLayoutGenerator.Generate(house).FloorLayouts[0].Cells.Select(c => new CellCoord(origin.X + c.Cell.X, origin.Y + c.Cell.Y)).ToHashSet();
            var (door, doorstep) = PlayerGround.DoorOf(house)!.Value;
            var seen = new HashSet<CellCoord> { doorstep };
            var queue = new Queue<CellCoord>([doorstep]);
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                foreach (var d in Four)
                {
                    var next = new CellCoord(cell.X + d.X, cell.Y + d.Y);
                    if (!cells.Contains(next) && next != doorstep) continue;
                    if (PlayerGround.TerrainRefusal(world, 0, null, cell, next, c => PlayerGround.At(world, c)) is null && seen.Add(next)) queue.Enqueue(next);
                }
            }

            Assert.True(cells.IsSubsetOf(seen), $"há cômodo sem caminho a pé na casa {house.Id} tipo {house.BuildingTypeId}");
            rooms += BuildingRooms.Of(house, world.ObjectCatalog).Floors[0].Rooms.Count;
        }

        Assert.True(rooms > houses.Count, "algum prédio tem de ter mais de um cômodo");
    }
}
