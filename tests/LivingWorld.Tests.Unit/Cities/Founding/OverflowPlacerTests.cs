using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Cities.Spatial;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Geography.Map;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cities;
using LivingWorld.Simulation.Cities.Founding;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Cities.Founding;

/// <summary>dynamic-city-growth, T2 (CITYGROW-02): <see cref="OverflowPlacer"/> — busca em anéis
/// crescentes a partir da borda dos bounds, só chamada quando <see
/// cref="CityOccupancy.FindFreeCellInBounds"/> não acha vaga dentro deles.</summary>
public class OverflowPlacerTests
{
    private static readonly GeographyCatalog TinyCatalog = new(
        TerrainIds: new HashSet<int> { 1 }, BiomeIds: new HashSet<int> { 1 }, ResourceIds: new HashSet<int>());

    private static readonly CostWeights TinyCostWeights = new(
        Base: 1.0, AltitudeWeight: 0.5, TerrainWeight: new Dictionary<int, double> { [1] = 1.0 });

    /// <summary>dynamic-city-growth, fix (major, CITYGROW-02b): o anel de overflow agora é
    /// amarrado ao mapa real (<see cref="OverflowPlacer.ResolveOverflowPosition"/> nunca devolve
    /// uma célula fora de <c>world.Map</c>) -- estes testes de "near"/"far" overflow precisam de
    /// espaço real de sobra fora do fosso/bounds ocupados, não do mapa 10x10 minúsculo de
    /// <see cref="ScenarioRunner.DefaultMap"/>, senão a busca corretamente devolveria escassez em
    /// vez do overflow que estes testes querem exercitar.</summary>
    private static WorldState BuildWorldWithMap(int width, int height, ulong seed)
    {
        var map = MapGenerator.Generate(seed, width, height, Math.Max(width, height), TinyCatalog, TinyCostWeights, [])
            .Value ?? throw new InvalidOperationException("mapa de teste inválido — bug no teste, não no gerador");
        return new WorldState(
            ScenarioRunner.DefaultCalendar, seed, map, ScenarioRunner.DefaultPopulationCatalog,
            ScenarioRunner.DefaultPopulationRules, ScenarioRunner.DefaultNeedsRules,
            ScenarioRunner.DefaultActionCatalog, ScenarioRunner.DefaultLifeStageRules);
    }

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
        var world = BuildWorldWithMap(200, 200, seed);
        var city = new City(world.NextCityId(), new CellCoord(0, 0), 0, null, AggregatePopulationPool.Empty);
        world.AddCity(city);
        var bounds = new CityBounds(new CellCoord(0, 0), w, h);
        world.AddBuilding(new Building(rectId, city.Id, typeId, completedAtTick: 0, position: new CellCoord(0, 0), orientation: 0));
        return (world, city, bounds, rectId, rectShape);
    }

    [Fact]
    public void ResolveOverflowPosition_returns_a_free_cell_outside_fully_occupied_bounds()
    {
        var (world, city, bounds, _, rectShape) = MakeFullyOccupiedCity(seed: 701, typeId: 8);

        var found = OverflowPlacer.ResolveOverflowPosition(world, city, bounds, new BuildingId(9001), rectShape);

        Assert.NotNull(found);
        var translated = CityOccupancy.Translate(rectShape, found!.Value);
        Assert.True(CityOccupancy.IsFree(world, city, bounds, translated));
        // Os bounds originais estão 100% ocupados (Done-when 1) -- a célula resolvida tem que
        // desbordar deles, nunca ficar inteiramente dentro.
        Assert.False(translated.All(bounds.Contains));
    }

    [Fact]
    public void ResolveOverflowPosition_is_deterministic_for_the_same_building_id_and_world_state()
    {
        var (world, city, bounds, _, rectShape) = MakeFullyOccupiedCity(seed: 702, typeId: 8);
        var id = new BuildingId(9002);

        var first = OverflowPlacer.ResolveOverflowPosition(world, city, bounds, id, rectShape);
        var second = OverflowPlacer.ResolveOverflowPosition(world, city, bounds, id, rectShape);

        Assert.Equal(first, second);
    }

    [Fact]
    public void ResolveOverflowPosition_skips_a_ring_cell_that_another_building_already_occupies()
    {
        var (world, city, bounds, rectId, rectShape) = MakeFullyOccupiedCity(seed: 703, typeId: 8);
        var id = new BuildingId(9003);

        var withoutBlocker = OverflowPlacer.ResolveOverflowPosition(world, city, bounds, id, rectShape);
        Assert.NotNull(withoutBlocker);

        // Ocupa exatamente a célula que seria escolhida sem bloqueio -- a próxima chamada não
        // pode devolver a mesma posição nem sobrepor o prédio recém-adicionado.
        world.AddBuilding(new Building(rectId, city.Id, buildingTypeId: 8, completedAtTick: 0, position: withoutBlocker.Value, orientation: 0));

        var withBlocker = OverflowPlacer.ResolveOverflowPosition(world, city, bounds, id, rectShape);

        Assert.NotNull(withBlocker);
        Assert.NotEqual(withoutBlocker, withBlocker);
        var translated = CityOccupancy.Translate(rectShape, withBlocker!.Value);
        Assert.True(CityOccupancy.IsFree(world, city, bounds, translated));
    }

    [Fact]
    public void ResolveOverflowPosition_keeps_growing_the_radius_until_it_clears_a_wide_occupied_moat()
    {
        var (rectId, rectShape, w, h) = FindRectangularFootprint(typeId: 8);
        var world = BuildWorldWithMap(200, 200, seed: 704);
        var city = new City(world.NextCityId(), new CellCoord(100, 100), 0, null, AggregatePopulationPool.Empty);
        world.AddCity(city);
        var bounds = new CityBounds(new CellCoord(100, 100), w, h);

        // "Fosso" de blocos w x h ladrilhando uma área bem maior que os bounds em torno deles --
        // qualquer anel de busca com raio pequeno cai inteiro dentro do fosso (ocupado), forçando
        // o método a crescer o raio várias vezes antes de achar uma célula livre de verdade (o
        // caso "far from city" do spec, ao lado do "near" já coberto pelo teste acima). Centrado
        // em (100,100) num mapa 200x200 pra ter espaço real de sobra fora do fosso nas quatro
        // direções (fix CITYGROW-02b amarra o anel ao mapa real).
        const int moatLayers = 2;
        var tilePositions = new List<CellCoord>();
        for (int tx = -moatLayers; tx <= moatLayers; tx++)
            for (int ty = -moatLayers; ty <= moatLayers; ty++)
                tilePositions.Add(new CellCoord(100 + tx * w, 100 + ty * h));

        foreach (var pos in tilePositions)
            world.AddBuilding(new Building(rectId, city.Id, buildingTypeId: 8, completedAtTick: 0, position: pos, orientation: 0));

        var found = OverflowPlacer.ResolveOverflowPosition(world, city, bounds, new BuildingId(9004), rectShape);

        bool InsideAnyTile(CellCoord cell) =>
            tilePositions.Any(pos => cell.X >= pos.X && cell.X < pos.X + w && cell.Y >= pos.Y && cell.Y < pos.Y + h);

        Assert.NotNull(found);
        var translated = CityOccupancy.Translate(rectShape, found!.Value);
        Assert.True(translated.All(cell => !InsideAnyTile(cell)));
        Assert.True(CityOccupancy.IsFree(world, city, bounds, translated));
    }

    /// <summary>dynamic-city-growth, round-3 fix F, item 3: o loop de <see
    /// cref="OverflowPlacer.ResolveOverflowPositionGiven"/> cresce o raio a partir de 1 e retorna
    /// no PRIMEIRO raio com célula livre -- mas nenhum teste existente prova que é o raio MÍNIMO,
    /// só que "alguma" célula fora dos bounds foi encontrada. Aqui o anel de raio 1 é
    /// completamente ocupado e o de raio 2 fica inteiramente livre -- se o método devolvesse
    /// qualquer célula livre (não necessariamente a mais próxima), nada o impediria de "pular"
    /// pro raio 2 mesmo se o 1 tivesse uma vaga; este teste também garante que o raio 1 realmente
    /// não tinha nenhuma (só assim a distância observada prova "mais próxima", não "a única").</summary>
    [Fact]
    public void ResolveOverflowPositionGiven_picks_the_nearest_free_ring_when_a_closer_one_is_fully_blocked()
    {
        var bounds = new CityBounds(new CellCoord(0, 0), 4, 4);
        var shape = new List<CellCoord> { new CellCoord(0, 0) }; // footprint de 1 célula só -- simplifica o cálculo do raio exato
        var id = new BuildingId(1);

        // Ocupa TODO o anel de raio 1 (perímetro imediatamente fora dos bounds) -- só o anel de
        // raio 2 (e além) tem células livres.
        var occupied = new HashSet<CellCoord>();
        for (int x = -1; x <= 4; x++) { occupied.Add(new CellCoord(x, -1)); occupied.Add(new CellCoord(x, 4)); }
        for (int y = -1; y <= 4; y++) { occupied.Add(new CellCoord(-1, y)); occupied.Add(new CellCoord(4, y)); }

        var found = OverflowPlacer.ResolveOverflowPositionGiven(occupied, bounds, id, shape, mapWidth: 100, mapHeight: 100);

        Assert.NotNull(found);
        int gap = Math.Max(
            Math.Max(bounds.Origin.X - found!.Value.X, found.Value.X - (bounds.Origin.X + bounds.Width - 1)),
            Math.Max(bounds.Origin.Y - found.Value.Y, found.Value.Y - (bounds.Origin.Y + bounds.Height - 1)));
        Assert.Equal(2, gap); // o anel de raio 1 estava 100% bloqueado -- o raio 2 é o mais próximo livre
    }

    /// <summary>Complemento do teste acima: lá o anel de raio 1 fica 100% bloqueado, então nunca
    /// prova que o método prefere um raio 1 livre sobre um raio 2 também livre quando os DOIS
    /// existem -- só prova que ele cresce o raio quando precisa. Aqui o raio 1 tem exatamente UMA
    /// célula livre e o raio 2 está inteiramente livre também; se o método ignorasse a ordem de
    /// raio crescente (ex.: bug que varre um raio maior primeiro, ou credita erroneamente uma
    /// distância a mais/a menos), poderia devolver uma célula do raio 2 mesmo com uma vaga mais
    /// próxima disponível. A diferença de apenas 1 célula de raio prova que é a distância mínima
    /// real, não uma "qualquer célula livre fora dos bounds".</summary>
    [Fact]
    public void ResolveOverflowPositionGiven_prefers_the_one_free_radius_1_cell_over_an_entirely_free_radius_2_ring()
    {
        var bounds = new CityBounds(new CellCoord(0, 0), 4, 4);
        var shape = new List<CellCoord> { new CellCoord(0, 0) };
        var id = new BuildingId(1);

        // Bloqueia todo o anel de raio 1 (perímetro em x=-1,4 e y=-1,4) EXCETO uma célula --
        // o anel de raio 2 fica inteiramente livre (não ocupado). A célula livre escolhida tem
        // coordenadas não-negativas (dentro do mapa 100x100) -- uma célula fora do mapa nunca
        // venceria de qualquer forma (WithinMap a rejeitaria antes de checar ocupação).
        var freeRadius1Cell = new CellCoord(4, 0);
        var occupied = new HashSet<CellCoord>();
        for (int x = -1; x <= 4; x++) { occupied.Add(new CellCoord(x, -1)); occupied.Add(new CellCoord(x, 4)); }
        for (int y = -1; y <= 4; y++) { occupied.Add(new CellCoord(-1, y)); occupied.Add(new CellCoord(4, y)); }
        occupied.Remove(freeRadius1Cell);

        var found = OverflowPlacer.ResolveOverflowPositionGiven(occupied, bounds, id, shape, mapWidth: 100, mapHeight: 100);

        Assert.Equal(freeRadius1Cell, found); // a única vaga de raio 1 vence, não uma célula de raio 2
    }
}
