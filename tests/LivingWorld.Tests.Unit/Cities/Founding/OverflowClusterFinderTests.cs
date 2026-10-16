using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Geography.Map;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cities.Founding;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Cities.Founding;

/// <summary>dynamic-city-growth, T6 (CITYGROW-04): <see cref="OverflowClusterFinder"/> — agrupa
/// prédios de overflow por distância mútua (encadeada), exclui os já dentro do alcance de
/// absorção de qualquer cidade, e conta só residentes materializados de verdade. Também cobre o
/// marcador <see cref="Building.ClusterFoundingScheduledAtTick"/>.</summary>
public class OverflowClusterFinderTests
{
    private static readonly GeographyCatalog TinyCatalog = new(
        TerrainIds: new HashSet<int> { 1 }, BiomeIds: new HashSet<int> { 1 }, ResourceIds: new HashSet<int>());

    private static readonly CostWeights TinyCostWeights = new(
        Base: 1.0, AltitudeWeight: 0.5, TerrainWeight: new Dictionary<int, double> { [1] = 1.0 });

    // Mapa bem maior que o teto de bounds populacionais (map/2) pra nenhum teste aqui bater no
    // teto de borda do mapa por acidente (CityOccupancyTests já usa essa mesma tática pra mapas
    // dedicados) — 3x3 populacional (pop=0) longe de qualquer canto em 300x300.
    private static WorldState BuildBigWorld(ulong seed)
    {
        var map = MapGenerator.Generate(seed, width: 300, height: 300, regionSize: 300, TinyCatalog, TinyCostWeights, [])
            .Value ?? throw new InvalidOperationException("mapa de teste inválido — bug no teste, não no gerador");
        return new WorldState(
            ScenarioRunner.DefaultCalendar, seed, map, ScenarioRunner.DefaultPopulationCatalog,
            ScenarioRunner.DefaultPopulationRules, ScenarioRunner.DefaultNeedsRules,
            ScenarioRunner.DefaultActionCatalog, ScenarioRunner.DefaultLifeStageRules);
    }

    private static readonly Personality NeutralPersonality =
        Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    private static Npc MakeNpc(WorldState world, long id, CellCoord location) => new(
        new NpcId(id), $"npc-{id}", Sex.Male, world.CurrentDate.AddYears(-30), ScenarioRunner.DefaultCulture,
        location, motherId: null, fatherId: null, household: null, health: 80,
        personality: NeutralPersonality, profession: ProfessionType.None, currentLocation: location);

    // --- Cluster grouping + absorption exclusion ---

    [Fact]
    public void FindClusters_groups_mutually_close_overflow_buildings_transitively_into_one_cluster()
    {
        var world = BuildBigWorld(801);
        var city = new City(world.NextCityId(), new CellCoord(100, 100), 0, null, AggregatePopulationPool.Empty);
        world.AddCity(city);

        // Bounds populacionais (pop=0) são 3x3 em (99,99)-(101,101). Os dois prédios ficam a
        // dezenas de células dali (nenhum absorvido pela própria cidade), mas o segundo tem sua
        // origem só 4 células a leste da origem do primeiro — dentro do AbsorptionRingCells
        // default (3) na pior hipótese de largura do footprint (4..6 células) — formam UM cluster
        // encadeado, mesmo sem estarem exatamente na mesma posição.
        var b1 = new Building(world.NextBuildingIdAndAdvance(), city.Id, buildingTypeId: 1, completedAtTick: 0,
            position: new CellCoord(200, 200), orientation: 0);
        var b2 = new Building(world.NextBuildingIdAndAdvance(), city.Id, buildingTypeId: 1, completedAtTick: 0,
            position: new CellCoord(204, 200), orientation: 0);
        world.AddBuilding(b1);
        world.AddBuilding(b2);

        var clusters = OverflowClusterFinder.FindClusters(world, city);

        var cluster = Assert.Single(clusters);
        Assert.Equal(2, cluster.Buildings.Count);
        Assert.Contains(b1, cluster.Buildings);
        Assert.Contains(b2, cluster.Buildings);
    }

    [Fact]
    public void FindClusters_excludes_a_building_within_absorption_range_of_its_own_city()
    {
        var world = BuildBigWorld(802);
        var city = new City(world.NextCityId(), new CellCoord(100, 100), 0, null, AggregatePopulationPool.Empty);
        world.AddCity(city);

        // Bounds populacionais 3x3: (99,99)-(101,101). Origem do prédio em x=102 -> a borda
        // esquerda do footprint (sempre = Position.X, independente da largura sorteada) fica a
        // só 1 célula da borda direita dos bounds (101) -- dentro do AbsorptionRingCells default
        // (3) -> absorvido pelos bounds crescidos da própria cidade, nunca overflow (spec Edge
        // Cases: absorção tem precedência).
        var absorbed = new Building(world.NextBuildingIdAndAdvance(), city.Id, buildingTypeId: 1, completedAtTick: 0,
            position: new CellCoord(102, 100), orientation: 0);
        world.AddBuilding(absorbed);

        var clusters = OverflowClusterFinder.FindClusters(world, city);

        Assert.Empty(clusters);
    }

