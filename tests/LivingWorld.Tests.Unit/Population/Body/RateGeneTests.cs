using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Tests.Unit.Population.Body;

/// <summary>Fase 6, task 5 (SKILL-09): gene de taxa herdado — multiplicador de taxa de ganho,
/// nunca de valor de habilidade.</summary>
public class RateGeneTests
{
    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(-0.001)]
    public void Create_rejects_zero_or_negative_value(double value)
    {
        var result = RateGene.Create(value);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Create_accepts_positive_value()
    {
        var result = RateGene.Create(1.5);

        Assert.True(result.IsSuccess);
        Assert.Equal(1.5, result.Value!.Value);
    }

    [Fact]
    public void RollInitial_never_produces_zero_or_negative_across_many_seeds()
    {
        for (ulong seed = 1; seed <= 200; seed++)
        {
            var rng = new WorldRng(seed);
            var gene = RateGene.RollInitial(rng);

            Assert.True(gene.Value > 0, $"seed {seed} produced {gene.Value}");
        }
    }

    [Fact]
    public void Inherit_never_produces_zero_or_negative_across_many_seeds()
    {
        var mother = new RateGene(0.05);
        var father = new RateGene(0.02);

        for (ulong seed = 1; seed <= 200; seed++)
        {
            var rng = new WorldRng(seed);
            var child = RateGene.Inherit(mother, father, rng);

            Assert.True(child.Value > 0, $"seed {seed} produced {child.Value}");
        }
    }

    [Fact]
    public void Inherit_with_identical_parents_varies_by_mutation_across_seeds()
    {
        var mother = new RateGene(1.0);
        var father = new RateGene(1.0);

        var results = Enumerable.Range(1, 20)
            .Select(seed => RateGene.Inherit(mother, father, new WorldRng((ulong)seed)).Value)
            .ToList();

        Assert.True(results.Distinct().Count() > 1, "mutação deveria produzir variação entre seeds");
    }

    [Fact]
    public void Inherit_centers_around_parents_average_value()
    {
        var mother = new RateGene(2.0);
        var father = new RateGene(2.0);

        var results = Enumerable.Range(1, 500)
            .Select(seed => RateGene.Inherit(mother, father, new WorldRng((ulong)seed)).Value)
            .ToList();

        double average = results.Average();
        Assert.InRange(average, 1.5, 2.5);
    }

    [Fact]
    public void RollInitial_is_deterministic_for_the_same_seed()
    {
        var first = RateGene.RollInitial(new WorldRng(777));
        var second = RateGene.RollInitial(new WorldRng(777));

        Assert.Equal(first.Value, second.Value);
    }
}
