using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Cities;

/// <summary>dynamic-city-growth, T1 (CITYGROW-01/02): <see cref="CityOccupancy"/> — livre/ocupado
/// derivado de <see cref="WorldState.Buildings"/> via <see cref="BuildingFootprintGenerator"/>,
/// nunca um grid próprio persistido.</summary>
public class CityOccupancyTests
{
    private static readonly GeographyCatalog TinyCatalog = new(
        TerrainIds: new HashSet<int> { 1 }, BiomeIds: new HashSet<int> { 1 }, ResourceIds: new HashSet<int>());

    private static readonly CostWeights TinyCostWeights = new(
        Base: 1.0, AltitudeWeight: 0.5, TerrainWeight: new Dictionary<int, double> { [1] = 1.0 });

    private static WorldState BuildWorldWithMap(int width, int height, ulong seed)
    {
        var map = MapGenerator.Generate(seed, width, height, Math.Max(width, height), TinyCatalog, TinyCostWeights, [])
            .Value ?? throw new InvalidOperationException("mapa de teste inválido — bug no teste, não no gerador");
        return new WorldState(
            ScenarioRunner.DefaultCalendar, seed, map, ScenarioRunner.DefaultPopulationCatalog,
            ScenarioRunner.DefaultPopulationRules, ScenarioRunner.DefaultNeedsRules,
            ScenarioRunner.DefaultActionCatalog, ScenarioRunner.DefaultLifeStageRules);
    }

    /// <summary>Procura, dentro de um intervalo pequeno de ids, um footprint que seja um
    /// retângulo perfeito (sem o entalhe do formato L) — necessário para os testes de "bounds
    /// totalmente ocupados", onde a bounding box precisa coincidir exatamente com o footprint.</summary>
    private static (BuildingId Id, IReadOnlyList<CellCoord> Shape, int Width, int Height) FindRectangularFootprint(int typeId)
    {
        for (long i = 1; i < 200; i++)
        {
            var id = new BuildingId(i);
            var cells = BuildingFootprintGenerator.Generate(id, typeId);
            int width = cells.Max(c => c.Cell.X) + 1;
            int height = cells.Max(c => c.Cell.Y) + 1;
            if (cells.Count == width * height)
                return (id, cells.Select(c => c.Cell).ToList(), width, height);
        }
        throw new InvalidOperationException("nenhum footprint rectangular encontrado no intervalo testado");
    }

    // --- IsFree ---

    [Fact]
    public void IsFree_returns_false_for_a_candidate_overlapping_an_existing_buildings_footprint()
    {
        var world = ScenarioRunner.Create(seed: 601, initialPopulation: 0).World;
        var city = new City(world.NextCityId(), new CellCoord(2, 2), 0, null, AggregatePopulationPool.Empty);
        world.AddCity(city);
        var existing = new Building(
            world.NextBuildingIdAndAdvance(), city.Id, buildingTypeId: 1, completedAtTick: 0,
            position: new CellCoord(2, 2), orientation: 0);
        world.AddBuilding(existing);
        var existingShape = BuildingFootprintGenerator.Generate(existing.Id, existing.BuildingTypeId).Select(c => c.Cell).ToList();
        var bounds = new CityBounds(new CellCoord(0, 0), 20, 20);

        var sameCells = CityOccupancy.Translate(existingShape, existing.Position!.Value);

        Assert.False(CityOccupancy.IsFree(world, city, bounds, sameCells));
    }

    [Fact]
    public void IsFree_returns_true_for_a_candidate_far_from_every_existing_footprint()
    {
        var world = ScenarioRunner.Create(seed: 602, initialPopulation: 0).World;
        var city = new City(world.NextCityId(), new CellCoord(2, 2), 0, null, AggregatePopulationPool.Empty);
        world.AddCity(city);
        var existing = new Building(
            world.NextBuildingIdAndAdvance(), city.Id, buildingTypeId: 1, completedAtTick: 0,
            position: new CellCoord(2, 2), orientation: 0);
        world.AddBuilding(existing);
        var existingShape = BuildingFootprintGenerator.Generate(existing.Id, existing.BuildingTypeId).Select(c => c.Cell).ToList();
        var bounds = new CityBounds(new CellCoord(0, 0), 20, 20);

        var farCells = CityOccupancy.Translate(existingShape, new CellCoord(500, 500));

        Assert.True(CityOccupancy.IsFree(world, city, bounds, farCells));
    }

    // --- FindFreeCellInBounds ---

