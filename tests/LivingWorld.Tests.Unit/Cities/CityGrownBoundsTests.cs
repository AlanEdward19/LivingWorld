using LivingWorld.Api.Visual;
using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Cities;

/// <summary>dynamic-city-growth, T4b (CITYGROW-03/05): <see cref="CityOccupancy.OwnedBuildingFootprintBoxes"/>
/// e <see cref="CityOccupancy.ResolveGrownBounds"/> — sem estes, T4 tornou
/// <see cref="CityBoundsResolver.Resolve"/> capaz de crescer, mas nenhum call site real (API/
/// tick) jamais alimentava boxes de overflow de volta, então bounds nunca cresciam fora de teste
/// unitário.</summary>
public class CityGrownBoundsTests
{
    private static readonly GeographyCatalog TinyCatalog = new(
        TerrainIds: new HashSet<int> { 1 }, BiomeIds: new HashSet<int> { 1 }, ResourceIds: new HashSet<int>());

    private static readonly CostWeights TinyCostWeights = new(
        Base: 1.0, AltitudeWeight: 0.5, TerrainWeight: new Dictionary<int, double> { [1] = 1.0 });

    /// <summary>dynamic-city-growth, fix (major, CITYGROW-02b): o overflow agora respeita
    /// <c>world.Map.Width/Height</c> de verdade -- este teste posiciona a cidade em (50,50), que
    /// não existe no mapa 10x10 padrão de <see cref="ScenarioRunner.Create"/> (usado só de boa fé
    /// antes do fix, quando o mapa era irrelevante pra ocupação); precisa de um mapa real grande
    /// o bastante pra conter a cidade e sobrar espaço de verdade.</summary>
    private static WorldState BuildWorldWithMap(int width, int height, ulong seed)
    {
        var map = MapGenerator.Generate(seed, width, height, Math.Max(width, height), TinyCatalog, TinyCostWeights, [])
            .Value ?? throw new InvalidOperationException("mapa de teste inválido — bug no teste, não no gerador");
        return new WorldState(
            ScenarioRunner.DefaultCalendar, seed, map, ScenarioRunner.DefaultPopulationCatalog,
            ScenarioRunner.DefaultPopulationRules, ScenarioRunner.DefaultNeedsRules,
            ScenarioRunner.DefaultActionCatalog, ScenarioRunner.DefaultLifeStageRules);
    }

    // --- OwnedBuildingFootprintBoxes ---

    [Fact]
    public void OwnedBuildingFootprintBoxes_returns_one_box_per_building_for_a_mix_of_authored_and_engine_placed()
    {
        var world = BuildWorldWithMap(200, 200, seed: 701);
        var city = new City(world.NextCityId(), new CellCoord(50, 50), 0, null, AggregatePopulationPool.Empty);
        world.AddCity(city);

        var authored = new Building(
            world.NextBuildingIdAndAdvance(), city.Id, buildingTypeId: 1, completedAtTick: 0,
            position: new CellCoord(52, 52), orientation: 0);
        world.AddBuilding(authored);
        var engineBuilt = new Building(world.NextBuildingIdAndAdvance(), city.Id, buildingTypeId: 1, completedAtTick: 0);
        world.AddBuilding(engineBuilt);

        var (populationBounds, _) = SpatialBoundsResolver.ResolveCity(city, population: 0, world.Map.Width, world.Map.Height);

        var boxes = CityOccupancy.OwnedBuildingFootprintBoxes(world, city, populationBounds);

        Assert.Equal(2, boxes.Count);

        var authoredShape = BuildingFootprintGenerator.Generate(authored.Id, authored.BuildingTypeId).Select(c => c.Cell).ToList();
        var authoredCells = CityOccupancy.Translate(authoredShape, authored.Position!.Value);
        var expectedAuthoredBox = new CityBounds(
            new CellCoord(authoredCells.Min(c => c.X), authoredCells.Min(c => c.Y)),
            authoredCells.Max(c => c.X) - authoredCells.Min(c => c.X) + 1,
            authoredCells.Max(c => c.Y) - authoredCells.Min(c => c.Y) + 1);
        Assert.Contains(expectedAuthoredBox, boxes);

        // O prédio sem posição autorada precisa aparecer como a mesma posição derivada que
        // BuildingPlacementResolver.Resolve escolheria pra ele — nunca um valor inventado aqui.
        var engineBuiltResolved = BuildingPlacementResolver.Resolve(engineBuilt, city, world, populationBounds);
        Assert.NotNull(engineBuiltResolved);
        var engineShape = BuildingFootprintGenerator.Generate(engineBuilt.Id, engineBuilt.BuildingTypeId).Select(c => c.Cell).ToList();
        var engineCells = CityOccupancy.Translate(engineShape, engineBuiltResolved!.Value.Position);
        var expectedEngineBox = new CityBounds(
            new CellCoord(engineCells.Min(c => c.X), engineCells.Min(c => c.Y)),
            engineCells.Max(c => c.X) - engineCells.Min(c => c.X) + 1,
            engineCells.Max(c => c.Y) - engineCells.Min(c => c.Y) + 1);
        Assert.Contains(expectedEngineBox, boxes);
    }

