using LivingWorld.Domain;

namespace LivingWorld.Tests.Behavior;

/// <summary>Fase 4, task 11: <see cref="TravelResolution.TicksBetween"/> — conversão pura de
/// <see cref="MovementCost.Between"/> em ticks de deslocamento (NEEDS-14).</summary>
public class TravelResolutionTests
{
    private static readonly GeographyCatalog Catalog = new(
        TerrainIds: new HashSet<int> { 1 }, BiomeIds: new HashSet<int> { 1 }, ResourceIds: new HashSet<int>());

    private static WorldMap MakeMap(double costBase, double altitudeWeight = 0)
    {
        var cost = new CostWeights(Base: costBase, AltitudeWeight: altitudeWeight, TerrainWeight: new Dictionary<int, double> { [1] = 1.0 });
        var cells = new List<MapCell>
        {
            new(new CellCoord(0, 0), new TerrainType(1), new BiomeType(1), Altitude: 0, HasWater: false, Resources: []),
            new(new CellCoord(1, 0), new TerrainType(1), new BiomeType(1), Altitude: 0, HasWater: false, Resources: []),
        };
        var regions = RegionGrid.Partition(width: 2, height: 1, regionSize: 2);
        return WorldMap.Create(width: 2, height: 1, seed: 1, Catalog, cost, cells, regions, settlements: []).Value!;
    }

    [Fact]
    public void Same_location_consumes_zero_ticks()
    {
        var map = MakeMap(costBase: 5.0);
        var here = new CellCoord(0, 0);

        Assert.Equal(0, TravelResolution.TicksBetween(map, here, here));
    }

    [Fact]
    public void Distinct_locations_with_cost_below_one_still_consume_at_least_one_tick()
    {
        var map = MakeMap(costBase: 0.1); // dist=1 * terrainFactor=1 * base=0.1 => cost=0.1 < 1
        double rawCost = MovementCost.Between(map, new CellCoord(0, 0), new CellCoord(1, 0));
        Assert.True(rawCost < 1);

        long ticks = TravelResolution.TicksBetween(map, new CellCoord(0, 0), new CellCoord(1, 0));

        Assert.Equal(1, ticks);
    }

    [Fact]
    public void Distinct_locations_consume_the_ceiling_of_the_movement_cost()
    {
        var map = MakeMap(costBase: 1.6); // dist=1 * terrainFactor=1 * base=1.6 => cost=1.6
        double rawCost = MovementCost.Between(map, new CellCoord(0, 0), new CellCoord(1, 0));

        long ticks = TravelResolution.TicksBetween(map, new CellCoord(0, 0), new CellCoord(1, 0));

        Assert.Equal((long)Math.Ceiling(rawCost), ticks);
        Assert.True(ticks > 1);
    }
}
