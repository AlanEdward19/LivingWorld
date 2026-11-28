using System.Diagnostics;
using System.Text.Json.Nodes;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Objects;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cities;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Objects;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Tests.Unit.Cities;

namespace LivingWorld.Tests.Unit.Objects;

/// <summary>#266 (épico #246): cada móvel do catálogo declara a pegada em células e o modelo em metros; o motor põe e valida a pegada inteira (todas as células em piso, nenhuma de outra
/// peça, nada atravessando parede, lado livre para usar, porta e passagem livres), em qualquer giro e em qualquer tamanho de casa do sandbox.</summary>
public class HomeFootprintTests
{
    private const double CellM = 1.5;
    private const double WallM = 0.22 * CellM; ///< espessura da parede do cliente (wall_panels.gd) em metros

    private static (WorldState World, Building House) Sandbox(Action<JsonObject>? edit = null, ulong seed = 266)
    {
        var root = SandboxFixtures.Parse();
        var households = root["AuthoredHouseholds"]!.AsArray();
        while (households.Count > 1) households.RemoveAt(households.Count - 1);
        root["Seed"] = seed;
        edit?.Invoke(root);
        var result = ScenarioLoaderV2.LoadWorld(root.ToJsonString());
        Assert.True(result.IsSuccess, result.Error);
        var world = result.Value.World;
        var body = world.Npcs.Single(n => n.Name == "Marina");
        return (world, world.FindBuilding(world.FindHousehold(body.Household!.Value)!.Dwelling!.Value)!);
    }

    private static void WithShape(JsonObject root, int width, int depth)
    {
        var shape = root["Rooms"]!["shapes"]!["-1"]!.AsObject();
        shape["width"] = width;
        shape["depth"] = depth;
    }

    [Fact]
    public void The_catalog_declares_a_footprint_and_a_model_in_meters_that_fit_inside_it()
    {
        var (world, _) = Sandbox();
        var catalog = world.ObjectCatalog!;
        int modeled = 0;
        foreach (var descriptor in catalog.Objects.Where(o => o.Model is not null))
        {
            var (w, d) = ObjectFootprint.SizeOf(descriptor);
            var size = descriptor.Model!.SizeM;
            // a pegada menos a meia espessura da parede (0,22 de célula) de cada lado: cabe mesmo com parede dos dois lados
            Assert.True(size[0] <= w * CellM - WallM && size[1] <= d * CellM - WallM, $"{descriptor.Id}: {size[0]}×{size[1]} m não cabe na pegada livre {w}×{d} ({w * CellM - WallM}×{d * CellM - WallM} m)");
            Assert.NotEmpty(descriptor.Model.Kind);
            modeled++;
        }

        Assert.True(modeled >= 16, "a mobília inteira tem modelo");
        Assert.Equal((1, 2), ObjectFootprint.SizeOf(catalog.Find("bed")));
        Assert.Equal((2, 1), ObjectFootprint.SizeOf(catalog.Find("sofa")));
        Assert.Equal((1, 1), ObjectFootprint.SizeOf(catalog.Find("fridge")));
        Assert.Equal((1, 1), ObjectFootprint.SizeOf(null));
    }

    [Fact]
    public void The_footprint_turns_around_its_origin_with_the_back_where_the_wall_is()
    {
        // cama 1×2 em (5,5): no giro 0 o fundo é o norte e a pegada desce; em 90 o fundo é o oeste e a pegada vai para o leste
        Assert.Equal(new[] { (5, 5), (5, 6) }, ObjectFootprint.Cells(5, 5, 1, 2, 0).ToArray());
        Assert.Equal(new[] { (5, 5), (6, 5) }, ObjectFootprint.Cells(5, 5, 1, 2, 90).ToArray());
        Assert.Equal(new[] { (5, 5), (5, 4) }, ObjectFootprint.Cells(5, 5, 1, 2, 180).ToArray());
        Assert.Equal(new[] { (5, 5), (4, 5) }, ObjectFootprint.Cells(5, 5, 1, 2, 270).ToArray());
        foreach (int facing in new[] { 0, 90, 180, 270 })
        {
            var (bx, by) = ObjectFootprint.Back(facing);
            var (fx, fy) = ObjectFootprint.Turn(0, 1, facing); // a frente é o contrário do fundo
            Assert.Equal((-bx, -by), (fx, fy));
        }
    }

