using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Cities;

/// <summary>dynamic-city-growth, T2 (CITYGROW-02): <see cref="OverflowPlacer"/> — busca em anéis
/// crescentes a partir da borda dos bounds, só chamada quando <see
/// cref="CityOccupancy.FindFreeCellInBounds"/> não acha vaga dentro deles.</summary>
public class OverflowPlacerTests
{
    /// <summary>Mesmo truque de <see cref="CityOccupancyTests"/>: procura um footprint sem o
    /// entalhe do formato L, pra poder ocupar exatamente uma bounding box conhecida.</summary>
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

    private static (WorldState World, City City, CityBounds Bounds, BuildingId RectId, IReadOnlyList<CellCoord> RectShape)
        MakeFullyOccupiedCity(ulong seed, int typeId)
    {
        var (rectId, rectShape, w, h) = FindRectangularFootprint(typeId);
        var world = ScenarioRunner.Create(seed: seed, initialPopulation: 0).World;
        var city = new City(world.NextCityId(), new CellCoord(0, 0), 0, null, AggregatePopulationPool.Empty);
        world.AddCity(city);
        var bounds = new CityBounds(new CellCoord(0, 0), w, h);
        world.AddBuilding(new Building(rectId, city.Id, typeId, completedAtTick: 0, position: new CellCoord(0, 0), orientation: 0));
        return (world, city, bounds, rectId, rectShape);
    }

    [Fact]
    public void ResolveOverflowPosition_returns_a_free_cell_outside_fully_occupied_bounds()
    {
        var (world, city, bounds, _, rectShape) = MakeFullyOccupiedCity(seed: 701, typeId: 7);

        var found = OverflowPlacer.ResolveOverflowPosition(world, city, bounds, new BuildingId(9001), rectShape);

        var translated = CityOccupancy.Translate(rectShape, found);
        Assert.True(CityOccupancy.IsFree(world, city, translated));
        // Os bounds originais estão 100% ocupados (Done-when 1) -- a célula resolvida tem que
        // desbordar deles, nunca ficar inteiramente dentro.
        Assert.False(translated.All(bounds.Contains));
    }

    [Fact]
    public void ResolveOverflowPosition_is_deterministic_for_the_same_building_id_and_world_state()
    {
        var (world, city, bounds, _, rectShape) = MakeFullyOccupiedCity(seed: 702, typeId: 7);
        var id = new BuildingId(9002);

        var first = OverflowPlacer.ResolveOverflowPosition(world, city, bounds, id, rectShape);
        var second = OverflowPlacer.ResolveOverflowPosition(world, city, bounds, id, rectShape);

        Assert.Equal(first, second);
    }

    [Fact]
    public void ResolveOverflowPosition_skips_a_ring_cell_that_another_building_already_occupies()
    {
        var (world, city, bounds, rectId, rectShape) = MakeFullyOccupiedCity(seed: 703, typeId: 7);
        var id = new BuildingId(9003);

        var withoutBlocker = OverflowPlacer.ResolveOverflowPosition(world, city, bounds, id, rectShape);

        // Ocupa exatamente a célula que seria escolhida sem bloqueio -- a próxima chamada não
        // pode devolver a mesma posição nem sobrepor o prédio recém-adicionado.
        world.AddBuilding(new Building(rectId, city.Id, buildingTypeId: 7, completedAtTick: 0, position: withoutBlocker, orientation: 0));

        var withBlocker = OverflowPlacer.ResolveOverflowPosition(world, city, bounds, id, rectShape);

        Assert.NotEqual(withoutBlocker, withBlocker);
        var translated = CityOccupancy.Translate(rectShape, withBlocker);
        Assert.True(CityOccupancy.IsFree(world, city, translated));
    }

    [Fact]
    public void ResolveOverflowPosition_keeps_growing_the_radius_until_it_clears_a_wide_occupied_moat()
    {
        var (rectId, rectShape, w, h) = FindRectangularFootprint(typeId: 8);
        var world = ScenarioRunner.Create(seed: 704, initialPopulation: 0).World;
        var city = new City(world.NextCityId(), new CellCoord(0, 0), 0, null, AggregatePopulationPool.Empty);
        world.AddCity(city);
        var bounds = new CityBounds(new CellCoord(0, 0), w, h);

        // "Fosso" de blocos w x h ladrilhando uma área bem maior que os bounds em torno deles --
        // qualquer anel de busca com raio pequeno cai inteiro dentro do fosso (ocupado), forçando
        // o método a crescer o raio várias vezes antes de achar uma célula livre de verdade (o
        // caso "far from city" do spec, ao lado do "near" já coberto pelo teste acima).
        const int moatLayers = 2;
        var tilePositions = new List<CellCoord>();
        for (int tx = -moatLayers; tx <= moatLayers; tx++)
            for (int ty = -moatLayers; ty <= moatLayers; ty++)
                tilePositions.Add(new CellCoord(tx * w, ty * h));

        foreach (var pos in tilePositions)
            world.AddBuilding(new Building(rectId, city.Id, buildingTypeId: 8, completedAtTick: 0, position: pos, orientation: 0));

        var found = OverflowPlacer.ResolveOverflowPosition(world, city, bounds, new BuildingId(9004), rectShape);

        bool InsideAnyTile(CellCoord cell) =>
            tilePositions.Any(pos => cell.X >= pos.X && cell.X < pos.X + w && cell.Y >= pos.Y && cell.Y < pos.Y + h);

        var translated = CityOccupancy.Translate(rectShape, found);
        Assert.True(translated.All(cell => !InsideAnyTile(cell)));
        Assert.True(CityOccupancy.IsFree(world, city, translated));
    }
}
