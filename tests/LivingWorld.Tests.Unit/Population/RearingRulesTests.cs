using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Tests.Unit.Population;

/// <summary>AD-132: ambiente compartilhado na personalidade. Famílias sintéticas (pais sorteados, filhos por
/// <see cref="Personality.Inherit"/> com a herdabilidade do cenário padrão), cada medida com braço de controle
/// (<see cref="RearingRules.Disabled"/>) sobre os mesmos sorteios, em 10 seeds.</summary>
public class RearingRulesTests
{
    private const double Heritability = 0.7; // ScenarioRunner.DefaultFamilyRules
    private const int Families = 400;

    private sealed record Sample(double TogetherDiff, double ApartDiff, double AdopteeToAdoptive, double AdopteeToBirth, double Sd, double InheritedSd);

    private static Sample Measure(ulong seed, RearingRules rules)
    {
        var rng = new WorldRng(seed);
        double together = 0, apart = 0;
        var adoptee = new List<double>();
        var adoptive = new List<double>();
        var birth = new List<double>();
        var adults = new List<double>();
        var inherited = new List<double>();
        for (int f = 0; f < Families; f++)
        {
            var (mother, father) = (Personality.RollFrom(rng), Personality.RollFrom(rng));
            var (fosterA, fosterB) = (Personality.RollFrom(rng), Personality.RollFrom(rng));
            var first = Personality.Inherit(mother, father, rng, Heritability);
            var second = Personality.Inherit(mother, father, rng, Heritability);
            var third = Personality.Inherit(mother, father, rng, Heritability);
            var home = Raised(mother, father, biological: 2);
            var foster = Raised(fosterA, fosterB, biological: 0);

            var firstAdult = rules.Apply(first, home, Heritability);
            var secondAdult = rules.Apply(second, home, Heritability); // criado junto
            var thirdAdult = rules.Apply(third, foster, Heritability); // adotado por outra casa
            together += Diff(firstAdult, secondAdult);
            apart += Diff(firstAdult, thirdAdult);
            adoptee.Add(thirdAdult.Openness);
            adoptive.Add((fosterA.Openness + fosterB.Openness) / 2.0);
            birth.Add((mother.Openness + father.Openness) / 2.0);
            adults.Add(firstAdult.Openness);
            inherited.Add(first.Openness);
        }
        return new(together / Families, apart / Families, Correlation(adoptee, adoptive), Correlation(adoptee, birth), Sd(adults), Sd(inherited));
    }

    private static RearingRecord Raised(Personality a, Personality b, int biological)
    {
        var record = RearingRecord.Empty;
        for (int month = 0; month < 12 * 15; month++) record = record.Add([a, b], biological);
        return record;
    }

    private static double Diff(Personality a, Personality b) =>
        (Math.Abs(a.Extroversion - b.Extroversion) + Math.Abs(a.Agreeableness - b.Agreeableness) + Math.Abs(a.Openness - b.Openness)
         + Math.Abs(a.Conscientiousness - b.Conscientiousness) + Math.Abs(a.Altruism - b.Altruism)) / 5.0;

    private static double Sd(List<double> values)
    {
        double mean = values.Average();
        return Math.Sqrt(values.Sum(v => (v - mean) * (v - mean)) / values.Count);
    }

    private static double Correlation(List<double> x, List<double> y)
    {
        double mx = x.Average(), my = y.Average();
        double cov = x.Zip(y, (a, b) => (a - mx) * (b - my)).Sum();
        return cov / Math.Sqrt(x.Sum(a => (a - mx) * (a - mx)) * y.Sum(b => (b - my) * (b - my)));
    }

    private static (Sample Treatment, Sample Control)[] Pairs() =>
        Enumerable.Range(1, 10).Select(s => (Measure((ulong)s, RearingRules.Default), Measure((ulong)s, RearingRules.Disabled))).ToArray();

    [Fact]
    public void Siblings_raised_apart_diverge_more_than_siblings_raised_together()
    {
        var pairs = Pairs();

        int hits = pairs.Count(p => p.Treatment.ApartDiff - p.Treatment.TogetherDiff > p.Control.ApartDiff - p.Control.TogetherDiff);
        Assert.True(hits >= 9, $"{hits}/10 seeds");
    }

    [Fact]
    public void Adoptee_resembles_birth_parents_in_what_is_inherited_and_adoptive_parents_in_what_is_raised()
    {
        var pairs = Pairs();

        int hits = pairs.Count(p => p.Treatment.AdopteeToAdoptive > p.Control.AdopteeToAdoptive && p.Treatment.AdopteeToBirth > 0);
        Assert.True(hits >= 9, $"{hits}/10 seeds");
    }

    [Fact]
    public void Rearing_keeps_the_spread_of_personality_in_the_population()
    {
        var pairs = Pairs();

        // a média dos 10 desvios-padrão adultos fica a até um erro-padrão-de-amostragem do desvio herdado (SD/√(2N))
        double adult = pairs.Average(p => p.Treatment.Sd), inheritedSd = pairs.Average(p => p.Treatment.InheritedSd);
        double tolerance = 3 * inheritedSd / Math.Sqrt(2.0 * Families * 10);
        Assert.InRange(adult, inheritedSd - tolerance, inheritedSd + tolerance);
    }

    [Fact]
    public void Disabled_rearing_returns_the_inherited_personality()
    {
        var inherited = Personality.Create(10, 20, 30, 40, 50, 60, 70, 80, 90, 100).Value!;
        var record = RearingRecord.Empty.Add([Personality.Create(90, 90, 90, 90, 90, 90, 90, 90, 90, 90).Value!], 0);

        Assert.Equal(inherited, RearingRules.Disabled.Apply(inherited, record, Heritability));
    }
}
