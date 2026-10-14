using System.Text.Json.Nodes;
using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Cities.Spatial;
using LivingWorld.Domain.Geography;
using LivingWorld.Simulation.Cities.Construction;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Cities;

/// <summary>city-building-separation: prédios derivados nunca encostam (folga mínima de
/// <see cref="CityOccupancy.MinGap"/> célula) nem pousam em água, e a busca continua determinística.</summary>
public class BuildingSeparationTests
{
    private static (WorldState World, City City, CityBounds Bounds) CityWithWholeMapBounds(ulong seed)
    {
        var world = ScenarioRunner.Create(seed: seed, initialPopulation: 80).World; // mapa maior que o 10x10 padrão
        var center = new CellCoord(world.Map.Width / 2, world.Map.Height / 2);
        var city = new City(world.NextCityId(), center, 0, null, AggregatePopulationPool.Empty);
        world.AddCity(city);
        return (world, city, new CityBounds(new CellCoord(0, 0), world.Map.Width, world.Map.Height));
    }

    private static List<Building> PlaceDerived(WorldState world, City city, CityBounds bounds, int count)
    {
        var placed = new List<Building>();
        for (int i = 0; i < count; i++)
        {
            var building = new Building(world.NextBuildingIdAndAdvance(), city.Id, buildingTypeId: 1, completedAtTick: 0);
            var resolved = BuildingPlacementResolver.Resolve(building, city, world, bounds);
            Assert.NotNull(resolved);
            placed.Add(new Building(
                building.Id, city.Id, building.BuildingTypeId, 0, resolved!.Value.Position, resolved.Value.Orientation));
            world.AddBuilding(placed[^1]);
        }
        return placed;
    }

    private static List<CellCoord> Cells(Building building) =>
        BuildingFootprintGenerator.Generate(building)
            .Select(c => new CellCoord(c.Cell.X + building.Position!.Value.X, c.Cell.Y + building.Position!.Value.Y)).ToList();

    [Fact]
    public void Derived_buildings_in_one_city_keep_at_least_one_free_cell_between_footprints()
    {
        var (world, city, bounds) = CityWithWholeMapBounds(seed: 2024);
        var placed = PlaceDerived(world, city, bounds, 20);

        for (int a = 0; a < placed.Count; a++)
            for (int b = a + 1; b < placed.Count; b++)
            {
                var cellsB = Cells(placed[b]).ToHashSet();
                foreach (var cell in Cells(placed[a]))
                    for (int dx = -1; dx <= 1; dx++)
                        for (int dy = -1; dy <= 1; dy++)
                            Assert.DoesNotContain(new CellCoord(cell.X + dx, cell.Y + dy), cellsB);
            }
    }

    [Fact]
    public void Derived_buildings_never_stand_on_water()
    {
        var (world, city, bounds) = CityWithWholeMapBounds(seed: 2024);
        Assert.Contains(world.Map.Cells, c => c.HasWater); // sem água o teste não provaria nada

        foreach (var building in PlaceDerived(world, city, bounds, 20))
            foreach (var cell in Cells(building))
                Assert.False(world.Map.TryGetCell(cell, out var mapCell) && mapCell.HasWater, $"prédio {building.Id.Value} sobre água em {cell}");
    }

    [Fact]
    public void Derived_placement_is_deterministic()
    {
        var first = CityWithWholeMapBounds(seed: 2024);
        var second = CityWithWholeMapBounds(seed: 2024);

        var a = PlaceDerived(first.World, first.City, first.Bounds, 12).Select(b => b.Position).ToList();
        var b = PlaceDerived(second.World, second.City, second.Bounds, 12).Select(x => x.Position).ToList();

        Assert.Equal(a, b);
    }

    [Fact]
    public void Seeding_more_households_than_the_ground_fits_shares_floors_instead_of_stacking_footprints()
    {
        var root = JsonNode.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "scenarios", "periods", "medieval.json")))!;
        root["InitialPopulation"] = 30; // mapa 10x10: o chão não comporta uma casa 3x3 (com folga) por domicílio, mas os andares comportam

        var result = ScenarioLoaderV2.LoadWorld(root.ToJsonString());

        Assert.True(result.IsSuccess, result.Error);
        var world = result.Value.Item1;
        var footprints = world.Buildings.Select(b => Cells(b).ToHashSet()).ToList();
        for (int a = 0; a < footprints.Count; a++)
            for (int b = a + 1; b < footprints.Count; b++)
                Assert.False(footprints[a].Overlaps(footprints[b]), $"prédios {a} e {b} sobrepostos");

        var tenantsByDwelling = world.Households.Where(h => h.Dwelling is not null).GroupBy(h => h.Dwelling!.Value).ToList();
        Assert.Contains(tenantsByDwelling, g => g.Count() > 1); // o caminho de andares compartilhados rodou
        foreach (var dwelling in tenantsByDwelling)
        {
            var house = world.Buildings.Single(b => b.Id == dwelling.Key);
            Assert.True(dwelling.Count() <= BuildingLayoutGenerator.FloorsOf(house.Id, house.BuildingTypeId), $"casa {house.Id.Value} com mais domicílios que andares");
        }
    }

    [Fact]
    public void No_building_footprint_leaves_the_world_map_even_when_the_ground_runs_out()
    {
        var root = JsonNode.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "scenarios", "periods", "medieval.json")))!;
        root["InitialPopulation"] = 90; // além do que chão e andares comportam: cai no último recurso

        var world = ScenarioLoaderV2.LoadWorld(root.ToJsonString()).Value.Item1;

        foreach (var building in world.Buildings)
            foreach (var cell in Cells(building))
                Assert.True(
                    cell.X >= 0 && cell.X < world.Map.Width && cell.Y >= 0 && cell.Y < world.Map.Height,
                    $"prédio {building.Id.Value} com célula fora do mapa em {cell}");
    }

    [Fact]
    public void An_authored_workplace_is_placed_before_the_seeded_houses_fill_a_small_map()
    {
        // Preset pequeno: sem cidade autorada (a loader funda a vila), 150 moradores num mapa 20x20. Antes as casas
        // semeadas ocupavam o terreno todo e a oficina autorada falhava com "nenhuma célula livre".
        var root = JsonNode.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "scenarios", "default.json")))!;
        root["Width"] = 20;
        root["Height"] = 20;
        root["InitialPopulation"] = 150;
        root["Cities"] = new JsonArray();
        root["BuildingRecipes"]!["12"] = new JsonObject { ["Inputs"] = new JsonObject(), ["TicksToBuild"] = 1, ["HousingCapacityProvided"] = 0 };
        ((JsonArray)root["Workplaces"]!).Add(new JsonObject
        {
            ["LocationTypeId"] = 12,
            ["X"] = 6,
            ["Y"] = 6,
            ["MaxVacancies"] = 2,
            ["Treasury"] = 0,
            ["Stock"] = new JsonObject(),
            ["Prices"] = new JsonObject(),
        });

        var result = ScenarioLoaderV2.LoadWorld(root.ToJsonString());

        Assert.True(result.IsSuccess, result.Error);
        var world = result.Value.Item1;
        var authored = world.Workplaces.Single(w => w.LocationType.Id == 12);
        Assert.NotNull(authored.Building); // virou prédio de verdade, em terreno livre
    }

    private static string RepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "LivingWorld.sln")))
            dir = Directory.GetParent(dir)?.FullName;
        return dir ?? throw new InvalidOperationException("LivingWorld.sln não encontrado");
    }
}
