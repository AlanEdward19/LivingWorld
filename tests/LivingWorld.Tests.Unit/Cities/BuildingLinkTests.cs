using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Serialization;

namespace LivingWorld.Tests.Unit.Cities;

/// <summary>npc-building-occupancy C1/C2/C5/C6: casa e local de trabalho conhecem o seu prédio.</summary>
public class BuildingLinkTests
{
    private static WorldState SeededWorld() => ScenarioRunner.Create(seed: 31, initialPopulation: 40).World;

    [Fact]
    public void Seeded_household_dwelling_is_its_house_building_containing_the_bed_cell()
    {
        var world = SeededWorld();

        Assert.NotEmpty(world.Households);
        foreach (var household in world.Households)
        {
            var dwelling = Assert.IsType<BuildingId>(household.Dwelling);
            var house = Assert.IsType<Building>(world.FindBuilding(dwelling));
            Assert.Equal(-1, house.BuildingTypeId);
            Assert.Equal(household.City, house.City);
            var footprint = BuildingFootprintGenerator.Generate(house)
                .Select(c => new CellCoord(house.Position!.Value.X + c.Cell.X, house.Position.Value.Y + c.Cell.Y));
            Assert.Contains(household.Location, footprint);
        }
        Assert.Equal(world.Households.Count, world.Households.Select(h => h.Dwelling).Distinct().Count());
    }

    [Fact]
    public void Default_seeded_workplaces_link_to_their_buildings()
    {
        var world = SeededWorld();

        Assert.NotEmpty(world.Workplaces);
        foreach (var workplace in world.Workplaces)
        {
            var building = Assert.IsType<Building>(world.FindBuilding(Assert.IsType<BuildingId>(workplace.Building)));
            Assert.Equal(workplace.Location, building.Position);
            Assert.Equal(workplace.LocationType.Id, building.BuildingTypeId);
        }
    }

    [Fact]
    public void Referential_sweep_fails_for_a_dwelling_that_does_not_exist()
    {
        var world = SeededWorld();
        Assert.Empty(ReferentialIntegritySweep.Check(world));

        world.Households[0].LiveIn(new BuildingId(9_999_999));

        Assert.NotEmpty(ReferentialIntegritySweep.Check(world));
    }

    [Fact]
    public void Referential_sweep_fails_for_a_workplace_building_that_does_not_exist()
    {
        var world = ScenarioRunner.Create(seed: 31, initialPopulation: 40).World;
        var legacy = new Workplace(
            world.NextWorkplaceIdAndAdvance(), new LocationType(1), new CellCoord(0, 0), 1, employees: [],
            new Dictionary<ResourceType, long>(), Money.Zero, new Dictionary<ResourceType, long>(), building: new BuildingId(9_999_999));
        world.AddWorkplace(legacy);

        Assert.NotEmpty(ReferentialIntegritySweep.Check(world));
    }

    [Fact]
    public void Snapshot_round_trip_keeps_links_and_missing_fields_load_as_null()
    {
        var world = SeededWorld();
        var json = WorldSnapshot.Serialize(world);

        var restored = WorldSnapshot.Deserialize(json);

        Assert.Equal(world.Households.Select(h => h.Dwelling), restored.Households.Select(h => h.Dwelling));
        Assert.Equal(world.Workplaces.Select(w => w.Building), restored.Workplaces.Select(w => w.Building));

        var legacy = System.Text.Json.Nodes.JsonNode.Parse(json)!.AsObject();
        foreach (var household in legacy["Households"]!.AsArray()) household!.AsObject().Remove("Dwelling");
        foreach (var workplace in legacy["Workplaces"]!.AsArray()) workplace!.AsObject().Remove("Building");
        var old = WorldSnapshot.Deserialize(legacy.ToJsonString());
        Assert.All(old.Households, h => Assert.Null(h.Dwelling));
        Assert.All(old.Workplaces, w => Assert.Null(w.Building));
    }
}
