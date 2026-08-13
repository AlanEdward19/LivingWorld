using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Geography;

/// <summary>Fase 15.1, T46 (ADR-0018): escala World/City/Building como dado de domínio,
/// navegação vertical reversível e contrato de caminhabilidade.</summary>
public class SpatialAddressAndScaleTests
{
    // --- SpaceScale ---

    [Fact]
    public void ToChild_multiplies_by_the_declared_scale_factor()
    {
        Assert.Equal(new CellCoord(2 * SpaceScale.WorldTilesPerCityTile, 3 * SpaceScale.WorldTilesPerCityTile),
            SpaceScale.ToChild(SpaceKind.City, new CellCoord(2, 3)));
        Assert.Equal(new CellCoord(5 * SpaceScale.CityTilesPerBuildingTile, 1 * SpaceScale.CityTilesPerBuildingTile),
            SpaceScale.ToChild(SpaceKind.Building, new CellCoord(5, 1)));
    }

    [Fact]
    public void ToParent_divides_by_the_declared_scale_factor()
    {
        Assert.Equal(new CellCoord(2, 3),
            SpaceScale.ToParent(SpaceKind.City, new CellCoord(2 * SpaceScale.WorldTilesPerCityTile, 3 * SpaceScale.WorldTilesPerCityTile)));
    }

    [Fact]
    public void Round_trip_is_exact_when_the_local_coordinate_is_aligned_to_the_scale_factor()
    {
        var aligned = new CellCoord(4 * SpaceScale.CityTilesPerBuildingTile, 7 * SpaceScale.CityTilesPerBuildingTile);

        var parent = SpaceScale.ToParent(SpaceKind.Building, aligned);
        var back = SpaceScale.ToChild(SpaceKind.Building, parent);

        Assert.Equal(aligned, back);
    }

    [Fact]
    public void WorldSpace_has_no_parent_and_throws()
    {
        Assert.Throws<ArgumentException>(() => SpaceScale.ToParent(SpaceKind.World, new CellCoord(0, 0)));
    }

    // --- FloorNavigator ---

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(-2)]
    public void Up_then_down_returns_to_the_original_floor(int startingFloor)
    {
        var start = new FloorLevel(startingFloor);

        var roundTrip = FloorNavigator.Down(FloorNavigator.Up(start));

        Assert.Equal(start, roundTrip);
    }

    [Fact]
    public void Down_then_up_also_returns_to_the_original_floor()
    {
        var start = FloorLevel.Ground;

        var roundTrip = FloorNavigator.Up(FloorNavigator.Down(start));

        Assert.Equal(start, roundTrip);
    }

    // --- InteriorWalkability ---

    [Theory]
    [InlineData(BuildingMaterial.Floor, true)]
    [InlineData(BuildingMaterial.Door, true)]
    [InlineData(BuildingMaterial.Stair, true)]
    [InlineData(BuildingMaterial.StoneWall, false)]
    [InlineData(BuildingMaterial.WoodWall, false)]
    public void IsWalkable_matches_the_declared_contract(BuildingMaterial material, bool expected)
    {
        Assert.Equal(expected, InteriorWalkability.IsWalkable(material));
    }

    // --- SpatialBoundsResolver ---

    [Fact]
    public void World_bounds_come_from_the_map_dimensions()
    {
        var map = ScenarioRunner.DefaultMap(seed: 1);

        var bounds = SpatialBoundsResolver.ResolveWorld(map);

        Assert.Equal(new CellCoord(0, 0), bounds.Origin);
        Assert.Equal(map.Width, bounds.Width);
        Assert.Equal(map.Height, bounds.Height);
    }

    [Fact]
    public void City_bounds_delegate_to_CityBoundsResolver()
    {
        var city = new City(new CityId(Guid.NewGuid()), new CellCoord(10, 10), 0, null, new AggregatePopulationPool(20, 0, 0));

        var (bounds, isDerived) = SpatialBoundsResolver.ResolveCity(city, population: 20, mapWidth: 100, mapHeight: 100);
        var (expectedBounds, expectedIsDerived) = CityBoundsResolver.Resolve(city, population: 20, mapWidth: 100, mapHeight: 100);

        Assert.Equal(expectedBounds, bounds);
        Assert.Equal(expectedIsDerived, isDerived);
    }

    [Fact]
    public void Building_bounds_match_the_footprint_dimensions()
    {
        var building = new Building(new BuildingId(9), new CityId(Guid.NewGuid()), buildingTypeId: 3, completedAtTick: 0);
        var footprint = BuildingFootprintGenerator.Generate(building.Id, building.BuildingTypeId);

        var bounds = SpatialBoundsResolver.ResolveBuilding(building);

        Assert.Equal(footprint.Max(c => c.Cell.X) + 1, bounds.Width);
        Assert.Equal(footprint.Max(c => c.Cell.Y) + 1, bounds.Height);
    }
}
