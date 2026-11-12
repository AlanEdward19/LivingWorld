using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Domain.Health;

/// <summary>Via de contágio (só rótulo para relatório e história: o que pesa são as taxas por via do perfil).</summary>
public enum DiseaseRoute { Contact, Respiratory, WaterFood, Vector }

/// <summary>Uma doença como vetor de eixos do cenário, sem nome fixo no código (AD-137). Os cinco campos posicionais são o modelo
/// antigo e continuam valendo: <see cref="ContagionPerContactDay"/> é a taxa por dia de convivência de toda via que o perfil não
/// declarar, <see cref="Lethality"/> a letalidade-base, <see cref="DurationDays"/> os dias de doença e <see cref="ImmunityDays"/>
/// a imunidade depois da cura. O resto tem padrão neutro: sem incubação, sem fonte ambiental, sem importação própria.</summary>
public sealed record DiseaseProfile(int Id, double ContagionPerContactDay, double Lethality, int DurationDays, int ImmunityDays)
{
    /// <summary>Nome do perfil (dado de cenário, só para relatório e história).</summary>
    public string Name { get; init; } = "";

    public DiseaseRoute Route { get; init; } = DiseaseRoute.Contact;

    /// <summary>Dias entre pegar e adoecer (sem sintoma e sem contágio, salvo <see cref="ContagiousBeforeOnsetDays"/>), mais um
    /// sorteio uniforme de até <see cref="IncubationJitterDays"/> dias.</summary>
    public int IncubationDays { get; init; }
    public int IncubationJitterDays { get; init; }

    /// <summary>Dias antes do sintoma em que já contagia andando pela vila (pródromo do sarampo, da varíola).</summary>
    public int ContagiousBeforeOnsetDays { get; init; }

    /// <summary>Dias de contágio depois do sintoma; nulo = a doença inteira.</summary>
    public int? ContagiousDays { get; init; }

    /// <summary>Taxa por dia de convivência com cada morador da casa, cada colega da turma e cada encontro na vila (mercado,
    /// conversa); nulo = <see cref="ContagionPerContactDay"/>.</summary>
    public double? HouseholdContagionPerDay { get; init; }
    public double? CrewContagionPerDay { get; init; }
    public double? CommunityContagionPerContact { get; init; }

    /// <summary>Casos por pessoa por ano vindos do ambiente (água, comida, frio), pela idade; nulo = nenhum.</summary>
    public AgeCurve? EnvironmentalIncidenceByAge { get; init; }

    /// <summary>Peso de cada mês do ano (12 valores, média ~1) sobre a fonte ambiental, a importação e o contágio; nulo = plano.</summary>
    public IReadOnlyList<double>? SeasonByMonth { get; init; }

    /// <summary>Chance por ano de chegar de fora numa cidade sem caso (abre um surto com evento); nulo = a regra antiga,
    /// <see cref="DiseaseRules.OutbreakDailyChance"/> por dia na maior cidade acima do limiar.</summary>
    public double? ImportAnnualChance { get; init; }

    /// <summary>Parte dos casos que fica de cama (não trabalha, não sai; quem morre sempre fica). Os outros andam, mais fracos.</summary>
    public double BedriddenShare { get; init; } = 1.0;

    /// <summary>Fração de <see cref="HealthPenalty"/> que o caso leve tira do teto (o de cama tira tudo).</summary>
    public double MildSeverity { get; init; } = 0.4;

    /// <summary>Pontos de saúde que a doença em curso tira do teto (AD-134); o doente fica fraco, trabalha menos e anda devagar.</summary>
    public double HealthPenalty { get; init; } = 30;

    /// <summary>Fator da letalidade pela idade (AD-134; nulo = igual em toda idade): concentra as mortes em crianças pequenas e
    /// velhos.</summary>
    public AgeCurve? LethalityByAge { get; init; }

    /// <summary>Quanto a desnutrição acumulada (0..1) multiplica a letalidade: 1 + fator × desnutrição (0 = não pesa).</summary>
    public double MalnutritionLethalityFactor { get; init; }

    /// <summary>Gancho da medicina (outra fatia): com <see cref="DiseaseRules.CareLevel"/> 1 a letalidade e a duração caem
    /// nestas frações.</summary>
    public double LethalityReductionAtFullCare { get; init; }
    public double DurationReductionAtFullCare { get; init; }

    /// <summary>Chance de ficar com sequela ao sarar, que envelhece o corpo em <see cref="SequelaFrailtyYears"/> anos.</summary>
    public double SequelaChance { get; init; }
    public double SequelaFrailtyYears { get; init; }

    /// <summary>Quem já existia no começo do mundo pegou a doença na infância com esta chance pela idade (imunidade de vida).</summary>
    public AgeCurve? FounderImmunityByAge { get; init; }

