using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Geography;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Economy;

public sealed class ScenarioRunnerWorkplaceBuildingTests
{
    [Theory]
    [InlineData(0, 10)]
    [InlineData(100, 60)]
    [InlineData(5_000, 390)]
    public void Initial_map_side_reserves_worst_case_building_area_for_population(
        int population, int expectedSide)
    {
        Assert.Equal(expectedSide, ScenarioRunner.InitialMapSideForPopulation(population));
    }

    [Fact]
    public void Create_default_workplaces_each_have_one_building_at_their_location()
    {
        var (world, _) = ScenarioRunner.Create(seed: 41, initialPopulation: 0);

        Assert.All(world.Workplaces, workplace =>
            Assert.Single(world.Buildings, building =>
                building.City == workplace.City && building.Position == workplace.Location));
    }

    [Fact]
    public void Create_default_workplace_building_footprints_do_not_overlap()
    {
        var (world, _) = ScenarioRunner.Create(seed: 42, initialPopulation: 0);
        var buildings = world.Buildings.OrderBy(building => building.Id.Value).ToArray();

        var firstCells = AbsoluteFootprint(buildings[0]).ToHashSet();
        var secondCells = AbsoluteFootprint(buildings[1]);

        Assert.DoesNotContain(secondCells, firstCells.Contains);
    }

    private static IEnumerable<CellCoord> AbsoluteFootprint(Building building)
    {
        var origin = building.Position!.Value;
        return BuildingFootprintGenerator.Generate(building.Id, building.BuildingTypeId)
            .Select(cell => new CellCoord(origin.X + cell.Cell.X, origin.Y + cell.Cell.Y));
    }
}
