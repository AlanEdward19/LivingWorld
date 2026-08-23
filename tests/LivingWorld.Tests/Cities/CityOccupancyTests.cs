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

    // --- performance regression (dynamic-city-growth, fix/blocker) ---

    /// <summary>Antes do fix, cada vizinho sem posição autorada reentrava em
    /// <see cref="BuildingPlacementResolver.Resolve"/> -&gt; <see cref="CityOccupancy"/> outra
    /// vez, custando 2^(N-1) resoluções (187s medidos pelo Verifier com N=6). Este teste é o
    /// guarda de performance: N=30 prédios não-autorados tem que resolver bem dentro de um
    /// timeout normal de teste, e o resultado ainda precisa ser não-sobreposto.
    ///
    /// dynamic-city-growth, round-3 fix B: os bounds usados aqui eram 200x200 -- todo prédio
    /// cabia dentro deles, então o anel de <see cref="OverflowPlacer"/> (o caminho O(N²)-ish mais
    /// custoso que este guarda existe pra proteger) nunca era exercitado. Bounds agora em
    /// <see cref="CityBoundsResolver.MaxSize"/> (12x12) -- o teto real de tamanho de cidade por
    /// população -- pra que boa parte dos 30 prédios genuinamente precise do overflow.
    ///
    /// dynamic-city-growth, round-3 fix C: <c>Timeout</c> (xUnit 2.9.3) só é respeitado em testes
    /// assíncronos ("Tests marked with Timeout are only supported for async tests", confirmado
    /// rodando o teste) -- por isso o corpo roda num <see cref="Task.Run"/> e o método passa a ser
    /// <c>async Task</c>. Sem isso, a reintrodução da recursão original trava o gate por 300+s em
    /// vez de falhar em segundos (o `Assert.True(stopwatch...)` só roda DEPOIS da chamada
    /// retornar, que nunca acontece sob a regressão).</summary>
    [Fact(Timeout = 10_000)]
    public async Task OwnedBuildingFootprintBoxesWithOwners_resolves_many_unauthored_buildings_quickly_and_without_overlap()
    {
        await Task.Run(() =>
        {
        // Mapa real e grande o bastante ao redor da cidade (não o mapa padrão 10x10 de
        // ScenarioRunner.Create) -- bounds 12x12 é bem menor que o mapa, então o overflow que
        // este teste força tem espaço real pra onde crescer, em vez de ficar bloqueado pela
        // borda do mapa (o que testaria escassez de terra, não performance).
        var world = BuildWorldWithMap(500, 500, seed: 608);
        var city = new City(world.NextCityId(), new CellCoord(250, 250), 0, null, AggregatePopulationPool.Empty);
        world.AddCity(city);
        for (int i = 0; i < 30; i++)
            world.AddBuilding(new Building(world.NextBuildingIdAndAdvance(), city.Id, buildingTypeId: 1, completedAtTick: 0));
        var bounds = new CityBounds(new CellCoord(244, 244), 12, 12);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var boxes = CityOccupancy.OwnedBuildingFootprintBoxesWithOwners(world, city, bounds);
        stopwatch.Stop();

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"levou {stopwatch.Elapsed} -- recursão exponencial voltou?");
        Assert.Equal(30, boxes.Count);

        var allCells = boxes.SelectMany(b =>
        {
            var shape = BuildingFootprintGenerator.Generate(b.Building.Id, b.Building.BuildingTypeId).Select(c => c.Cell).ToList();
            return CityOccupancy.Translate(shape, b.Box.Origin);
        }).ToList();
        Assert.Equal(allCells.Count, allCells.Distinct().Count()); // nenhum prédio derivado sobrepõe outro

        // Prova de que o overflow foi genuinamente exercitado (round-3 fix B) -- pelo menos um
        // prédio precisou desbordar dos bounds 12x12, não só caber tranquilo dentro deles.
        Assert.Contains(boxes, b => !(b.Box.Origin.X >= bounds.Origin.X && b.Box.Origin.Y >= bounds.Origin.Y
            && b.Box.Origin.X + b.Box.Width <= bounds.Origin.X + bounds.Width
            && b.Box.Origin.Y + b.Box.Height <= bounds.Origin.Y + bounds.Height));
        });
    }

    // --- absorção só na própria cidade (dynamic-city-growth, round-3 fix F, item 1) ---

    /// <summary>spec.md, Edge Cases: um prédio de overflow nunca é absorvido pelos bounds
    /// RENDERIZADOS de uma cidade que não é a sua dona (via <see cref="Building.City"/>) -- o
    /// filtro de posse em <see cref="OwnedBuildingFootprintBoxesWithOwners"/> exclui o prédio da
    /// lista de qualquer cidade que não seja a dona, antes mesmo do cálculo de distância/anel de
    /// absorção rodar pra essa outra cidade.
    ///
    /// Post-ship fix (2026-08-23, cross-city bounds clamp): esta era originalmente
    /// "...mesmo quando outra cidade está geometricamente mais próxima dele" e afirmava que A
    /// absorvia o prédio mesmo assim. Isso é matematicamente incompatível com o fix — se o prédio
    /// já está mais perto de B do que a distância mínima exigida entre cidades
    /// (AbsorptionRingCells), então absorvê-lo em A necessariamente puxaria os bounds de A pra
    /// dentro do mesmo raio de B (o prédio se torna a borda de A mais próxima de B), violando
    /// exatamente a garantia que o fix existe pra proteger. O comportamento correto agora: nem A
    /// nem B crescem pra incluí-lo — ele fica overflow (ainda pertencente a A via Building.City,
    /// só não faz parte dos bounds renderizados de ninguém) até deixar de estar tão perto de B.</summary>
    [Fact]
    public void ResolveGrownBounds_never_absorbs_an_overflow_building_into_a_city_that_is_not_its_owner()
    {
        var world = BuildWorldWithMap(200, 200, seed: 611);
        var cityA = new City(world.NextCityId(), new CellCoord(100, 100), 0, null, AggregatePopulationPool.Empty);
        world.AddCity(cityA);

        // Prédio autorado (posição fixa, sem depender de Resolve) de A: dentro do anel de
        // absorção de A (distância 3, exatamente o teto default). baseA (população 0) é sempre
        // 3x3 com origem (99,99) -- independente do footprint w/h do prédio.
        var (rectId, _, w, _) = FindRectangularFootprint(typeId: 5);
        var overflowBuilding = new Building(
            rectId, cityA.Id, buildingTypeId: 5, completedAtTick: 0, position: new CellCoord(104, 99), orientation: 0);
        world.AddBuilding(overflowBuilding);

        // B posicionada de forma que o MESMO prédio fique geometricamente mais perto de B
        // (distância 1) que de A (distância 3) -- 105+w garante isso pra qualquer w gerado
        // (ver cálculo do gap na doc comment do teste). Sob o cross-city clamp, isso bloqueia a
        // absorção do prédio por A (absorvê-lo fecharia o gap com B), não só a absorção por B.
        var cityB = new City(world.NextCityId(), new CellCoord(105 + w, 100), 0, null, AggregatePopulationPool.Empty);
        world.AddCity(cityB);

        var (grownA, _) = CityOccupancy.ResolveGrownBounds(world, cityA, population: 0);
        var (grownB, _) = CityOccupancy.ResolveGrownBounds(world, cityB, population: 0);

        var buildingCells = CityOccupancy.Translate(
            BuildingFootprintGenerator.Generate(rectId, 5).Select(c => c.Cell).ToList(), overflowBuilding.Position!.Value);
        Assert.All(buildingCells, cell => Assert.False(grownA.Contains(cell))); // bloqueado: absorver fecharia o gap com B
        Assert.All(buildingCells, cell => Assert.False(grownB.Contains(cell))); // nunca por B, que nem é a dona
        Assert.Equal(cityA.Id, overflowBuilding.City); // posse (Building.City) nunca muda, só os bounds renderizados
        var (baseB, _) = CityBoundsResolver.Resolve(cityB, population: 0, mapWidth: world.Map.Width, mapHeight: world.Map.Height);
        Assert.Equal(baseB, grownB); // B intocado -- o prédio nem entra na lista de posse de B
    }

    // --- ordem causal ascendente (dynamic-city-growth, round-3 fix A / Gap D) ---

    /// <summary>O doc comment de <see cref="CityOccupancy.OccupiedCellsOfCity"/> chama a ordem
    /// ascendente por <see cref="BuildingId"/> de "obrigatória": ao resolver o prédio k, o conjunto
    /// já ocupado precisa conter exatamente as células dos prédios de id MENOR, a mesma
    /// causalidade que a versão recursiva original tinha. Nenhum teste existente pega uma troca
    /// para <c>OrderByDescending</c> -- o resultado em lote continua internamente consistente
    /// (sem sobreposição) mesmo invertido, só discorda da posição real que cada prédio teria se
    /// resolvido sozinho na ordem causal correta. Este teste compara exatamente isso: a posição
    /// que <see cref="OwnedBuildingFootprintBoxesWithOwners"/> (lote) atribui a cada prédio contra
    /// a posição "verdade fundamental" obtida resolvendo cada um sozinho, em ordem ascendente,
    /// adicionando sua posição real ao mundo antes do próximo -- exatamente a causalidade que o
    /// comentário chama de obrigatória.</summary>
    [Fact]
    public void OwnedBuildingFootprintBoxesWithOwners_places_each_building_using_the_causal_ascending_id_order()
    {
        const int buildingType = 1;
        var ids = new long[] { 10, 11, 12, 13 };
        // Bounds pequenos o bastante pra que a posição do prédio k dependa de verdade das
        // células já ocupadas pelos prédios de id menor (sem isso, todo mundo caberia livre em
        // qualquer ordem e o teste não pegaria a inversão). Mapa grande o bastante em volta
        // (igual ao teste de perf abaixo) pra que um eventual overflow tenha pra onde crescer,
        // nunca escassez de terra por causa do MAPA (o que testaria outra coisa).
        var bounds = new CityBounds(new CellCoord(100, 100), 6, 6);

        // Verdade fundamental: resolve cada prédio na ordem causal ascendente, uma chamada por
        // vez, gravando a posição real no mundo antes de resolver o próximo.
        var truthWorld = BuildWorldWithMap(200, 200, seed: 609);
        var truthCity = new City(truthWorld.NextCityId(), new CellCoord(100, 100), 0, null, AggregatePopulationPool.Empty);
        truthWorld.AddCity(truthCity);
        var truthPosition = new Dictionary<long, CellCoord>();
        foreach (var id in ids)
        {
            var building = new Building(new BuildingId(id), truthCity.Id, buildingType, completedAtTick: 0);
            var resolved = BuildingPlacementResolver.Resolve(building, truthCity, truthWorld, bounds);
            Assert.NotNull(resolved);
            truthPosition[id] = resolved!.Value.Position;
            truthWorld.AddBuilding(new Building(
                building.Id, truthCity.Id, buildingType, completedAtTick: 0,
                position: resolved.Value.Position, orientation: resolved.Value.Orientation));
        }

        // Lote: os mesmos ids/tipo, todos sem posição autorada, resolvidos de uma vez pelo
        // método sob teste.
        var batchWorld = BuildWorldWithMap(200, 200, seed: 609);
        var batchCity = new City(batchWorld.NextCityId(), new CellCoord(100, 100), 0, null, AggregatePopulationPool.Empty);
        batchWorld.AddCity(batchCity);
        foreach (var id in ids)
            batchWorld.AddBuilding(new Building(new BuildingId(id), batchCity.Id, buildingType, completedAtTick: 0));

        var boxes = CityOccupancy.OwnedBuildingFootprintBoxesWithOwners(batchWorld, batchCity, bounds);

        foreach (var id in ids)
        {
            var box = boxes.Single(b => b.Building.Id.Value == id).Box;
            // Formato do footprint sempre inclui a célula local (0,0) (BuildingFootprintGenerator),
            // então a origem do box coincide exatamente com a posição resolvida.
            Assert.Equal(truthPosition[id], box.Origin);
        }
    }

    // --- cross-city bounds clamp (post-ship fix, 2026-08-23) ---

    private static int ChebyshevGapForTest(CityBounds a, CityBounds b)
    {
        int aRight = a.Origin.X + a.Width - 1, aBottom = a.Origin.Y + a.Height - 1;
        int bRight = b.Origin.X + b.Width - 1, bBottom = b.Origin.Y + b.Height - 1;
        int dx = Math.Max(0, Math.Max(a.Origin.X - bRight, b.Origin.X - aRight));
        int dy = Math.Max(0, Math.Max(a.Origin.Y - bBottom, b.Origin.Y - aBottom));
        return Math.Max(dx, dy);
    }

    /// <summary>Bug relatado em produção: duas cidades fundadas a uma distância segura, cada uma
    /// crescendo por overflow tick após tick sem NUNCA se checar contra a outra -- eventualmente
    /// seus bounds encostavam/se sobrepunham (paredes literalmente coladas). Reproduz o cenário:
    /// A e B têm population boxes separadas por exatamente 2x <c>AbsorptionRingCells</c> (6
    /// células, ring=3) -- geometria em que, isoladamente, cada uma pode absorver um prédio até
    /// `ring` células em direção à outra (regra existente antes do fix), o que faria seus bounds
    /// se tocarem ou sobrepor (gap 0 ou negativo) SEM o cross-city clamp. Simula várias "rodadas"
    /// de absorção (um novo prédio de overflow mais próximo em cada uma, como novos ticks de
    /// construção trariam) e assere que o gap real entre os bounds crescidos nunca cai abaixo de
    /// <c>AbsorptionRingCells</c> em nenhuma rodada.</summary>
    [Fact]
    public void ResolveGrownBounds_never_lets_two_citys_grown_bounds_come_within_the_absorption_ring_of_each_other()
    {
        var world = BuildWorldWithMap(200, 200, seed: 612);
        int ring = world.CityRules.AbsorptionRingCells; // 3, default

        var cityA = new City(world.NextCityId(), new CellCoord(100, 100), 0, null, AggregatePopulationPool.Empty);
        world.AddCity(cityA);
        var cityB = new City(world.NextCityId(), new CellCoord(100, 108), 0, null, AggregatePopulationPool.Empty);
        world.AddCity(cityB);
        // popBoxA (pop 0) = (99,99)-(101,101); popBoxB = (99,107)-(101,109) -- 6 células de gap
        // (2x ring) entre elas.

        var (_, _, _, h) = FindRectangularFootprint(typeId: 5);
        long nextId = 1;

        for (int offset = 0; offset <= ring; offset++)
        {
            // Prédio de A avançando pra baixo, em direção a B; sempre dentro do próprio anel de
            // absorção de A (distância `offset` <= ring da própria population box).
            world.AddBuilding(new Building(new BuildingId(nextId++), cityA.Id, buildingTypeId: 5, completedAtTick: 0,
                position: new CellCoord(100, 101 + offset), orientation: 0));
            // Prédio de B avançando pra cima, em direção a A; mesma lógica, do outro lado.
            world.AddBuilding(new Building(new BuildingId(nextId++), cityB.Id, buildingTypeId: 5, completedAtTick: 0,
                position: new CellCoord(100, 108 - offset - h), orientation: 0));

            var (grownA, _) = CityOccupancy.ResolveGrownBounds(world, cityA, population: 0);
            var (grownB, _) = CityOccupancy.ResolveGrownBounds(world, cityB, population: 0);

            int gap = ChebyshevGapForTest(grownA, grownB);
            Assert.True(gap >= ring, $"offset={offset}: gap real entre A e B = {gap}, violou AbsorptionRingCells={ring}");
        }
    }

    /// <summary>Regressão (spec.md, Success Criteria): uma cidade sem nenhuma outra por perto
    /// continua crescendo normalmente -- o cross-city clamp não afeta o caso de uma única cidade
    /// (mesmo comportamento de antes do fix, `otherCityBoundsToAvoid` vazio/nulo).</summary>
    [Fact]
    public void ResolveGrownBounds_still_absorbs_an_overflow_building_when_there_is_no_other_city_nearby()
    {
        var world = BuildWorldWithMap(200, 200, seed: 613);
        var city = new City(world.NextCityId(), new CellCoord(100, 100), 0, null, AggregatePopulationPool.Empty);
        world.AddCity(city);

        var (rectId, _, _, _) = FindRectangularFootprint(typeId: 5);
        var overflowBuilding = new Building(
            rectId, city.Id, buildingTypeId: 5, completedAtTick: 0, position: new CellCoord(104, 99), orientation: 0);
        world.AddBuilding(overflowBuilding);

        var (grown, _) = CityOccupancy.ResolveGrownBounds(world, city, population: 0);

        var buildingCells = CityOccupancy.Translate(
            BuildingFootprintGenerator.Generate(rectId, 5).Select(c => c.Cell).ToList(), overflowBuilding.Position!.Value);
        Assert.All(buildingCells, cell => Assert.True(grown.Contains(cell))); // absorvido normalmente, sem clamp pra aplicar
    }
}