    [Fact]
    public void Every_sandbox_house_gets_its_furniture_inside_the_floor_without_overlap_or_crossing_a_wall_in_any_rotation()
    {
        int checkedPieces = 0;
        foreach (var (w, d) in new[] { (5, 4), (6, 5), (7, 6), (8, 6), (8, 7), (10, 8) })
            foreach (ulong seed in new ulong[] { 266, 7, 91 })
            {
                var (world, house) = Sandbox(r => WithShape(r, w, d), seed);
                var catalog = world.ObjectCatalog!;
                var layout = BuildingLayoutCache.Of(house).Layout.FloorLayouts[0];
                var rooms = BuildingRooms.Of(house, catalog);
                var walls = rooms.Blocked[0];
                var floor = layout.Cells.Where(c => c.Material == BuildingMaterial.Floor).Select(c => c.Cell).ToHashSet();
                var walkable = layout.Cells.Where(c => InteriorWalkability.IsWalkable(c.Material)).Select(c => c.Cell).ToHashSet();
                var pieces = HomeFurniture.Of(house, catalog);
                var occupied = new Dictionary<CellCoord, string>();
                var materials = layout.Cells.ToDictionary(c => c.Cell, c => c.Material);
                var entry = FurniturePlanner.EntryOf(materials, FurniturePlanner.StartOf(materials, 0)!.Value, 0);
                var doorways = HomeEditor.Doorways(rooms.Floors[0], entry);
                foreach (var piece in pieces.Where(p => p.Spot is not null))
                {
                    var spot = piece.Spot!.Value;
                    var body = FurnitureBody.Of(catalog.Find(piece.Id), spot.Cell, spot.Facing);
                    var cells = body.Cells.ToList();
                    Assert.Equal(body.Width * body.Depth, cells.Count);
                    foreach (var cell in cells)
                    {
                        Assert.True(floor.Contains(cell), $"{piece.Id} em {cell}: a pegada sai do piso ({w}×{d})");
                        Assert.True(occupied.TryAdd(cell, piece.Id), $"{piece.Id} e {occupied.GetValueOrDefault(cell)} dividem a célula {cell}");
                        Assert.DoesNotContain(cell, doorways);
                        foreach (var around in HomeEditor.Around)
                        {
                            var next = new CellCoord(cell.X + around.X, cell.Y + around.Y);
                            if (cells.Contains(next)) Assert.False(walls.Contains(WallEdge.Between(cell, next)), $"{piece.Id} atravessa uma parede em {cell}");
                        }
                    }

                    checkedPieces++;
                }

                // com a casa pronta, cada peça ainda tem um lado livre e a porta alcança todo o piso que sobrou
                foreach (var piece in pieces.Where(p => p.Spot is not null))
                {
                    var spot = piece.Spot!.Value;
                    Assert.True(FurniturePlacement.HasAccess(FurnitureBody.Of(catalog.Find(piece.Id), spot.Cell, spot.Facing), occupied.Keys.ToHashSet(), walls, walkable), $"{piece.Id} sem lado livre ({w}×{d})");
                }

                Assert.True(HomeEditor.AllConnected(layout, rooms.Floors[0], occupied.Keys.ToHashSet()), $"porta sem caminho em {w}×{d}");
            }

        Assert.True(checkedPieces > 60, "o teste não pode passar vazio");
    }

    [Fact]
    public void The_sandbox_house_gets_every_piece_placed_with_the_bed_and_the_sofa_against_a_wall_and_the_footprints_the_catalog_gives()
    {
        var (world, house) = Sandbox();
        var catalog = world.ObjectCatalog!;
        var pieces = HomeFurniture.Of(house, catalog);
        var rooms = BuildingRooms.Of(house, catalog);
        Assert.All(pieces, p => Assert.NotNull(p.Spot)); // a casa 7×6 comporta tudo o que o cenário manda
        foreach (var id in new[] { "bed", "sofa", "fridge", "stove" })
        {
            var spot = pieces.First(p => p.Id == id).Spot!.Value;
            Assert.True(FurniturePlacement.BackOnWall(FurnitureBody.Of(catalog.Find(id), spot.Cell, spot.Facing), rooms.Blocked[0]), $"{id} com o fundo fora da parede");
            Assert.Equal(id == "bed" ? "quarto" : id == "sofa" ? "sala" : "cozinha", HomeFurniture.RoomOf(house, catalog, spot));
        }

        var bed = pieces.First(p => p.Id == "bed").Spot!.Value;
        var bedCells = FurnitureBody.Of(catalog.Find("bed"), bed.Cell, bed.Facing).Cells.ToList();
        Assert.Equal(2, bedCells.Count);
        Assert.All(bedCells, c => Assert.Equal("quarto", rooms.RoomAt(0, c)?.Kind)); // a cama inteira dentro do quarto
    }

