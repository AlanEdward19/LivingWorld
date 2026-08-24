using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Cities;

/// <summary>Fase 15.1, T45 (backend-gaps.md G4): footprint por material, bounds de cidade e
/// posição/orientação de prédio — geometria canônica que T20 vai expor na projeção. Autoria
/// (T44) tem precedência; sem ela, fallback determinístico e estável.</summary>
public class BuildingFootprintAndPlacementTests
{
    private static readonly GeographyCatalog TinyCatalog = new(
        TerrainIds: new HashSet<int> { 1 }, BiomeIds: new HashSet<int> { 1 }, ResourceIds: new HashSet<int>());

    private static readonly CostWeights TinyCostWeights = new(
        Base: 1.0, AltitudeWeight: 0.5, TerrainWeight: new Dictionary<int, double> { [1] = 1.0 });

    /// <summary>Mesmo helper de <see cref="Cities.CityOccupancyTests"/>/<see
    /// cref="Cities.OverflowPlacerTests"/>: um <see cref="WorldState"/> com um mapa real de
    /// dimensões controladas, necessário pros testes de CITYGROW-02b (o anel de overflow agora
    /// respeita <c>world.Map.Width/Height</c> de verdade).</summary>
    private static WorldState BuildWorldWithMap(int width, int height, ulong seed)
    {
        var map = MapGenerator.Generate(seed, width, height, Math.Max(width, height), TinyCatalog, TinyCostWeights, [])
            .Value ?? throw new InvalidOperationException("mapa de teste inválido — bug no teste, não no gerador");
        return new WorldState(
            ScenarioRunner.DefaultCalendar, seed, map, ScenarioRunner.DefaultPopulationCatalog,
            ScenarioRunner.DefaultPopulationRules, ScenarioRunner.DefaultNeedsRules,
            ScenarioRunner.DefaultActionCatalog, ScenarioRunner.DefaultLifeStageRules);
    }

    // --- BuildingFootprintGenerator ---

    [Fact]
    public void Same_building_id_and_type_always_produce_the_same_footprint()
    {
        var a = BuildingFootprintGenerator.Generate(new BuildingId(7), 1);
        var b = BuildingFootprintGenerator.Generate(new BuildingId(7), 1);

        Assert.Equal(a, b);
    }

    [Fact]
    public void Different_building_type_id_changes_the_wall_material_by_parity()
    {
        var evenType = BuildingFootprintGenerator.Generate(new BuildingId(1), 2);
        var oddType = BuildingFootprintGenerator.Generate(new BuildingId(1), 3);

        Assert.Contains(evenType, c => c.Material == BuildingMaterial.StoneWall);
        Assert.DoesNotContain(evenType, c => c.Material == BuildingMaterial.WoodWall);
        Assert.Contains(oddType, c => c.Material == BuildingMaterial.WoodWall);
        Assert.DoesNotContain(oddType, c => c.Material == BuildingMaterial.StoneWall);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(3, 2)]
    [InlineData(42, 7)]
    [InlineData(1000, 9)]
    public void Footprint_always_contains_exactly_one_door_and_it_belongs_to_the_shape(long buildingId, int buildingTypeId)
    {
        var footprint = BuildingFootprintGenerator.Generate(new BuildingId(buildingId), buildingTypeId);

        var doors = footprint.Where(c => c.Material == BuildingMaterial.Door).ToList();
        Assert.Single(doors);
        Assert.Contains(doors[0], footprint); // a porta é uma célula do próprio footprint, nunca fora dele
    }

    [Fact]
    public void Footprint_is_stable_across_ticks_because_it_reads_no_world_state()
    {
        var before = BuildingFootprintGenerator.Generate(new BuildingId(55), 4);

        var (world, clock) = ScenarioRunner.Create(seed: 61);
        clock.Run(world, ticks: 200);

        var after = BuildingFootprintGenerator.Generate(new BuildingId(55), 4);
        Assert.Equal(before, after);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(99)]
    public void Initial_house_is_always_compact_3_by_3_with_an_internal_floor_cell(long buildingId)
    {
        var footprint = BuildingFootprintGenerator.Generate(new BuildingId(buildingId), buildingTypeId: -1);

        Assert.Equal((3, 3, 1), (
            footprint.Max(cell => cell.Cell.X) + 1,
            footprint.Max(cell => cell.Cell.Y) + 1,
            footprint.Count(cell => cell.Material == BuildingMaterial.Floor)));
    }

