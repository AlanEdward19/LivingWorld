namespace LivingWorld.Domain.Population;

/// <summary>Posse comum como disputa de vontade, estranhamento de quem convive e reação (AD-197f, decisão do dono de 2026-11-23:
/// "ela tem que ser mais forte que o controle da pessoa"; "conduta e estranhamento real"; "nada hardcoded"). Dado de cenário. Os
/// números são escolhas do modelo, sem fonte; a forma é a que o dono pediu. Cada regra tem chave própria para o pareado
/// (<c>LW_AD197F=off|nowill|nostrange|noreact</c>).</summary>
public sealed record PossessionRules
{
    /// <summary>Disputa de vontade na posse comum (recusa por valores, cansaço, persuasão, expulsão, impedir a entrada).</summary>
    public bool WillContest { get; init; } = true;

    /// <summary>Quem convive estranha a conduta do corpo, o boato corre e, com evidência e crença do lugar, conclui possessão.</summary>
    public bool Strangeness { get; init; } = true;

    /// <summary>Quem conclui reage: apoia a pessoa (fortalece a vontade dela), vigia, ou ataca o corpo.</summary>
    public bool Reaction { get; init; } = true;

    // --- força do controle (jogador): cresce com o tempo de posse, a perícia de conduzir aquele corpo ---
    public double ControlBase { get; init; } = 40;
    public double ControlGrowth { get; init; } = 30;
    public int ControlGrowthHours { get; init; } = 168;

    // --- vontade e cansaço ---
    /// <summary>Resistência perdida por hora sob controle de força <see cref="ControlBase"/> (proporcional à força do controle).</summary>
    public double DrainPerHour { get; init; } = 0.5;

    /// <summary>Resistência recuperada por hora fora do controle, até a força de vontade.</summary>
    public double RecoverPerHour { get; init; } = 1.0;

    /// <summary>Peso do altruísmo no quanto ferir qualquer pessoa vai contra os valores (altruísmo 90 → 72).</summary>
    public double AltruismHarmWeight { get; init; } = 0.8;

    /// <summary>Recusa quando resistência × peso da ordem / 100 passa deste múltiplo da força do controle.</summary>
    public double RefuseFactor { get; init; } = 0.5;

    /// <summary>Vontade a partir deste múltiplo da força inicial do controle impede a entrada.</summary>
    public double PreventFactor { get; init; } = 2.2;

    /// <summary>Resistência acima deste múltiplo do controle em <see cref="ExpelChecks"/> checagens seguidas expulsa a posse.</summary>
    public double ExpelFactor { get; init; } = 1.6;
    public int ExpelChecks { get; init; } = 3;
    public int CheckEveryHours { get; init; } = 6;

    /// <summary>Resistência que uma persuasão tira (na escala da amabilidade da pessoa), no máximo uma por hora.</summary>
    public double PersuadeAmount { get; init; } = 8;

    // --- estranhamento e descoberta ---
    /// <summary>Desvio de uma ação pedida fora do costume da pessoa naquela hora (rotina do ofício e da idade).</summary>
    public double OffRoutineDeviation { get; init; } = 15;

    /// <summary>Desvio mínimo de um golpe (cresce com o quanto ferir vai contra os valores da pessoa).</summary>
    public double StrikeDeviation { get; init; } = 30;

    /// <summary>Estranhamento a partir do qual o observador comenta (o boato chega a quem convive com ele, pela metade).</summary>
    public double RumorAt { get; init; } = 40;

    /// <summary>Estranhamento para concluir possessão num lugar de crença plena; dividido pela crença do lugar.</summary>
    public double ConcludeAt { get; init; } = 100;

    /// <summary>Crença do lugar em possessão (0..1); nulo = 1 onde o extraordinário existe no mundo, 0,5 onde não.</summary>
    public double? Belief { get; init; }

    // --- reação ---
    /// <summary>Quem conclui e se importa com a pessoa a partir disto apoia: a resistência dela sobe <see cref="SupportBoost"/>.</summary>
    public double HelpCare { get; init; } = 50;
    public double SupportBoost { get; init; } = 15;

    /// <summary>Quem conclui sem se importar e com propensão à violência a partir disto ataca o corpo (briga e justiça de sempre).</summary>
    public double ViolentPropensity { get; init; } = 1.5;

    public static readonly PossessionRules Default = new();

    public static readonly PossessionRules Off = new() { WillContest = false, Strangeness = false, Reaction = false };
}