    [Fact]
    public void The_chairs_the_catalog_wants_near_the_table_sit_against_it_facing_it()
    {
        foreach (ulong seed in new ulong[] { 266, 7, 91, 5 })
        {
            var (world, house) = Sandbox(seed: seed);
            var catalog = world.ObjectCatalog!;
            Assert.Equal(["table"], catalog.Find("chair")!.Near);
            var pieces = HomeFurniture.Of(house, catalog);
            var table = pieces.Single(p => p.Id == "table").Spot!.Value.Cell;
            // a primeira cadeira senta junto da mesa, de frente para ela; a segunda faz o mesmo se a cozinha ainda deixa o caminho aberto (senão vai para a parede)
            var facing = pieces.Where(p => p.Id == "chair").Select(chair =>
            {
                var spot = chair.Spot!.Value;
                var (bx, by) = ObjectFootprint.Back(spot.Facing);
                return new CellCoord(spot.Cell.X - bx, spot.Cell.Y - by) == table; // a frente da cadeira (o lado oposto ao fundo) é a mesa
            }).ToList();
            Assert.True(facing[0], $"a primeira cadeira não senta de frente para a mesa (semente {seed})");
        }
    }

    [Fact]
    public void Placing_or_moving_refuses_overlap_of_any_cell_of_the_footprint_crossing_a_wall_and_a_blocked_side()
    {
        var (world, house) = Sandbox();
        var catalog = world.ObjectCatalog!;
        var home = catalog.Home!;
        var pieces = HomeFurniture.Of(house, catalog);
        var rooms = BuildingRooms.Of(house, catalog);
        var walls = rooms.Blocked[0];
        var sofa = pieces.First(p => p.Id == "sofa").Spot!.Value;
        var sofaCells = FurnitureBody.Of(catalog.Find("sofa"), sofa.Cell, sofa.Facing).Cells.ToList();
        string? Refuse(string id, CellCoord cell, int facing) => HomeCommands.Placement(house, catalog, home, pieces, -1, home.Item(id)!, 0, cell, facing);

        // um móvel de 1 célula em cima da segunda célula do sofá (não só na de origem) é recusado
        Assert.Contains("já tem um móvel", Refuse("plant", sofaCells[1], 0));
        // uma cama de 1×2 que sobreporia a segunda célula do sofá também
        foreach (int facing in new[] { 0, 90, 180, 270 })
            foreach (var around in HomeEditor.Around)
            {
                var origin = new CellCoord(sofaCells[1].X + around.X, sofaCells[1].Y + around.Y);
                var body = FurnitureBody.Of(catalog.Find("bed"), origin, facing);
                if (body.Cells.Contains(sofaCells[1])) Assert.NotNull(Refuse("bed", origin, facing));
            }

        // nenhum giro de nenhuma célula aceita uma pegada que atravessa parede, e o que se aceita tem o lado livre e a pegada no piso
        var layout = BuildingLayoutCache.Of(house).Layout.FloorLayouts[0];
        int accepted = 0, crossing = 0;
        foreach (var cell in layout.Cells.Select(c => c.Cell))
            foreach (int facing in new[] { 0, 90, 180, 270 })
            {
                var cells = FurnitureBody.Of(catalog.Find("bed"), cell, facing).Cells.ToList();
                bool crosses = cells.Count == 2 && walls.Contains(WallEdge.Between(cells[0], cells[1]));
                var why = Refuse("bed", cell, facing);
                if (crosses) { crossing++; Assert.NotNull(why); }
                if (why is null) { accepted++; Assert.False(crosses); }
            }

        Assert.True(accepted > 0 && crossing > 0, "o teste precisa ver os dois casos");
    }

    [Fact]
    public void Planning_the_furniture_stays_cheap_and_is_cached_per_house()
    {
        var (world, house) = Sandbox();
        var catalog = world.ObjectCatalog!;
        int times = 200;
        var watch = Stopwatch.StartNew();
        for (int i = 0; i < times; i++)
        {
            var other = new Building(new BuildingId(1000 + i), default, -1, 0);
            HomeFurniture.Of(other, catalog, 0);
        }

        watch.Stop();
        Assert.True(watch.Elapsed.TotalMilliseconds / times < 25, $"planejar a mobília de uma casa levou {watch.Elapsed.TotalMilliseconds / times:F2} ms");
        Assert.Same(HomeFurniture.Of(house, catalog), HomeFurniture.Of(house, catalog)); // o resultado fica em cache
    }
}