    [Fact]
    public void Derived_building_orientations_are_deterministic_and_vary_between_identities()
    {
        var orientations = Enumerable.Range(1, 16)
            .Select(id => BuildingFootprintGenerator.DerivedOrientation(new BuildingId(id), buildingTypeId: -1))
            .ToList();

        Assert.All(orientations, orientation => Assert.Contains(orientation, new[] { 0, 90, 180, 270 }));
        Assert.True(orientations.Distinct().Count() > 1);
        Assert.Equal(orientations, Enumerable.Range(1, 16)
            .Select(id => BuildingFootprintGenerator.DerivedOrientation(new BuildingId(id), buildingTypeId: -1)));
    }

    [Theory]
    [InlineData(90)]
    [InlineData(270)]
    public void Generate_building_honors_persisted_orientation_for_an_asymmetric_L_shape(int orientation)
    {
        var building = new Building(
            new BuildingId(77), new CityId(Guid.Empty), buildingTypeId: 7, completedAtTick: 0,
            position: new CellCoord(10, 10), orientation: orientation);

        var actual = BuildingFootprintGenerator.Generate(building);
        var expected = BuildingFootprintGenerator.Generate(building.Id, building.BuildingTypeId, orientation);
        var unrotated = BuildingFootprintGenerator.Generate(building.Id, building.BuildingTypeId, orientation: 0);
        static string Signature(IEnumerable<FootprintCell> cells) => string.Join(
            "|", cells.OrderBy(cell => cell.Cell.Y).ThenBy(cell => cell.Cell.X)
                .Select(cell => $"{cell.Cell.X},{cell.Cell.Y}:{cell.Material}"));

        Assert.Equal(Signature(expected), Signature(actual));
        Assert.NotEqual(Signature(unrotated), Signature(actual));
        Assert.Single(actual, cell => cell.Material == BuildingMaterial.Door);
        Assert.All(actual.Where(cell => cell.Material != BuildingMaterial.Door), cell =>
            Assert.Contains(cell.Material, new[] { BuildingMaterial.WoodWall, BuildingMaterial.Floor }));
    }

    // --- CityBoundsResolver ---

    [Fact]
    public void City_bounds_are_always_derived_today_and_scale_with_population_within_a_floor_and_cap()
    {
        var city = new City(new CityId(Guid.NewGuid()), new CellCoord(50, 60), 0, null, new AggregatePopulationPool(0, 0, 0));

        var (emptyBounds, isDerived) = CityBoundsResolver.Resolve(city, population: 0, mapWidth: 1000, mapHeight: 1000);
        var (bigBounds, _) = CityBoundsResolver.Resolve(city, population: 10_000, mapWidth: 1000, mapHeight: 1000);

        Assert.True(isDerived);
        // Piso: população zero nunca produz um footprint maior que o mapa de um mundo Pequeno
        // (10x10) — bug real reportado pelo usuário, a fórmula antiga sempre desenhava 34x24
        // fixo, estourando qualquer mundo menor que isso.
        Assert.Equal(3, emptyBounds.Width);
        Assert.Equal(3, emptyBounds.Height);
        Assert.Equal(new CellCoord(50 - 1, 60 - 1), emptyBounds.Origin);
        // Teto visual compacto, mesmo para população muito grande (num mapa grande o bastante
        // para o teto por população ser o fator limitante, não o mapa).
        Assert.Equal(12, bigBounds.Width);
        Assert.Equal(12, bigBounds.Height);
    }

