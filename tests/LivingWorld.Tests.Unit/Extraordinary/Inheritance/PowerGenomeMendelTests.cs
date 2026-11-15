using LivingWorld.Domain.Extraordinary;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Extraordinary.Inheritance;

namespace LivingWorld.Tests.Unit.Extraordinary.Inheritance;

/// <summary>AD-156: segregação mendeliana do genótipo de poder. Amostras de 4.000 filhos; tolerância declarada de ±5 pontos
/// percentuais (o desvio-padrão de uma proporção de 50% em 4.000 é 0,8 ponto: a faixa é mais de 6 desvios).</summary>
public sealed class PowerGenomeMendelTests
{
    private const int Children = 4000;
    private const double Tolerance = 0.05;
    private const ulong None = 0, Het = 1, Hom = 2;

    private static PowerLocus Locus(PowerInheritanceMode mode, double mutation = 0, int loci = 4, double threshold = 5, int first = 0,
        string id = "p", int ordinal = 0) =>
        new(ordinal, id, new PowerGenetics(mode, MutationRate: mutation, PolygenicLoci: loci, PolygenicThreshold: threshold,
            PolygenicEnvironmentStdDev: 0), first, FounderFrequency: 0);

    private static (double Affected, double[] Genotypes) Cross(PowerLocus locus, ulong mother, ulong father,
        Sex? onlySex = null, int children = Children)
    {
        var rng = new WorldRng(42);
        var genotypes = new double[3];
        int affected = 0, counted = 0;
        for (int i = 0; i < children; i++)
        {
            var sex = onlySex ?? (i % 2 == 0 ? Sex.Female : Sex.Male);
            ulong child = PowerGenome.Inherit([locus], mother, father, sex, rng);
            counted++;
            genotypes[PowerGenome.Copies(child, locus.FirstLocus)]++;
            if (PowerGenome.IsAffected(locus, child, sex, 0)) affected++;
        }
        return (affected / (double)counted, genotypes.Select(g => g / counted).ToArray());
    }

    [Fact]
    public void Dominant_heterozygous_with_non_carrier_affects_half_of_the_children()
    {
        Assert.InRange(Cross(Locus(PowerInheritanceMode.Dominant), Het, None).Affected, 0.5 - Tolerance, 0.5 + Tolerance);
    }

    [Fact]
    public void Dominant_heterozygous_cross_gives_three_to_one()
    {
        Assert.InRange(Cross(Locus(PowerInheritanceMode.Dominant), Het, Het).Affected, 0.75 - Tolerance, 0.75 + Tolerance);
    }

    [Fact]
    public void Recessive_heterozygous_cross_gives_one_two_one_and_a_quarter_affected()
    {
        var (affected, genotypes) = Cross(Locus(PowerInheritanceMode.Recessive), Het, Het);

        Assert.All(new[] { genotypes[0] - 0.25, genotypes[1] - 0.5, genotypes[2] - 0.25, affected - 0.25 },
            d => Assert.InRange(d, -Tolerance, Tolerance));
    }

    [Fact]
    public void Recessive_skips_a_generation_children_of_an_affected_parent_are_silent_carriers()
    {
        var locus = Locus(PowerInheritanceMode.Recessive);
        var (affected, genotypes) = Cross(locus, Hom, None);

        Assert.Equal((0.0, 1.0), (affected, genotypes[1])); // nenhum manifesta, todos transmitem
    }

    [Fact]
    public void X_linked_affected_father_never_passes_to_sons_and_always_to_daughters()
    {
        var locus = Locus(PowerInheritanceMode.XLinkedRecessive);
        var sons = Cross(locus, None, Het, Sex.Male, 1000);
        var daughters = Cross(locus, None, Het, Sex.Female, 1000);

        Assert.Equal((0.0, 1.0, 0.0), (sons.Genotypes[1], daughters.Genotypes[1], daughters.Affected));
    }

    [Fact]
    public void X_linked_recessive_carrier_mother_affects_half_of_her_sons()
    {
        Assert.InRange(Cross(Locus(PowerInheritanceMode.XLinkedRecessive), Het, None, Sex.Male).Affected,
            0.5 - Tolerance, 0.5 + Tolerance);
    }

    [Fact]
    public void Polygenic_manifests_only_when_the_summed_dose_reaches_the_threshold()
    {
        var locus = Locus(PowerInheritanceMode.Polygenic, loci: 3, threshold: 4);
        ulong four = PowerGenome.With(PowerGenome.With(0, 0, 2), 1, 2); // 2 + 2 + 0
        ulong three = PowerGenome.With(PowerGenome.With(PowerGenome.With(0, 0, 1), 1, 1), 2, 1);

        Assert.Equal((true, false), (PowerGenome.IsAffected(locus, four, Sex.Female, 0), PowerGenome.IsAffected(locus, three, Sex.Female, 0)));
    }

    [Fact]
    public void New_mutation_appears_at_the_declared_rate_per_gamete()
    {
        // pais sem o alelo, taxa 1%: dois gametas por filho → ~2% dos filhos com uma cópia
        var (_, genotypes) = Cross(Locus(PowerInheritanceMode.Dominant, mutation: 0.01), None, None, children: 20000);

        Assert.InRange(genotypes[1], 0.015, 0.025);
    }

    [Fact]
    public void Loci_segregate_independently_so_a_child_of_two_different_carriers_does_not_sum_both()
    {
        // mãe heterozigota do poder A, pai heterozigoto do poder B: ambos em 25%, nenhum em 25% (não 100% de "ambos")
        PowerLocus a = Locus(PowerInheritanceMode.Dominant, id: "a"), b = Locus(PowerInheritanceMode.Dominant, first: 1, id: "b", ordinal: 1);
        var rng = new WorldRng(7);
        int both = 0, neither = 0;
        for (int i = 0; i < Children; i++)
        {
            ulong child = PowerGenome.Inherit([a, b], mother: 1UL, father: 1UL << 2, Sex.Female, rng);
            bool hasA = PowerGenome.IsAffected(a, child, Sex.Female, 0), hasB = PowerGenome.IsAffected(b, child, Sex.Female, 0);
            if (hasA && hasB) both++;
            if (!hasA && !hasB) neither++;
        }

        Assert.All(new[] { both / (double)Children - 0.25, neither / (double)Children - 0.25 }, d => Assert.InRange(d, -Tolerance, Tolerance));
    }
}