    [Fact]
    public void FindClusters_excludes_overflow_building_within_absorption_range_of_a_different_city()
    {
        var world = BuildBigWorld(803);
        var ownCity = new City(world.NextCityId(), new CellCoord(10, 10), 0, null, AggregatePopulationPool.Empty);
        var otherCity = new City(world.NextCityId(), new CellCoord(100, 100), 0, null, AggregatePopulationPool.Empty);
        world.AddCity(ownCity);
        world.AddCity(otherCity);

        // Prédio pertence a ownCity (longe demais dos bounds de ownCity pra ser absorvido por
        // ela), mas sua borda esquerda (=Position.X, independente da largura sorteada) fica a só
        // 1 célula da borda direita dos bounds populacionais de otherCity (99,99)-(101,101) ->
        // dentro do alcance de absorção de otherCity -> excluído da elegibilidade de fundação de
        // ownCity (spec Edge Cases: nunca funda perto demais de QUALQUER cidade existente).
        var overflow = new Building(world.NextBuildingIdAndAdvance(), ownCity.Id, buildingTypeId: 1, completedAtTick: 0,
            position: new CellCoord(102, 100), orientation: 0);
        world.AddBuilding(overflow);

        var clusters = OverflowClusterFinder.FindClusters(world, ownCity);

        Assert.Empty(clusters);
    }

    // --- Population count ---

    [Fact]
    public void FindClusters_counts_only_materialized_alive_npcs_located_inside_the_cluster_bounds()
    {
        var world = BuildBigWorld(804);
        var city = new City(world.NextCityId(), new CellCoord(100, 100), 0, null, AggregatePopulationPool.Empty);
        world.AddCity(city);
        var overflow = new Building(world.NextBuildingIdAndAdvance(), city.Id, buildingTypeId: 1, completedAtTick: 0,
            position: new CellCoord(200, 200), orientation: 0);
        world.AddBuilding(overflow);

        // (200,200) é sempre a célula (0,0) do footprint (canto superior-esquerdo da planta),
        // presente em toda planta gerada independente de largura/altura/formato L.
        var inside = MakeNpc(world, 1, new CellCoord(200, 200));
        var outside = MakeNpc(world, 2, new CellCoord(0, 0)); // fora do cluster inteiramente
        var dead = MakeNpc(world, 3, new CellCoord(200, 200));
        dead.Die(world.CurrentDate);
        world.AddNpc(inside);
        world.AddNpc(outside);
        world.AddNpc(dead);

        var cluster = Assert.Single(OverflowClusterFinder.FindClusters(world, city));

        Assert.Equal(1, cluster.Population); // só o vivo dentro dos bounds do cluster
    }

    [Fact]
    public void FindClusters_returns_zero_population_for_a_cluster_with_buildings_but_no_residents()
    {
        var world = BuildBigWorld(805);
        var city = new City(world.NextCityId(), new CellCoord(100, 100), 0, null, AggregatePopulationPool.Empty);
        world.AddCity(city);
        var overflow = new Building(world.NextBuildingIdAndAdvance(), city.Id, buildingTypeId: 1, completedAtTick: 0,
            position: new CellCoord(200, 200), orientation: 0);
        world.AddBuilding(overflow);

        var cluster = Assert.Single(OverflowClusterFinder.FindClusters(world, city));

        Assert.Equal(0, cluster.Population); // 1 prédio, 0 residentes -- nunca funda nada (T7)
    }

    // --- Building.ClusterFoundingScheduledAtTick marker ---

    [Fact]
    public void ClusterFoundingScheduledAtTick_defaults_to_null()
    {
        var building = new Building(new BuildingId(1), new CityId(Guid.NewGuid()), buildingTypeId: 1, completedAtTick: 0);

        Assert.Null(building.ClusterFoundingScheduledAtTick);
    }

    [Fact]
    public void MarkClusterFoundingScheduled_sets_the_marker_to_the_given_tick()
    {
        var building = new Building(new BuildingId(1), new CityId(Guid.NewGuid()), buildingTypeId: 1, completedAtTick: 0);

        building.MarkClusterFoundingScheduled(42);

        Assert.Equal(42, building.ClusterFoundingScheduledAtTick);
    }

    [Fact]
    public void MarkClusterFoundingScheduled_called_again_overwrites_rather_than_throwing()
    {
        var building = new Building(new BuildingId(1), new CityId(Guid.NewGuid()), buildingTypeId: 1, completedAtTick: 0);
        building.MarkClusterFoundingScheduled(10);

        building.MarkClusterFoundingScheduled(20);

        Assert.Equal(20, building.ClusterFoundingScheduledAtTick);
    }
}