    [Fact]
    public void City_bounds_never_exceed_the_smaller_map_dimension_even_when_population_asks_for_more()
    {
        // Bugfix real (usuário, 2026-08-13, rodada 2): confirmado ao vivo — template "Cidade
        // média" (mapa 20x20, população 150) ainda produzia lado 25, maior que o próprio mapa.
        var city = new City(new CityId(Guid.NewGuid()), new CellCoord(10, 10), 0, null, new AggregatePopulationPool(0, 0, 0));

        var (bounds, _) = CityBoundsResolver.Resolve(city, population: 150, mapWidth: 20, mapHeight: 20);

        Assert.True(bounds.Width <= 10);
        Assert.True(bounds.Height <= 10);
    }

    /// <summary>Post-ship fix (user-reported, 2026-08-23, "MorNorHol" fundada fora do mapa):
    /// Resolve já clampava WIDTH/HEIGHT ao mapa mas nunca a ORIGEM -- uma cidade encostada na borda
    /// (0,0) reportava dimensões dentro do mapa enquanto a caixa inteira ficava parcialmente fora
    /// dele (origem em -1,-1). A caixa inteira precisa caber em [0,mapWidth) x [0,mapHeight), não
    /// só o tamanho.</summary>
    [Fact]
    public void City_bounds_origin_stays_on_map_when_the_city_sits_right_at_the_map_edge()
    {
        var city = new City(new CityId(Guid.NewGuid()), new CellCoord(0, 0), 0, null, new AggregatePopulationPool(0, 0, 0));

        var (bounds, _) = CityBoundsResolver.Resolve(city, population: 0, mapWidth: 1000, mapHeight: 1000);

        Assert.True(bounds.Origin.X >= 0);
        Assert.True(bounds.Origin.Y >= 0);
        Assert.True(bounds.Origin.X + bounds.Width <= 1000);
        Assert.True(bounds.Origin.Y + bounds.Height <= 1000);
    }

    /// <summary>Mesmo gap, mas via o caminho de CRESCIMENTO (overflow) -- absorver um prédio de
    /// overflow perto da borda (0,0) empurra minX/minY negativos antes do fix; a caixa resultante
    /// precisa continuar inteiramente dentro do mapa depois de crescer.</summary>
    [Fact]
    public void Absorption_growth_near_the_map_edge_keeps_the_grown_box_fully_on_map()
    {
        var city = new City(new CityId(Guid.NewGuid()), new CellCoord(0, 0), 0, null, new AggregatePopulationPool(0, 0, 0));
        // Prédio de overflow ainda mais perto da borda que a própria cidade -- empurraria a
        // origem pra (-2,-2) sem o clamp.
        var overflowBox = new CityBounds(new CellCoord(-2, -2), 1, 1);

        var (grown, _) = CityBoundsResolver.Resolve(
            city, population: 0, mapWidth: 1000, mapHeight: 1000, ownedBuildingFootprintBoxes: [overflowBox]);

        Assert.True(grown.Origin.X >= 0);
        Assert.True(grown.Origin.Y >= 0);
        Assert.True(grown.Origin.X + grown.Width <= 1000);
        Assert.True(grown.Origin.Y + grown.Height <= 1000);
    }

    // --- CityBoundsResolver (dynamic-city-growth, T4: absorption growth, CITYGROW-03/05) ---

    [Fact]
    public void Resolve_grows_bounds_to_include_an_overflow_building_within_the_absorption_ring()
    {
        var city = new City(new CityId(Guid.NewGuid()), new CellCoord(50, 50), 0, null, new AggregatePopulationPool(0, 0, 0));
        var (baseBounds, _) = CityBoundsResolver.Resolve(city, population: 0, mapWidth: 1000, mapHeight: 1000);
        // 1 célula de distância da borda direita, dentro do AbsorptionRingCells default (3).
        int overflowX = baseBounds.Origin.X + baseBounds.Width - 1 + 1;
        var overflowBox = new CityBounds(new CellCoord(overflowX, baseBounds.Origin.Y), 2, 2);

        var (grown, isDerived) = CityBoundsResolver.Resolve(
            city, population: 0, mapWidth: 1000, mapHeight: 1000, ownedBuildingFootprintBoxes: [overflowBox]);

        Assert.True(isDerived);
        // O footprint inteiro do prédio de overflow (não só a célula mais próxima) precisa caber
        // no box resolvido (AC3: "expand to include that building's full footprint").
        Assert.True(grown.Contains(new CellCoord(overflowBox.Origin.X, overflowBox.Origin.Y)));
        Assert.True(grown.Contains(new CellCoord(overflowBox.Origin.X + 1, overflowBox.Origin.Y + 1)));
        Assert.True(grown.Width > baseBounds.Width || grown.Height > baseBounds.Height);
    }