    [Fact]
    public void FindFreeCellInBounds_is_deterministic_and_never_returns_an_overlapping_origin()
    {
        var world = ScenarioRunner.Create(seed: 603, initialPopulation: 0).World;
        var city = new City(world.NextCityId(), new CellCoord(2, 2), 0, null, AggregatePopulationPool.Empty);
        world.AddCity(city);
        var existing = new Building(
            world.NextBuildingIdAndAdvance(), city.Id, buildingTypeId: 1, completedAtTick: 0,
            position: new CellCoord(2, 2), orientation: 0);
        world.AddBuilding(existing);
        var bounds = new CityBounds(new CellCoord(0, 0), 20, 20);
        var newShape = BuildingFootprintGenerator.Generate(new BuildingId(999), 1).Select(c => c.Cell).ToList();

        var first = CityOccupancy.FindFreeCellInBounds(world, city, bounds, newShape);
        var second = CityOccupancy.FindFreeCellInBounds(world, city, bounds, newShape);

        Assert.NotNull(first);
        Assert.Equal(first, second); // mesmo id/shape, mesmo estado do mundo -> mesmo resultado, sem RNG
        var translated = CityOccupancy.Translate(newShape, first!.Value);
        Assert.True(CityOccupancy.IsFree(world, city, bounds, translated));
    }

    [Fact]
    public void FindFreeCellInBounds_returns_null_when_bounds_are_fully_occupied()
    {
        var (rectId, rectShape, w, h) = FindRectangularFootprint(typeId: 5);
        var world = ScenarioRunner.Create(seed: 604, initialPopulation: 0).World;
        var city = new City(world.NextCityId(), new CellCoord(50, 50), 0, null, AggregatePopulationPool.Empty);
        world.AddCity(city);
        var rectBuilding = new Building(rectId, city.Id, buildingTypeId: 5, completedAtTick: 0, position: new CellCoord(0, 0), orientation: 0);
        world.AddBuilding(rectBuilding);
        var bounds = new CityBounds(new CellCoord(0, 0), w, h); // exatamente a bounding box do único prédio existente

        var found = CityOccupancy.FindFreeCellInBounds(world, city, bounds, rectShape);

        Assert.Null(found);
    }

    [Fact]
    public void FindFreeCellInBounds_returns_an_in_bounds_free_origin_when_bounds_have_room_beyond_the_existing_building()
    {
        var (rectId, rectShape, w, h) = FindRectangularFootprint(typeId: 5);
        var world = ScenarioRunner.Create(seed: 605, initialPopulation: 0).World;
        var city = new City(world.NextCityId(), new CellCoord(50, 50), 0, null, AggregatePopulationPool.Empty);
        world.AddCity(city);
        var rectBuilding = new Building(rectId, city.Id, buildingTypeId: 5, completedAtTick: 0, position: new CellCoord(0, 0), orientation: 0);
        world.AddBuilding(rectBuilding);
        var bounds = new CityBounds(new CellCoord(0, 0), w * 2, h); // espaço extra ao lado do prédio existente

        var found = CityOccupancy.FindFreeCellInBounds(world, city, bounds, rectShape);

        Assert.NotNull(found);
        var translated = CityOccupancy.Translate(rectShape, found!.Value);
        Assert.True(translated.All(bounds.Contains));
        Assert.True(CityOccupancy.IsFree(world, city, bounds, translated));
    }

    // --- IsLandScarce ---

    [Fact]
    public void IsLandScarce_is_true_when_a_whole_map_scan_finds_zero_free_cells()
    {
        var (rectId, _, w, h) = FindRectangularFootprint(typeId: 5);
        var world = BuildWorldWithMap(w, h, seed: 606); // mapa do tamanho exato do único prédio
        var city = new City(world.NextCityId(), new CellCoord(0, 0), 0, null, AggregatePopulationPool.Empty);
        world.AddCity(city);
        var rectBuilding = new Building(rectId, city.Id, buildingTypeId: 5, completedAtTick: 0, position: new CellCoord(0, 0), orientation: 0);
        world.AddBuilding(rectBuilding);

        var scarce = CityOccupancy.IsLandScarce(world, city, [new CellCoord(0, 0)]);

        Assert.True(scarce);
    }

    [Fact]
    public void IsLandScarce_is_false_when_a_free_cell_still_exists_anywhere_on_the_map()
    {
        var (rectId, _, w, h) = FindRectangularFootprint(typeId: 5);
        var world = BuildWorldWithMap(w + 1, h, seed: 607); // uma coluna extra sempre livre
        var city = new City(world.NextCityId(), new CellCoord(0, 0), 0, null, AggregatePopulationPool.Empty);
        world.AddCity(city);
        var rectBuilding = new Building(rectId, city.Id, buildingTypeId: 5, completedAtTick: 0, position: new CellCoord(0, 0), orientation: 0);
        world.AddBuilding(rectBuilding);

        var scarce = CityOccupancy.IsLandScarce(world, city, [new CellCoord(0, 0)]);

        Assert.False(scarce);
    }
}