    // --- ResolveGrownBounds / real call site (CityProjector, CITYGROW-03/05 "made observable") ---

    [Fact]
    public void CityProjector_Build_reports_bounds_grown_to_include_a_real_overflow_building()
    {
        // Post-ship fix (2026-08-23, off-map city clamp): a cidade precisa nascer DENTRO do mapa
        // 10x10 default de ScenarioRunner.Create -- (50,50) (usado antes do fix) já nascia fora do
        // próprio mapa, e o fix de CityBoundsResolver.Resolve (clamp de origem) corretamente deixa
        // de deixar a caixa resolvida fora do mapa nesse caso, o que quebrava este teste por um
        // motivo alheio ao que ele verifica (crescimento por overflow).
        var world = EmptyWorld(seed: 702);
        var city = new City(world.NextCityId(), new CellCoord(4, 4), 0, null, AggregatePopulationPool.Empty);
        world.AddCity(city);

        // population = 0 -> bounds populacionais são 3x3 centrados em (4,4): origem (3,3),
        // borda direita/inferior em x=5/y=5. Um prédio autorado 1 célula fora dessa borda está
        // dentro do AbsorptionRingCells default (3) e deve ser absorvido.
        var overflow = new Building(
            world.NextBuildingIdAndAdvance(), city.Id, buildingTypeId: 1, completedAtTick: 0,
            position: new CellCoord(6, 3), orientation: 0);
        world.AddBuilding(overflow);

        var snapshot = CityProjector.Build(world, city.Id).Value!;

        Assert.True(snapshot.BoundsAreDerived);
        Assert.True(snapshot.Bounds.Width > 3 || snapshot.Bounds.Height > 3);
        var grown = new CityBounds(new CellCoord(snapshot.Bounds.X, snapshot.Bounds.Y), snapshot.Bounds.Width, snapshot.Bounds.Height);
        Assert.True(grown.Contains(overflow.Position!.Value));
    }

    [Fact]
    public void CityProjector_Build_bounds_are_unchanged_from_population_only_when_the_city_has_no_buildings()
    {
        var world = EmptyWorld(seed: 703);
        var city = new City(world.NextCityId(), new CellCoord(50, 50), 0, null, AggregatePopulationPool.Empty);
        world.AddCity(city);

        var snapshot = CityProjector.Build(world, city.Id).Value!;

        var (populationBounds, _) = SpatialBoundsResolver.ResolveCity(city, population: 0, world.Map.Width, world.Map.Height);
        Assert.Equal(populationBounds.Width, snapshot.Bounds.Width);
        Assert.Equal(populationBounds.Height, snapshot.Bounds.Height);
        Assert.Equal(populationBounds.Origin.X, snapshot.Bounds.X);
        Assert.Equal(populationBounds.Origin.Y, snapshot.Bounds.Y);
    }

    private static WorldState EmptyWorld(ulong seed) => new(
        ScenarioRunner.DefaultCalendar, seed, ScenarioRunner.DefaultMap(seed),
        ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
        ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
        ScenarioRunner.DefaultLifeStageRules);
}