    [Fact]
    public void Resolve_ignores_an_overflow_building_farther_than_the_absorption_ring()
    {
        var city = new City(new CityId(Guid.NewGuid()), new CellCoord(50, 50), 0, null, new AggregatePopulationPool(0, 0, 0));
        var (baseBounds, _) = CityBoundsResolver.Resolve(city, population: 0, mapWidth: 1000, mapHeight: 1000);
        // Bem além do AbsorptionRingCells default (3) — não deve alterar os bounds.
        var farBox = new CityBounds(new CellCoord(baseBounds.Origin.X + baseBounds.Width + 50, baseBounds.Origin.Y), 1, 1);

        var (grown, _) = CityBoundsResolver.Resolve(
            city, population: 0, mapWidth: 1000, mapHeight: 1000, ownedBuildingFootprintBoxes: [farBox]);

        Assert.Equal(baseBounds, grown);
    }

    [Fact]
    public void Absorption_growth_still_never_exceeds_the_hard_map_dimension_cap()
    {
        // Mesmo cenário de teste do bugfix real (mapa 20x20) que já garante o teto por população
        // — aqui um prédio de overflow longe o bastante (mas dentro do anel) tentaria empurrar o
        // box além do mapa; o teto de Math.Min(mapWidth, mapHeight)/2 continua valendo.
        var city = new City(new CityId(Guid.NewGuid()), new CellCoord(10, 10), 0, null, new AggregatePopulationPool(0, 0, 0));
        var hugeOverflowBox = new CityBounds(new CellCoord(-100, -100), 300, 300);

        var (grown, _) = CityBoundsResolver.Resolve(
            city, population: 150, mapWidth: 20, mapHeight: 20, ownedBuildingFootprintBoxes: [hugeOverflowBox]);

        Assert.True(grown.Width <= 10);
        Assert.True(grown.Height <= 10);
    }

    /// <summary>dynamic-city-growth, round-3 fix F (spec.md AC3/AC5): a absorção só é limitada
    /// pelo mapa (<c>Math.Min(mapWidth, mapHeight) / 2</c>), nunca por <see
    /// cref="CityBoundsResolver"/>'s teto por população (MaxSize=12) -- o teste de população já
    /// confirma o teto SEM overflow; este confirma que overflow suficiente ainda cresce PASSADO
    /// ele.</summary>
    [Fact]
    public void Absorption_growth_can_exceed_the_population_based_max_size_of_12()
    {
        var city = new City(new CityId(Guid.NewGuid()), new CellCoord(500, 500), 0, null, new AggregatePopulationPool(0, 0, 0));
        // População grande o bastante pra bater no teto por população (12) num mapa gigante
        // (o teto por mapa não é o fator limitante aqui).
        var (baseBounds, _) = CityBoundsResolver.Resolve(city, population: 10_000, mapWidth: 10_000, mapHeight: 10_000);
        Assert.Equal(12, baseBounds.Width); // confirma que já está no teto por população antes do overflow

        // Prédio de overflow dentro do anel de absorção (default 3), mas longe o bastante da
        // borda pra empurrar a bounding box além de 12.
        int overflowX = baseBounds.Origin.X + baseBounds.Width - 1 + 2; // 2 células fora da borda direita
        var overflowBox = new CityBounds(new CellCoord(overflowX, baseBounds.Origin.Y), 3, 3);

        var (grown, _) = CityBoundsResolver.Resolve(
            city, population: 10_000, mapWidth: 10_000, mapHeight: 10_000, ownedBuildingFootprintBoxes: [overflowBox]);

        Assert.True(grown.Width > 12); // cresceu passado o teto por população -- só o mapa (5000) limita
    }

