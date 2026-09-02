using LivingWorld.Simulation.Ecology;
using LivingWorld.Simulation.Geography;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Ecology;

/// <summary>REALISM-20 — ordem fixa fauna → flora → temperatura em DefaultSystems.</summary>
public sealed class WorldRealismOrderTests
{
    [Fact]
    public void DefaultSystems_orders_fauna_then_flora_then_temperature()
    {
        var types = ScenarioRunner.DefaultSystems()
            .Select(system => system.GetType())
            .ToList();

        int fauna = types.IndexOf(typeof(FaunaLifecycleSystem));
        int flora = types.IndexOf(typeof(FloraLifecycleSystem));
        int temperature = types.IndexOf(typeof(TemperatureSeasonSystem));

        Assert.True(fauna >= 0, "FaunaLifecycleSystem missing from DefaultSystems");
        Assert.True(flora >= 0, "FloraLifecycleSystem missing from DefaultSystems");
        Assert.True(temperature >= 0, "TemperatureSeasonSystem missing from DefaultSystems");
        Assert.True(fauna < flora, "fauna must precede flora");
        Assert.True(flora < temperature, "flora must precede temperature");
    }
}
