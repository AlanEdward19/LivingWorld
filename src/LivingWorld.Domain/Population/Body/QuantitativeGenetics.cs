using LivingWorld.Domain.Shared;

namespace LivingWorld.Domain.Population.Body;

/// <summary>Herança de traço contínuo pelo modelo infinitesimal (AD-134): filho = média dos pais + segregação mendeliana com
/// metade da variância aditiva da população fundadora. Sem a segregação (média simples + ruído pequeno) a variância cai pela
/// metade a cada geração. <see cref="Mendelian"/> falso volta à mistura antiga (média + ruído uniforme de
/// <c>FamilyRules.VitalityMutationStdDev</c> e ±0,3 no gene de taxa).</summary>
public sealed record QuantitativeGeneticsRules(
    bool Mendelian, double VitalitySegregationStdDev, double RateGeneSegregationStdDev, double StatureSegregationStdDev)
{
    /// <summary>Desvios de segregação = √(Va/2) da população fundadora: vitalidade U(20,80) tem variância 300 (√150 ≈ 12,25);
    /// gene de taxa U(0,7;1,3) tem 0,03 (√0,015 ≈ 0,1225); o gene de estatura, em desvios-padrão, tem h² = 0,8 (√0,4).</summary>
    public static QuantitativeGeneticsRules Default { get; } = new(
        Mendelian: true, VitalitySegregationStdDev: 12.25, RateGeneSegregationStdDev: 0.1225,
        StatureSegregationStdDev: 0.632);

    public static QuantitativeGeneticsRules Blending { get; } = Default with { Mendelian = false };
}

/// <summary>Funções puras do modelo infinitesimal.</summary>
public static class QuantitativeGenetics
{
    /// <summary>Valor do filho: média dos pais + N(0, <paramref name="segregationStdDev"/>).</summary>
    public static double Inherit(double mother, double father, double segregationStdDev, WorldRng rng) =>
        (mother + father) / 2.0 + Normal(rng) * segregationStdDev;

    /// <summary>N(0,1) por Box-Muller com as funções determinísticas.</summary>
    public static double Normal(WorldRng rng)
    {
        double u1 = Math.Max(rng.NextDouble(), 1e-12);
        double u2 = rng.NextDouble();
        return Math.Sqrt(-2.0 * DeterministicMath.Log(u1)) * DeterministicMath.CosTurns(u2);
    }
}
