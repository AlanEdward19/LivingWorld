namespace LivingWorld.Domain.Cognition;

/// <summary>Quanto um acontecimento marca: saliência da lembrança (0..100) e emoções despertadas (0..1).</summary>
public readonly record struct LifeEventImpact(int Salience, double Grief = 0, double Fear = 0, double Anger = 0, double Joy = 0, double Shame = 0);

/// <summary>Memória da própria vida, emoção e humor (AD-138), dado de cenário com padrão de época. Números aproximados do meio
/// das faixas da literatura; todos precisam de conferência (lista no relatório da fatia). Snapshot ou cenário sem o bloco herda
/// <see cref="Default"/>; <see cref="Disabled"/> desliga memória, emoção e os efeitos na decisão.</summary>
public sealed record PsycheRules
{
    public bool Enabled { get; init; } = true;

    // --- memória episódica ---

    /// <summary>Lembranças guardadas por pessoa; a mais fraca dá lugar à nova quando a nova é mais forte.</summary>
    public int MemoryCapacity { get; init; } = 12;

    /// <summary>Meia-vida (dias) de uma lembrança de saliência 0; cresce com a saliência (viés emocional).</summary>
    public double MemoryHalfLifeDays { get; init; } = 365;

    /// <summary>Ganho da meia-vida por ponto de saliência: saliência 95 dura ~8,6× mais (~23 anos) (luto de filho lembrado por décadas).</summary>
    public double SalienceHalfLifeGain { get; init; } = 0.08;

    /// <summary>Força (0..100) abaixo da qual a lembrança se apaga.</summary>
    public int ForgetBelow { get; init; } = 3;

    /// <summary>Saliência somada quando algo relacionado (mesma pessoa ou mesmo tipo) reaviva a lembrança.</summary>
    public int RehearsalBoost { get; init; } = 10;

    // --- emoção (meia-vida em dias, para estabilidade emocional média) ---

    public double GriefHalfLifeDays { get; init; } = 120;
    public double FearHalfLifeDays { get; init; } = 21;
    public double AngerHalfLifeDays { get; init; } = 30;
    public double JoyHalfLifeDays { get; init; } = 14;
    public double ShameHalfLifeDays { get; init; } = 45;
    public double LonelinessHalfLifeDays { get; init; } = 10;

    /// <summary>Solidão somada a cada vez que a pessoa procura companhia e não acha ninguém.</summary>
    public double LonelinessPerSolitude { get; init; } = 0.08;

    /// <summary>Solidão aliviada a cada conversa de verdade.</summary>
    public double LonelinessReliefPerContact { get; init; } = 0.25;

    /// <summary><c>Socialize</c> só satisfaz a necessidade social com alguém por perto.</summary>
    public bool SocializeNeedsCompany { get; init; } = true;

    /// <summary>AD-186: quem quer conversar e não tem ninguém por perto sai para achar alguém — o conhecido de quem mais gosta (parente,
    /// vizinho, amigo) que está a menos de uma hora de casa, e sem ninguém o largo da cidade — andando pela rota; antes conversava
    /// sozinho, a solidão subia e Conversar seguia ganhando. Falso = o de antes.</summary>
    public bool SeekCompany { get; init; } = true;

    /// <summary>AD-186: até quantas células da casa (Chebyshev) quem procura companhia vai atrás de um conhecido: a vizinhança a pé
    /// (escolha do modelo; a regra da despensa a uma hora vale para o largo).</summary>
    public int SeekCompanyMaxCells { get; init; } = 12;

    /// <summary>AD-186: solidão (0–1) a partir da qual quem quer conversar sai de casa atrás de alguém (cada conversa sem ninguém soma
    /// <see cref="LonelinessPerSolitude"/>: umas três noites sozinho). Escolha do modelo.</summary>
    public double SeekCompanyLoneliness { get; init; } = 0.2;

    /// <summary>AD-147 (backlog 08, item 47): com fome ou sede crítica que a despensa serve, Conversar e Ficar à toa param no déficit
    /// crítico e Comer vence. Ligado por padrão desde o AD-148 (com o trabalho e a compra da casa consertados, 6 seeds × 100 anos:
    /// sede 0,2% → 0,0%, fome 3,7% → 3,5%); a causa: sozinho em casa a solidão nunca alivia e o peso de personalidade, até 3×,
    /// punha Conversar acima da sede no zero.</summary>
    public bool CriticalBodyOverridesLeisure { get; init; } = true;

    /// <summary>AD-182 (08c item 149): o teto do lazer vale também quando a compra serve a fome ou a sede crítica (a despensa está
    /// vazia, mas há moeda e comida à venda e a pessoa sabe comprar). Antes só valia com a despensa servindo: o jovem que ficou só em
    /// casa, com 85–154 moedas e a despensa vazia, conversava sozinho (a solidão no zero e o peso de personalidade, ~190) contra a
    /// compra (160) até morrer de fome. Falso = o de antes.</summary>
    public bool CriticalBodyCountsPurchase { get; init; } = true;

