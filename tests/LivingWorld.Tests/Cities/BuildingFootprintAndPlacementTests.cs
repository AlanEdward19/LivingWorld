using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Cities;

/// <summary>Fase 15.1, T45 (backend-gaps.md G4): footprint por material, bounds de cidade e
/// posição/orientação de prédio — geometria canônica que T20 vai expor na projeção. Autoria
/// (T44) tem precedência; sem ela, fallback determinístico e estável.</summary>
public class BuildingFootprintAndPlacementTests
{
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

        var (position, orientation, isDerived) = BuildingPlacementResolver.Resolve(authored, city, world, bounds);

        Assert.False(isDerived);
        Assert.Equal(new CellCoord(9, 9), position);
        Assert.Equal(180, orientation);
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

        Assert.True(first.IsDerived);
        Assert.Equal(first.Position, second.Position); // determinístico, não sorteia
        Assert.Equal(0, first.Orientation);
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

        var (positionA, _, _) = BuildingPlacementResolver.Resolve(buildingA, city, world, bounds);
        world.AddBuilding(buildingA); // agora ocupa de verdade — B precisa desviar dela
        var (positionB, _, _) = BuildingPlacementResolver.Resolve(buildingB, city, world, bounds);

        Assert.NotEqual(positionA, positionB);
    }

    [Fact]
    public void Resolve_places_inside_the_citys_current_bounds_when_a_free_cell_exists_there()
    {
        var world = ScenarioRunner.Create(seed: 45, initialPopulation: 0).World;
        var city = new City(world.NextCityId(), new CellCoord(0, 0), 0, null, new AggregatePopulationPool(0, 0, 0));
        world.AddCity(city);
        var bounds = new CityBounds(new CellCoord(0, 0), 12, 12); // vazia -- sempre há célula livre
        var building = new Building(new BuildingId(1), city.Id, buildingTypeId: 1, completedAtTick: 0);

        var (position, _, isDerived) = BuildingPlacementResolver.Resolve(building, city, world, bounds);

        Assert.True(isDerived);
        var shape = BuildingFootprintGenerator.Generate(building.Id, building.BuildingTypeId).Select(c => c.Cell).ToList();
        Assert.True(CityOccupancy.Translate(shape, position).All(bounds.Contains));
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
        var (position, _, isDerived) = BuildingPlacementResolver.Resolve(newBuilding, city, world, bounds);

        Assert.True(isDerived);
        var newShape = BuildingFootprintGenerator.Generate(newBuilding.Id, newBuilding.BuildingTypeId).Select(c => c.Cell).ToList();
        var translated = CityOccupancy.Translate(newShape, position);
        Assert.False(translated.All(bounds.Contains)); // desbordou dos bounds, que estão 100% ocupados
        Assert.True(CityOccupancy.IsFree(world, city, bounds, translated));
    }
}
