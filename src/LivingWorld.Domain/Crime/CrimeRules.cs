namespace LivingWorld.Domain.Crime;

/// <summary>Violência entre pessoas, justiça e herança do cenário (AD-150). O furto fica em <see cref="TheftRules"/> (já era
/// regra própria do mundo). <see cref="Disabled"/> = mundo antigo: sem briga, sem justiça, herança toda ao cônjuge.</summary>
public sealed record CrimeRules(bool Enabled, ViolenceRules Violence, JusticeRules Justice, InheritanceRules Inheritance)
{
    public static CrimeRules Default { get; } = new(true, new ViolenceRules(), new JusticeRules(), InheritanceRules.SpouseTakesAll);

    public static CrimeRules Disabled { get; } = new(false, new ViolenceRules { Enabled = false },
        new JusticeRules { Enabled = false }, InheritanceRules.SpouseTakesAll);
}

/// <summary>Briga e agressão (AD-150). Motivos: ofensa (atrito do convívio, mais com raiva no peito), ciúme (o amante do
/// cônjuge), traição descoberta (quem furtou de quem confiava), herança disputada, denúncia e vingança de parente. A
/// propensão é a agressividade do corpo (testosterona por idade e sexo, AD-139) elevada a <see cref="SexAgeExponent"/>,
/// vezes impulsividade e instabilidade emocional. Desfecho: ferimento (saúde dinâmica), às vezes morte, mágoa duradoura e
/// vingança dos parentes. Bebida e dívida em dinheiro ficam fora: o mundo não tem nenhuma das duas.</summary>
public sealed record ViolenceRules
{
    public bool Enabled { get; init; } = true;

    /// <summary>Chance por ano de uma briga por ofensa de um adulto de propensão 1 sem raiva (pessoa comum).</summary>
    public double QuarrelPerYear { get; init; } = 0.012;

    /// <summary>Chance por ano de uma mágoa de intensidade 100 virar agressão, para propensão 1.</summary>
    public double GrudgeFightPerYear { get; init; } = 0.08;

    /// <summary>Quanto a raiva do momento (0..1) multiplica a chance de briga: 1 + este valor × raiva.</summary>
    public double AngerGain { get; init; } = 4.0;

    public double SexAgeExponent { get; init; } = 3.0;
    public int MinAgeYears { get; init; } = 14;

    /// <summary>Dano médio (pontos de saúde) de uma agressão de força comum; espalhado ±<see cref="HarmSpread"/>.</summary>
    public int HarmMean { get; init; } = 18;
    public double HarmSpread { get; init; } = 0.8;

    /// <summary>Chance de um golpe mortal numa briga (faca, cabeça na pedra), antes da força relativa.</summary>
    public double LethalChance { get; init; } = 0.02;

    /// <summary>Mágoa que cada motivo cria em quem sofreu (0..100).</summary>
    public int AssaultGrudge { get; init; } = 60;
    public int JealousyGrudge { get; init; } = 70;
    public int BetrayalTheftGrudge { get; init; } = 40;
    public int DenunciationGrudge { get; init; } = 50;
    public int KinKilledGrudge { get; init; } = 100;
    public int KinWoundedGrudge { get; init; } = 40;

    /// <summary>Confiança mínima (0..100) da vítima no ladrão para o furto descoberto virar traição (mágoa).</summary>
    public double BetrayalTrust { get; init; } = 45;

    /// <summary>Meia-vida da mágoa, em dias; abaixo de <see cref="GrudgeFloor"/> ela some.</summary>
    public int GrudgeHalfLifeDays { get; init; } = 540;
    public int GrudgeFloor { get; init; } = 5;

    /// <summary>Teto de mágoas guardadas no mundo (as mais fracas saem primeiro).</summary>
    public int MaxGrudges { get; init; } = 4000;
}

/// <summary>Herança conforme a norma do cenário (achado 30). Padrão medieval: a viúva fica com a terça parte (dote de
/// viuvez) e os filhos repartem o resto em partes iguais; a oficina vai ao filho homem mais velho. A disputa entre herdeiros
/// nasce da desigualdade (quem ficou sem a oficina) e do temperamento, e vira mágoa (motivo de briga).</summary>
public sealed record InheritanceRules
{
    public double SpouseShare { get; init; } = 1.0 / 3.0;
    public bool WorkplaceToEldestSon { get; init; } = true;

    /// <summary>Chance de disputa entre dois herdeiros por pessoa de agradabilidade 0, escalada pela desigualdade.</summary>
    public double DisputeChance { get; init; } = 0.35;

    /// <summary>Espólio mínimo (moeda) para valer a briga.</summary>
    public long DisputeMinValue { get; init; } = 30;

    public int DisputeGrudge { get; init; } = 45;

    /// <summary>Regra antiga: o cônjuge vivo herda tudo; sem disputa.</summary>
    public static InheritanceRules SpouseTakesAll { get; } = new() { SpouseShare = 1.0, WorkplaceToEldestSon = false, DisputeChance = 0 };
}
