using LivingWorld.Domain.Extraordinary;

namespace LivingWorld.Tests.Unit.Extraordinary;

/// <summary>AD-156: a genética do poder como dado de cenário (substitui os pesos dos três caminhos antigos).</summary>
public sealed class PowerInheritanceRulesTests
{
    [Theory]
    [InlineData(PowerInheritanceMode.Dominant)]
    [InlineData(PowerInheritanceMode.Recessive)]
    [InlineData(PowerInheritanceMode.XLinkedRecessive)]
    [InlineData(PowerInheritanceMode.XLinkedDominant)]
    public void Founder_frequency_reproduces_the_declared_manifest_fraction_at_hardy_weinberg(PowerInheritanceMode mode)
    {
        var genetics = new PowerGenetics(mode, Penetrance: 1);
        double q = genetics.FounderFrequency(0.02);
        double female = mode is PowerInheritanceMode.Recessive or PowerInheritanceMode.XLinkedRecessive ? q * q : 1 - (1 - q) * (1 - q);
        double male = mode switch
        {
            PowerInheritanceMode.XLinkedRecessive or PowerInheritanceMode.XLinkedDominant => q,
            _ => female,
        };

        Assert.Equal(0.02, (female + male) / 2, 12);
    }

    [Fact]
    public void Incomplete_penetrance_raises_the_genotype_frequency_to_keep_the_manifest_fraction()
    {
        double full = new PowerGenetics(Penetrance: 1).FounderFrequency(0.02);
        double half = new PowerGenetics(Penetrance: 0.5).FounderFrequency(0.02);

        Assert.True(half > full);
    }

    [Fact]
    public void Declared_allele_frequency_wins_over_the_prevalence()
    {
        Assert.Equal(0.3, new PowerGenetics(AlleleFrequency: 0.3).FounderFrequency(0.5));
    }

    [Fact]
    public void Undeclared_power_uses_the_scenario_default_and_declared_power_its_own()
    {
        var own = new PowerGenetics(PowerInheritanceMode.Recessive);
        var rules = new PowerInheritanceRules(new PowerGenetics(Penetrance: 0.4),
            new Dictionary<string, PowerGenetics> { ["x"] = own });

        Assert.Equal((own, 0.4), (rules.For("x"), rules.For("y").Penetrance));
    }

    [Fact]
    public void Missing_rules_resolve_to_the_documented_default()
    {
        Assert.Same(PowerInheritanceRules.Default, PowerInheritanceRules.Resolve(null));
    }

    [Theory]
    [InlineData(-0.1, 0.9, 1e-5)]
    [InlineData(0.1, 1.5, 1e-5)]
    [InlineData(0.1, 0.9, 2.0)]
    public void Out_of_range_genetics_is_rejected(double frequency, double penetrance, double mutation)
    {
        var rules = new PowerInheritanceRules(
            new PowerGenetics(AlleleFrequency: frequency, Penetrance: penetrance, MutationRate: mutation));

        Assert.NotNull(rules.Validate());
    }
}
