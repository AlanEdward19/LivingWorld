using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cities;
using LivingWorld.Simulation.Objects;

namespace LivingWorld.Tests.Unit.Objects;

/// <summary>#237: a mobília é posta pelo motor só em piso, fora da porta e da célula à frente dela, e sempre com caminho; quem dorme deita
/// na cama ou em piso livre, nunca na porta se houver piso.</summary>
public class HomeFurnitureTests
{
    private static readonly int[] Types = [-1, 0, 1, 2, 3, 4, 7, 10, 11, 14, 21];

    private static IEnumerable<Building> Houses()
    {
        foreach (var type in Types)
            for (long id = 1; id <= 24; id++)
                foreach (int orientation in new[] { 0, 90, 180, 270 })
                    yield return new Building(new BuildingId(id), default, type, 0, orientation: orientation);
    }

    private static (Dictionary<CellCoord, BuildingMaterial> Plan, HashSet<CellCoord> Walkable) Floor(BuildingLayout layout, int floor)
    {
        var plan = layout.FloorLayouts[floor].Cells.ToDictionary(c => c.Cell, c => c.Material);
        return (plan, plan.Where(kv => InteriorWalkability.IsWalkable(kv.Value)).Select(kv => kv.Key).ToHashSet());
    }

    [Fact]
    public void Every_generated_house_has_furniture_only_on_floor_away_from_the_door_with_a_path_to_every_free_cell()
    {
        var catalog = ObjectCatalogTests.SampleCatalog();
        int placed = 0;
        foreach (var building in Houses())
            foreach (int lodging in new[] { 0, 3 })
            {
                var layout = BuildingLayoutGenerator.Generate(building);
                var pieces = HomeFurniture.Of(building, catalog, lodging).Where(p => p.Spot is not null).Select(p => p.Spot!.Value).ToList();
                foreach (var floor in pieces.GroupBy(s => s.Floor))
                {
                    var (plan, walkable) = Floor(layout, floor.Key);
                    var cells = floor.Select(s => s.Cell).ToList();
                    Assert.Equal(cells.Count, cells.Distinct().Count()); // uma peça por célula
                    var start = FurniturePlanner.StartOf(plan, floor.Key)!.Value;
                    var entry = FurniturePlanner.EntryOf(plan, start, floor.Key);
                    foreach (var cell in cells)
                    {
                        Assert.Equal(BuildingMaterial.Floor, plan[cell]); // nem parede, nem porta, nem escada
                        Assert.NotEqual(entry, cell); // nem a célula à frente da porta
                        placed++;
                    }

                    Assert.True(FurniturePlanner.AllReachable(walkable, cells.ToHashSet(), start), $"caminho fechado em {building.Id} tipo {building.BuildingTypeId}");
                }
            }

        Assert.True(placed > 100, "o teste não pode passar vazio");
    }

    [Fact]
    public void A_bed_sits_against_a_wall_with_its_back_to_it()
    {
        var catalog = ObjectCatalogTests.SampleCatalog();
        foreach (var building in Houses().Where(b => b.BuildingTypeId == 3))
        {
            var layout = BuildingLayoutGenerator.Generate(building);
            foreach (var spot in HomeFurniture.Of(building, catalog).Where(p => p.Spot is not null).Select(p => p.Spot!.Value))
            {
                var (plan, _) = Floor(layout, spot.Floor);
                Assert.Equal(spot.Facing, FurniturePlanner.FacingOf(plan, spot.Cell) ?? 0);
                Assert.Contains(spot.Facing, new[] { 0, 90, 180, 270 });
            }
        }
    }

    [Fact]
    public void The_plan_is_pure()
    {
        var catalog = ObjectCatalogTests.SampleCatalog();
        foreach (var building in Houses().Take(200))
            Assert.True(HomeFurniture.Of(building, catalog, 2).SequenceEqual(HomeFurniture.Of(new Building(building.Id, default, building.BuildingTypeId, 0, orientation: building.Orientation), catalog, 2)));
    }

    [Fact]
    public void Sleepers_never_lie_on_the_door_or_the_stair_when_the_house_has_free_floor_and_use_a_bed_when_it_exists()
    {
        var catalog = ObjectCatalogTests.SampleCatalog();
        foreach (var building in Houses())
        {
            var layout = BuildingLayoutGenerator.Generate(building);
            var beds = HomeFurniture.BedSpots(building, catalog);
            bool hasFloor = layout.FloorLayouts.Any(f => f.Cells.Any(c => c.Material == BuildingMaterial.Floor));
            for (long n = 1; n <= 12; n++)
            {
                var (_, floor, cell) = BuildingOccupancySystem.Place(new NpcId(n), building, groundOnly: false, catalog);
                var material = layout.FloorLayouts[floor.Value].Cells.Single(c => c.Cell == cell).Material;
                if (beds.Count > 0) Assert.Contains(beds, b => b.Floor == floor.Value && b.Cell == cell);
                else if (hasFloor) Assert.Equal(BuildingMaterial.Floor, material);
                else Assert.NotEqual(BuildingMaterial.Door, material); // casa 3x3: o centro é escada; a porta fica livre
            }
        }
    }
}
