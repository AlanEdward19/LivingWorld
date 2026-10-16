using LivingWorld.Api.Visual.Projection;
using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Visual.Projection;

/// <summary>Fase 15, T7 (VTT-08, VTT-09): <see cref="CityVisibilityFilter"/> — só moradores
/// dentro do raio do personagem sobrevivem ao filtro; override admin devolve o snapshot intacto.</summary>
public class CityVisibilityFilterTests
{
    private static (WorldState World, City City, Npc Near, Npc Far) MakeWorldWithCity()
    {
        var world = ScenarioRunner.Create(seed: 41, initialPopulation: 2).World;
        var npcs = world.Npcs.ToList();
        var near = npcs[0];
        var far = npcs[1];

        var city = new City(world.NextCityId(), near.CurrentLocation, foundedAtTick: 0, foundedFromCityId: null,
            aggregatePool: AggregatePopulationPool.Empty);
        world.AddCity(city);
        near.JoinCity(city.Id);
        far.JoinCity(city.Id);
        far.MoveTo(new CellCoord(near.CurrentLocation.X + 100, near.CurrentLocation.Y + 100), tick: 0);

        return (world, city, near, far);
    }

    [Fact]
    public void ApplyFog_keeps_only_residents_within_sight_radius_of_the_player()
    {
        var (world, city, near, far) = MakeWorldWithCity();
        var snapshot = CityProjector.Build(world, city.Id).Value!;

        var filtered = CityVisibilityFilter.ApplyFog(snapshot, near.CurrentLocation, adminOverride: false);

        var marker = Assert.Single(filtered.Residents);
        Assert.Equal(near.Id, marker.Id);
        Assert.DoesNotContain(filtered.Residents, r => r.Id == far.Id);
    }

    [Fact]
    public void ApplyFog_with_admin_override_returns_the_snapshot_unfiltered()
    {
        var (world, city, _, _) = MakeWorldWithCity();
        var snapshot = CityProjector.Build(world, city.Id).Value!;

        var filtered = CityVisibilityFilter.ApplyFog(snapshot, new CellCoord(0, 0), adminOverride: true);

        Assert.Equal(snapshot.Residents.Count, filtered.Residents.Count);
    }
}
