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

    [Fact]
    public void Each_resident_gets_a_place_of_their_own_the_beds_first_then_free_floor()
    {
        // #242: seis moradores deitavam todos na mesma célula (o hash dava a mesma cama); agora o morador de número r fica com o r-ésimo lugar
        var catalog = ObjectCatalogTests.SampleCatalog();
        int checkedHouses = 0;
        foreach (var building in Houses())
        {
            var layout = BuildingLayoutGenerator.Generate(building);
            var beds = HomeFurniture.BedSpots(building, catalog);
            int cells = layout.FloorLayouts.Sum(f => f.Cells.Count(c => c.Material == BuildingMaterial.Floor));
            int furnished = HomeFurniture.Of(building, catalog).Count(p => p.Spot is not null);
            int residents = Math.Min(6, beds.Count + Math.Max(0, cells - furnished - 1)); // as camas, mais o piso livre fora da mobília e da entrada
            if (residents < 2) continue;
            var places = Enumerable.Range(0, residents)
                .Select(r => BuildingOccupancySystem.Place(new NpcId(100 + r), building, groundOnly: false, catalog, rank: r)).ToList();
            Assert.Equal(places.Count, places.Select(p => (p.Item2.Value, p.Item3)).Distinct().Count());
            for (int r = 0; r < Math.Min(beds.Count, residents); r++)
                Assert.Contains(beds, b => b.Floor == places[r].Item2.Value && b.Cell == places[r].Item3);
            checkedHouses++;
        }

        Assert.True(checkedHouses > 0);
    }

    [Fact]
    public void The_resting_places_are_kept_but_never_change_the_answer_and_follow_the_catalog_in_use()
    {
        // #242: a lista dos lugares de dormir é guardada por prédio e catálogo; guardada ou não, o lugar de cada pessoa é o mesmo
        var catalog = ObjectCatalogTests.SampleCatalog();
        foreach (var building in Houses())
        {
            var first = Enumerable.Range(1, 12).Select(n => BuildingOccupancySystem.Place(new NpcId(n), building, groundOnly: false, catalog)).ToList();
            var again = Enumerable.Range(1, 12).Select(n => BuildingOccupancySystem.Place(new NpcId(n), building, groundOnly: false, catalog)).ToList();
            Assert.Equal(first, again);
            // sem catálogo (sem cama), outro catálogo na mesma casa: o lugar sai da planta, não da lista guardada para a cama
            var bare = Enumerable.Range(1, 12).Select(n => BuildingOccupancySystem.Place(new NpcId(n), building, groundOnly: false, null)).ToList();
            var layout = BuildingLayoutGenerator.Generate(building);
            foreach (var (_, floor, cell) in bare)
                Assert.Contains(layout.FloorLayouts[floor.Value].Cells, c => c.Cell == cell);
            Assert.Equal(first, Enumerable.Range(1, 12).Select(n => BuildingOccupancySystem.Place(new NpcId(n), building, groundOnly: false, catalog)).ToList());
        }
    }
}