    /// <summary>Fome em zero por pelo menos tantas horas vira lembrança de fome.</summary>
    public int FamineMinHours { get; init; } = 24;

    /// <summary>Afeto (0..100) a partir do qual a morte de alguém de fora da família enluta.</summary>
    public double FriendAffectionForGrief { get; init; } = 60;

    // --- efeitos na vida (consumidores) ---

    /// <summary>Fração dos dias de trabalho que o luto pleno (1,0) tira; cai com o luto.</summary>
    public double WorkWithdrawalAtFullGrief { get; init; } = 0.4;

    /// <summary>Fração das horas de convívio que o luto pleno tira (fica em casa).</summary>
    public double SocialWithdrawalAtFullGrief { get; init; } = 0.6;

    /// <summary>Peso da vergonha no recolhimento (trabalho e convívio).</summary>
    public double ShameWithdrawal { get; init; } = 0.5;

    /// <summary>Meta de despensa × (1 + ganho × força da lembrança de fome / 100).</summary>
    public double HoardGain { get; init; } = 1.0;

    /// <summary>Chance de socorrer × (1 + ganho × gratidão / 100): quem foi ajudado retribui mais.</summary>
    public double ReciprocityGain { get; init; } = 1.0;

    /// <summary>Teto de confiança × (1 − cautela × força da traição / 100): quem foi traído confia menos em todos.</summary>
    public double BetrayalWariness { get; init; } = 0.4;

    /// <summary>Apelo a menos para conversar com quem nos fez mal (força 100); evita.</summary>
    public double GrudgeAppealPenalty { get; init; } = 60;

    /// <summary>Apelo a mais para conversar com parente quando enlutado (luto 1,0): procura os seus.</summary>
    public double KinSeekingAppeal { get; init; } = 40;

    /// <summary>Apelo a menos para estranhos quando com medo (medo 1,0).</summary>
    public double FearStrangerPenalty { get; init; } = 30;

    // --- pesos na utilidade (decisão urgente), em unidades de utilidade por emoção 1,0 ---

    public double GriefWorkPenalty { get; init; } = 40;
    public double GriefIdleBonus { get; init; } = 30;

    /// <summary>AD-166: o luto tira do trabalho e do lazer, mas não da comida. Com a fome ou a sede pedindo comer (o que a
    /// despensa serve) ou a casa pedindo compra, o apelo do luto a ficar à toa não entra: quem chora o morto ainda come e
    /// abastece a casa. Falso volta ao de antes (o luto podia vencer o comer e o comprar).</summary>
    public bool GriefYieldsToBody { get; init; } = true;

    /// <summary>AD-166: morte por acidente e por velhice também enlutam (antes só as outras causas). Falso volta ao de antes.</summary>
    public bool MournEveryDeath { get; init; } = true;
    public double GriefSocialPenalty { get; init; } = 20;
    public double ShameSocialPenalty { get; init; } = 30;
    public double FearTravelPenalty { get; init; } = 40;
    public double JoySocialBonus { get; init; } = 20;

    /// <summary>Fração do impulso social que a solidão recente (1,0) desconta em <c>Socialize</c>: quem procurou e não achou
    /// ninguém espera achar menos e vai fazer outra coisa (a necessidade continua e pesa no humor).</summary>
    public double SolitudeDiscount { get; init; } = 0.8;

    /// <summary>Quem não tem moeda e vê a despensa abaixo da meta pesa ganhar o pão: até este valor a mais em <c>Work</c>.</summary>
    public double IncomeNeedMax { get; init; } = 30;

    /// <summary>AD-191 (2ª rodada): a vontade de ganhar o pão só puxa ao trabalho quem tem onde ganhar (emprego ou obra). Falso = o de antes.</summary>
    public bool IncomeNeedNeedsWork { get; init; } = true;

    /// <summary>Lembrança de fome a mais em <c>Buy</c> (força 100) quando a despensa está abaixo da meta.</summary>
    public double FamineProvisionBonus { get; init; } = 20;

    /// <summary>Pontos de estabilidade emocional que um acontecimento devastador (saliência ≥ <see cref="ImprintSalience"/>, com
    /// luto, raiva ou vergonha) tira para sempre, e que o casamento dá (achado 28: a personalidade muda devagar com a vida).</summary>
    public int ImprintPoints { get; init; } = 2;

    public int ImprintSalience { get; init; } = 85;

    public IReadOnlyDictionary<LifeEventKind, LifeEventImpact> Impacts { get; init; } = DefaultImpacts;

