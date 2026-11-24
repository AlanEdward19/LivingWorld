namespace LivingWorld.Domain.Population.Body;

/// <summary>Um locus de doença genética (AD-139), anônimo (só o índice o identifica): herança dominante ou recessiva,
/// frequência do alelo nos fundadores, mutação nova por gameta e o efeito em quem é afetado (risco de morte a partir da idade
/// de início e fertilidade). Portador de recessiva não tem sintoma.</summary>
public sealed record DisorderLocus(
    bool Dominant, double FounderAlleleFrequency, double MutationRate, double HazardMultiplier, double OnsetAgeYears,
    double FertilityMultiplier);

/// <summary>Doenças genéticas mendelianas (AD-139), dado de cenário. Os fundadores recebem alelos pela frequência de cada
/// locus; o filho herda um alelo de cada pai (segregação mendeliana) com mutação nova rara. A união entre parentes aumenta os
/// afetados por recessiva sem regra própria: os dois alelos vêm do mesmo ancestral. Até 16 loci. Padrão: três classes da
/// literatura (recessiva grave da infância com portadores ~4%, recessiva moderada, dominante de início tardio rara); números
/// de memória, precisam de conferência.</summary>
public sealed record HeritableDisorderRules(IReadOnlyList<DisorderLocus> Loci)
{
    public const int MaxLoci = 16;

    public static HeritableDisorderRules Default { get; } = new(
    [
        new DisorderLocus(Dominant: false, FounderAlleleFrequency: 0.02, MutationRate: 1e-5, HazardMultiplier: 8.0,
            OnsetAgeYears: 0, FertilityMultiplier: 0.2),
        new DisorderLocus(Dominant: false, FounderAlleleFrequency: 0.04, MutationRate: 1e-5, HazardMultiplier: 2.0,
            OnsetAgeYears: 0, FertilityMultiplier: 0.8),
        new DisorderLocus(Dominant: true, FounderAlleleFrequency: 0.0005, MutationRate: 2e-6, HazardMultiplier: 4.0,
            OnsetAgeYears: 40, FertilityMultiplier: 1.0),
    ]);

    public static HeritableDisorderRules None { get; } = new([]);

    public bool Enabled => Loci.Count > 0;

    public string? Validate()
    {
        if (Loci.Count > MaxLoci) return $"Disorders: no máximo {MaxLoci} loci";
        foreach (var l in Loci)
            if (l.FounderAlleleFrequency is < 0 or > 1 || l.MutationRate is < 0 or > 1 || l.HazardMultiplier < 0 || l.FertilityMultiplier < 0)
                return "Disorders: frequência, mutação ou efeito fora da faixa";
        return null;
    }
}