    [Fact]
    public void Absorption_growth_never_mutates_the_positions_of_existing_buildings()
    {
        var city = new City(new CityId(Guid.NewGuid()), new CellCoord(50, 50), 0, null, new AggregatePopulationPool(0, 0, 0));
        var authored = new Building(
            new BuildingId(1), city.Id, buildingTypeId: 1, completedAtTick: 0,
            position: new CellCoord(52, 52), orientation: 0);
        var overflowBox = new CityBounds(authored.Position!.Value, 1, 1);

        CityBoundsResolver.Resolve(city, population: 0, mapWidth: 1000, mapHeight: 1000, ownedBuildingFootprintBoxes: [overflowBox]);

        // Resolve é uma função pura sobre dados de entrada -- nunca escreve de volta em Building.
        Assert.Equal(new CellCoord(52, 52), authored.Position);
        Assert.Equal(0, authored.Orientation);
    }

    /// <summary>Post-ship fix (round 2, 2026-08-23): the base population-only box (no overflow
    /// buildings at all) used to skip the <c>otherCityBoundsToAvoid</c> clamp entirely -- the
    /// clamp only ever applied to the MERGED overflow boxes. Since households don't have real
    /// building positions yet, this population box is what's actually rendered/dominant on the
    /// map, so two cities' population boxes could grow into contact purely from population
    /// increase, with zero overflow buildings involved. Reproduces exactly that: no
    /// <c>ownedBuildingFootprintBoxes</c> passed anywhere, just population growth toward a
    /// neighbor.</summary>
    [Fact]
    public void Resolve_clamps_the_population_only_box_against_other_cities_even_with_no_overflow_buildings()
    {
        // Longe da borda do mapa (500,500 num mapa 1000x1000) -- o clamp de borda de mapa
        // (ClampOrigin) não pode interferir, só o clamp entre cidades sob teste aqui.
        var cityA = new City(new CityId(Guid.NewGuid()), new CellCoord(500, 500), 0, null, new AggregatePopulationPool(0, 0, 0));
        var cityB = new City(new CityId(Guid.NewGuid()), new CellCoord(510, 500), 0, null, new AggregatePopulationPool(0, 0, 0));
        const int absorptionRingCells = 3;

        var (boundsB, _) = CityBoundsResolver.Resolve(cityB, population: 10_000, mapWidth: 1000, mapHeight: 1000);

        var (boundsA, _) = CityBoundsResolver.Resolve(
            cityA, population: 10_000, mapWidth: 1000, mapHeight: 1000,
            otherCityBoundsToAvoid: [boundsB]);

        Assert.True(ChebyshevGapForTest(boundsA, boundsB) >= absorptionRingCells);
    }

    /// <summary>Mesma fórmula de <c>CityBoundsResolver.ChebyshevGap</c> (privada) -- duplicada aqui
    /// só pra afirmar o comportamento observável (o gap real entre as duas caixas resolvidas),
    /// não detalhe de implementação.</summary>
    private static int ChebyshevGapForTest(CityBounds a, CityBounds b)
    {
        int aRight = a.Origin.X + a.Width - 1, aBottom = a.Origin.Y + a.Height - 1;
        int bRight = b.Origin.X + b.Width - 1, bBottom = b.Origin.Y + b.Height - 1;
        int dx = Math.Max(0, Math.Max(a.Origin.X - bRight, b.Origin.X - aRight));
        int dy = Math.Max(0, Math.Max(a.Origin.Y - bBottom, b.Origin.Y - aBottom));
        return Math.Max(dx, dy);
    }

    // --- BuildingPlacementResolver (dynamic-city-growth, T3: occupancy/overflow-aware) ---

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