    public static IReadOnlyDictionary<LifeEventKind, LifeEventImpact> DefaultImpacts { get; } =
        new Dictionary<LifeEventKind, LifeEventImpact>
        {
            [LifeEventKind.ChildBorn] = new(60, Joy: 0.7),
            [LifeEventKind.ChildDied] = new(95, Grief: 1.0),
            [LifeEventKind.SpouseDied] = new(90, Grief: 1.0),
            [LifeEventKind.ParentDied] = new(70, Grief: 0.6),
            [LifeEventKind.SiblingDied] = new(55, Grief: 0.45),
            [LifeEventKind.FriendDied] = new(40, Grief: 0.3),
            [LifeEventKind.StillBirth] = new(65, Grief: 0.6),
            [LifeEventKind.Famine] = new(60, Fear: 0.4),
            [LifeEventKind.Betrayed] = new(85, Grief: 0.3, Anger: 0.9, Shame: 0.2),
            [LifeEventKind.BetrayedSpouse] = new(70, Fear: 0.2, Shame: 0.6),
            [LifeEventKind.Robbed] = new(45, Fear: 0.4, Anger: 0.6),
            [LifeEventKind.CaughtStealing] = new(55, Fear: 0.3, Shame: 0.6),
            [LifeEventKind.AidReceived] = new(40, Joy: 0.3, Shame: 0.1),
            [LifeEventKind.AidGiven] = new(25, Joy: 0.2),
            [LifeEventKind.Married] = new(80, Joy: 0.8),
            [LifeEventKind.Separated] = new(75, Grief: 0.5, Anger: 0.4, Shame: 0.2),
            [LifeEventKind.Abandoned] = new(80, Grief: 0.6, Fear: 0.2, Anger: 0.6, Shame: 0.3),
            [LifeEventKind.Hired] = new(25, Joy: 0.2),
            [LifeEventKind.Fired] = new(40, Fear: 0.2, Anger: 0.3, Shame: 0.3),
            [LifeEventKind.Evicted] = new(55, Grief: 0.2, Fear: 0.4, Anger: 0.3, Shame: 0.3),
            [LifeEventKind.TakenIn] = new(60, Grief: 0.3, Fear: 0.2, Joy: 0.2),
            [LifeEventKind.Violence] = new(75, Fear: 0.8, Anger: 0.5),
            [LifeEventKind.FellIll] = new(45, Fear: 0.4),
            [LifeEventKind.HouseholdMemberIll] = new(35, Fear: 0.3),
            [LifeEventKind.UnwedPregnancy] = new(80, Fear: 0.5, Shame: 0.8),
            [LifeEventKind.ChildUnwedPregnancy] = new(60, Anger: 0.5, Shame: 0.7),
            [LifeEventKind.Punished] = new(75, Fear: 0.7, Anger: 0.3, Shame: 0.8),
            [LifeEventKind.KinMurdered] = new(90, Grief: 0.5, Anger: 1.0),
            [LifeEventKind.WronglyPunished] = new(80, Fear: 0.4, Anger: 0.9, Shame: 0.4),
            [LifeEventKind.SexualAssault] = new(95, Grief: 0.4, Fear: 1.0, Anger: 0.4, Shame: 0.7),
            [LifeEventKind.Kidnapped] = new(90, Fear: 0.9, Anger: 0.4),
            [LifeEventKind.KinAssaulted] = new(75, Grief: 0.3, Fear: 0.3, Anger: 0.9),
        };

    public static PsycheRules Default { get; } = new();

    public static PsycheRules Disabled { get; } = new() { Enabled = false, SocializeNeedsCompany = false };

    public LifeEventImpact ImpactOf(LifeEventKind kind) => Impacts.TryGetValue(kind, out var impact) ? impact : default;

    public double HalfLifeDaysOf(Emotion emotion) => emotion switch
    {
        Emotion.Grief => GriefHalfLifeDays,
        Emotion.Fear => FearHalfLifeDays,
        Emotion.Anger => AngerHalfLifeDays,
        Emotion.Joy => JoyHalfLifeDays,
        Emotion.Shame => ShameHalfLifeDays,
        _ => LonelinessHalfLifeDays,
    };

    public string? Validate()
    {
        if (MemoryCapacity < 0) return "Psyche.MemoryCapacity: deve ser >= 0";
        if (MemoryHalfLifeDays <= 0) return "Psyche.MemoryHalfLifeDays: deve ser > 0";
        if (GriefHalfLifeDays <= 0 || FearHalfLifeDays <= 0 || AngerHalfLifeDays <= 0 || JoyHalfLifeDays <= 0
            || ShameHalfLifeDays <= 0 || LonelinessHalfLifeDays <= 0)
            return "Psyche: meia-vida de emoção deve ser > 0";
        return null;
    }
}
