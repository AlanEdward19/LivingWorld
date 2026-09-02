using LivingWorld.Simulation.Extraordinary.Mechanics;

namespace LivingWorld.Tests.Unit.Extraordinary;

/// <summary>Trava o contrato AD-071: resistência à possessão usa Vitality.</summary>
public sealed class PossessionResistanceContractTests
{
    [Fact]
    public void Possession_resistance_attribute_is_vitality_per_ad071()
    {
        Assert.Equal("Vitality", ControlMechanic.PossessionResistanceAttribute);
    }
}
