using LivingWorld.Api.Visual.Scope;
using LivingWorld.Domain.Cities;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Integration.Visual;

/// <summary>AD-192 (achado do cliente): tudo o que a cidade tem — prédio, canteiro, o lote dele — aparece no escopo Cidade, porque o
/// limite da cidade no motor vai até o que ela ocupa (e não porque o recorte da projeção foi alargado).</summary>
public class CityScopeHoldsItsBuildingsTests
{
    private static (int World, int Scope) Count(LivingWorld.Simulation.Core.WorldState world)
    {
        int inWorld = 0, inScope = 0;
        foreach (var city in world.ActiveCities())
        {
            inWorld += world.Buildings.Count(b => b.City == city.Id && b.Position is not null);
            var scope = new VisualScope(VisualScopeKind.City, city.Id.ToString());
            inScope += LivingScopeProjector.Build(world, scope).Buildings.Count;
        }
        return (inWorld, inScope);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void The_village_scope_draws_every_building_of_the_village_only_with_the_rule(bool hold)
    {
        var (world, clock) = ScenarioRunner.Create(3, cityLayout: CityLayoutRules.Default with { Streets = StreetRules.Default with { CityHoldsItsBuildings = hold } });
        clock.Run(world, world.Calendar.HoursPerDay * world.Calendar.DaysPerMonth * 3);
        var (inWorld, inScope) = Count(world);
        Assert.Equal(hold, inWorld == inScope);
    }
}
