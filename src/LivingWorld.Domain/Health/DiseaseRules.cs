using LivingWorld.Domain.Shared;

namespace LivingWorld.Domain.Health;

/// <summary>Uma doença como vetor de eixos do cenário, sem nome fixo no código: quanto contagia por dia de contato,
/// quanto mata ao fim, quanto dura e quanto de imunidade deixa.</summary>
public sealed record DiseaseProfile(int Id, double ContagionPerContactDay, double Lethality, int DurationDays, int ImmunityDays)
{
    /// <summary>Pontos de saúde que a doença em curso tira do teto (AD-134); o doente fica fraco, trabalha menos e anda devagar.</summary>
    public double HealthPenalty { get; init; } = 30;

    /// <summary>Fator da letalidade pela idade (AD-134; nulo = igual em toda idade): concentra as mortes em crianças pequenas e
    /// velhos.</summary>
    public LivingWorld.Domain.Population.Body.AgeCurve? LethalityByAge { get; init; }

    /// <summary>Quanto a desnutrição acumulada (0..1) multiplica a letalidade: 1 + fator × desnutrição (0 = não pesa).</summary>
    public double MalnutritionLethalityFactor { get; init; }

    /// <summary>Letalidade efetiva para quem tem <paramref name="ageYears"/> e a desnutrição dada, em [0,1].</summary>
    public double LethalityFor(double ageYears, double undernutrition) => Math.Clamp(
        Lethality * (LethalityByAge?.At(ageYears) ?? 1.0) * (1 + MalnutritionLethalityFactor * Math.Clamp(undernutrition, 0, 1)), 0, 1);
}

/// <summary>Regras de doença do cenário (disease-epidemics). <see cref="Disabled"/> é o padrão: o mundo não tem doença. Um surto
/// nasce quando alguma cidade passa de <see cref="OutbreakPopulationThreshold"/> habitantes vivos, com chance
/// <see cref="OutbreakDailyChance"/> por dia e por doença sem infectado.</summary>
public sealed record DiseaseRules(
    bool Enabled, IReadOnlyList<DiseaseProfile> Diseases, int OutbreakPopulationThreshold, double OutbreakDailyChance)
{
    public static DiseaseRules Disabled { get; } = new(false, [], int.MaxValue, 0);
}

/// <summary>Infecção ativa: uma por NPC e doença; termina em <see cref="EndTick"/> com cura (imunidade) ou morte.</summary>
public sealed record Infection(NpcId Npc, int Disease, long EndTick);

/// <summary>Imunidade depois da cura; expira por calendário em <see cref="UntilTick"/>.</summary>
public sealed record Immunity(NpcId Npc, int Disease, long UntilTick);