    [Fact]
    public void Authored_building_position_and_orientation_take_precedence_and_are_marked_not_derived()
    {
        var world = ScenarioRunner.Create(seed: 41, initialPopulation: 0).World;
        var city = new City(world.NextCityId(), new CellCoord(0, 0), 0, null, new AggregatePopulationPool(0, 0, 0));
        world.AddCity(city);
        var bounds = new CityBounds(new CellCoord(0, 0), 12, 12);
        var authored = new Building(
            new BuildingId(1), city.Id, buildingTypeId: 1, completedAtTick: 0,
            position: new CellCoord(9, 9), orientation: 180);

        var resolved = BuildingPlacementResolver.Resolve(authored, city, world, bounds);

        Assert.NotNull(resolved);
        Assert.False(resolved!.Value.IsDerived);
        Assert.Equal(new CellCoord(9, 9), resolved.Value.Position);
        Assert.Equal(180, resolved.Value.Orientation);
    }

    [Fact]
    public void Engine_built_building_without_authored_position_gets_a_deterministic_derived_fallback()
    {
        var world = ScenarioRunner.Create(seed: 42, initialPopulation: 0).World;
        var city = new City(world.NextCityId(), new CellCoord(20, 20), 0, null, new AggregatePopulationPool(0, 0, 0));
        world.AddCity(city);
        var bounds = new CityBounds(new CellCoord(10, 10), 20, 20);
        var legacy = new Building(new BuildingId(42), city.Id, buildingTypeId: 1, completedAtTick: 0);

        var first = BuildingPlacementResolver.Resolve(legacy, city, world, bounds);
        var second = BuildingPlacementResolver.Resolve(legacy, city, world, bounds);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.True(first!.Value.IsDerived);
        Assert.Equal(first.Value.Position, second!.Value.Position); // determinístico, não sorteia
        Assert.Contains(first.Value.Orientation, new[] { 0, 90, 180, 270 });
    }

    [Fact]
    public void Derived_positions_for_two_different_buildings_in_the_same_city_do_not_collide()
    {
        var world = ScenarioRunner.Create(seed: 44, initialPopulation: 0).World;
        var city = new City(world.NextCityId(), new CellCoord(0, 0), 0, null, new AggregatePopulationPool(0, 0, 0));
        world.AddCity(city);
        var bounds = new CityBounds(new CellCoord(-20, -20), 40, 40);
        var buildingA = new Building(new BuildingId(1), city.Id, buildingTypeId: 1, completedAtTick: 0);
        var buildingB = new Building(new BuildingId(2), city.Id, buildingTypeId: 1, completedAtTick: 0);

        var resolvedA = BuildingPlacementResolver.Resolve(buildingA, city, world, bounds);
        world.AddBuilding(buildingA); // agora ocupa de verdade — B precisa desviar dela
        var resolvedB = BuildingPlacementResolver.Resolve(buildingB, city, world, bounds);

        Assert.NotNull(resolvedA);
        Assert.NotNull(resolvedB);
        Assert.NotEqual(resolvedA!.Value.Position, resolvedB!.Value.Position);
    }

    [Fact]
    public void Resolve_places_inside_the_citys_current_bounds_when_a_free_cell_exists_there()
    {
        var world = ScenarioRunner.Create(seed: 45, initialPopulation: 0).World;
        var city = new City(world.NextCityId(), new CellCoord(0, 0), 0, null, new AggregatePopulationPool(0, 0, 0));
        world.AddCity(city);
        var bounds = new CityBounds(new CellCoord(0, 0), 12, 12); // vazia -- sempre há célula livre
        var building = new Building(new BuildingId(1), city.Id, buildingTypeId: 1, completedAtTick: 0);

        var resolved = BuildingPlacementResolver.Resolve(building, city, world, bounds);

        Assert.NotNull(resolved);
        Assert.True(resolved!.Value.IsDerived);
        var shape = BuildingFootprintGenerator.Generate(building.Id, building.BuildingTypeId).Select(c => c.Cell).ToList();
        Assert.True(CityOccupancy.Translate(shape, resolved.Value.Position).All(bounds.Contains));
    }