    /// <summary>Medo: com o surto conhecido na cidade (2 ou mais doentes), os encontros fora de casa caem nesta fração.</summary>
    public double Avoidance { get; init; }

    /// <summary>Letalidade efetiva para quem tem <paramref name="ageYears"/> e a desnutrição dada, em [0,1].</summary>
    public double LethalityFor(double ageYears, double undernutrition) => Math.Clamp(
        Lethality * (LethalityByAge?.At(ageYears) ?? 1.0) * (1 + MalnutritionLethalityFactor * Math.Clamp(undernutrition, 0, 1)), 0, 1);

    public double Household => HouseholdContagionPerDay ?? ContagionPerContactDay;
    public double Crew => CrewContagionPerDay ?? ContagionPerContactDay;
    public double Community => CommunityContagionPerContact ?? ContagionPerContactDay;

    /// <summary>Peso do mês (0..11) do ano.</summary>
    public double Season(int month) => SeasonByMonth is { Count: 12 } s ? s[((month % 12) + 12) % 12] : 1.0;
}

/// <summary>Regras de doença do cenário (disease-epidemics, AD-137). <see cref="Disabled"/> não tem doença; o cenário de código usa
/// <see cref="DiseasePresets.PreIndustrial"/>. Um perfil sem <see cref="DiseaseProfile.ImportAnnualChance"/> nasce quando alguma
/// cidade passa de <see cref="OutbreakPopulationThreshold"/> habitantes, com chance <see cref="OutbreakDailyChance"/> por dia.</summary>
public sealed record DiseaseRules(
    bool Enabled, IReadOnlyList<DiseaseProfile> Diseases, int OutbreakPopulationThreshold, double OutbreakDailyChance)
{
    public static DiseaseRules Disabled { get; } = new(false, [], int.MaxValue, 0);

    /// <summary>Doença como causa das mortes que a tábua prevê: a mortalidade-base é multiplicada por 1 − esta parte (nulo = 0,
    /// a doença soma por cima).</summary>
    public AgeCurve? BaselineShareByAge { get; init; }

    /// <summary>Encontros por dia fora de casa e do trabalho (mercado, conversa) de quem anda pela vila.</summary>
    public int CommunityContactsPerDay { get; init; }

    /// <summary>Colegas de oficina com quem se trabalha lado a lado (vizinhos na ordem estável por id).</summary>
    public int WorkCrewSize { get; init; } = 6;

    /// <summary>De cama ainda se recebe visita (parente, vizinho): fração dos encontros de quem anda.</summary>
    public double BedriddenVisitFactor { get; init; }

    /// <summary>Quem cuida: morador da casa desta idade para cima; perde esta parte dos dias de trabalho e convive mais com o doente.</summary>
    public int CaregiverMinAgeYears { get; init; } = 12;
    public double CareWorkShare { get; init; } = 0.5;
    public double CaregiverExposureMultiplier { get; init; } = 2.0;

    /// <summary>Doente de cama sem ninguém da casa para cuidar morre mais (sem água, comida e fogo).</summary>
    public double UncaredLethalityMultiplier { get; init; } = 1.0;

    /// <summary>Nível de medicina da época (0 = nenhum); multiplica as reduções de cada perfil.</summary>
    public double CareLevel { get; init; }

    /// <summary>Perfil pelo id (lista curta do cenário); nulo se não existe.</summary>
    public DiseaseProfile? Find(int id)
    {
        foreach (var profile in Diseases)
            if (profile.Id == id) return profile;
        return null;
    }

    /// <summary>Parte da mortalidade-base que a doença explica nesta idade (0 se desligada).</summary>
    public double BaselineShare(double ageYears) =>
        !Enabled || BaselineShareByAge is null ? 0.0 : Math.Clamp(BaselineShareByAge.At(ageYears), 0, 1);
}

/// <summary>Infecção: uma por NPC e doença. Pega em <see cref="InfectedTick"/>, adoece em <see cref="OnsetTick"/> e termina em
/// <see cref="EndTick"/> com cura (imunidade) ou, se <see cref="DeathTick"/> existe, morre nele. Campos com padrão: snapshot antigo
/// abre com a infecção já doente e sem desfecho decidido.</summary>
public sealed record Infection(NpcId Npc, int Disease, long EndTick)
{
    public long InfectedTick { get; init; }
    public long OnsetTick { get; init; }
    public bool Symptomatic { get; init; }
    public bool Bedridden { get; init; }
    public long? DeathTick { get; init; }
    public NpcId? Caregiver { get; init; }

    /// <summary>Tick em que o surto importado começou (o caso herda de quem passou); −1 = caso esporádico do ambiente.</summary>
    public long OutbreakTick { get; init; } = -1;
    public CityId OutbreakCity { get; init; }
}

/// <summary>Imunidade depois da cura; expira por calendário em <see cref="UntilTick"/>.</summary>
public sealed record Immunity(NpcId Npc, int Disease, long UntilTick);
