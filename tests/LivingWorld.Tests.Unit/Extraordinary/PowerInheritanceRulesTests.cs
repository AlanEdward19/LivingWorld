using LivingWorld.Domain.Extraordinary;

namespace LivingWorld.Tests.Unit.Extraordinary;

/// <summary>Fase 16.2 T6 (suporta EVO-10): regra de cenário da herança de poder com
/// defaults documentados quando o cenário omite a declaração.</summary>
public sealed class PowerInheritanceRulesTests
{
    [Fact]
    public void Default_uses_documented_uniform_path_weights_and_inheritance_chance()
    {
        var rules = PowerInheritanceRules.Default;

        Assert.Equal(PowerInheritanceRules.DefaultInheritanceChance, rules.InheritanceChance);
        Assert.Equal(PowerInheritanceRules.UniformPathWeight, rules.BothWeight);
        Assert.Equal(PowerInheritanceRules.UniformPathWeight, rules.OneOfWeight);
        Assert.Equal(PowerInheritanceRules.UniformPathWeight, rules.MixedWeight);
        Assert.Equal(1.0, rules.BothWeight + rules.OneOfWeight + rules.MixedWeight, precision: 10);
    }

    [Fact]
    public void Resolve_null_uses_documented_defaults_and_never_fails()
    {
        var rules = PowerInheritanceRules.Resolve(declared: null);

        Assert.Same(PowerInheritanceRules.Default, rules);
        Assert.Equal(1.0, rules.InheritanceChance);
        Assert.Equal(1.0 / 3.0, rules.BothWeight);
        Assert.Equal(1.0 / 3.0, rules.OneOfWeight);
        Assert.Equal(1.0 / 3.0, rules.MixedWeight);
    }

    [Fact]
    public void Resolve_keeps_explicit_scenario_declaration()
    {
        var declared = PowerInheritanceRules.Create(0.4, 2, 1, 1).Value!;

        var rules = PowerInheritanceRules.Resolve(declared);

        Assert.Same(declared, rules);
        Assert.Equal(0.4, rules.InheritanceChance);
        Assert.Equal(2, rules.BothWeight);
        Assert.Equal(1, rules.OneOfWeight);
        Assert.Equal(1, rules.MixedWeight);
    }

    [Fact]
    public void Create_accepts_valid_parameters()
    {
        var result = PowerInheritanceRules.Create(0.5, 1, 1, 1);

        Assert.True(result.IsSuccess);
        Assert.Equal(0.5, result.Value!.InheritanceChance);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public void Create_rejects_inheritance_chance_out_of_range(double chance)
    {
        var result = PowerInheritanceRules.Create(chance, 1, 1, 1);

        Assert.False(result.IsSuccess);
    }

    [Theory]
    [InlineData(-0.01, 1, 1)]
    [InlineData(1, -0.01, 1)]
    [InlineData(1, 1, -0.01)]
    public void Create_rejects_negative_path_weight(double both, double oneOf, double mixed)
    {
        var result = PowerInheritanceRules.Create(0.5, both, oneOf, mixed);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Create_rejects_path_weights_that_sum_to_zero()
    {
        var result = PowerInheritanceRules.Create(0.5, 0, 0, 0);

        Assert.False(result.IsSuccess);
    }
}