    [Fact]
    public void Resolve_falls_back_to_the_overflow_ring_when_the_citys_bounds_are_fully_occupied()
    {
        var (rectId, _, w, h) = FindRectangularFootprint(typeId: 9);
        var world = ScenarioRunner.Create(seed: 46, initialPopulation: 0).World;
        var city = new City(world.NextCityId(), new CellCoord(0, 0), 0, null, new AggregatePopulationPool(0, 0, 0));
        world.AddCity(city);
        var bounds = new CityBounds(new CellCoord(0, 0), w, h);
        world.AddBuilding(new Building(rectId, city.Id, buildingTypeId: 9, completedAtTick: 0, position: new CellCoord(0, 0), orientation: 0));

        var newBuilding = new Building(new BuildingId(500), city.Id, buildingTypeId: 9, completedAtTick: 0);
        var resolved = BuildingPlacementResolver.Resolve(newBuilding, city, world, bounds);

        Assert.NotNull(resolved);
        Assert.True(resolved!.Value.IsDerived);
        var newShape = BuildingFootprintGenerator.Generate(newBuilding.Id, newBuilding.BuildingTypeId).Select(c => c.Cell).ToList();
        var translated = CityOccupancy.Translate(newShape, resolved.Value.Position);
        Assert.False(translated.All(bounds.Contains)); // desbordou dos bounds, que estão 100% ocupados
        Assert.True(CityOccupancy.IsFree(world, city, bounds, translated));
    }

    // --- dynamic-city-growth, fix (major, CITYGROW-02b): escassez de terra no caminho de posicionamento ---

    [Fact]
    public void Resolve_returns_null_when_the_whole_map_has_no_free_cell_anywhere()
    {
        var (rectId, _, w, h) = FindRectangularFootprint(typeId: 9);
        var world = BuildWorldWithMap(w, h, seed: 47); // mapa do tamanho exato do único prédio -- zero espaço de sobra
        var city = new City(world.NextCityId(), new CellCoord(0, 0), 0, null, new AggregatePopulationPool(0, 0, 0));
        world.AddCity(city);
        var bounds = new CityBounds(new CellCoord(0, 0), w, h);
        world.AddBuilding(new Building(rectId, city.Id, buildingTypeId: 9, completedAtTick: 0, position: new CellCoord(0, 0), orientation: 0));

        var newBuilding = new Building(new BuildingId(500), city.Id, buildingTypeId: 9, completedAtTick: 0);
        var resolved = BuildingPlacementResolver.Resolve(newBuilding, city, world, bounds);

        // CITYGROW-02b: nenhuma célula livre em lugar nenhum do mapa -> Resolve recusa
        // (null), nunca inventa uma posição fora do mapa nem sobrepõe o prédio existente.
        Assert.Null(resolved);
    }

    [Fact]
    public void Resolve_never_returns_a_position_outside_the_maps_bounds_when_only_far_room_remains()
    {
        var (rectId, _, w, h) = FindRectangularFootprint(typeId: 9);
        const int mapWidth = 30, mapHeight = 30;
        var world = BuildWorldWithMap(mapWidth, mapHeight, seed: 48); // mapa com espaço real, só longe da cidade
        var city = new City(world.NextCityId(), new CellCoord(0, 0), 0, null, new AggregatePopulationPool(0, 0, 0));
        world.AddCity(city);
        var bounds = new CityBounds(new CellCoord(0, 0), w, h); // bounds da cidade 100% ocupados
        world.AddBuilding(new Building(rectId, city.Id, buildingTypeId: 9, completedAtTick: 0, position: new CellCoord(0, 0), orientation: 0));

        var newBuilding = new Building(new BuildingId(501), city.Id, buildingTypeId: 9, completedAtTick: 0);
        var resolved = BuildingPlacementResolver.Resolve(newBuilding, city, world, bounds);

        Assert.NotNull(resolved);
        var newShape = BuildingFootprintGenerator.Generate(newBuilding.Id, newBuilding.BuildingTypeId).Select(c => c.Cell).ToList();
        var translated = CityOccupancy.Translate(newShape, resolved!.Value.Position);
        // A célula (e o footprint inteiro) tem que caber dentro do mapa real -- nunca negativa
        // nem além de mapWidth/mapHeight, mesmo tendo crescido o raio do anel bem além dos bounds.
        Assert.True(translated.All(cell => cell.X >= 0 && cell.X < mapWidth && cell.Y >= 0 && cell.Y < mapHeight));
    }
}
