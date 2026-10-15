using System.Text.Json;
using LivingWorld.Api.Visual.Projection;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Geography.Spatial;
using LivingWorld.Domain.Population;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Visual.Projection;

/// <summary>#46 fatia 2: quem está dentro de um prédio chega ao cliente com a mesma aparência de fora (snapshot e delta de interior);
/// sem <c>AppearanceRules</c> o campo nem é serializado.</summary>
public class InteriorAppearanceTests
{
    private static (WorldState World, Building Building, Npc Inside) Setup(AppearanceRules? rules)
    {
        var world = ScenarioRunner.Create(seed: 13, initialPopulation: 4, appearanceRules: rules).World;
        var city = world.ActiveCities().First();
        var building = new Building(world.NextBuildingIdAndAdvance(), city.Id, buildingTypeId: 1, completedAtTick: 0);
        world.AddBuilding(building);
        var inside = world.Npcs.First(n => n.IsAlive);
        inside.EnterBuilding(building.Id, FloorLevel.Ground, new CellCoord(1, 1));
        return (world, building, inside);
    }

    [Fact]
    public void Snapshot_and_delta_occupants_carry_the_looks_of_the_npc()
    {
        var (world, building, inside) = Setup(new AppearanceRules(0.6));

        var snapshot = Assert.Single(InteriorProjector.Build(world, building.Id).Value!.Occupants);
        var delta = Assert.Single(InteriorProjector.BuildOccupants(world, building.Id)!);

        Assert.NotNull(inside.Appearance);
        Assert.Equal(inside.Appearance, snapshot.Appearance);
        Assert.Equal(inside.Appearance, delta.Appearance);
        Assert.Contains("\"appearance\"", JsonSerializer.Serialize(snapshot, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }

    [Fact]
    public void Without_the_rules_the_occupant_has_no_looks_and_the_field_is_not_serialized()
    {
        var (world, building, _) = Setup(null);

        var occupant = Assert.Single(InteriorProjector.Build(world, building.Id).Value!.Occupants);

        Assert.Null(occupant.Appearance);
        Assert.DoesNotContain("ppearance", JsonSerializer.Serialize(occupant, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }
}
