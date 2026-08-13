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
        Assert.Equal(4, emptyBounds.Width);
        Assert.Equal(4, emptyBounds.Height);
        Assert.Equal(new CellCoord(50 - 2, 60 - 2), emptyBounds.Origin);
        // Teto: nunca maior que o antigo tamanho fixo, mesmo para população muito grande (num
        // mapa grande o bastante pra o teto por população ser o fator limitante, não o mapa).
        Assert.Equal(34, bigBounds.Width);
        Assert.Equal(34, bigBounds.Height);
    }

    [Fact]
    public void City_bounds_never_exceed_the_smaller_map_dimension_even_when_population_asks_for_more()
    {
        // Bugfix real (usuário, 2026-08-13, rodada 2): confirmado ao vivo — template "Cidade
        // média" (mapa 20x20, população 150) ainda produzia lado 25, maior que o próprio mapa.
        var city = new City(new CityId(Guid.NewGuid()), new CellCoord(10, 10), 0, null, new AggregatePopulationPool(0, 0, 0));

        var (bounds, _) = CityBoundsResolver.Resolve(city, population: 150, mapWidth: 20, mapHeight: 20);

        Assert.True(bounds.Width <= 20);
        Assert.True(bounds.Height <= 20);
    }

    // --- BuildingPlacementResolver ---

    [Fact]
    public void Authored_building_position_and_orientation_take_precedence_and_are_marked_not_derived()
    {
        var city = new City(new CityId(Guid.NewGuid()), new CellCoord(0, 0), 0, null, new AggregatePopulationPool(0, 0, 0));
        var authored = new Building(
            new BuildingId(1), city.Id, buildingTypeId: 1, completedAtTick: 0,
            position: new CellCoord(9, 9), orientation: 180);

        var (position, orientation, isDerived) = BuildingPlacementResolver.Resolve(authored, city);

        Assert.False(isDerived);
        Assert.Equal(new CellCoord(9, 9), position);
        Assert.Equal(180, orientation);
    }

    [Fact]
    public void Engine_built_building_without_authored_position_gets_a_deterministic_derived_fallback()
    {
        var city = new City(new CityId(Guid.NewGuid()), new CellCoord(20, 20), 0, null, new AggregatePopulationPool(0, 0, 0));
        var legacy = new Building(new BuildingId(42), city.Id, buildingTypeId: 1, completedAtTick: 0);

        var first = BuildingPlacementResolver.Resolve(legacy, city);
        var second = BuildingPlacementResolver.Resolve(legacy, city);

        Assert.True(first.IsDerived);
        Assert.Equal(first.Position, second.Position); // determinístico, não sorteia
        Assert.Equal(0, first.Orientation);
    }

    [Fact]
    public void Derived_positions_for_two_different_buildings_in_the_same_city_do_not_collide()
    {
        var city = new City(new CityId(Guid.NewGuid()), new CellCoord(0, 0), 0, null, new AggregatePopulationPool(0, 0, 0));
        var buildingA = new Building(new BuildingId(1), city.Id, buildingTypeId: 1, completedAtTick: 0);
        var buildingB = new Building(new BuildingId(2), city.Id, buildingTypeId: 1, completedAtTick: 0);

        var (positionA, _, _) = BuildingPlacementResolver.Resolve(buildingA, city);
        var (positionB, _, _) = BuildingPlacementResolver.Resolve(buildingB, city);

        Assert.NotEqual(positionA, positionB);
    }
}
