using LivingWorld.Domain.Shared;

namespace LivingWorld.Domain.Health;

/// <summary>Uma doença como vetor de eixos do cenário, sem nome fixo no código: quanto contagia por dia de contato,
/// quanto mata ao fim, quanto dura e quanto de imunidade deixa.</summary>
public sealed record DiseaseProfile(int Id, double ContagionPerContactDay, double Lethality, int DurationDays, int ImmunityDays);

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
