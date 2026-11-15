using LivingWorld.Domain.Shared;

namespace LivingWorld.Domain.Extraordinary;

/// <summary>Como um poder passa de pais a filhos (AD-156).</summary>
public enum PowerInheritanceMode
{
    /// <summary>Uma cópia do alelo basta.</summary>
    Dominant,
    /// <summary>Só manifesta com duas cópias; o heterozigoto transmite sem manifestar.</summary>
    Recessive,
    /// <summary>No X: o homem (um X, da mãe) manifesta com uma cópia; a mulher, com duas.</summary>
    XLinkedRecessive,
    /// <summary>No X: uma cópia basta nos dois sexos; o pai passa a todas as filhas e a nenhum filho.</summary>
    XLinkedDominant,
    /// <summary>Soma das cópias de vários loci mais um desvio do ambiente; manifesta acima do limiar (modelo de liability).</summary>
    Polygenic,
}

/// <summary>Genética de um poder (AD-156), dado de cenário. Números padrão de memória, sem fonte: precisam de conferência.
/// <see cref="AlleleFrequency"/> nula = derivada da prevalência do cenário pelo equilíbrio de Hardy-Weinberg.</summary>
public sealed record PowerGenetics(
    PowerInheritanceMode Mode = PowerInheritanceMode.Dominant,
    double? AlleleFrequency = null,
    double Penetrance = 0.9,
    double ExpressivityStdDev = 0.15,
    double OnsetAgeMeanYears = 13,
    double OnsetAgeStdDevYears = 1.5,
    double MutationRate = 1e-5,
    int PolygenicLoci = 4,
    double PolygenicThreshold = 5,
    double PolygenicEnvironmentStdDev = 0.5,
    double HungerMultiplier = 1.1,
    double ThirstMultiplier = 1.1,
    double FertilityMultiplier = 0.9,
    double HazardMultiplier = 1.0,
    bool TriggerRequiresGenotype = false)
{
    public const int MaxPolygenicLoci = 8;

    /// <summary>Frequência do alelo nos fundadores quando o modo poligênico não a declara.</summary>
    public const double DefaultPolygenicAlleleFrequency = 0.25;

    public int LociCount => Mode == PowerInheritanceMode.Polygenic ? PolygenicLoci : 1;

    public bool IsXLinked => Mode is PowerInheritanceMode.XLinkedRecessive or PowerInheritanceMode.XLinkedDominant;

    public string? Validate()
    {
        if (AlleleFrequency is < 0 or > 1) return "Genetics.AlleleFrequency: fora de [0,1]";
        if (Penetrance is < 0 or > 1) return "Genetics.Penetrance: fora de [0,1]";
        if (MutationRate is < 0 or > 1) return "Genetics.MutationRate: fora de [0,1]";
        if (ExpressivityStdDev < 0 || OnsetAgeMeanYears < 0 || OnsetAgeStdDevYears < 0 || PolygenicEnvironmentStdDev < 0)
            return "Genetics: desvio ou idade negativos";
        if (PolygenicLoci is < 1 or > MaxPolygenicLoci) return $"Genetics.PolygenicLoci: entre 1 e {MaxPolygenicLoci}";
        if (HungerMultiplier < 0 || ThirstMultiplier < 0 || FertilityMultiplier < 0 || HazardMultiplier < 0)
            return "Genetics: multiplicador negativo";
        return null;
    }

    /// <summary>Frequência do alelo cuja fração de manifestos no equilíbrio é <paramref name="manifestFraction"/>.</summary>
    public double FounderFrequency(double manifestFraction)
    {
        if (AlleleFrequency is { } declared) return declared;
        if (Mode == PowerInheritanceMode.Polygenic) return manifestFraction > 0 ? DefaultPolygenicAlleleFrequency : 0;
        double p = Penetrance <= 0 ? 0 : Math.Clamp(manifestFraction / Penetrance, 0, 1);
        return Mode switch
        {
            PowerInheritanceMode.Dominant => 1 - Math.Sqrt(1 - p),                 // 1 - (1 - q)² = p
            PowerInheritanceMode.Recessive => Math.Sqrt(p),                        // q² = p
            PowerInheritanceMode.XLinkedRecessive => (-1 + Math.Sqrt(1 + 8 * p)) / 2, // (q + q²) / 2 = p
            _ => (3 - Math.Sqrt(9 - 8 * p)) / 2,                                    // (q + 2q - q²) / 2 = p
        };
    }
}

/// <summary>Genética dos poderes do cenário (AD-156): a regra padrão e a de cada poder que declara a sua. Nula no mundo =
/// <see cref="Default"/>. Substitui os três caminhos antigos (ambos, um só, mistura), que somavam poderes a cada geração.</summary>
public sealed record PowerInheritanceRules(PowerGenetics? Genetics, IReadOnlyDictionary<string, PowerGenetics>? PerPower = null)
{
    public static readonly PowerInheritanceRules Default = new(new PowerGenetics());

    public PowerGenetics For(string powerId) =>
        PerPower is not null && PerPower.TryGetValue(powerId, out var own) ? own : Genetics ?? Default.Genetics!;

    public static PowerInheritanceRules Resolve(PowerInheritanceRules? declared) =>
        ScenarioRuleDefaults.Resolve(declared, Default);

    public string? Validate()
    {
        if (Genetics?.Validate() is { } error) return error;
        foreach (var (id, genetics) in PerPower ?? new Dictionary<string, PowerGenetics>())
            if (genetics.Validate() is { } perPower) return $"{id}: {perPower}";
        return null;
    }
}
