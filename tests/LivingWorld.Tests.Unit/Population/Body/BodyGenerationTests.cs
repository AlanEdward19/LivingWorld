using LivingWorld.Domain;

namespace LivingWorld.Tests.Population;

/// <summary>Fase 16.3, T7 (COH-21): geração truncada de Height/Weight/MuscleMass.</summary>
public class BodyGenerationTests
{
    [Fact]
    public void RollHeight_never_out_of_range_across_200_seeds()
    {
        var rules = BodyRules.Default;
        for (ulong seed = 1; seed <= 200; seed++)
        {
            double height = BodyGeneration.RollHeight(new WorldRng(seed), rules);
            Assert.InRange(height, rules.HeightMin, rules.HeightMax);
        }
    }

    [Fact]
    public void RollWeight_never_out_of_range_across_200_seeds()
    {
        var rules = BodyRules.Default;
        for (ulong seed = 1; seed <= 200; seed++)
        {
            double weight = BodyGeneration.RollWeight(new WorldRng(seed), rules);
            Assert.InRange(weight, rules.WeightMin, rules.WeightMax);
        }
    }

    [Fact]
    public void RollMuscleMass_never_out_of_range_across_200_seeds()
    {
        var rules = BodyRules.Default;
        for (ulong seed = 1; seed <= 200; seed++)
        {
            double muscle = BodyGeneration.RollMuscleMass(new WorldRng(seed), rules);
            Assert.InRange(muscle, rules.MuscleMassMin, rules.MuscleMassMax);
        }
    }

    [Fact]
    public void Same_seed_produces_same_body_values()
    {
        var rules = BodyRules.Default;
        var a = (
            BodyGeneration.RollHeight(new WorldRng(42), rules),
            BodyGeneration.RollWeight(new WorldRng(42), rules),
            BodyGeneration.RollMuscleMass(new WorldRng(42), rules));
        var b = (
            BodyGeneration.RollHeight(new WorldRng(42), rules),
            BodyGeneration.RollWeight(new WorldRng(42), rules),
            BodyGeneration.RollMuscleMass(new WorldRng(42), rules));

        Assert.Equal(a, b);
    }
}
