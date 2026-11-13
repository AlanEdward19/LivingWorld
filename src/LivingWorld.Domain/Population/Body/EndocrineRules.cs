namespace LivingWorld.Domain.Population.Body;

/// <summary>Hormônios como estado do corpo (AD-139), dado de cenário com padrão de vila pré-industrial. Nada disso é guardado
/// por tick: puberdade (um tick por pessoa), ciclo (fase pela duração individual) e impulso da adolescência são calculados na
/// leitura a partir da idade, do sexo, da desnutrição e de sorteios fixos da pessoa. Números aproximados do meio das faixas da
/// literatura, citados de memória no registro AD-139: precisam de conferência. <see cref="Disabled"/> volta ao corpo sem
/// hormônios (concepção num dia qualquer do mês, sem puberdade, adolescência nem ciclo).</summary>
public sealed record EndocrineRules
{
    public bool Enabled { get; init; } = true;

    // --- puberdade (menarca nelas, gonadarca neles) ---

    /// <summary>Idade média da menarca bem nutrida e o desvio individual; a de época é mais tardia que a atual (~12,5).</summary>
    public double FemalePubertyMeanYears { get; init; } = 15.0;
    public double MalePubertyMeanYears { get; init; } = 15.5;
    public double PubertyStdDevYears { get; init; } = 1.2;
    public double PubertyMinYears { get; init; } = 10.5;
    public double PubertyMaxYears { get; init; } = 19.0;

    /// <summary>Anos de atraso por desnutrição plena (1,0) no mês em que a puberdade viria: a fome adia a menarca.</summary>
    public double UndernutritionPubertyDelayYears { get; init; } = 3.0;

    // --- adolescência (sistemas duais: impulso que sobe com a puberdade, controle que amadurece até ~25) ---

    /// <summary>Anos depois da puberdade até o impulso chegar ao pico; depois ele cai em linha até <see cref="MaturityAgeYears"/>.</summary>
    public double DriveRiseYears { get; init; } = 2.0;
    public double MaturityAgeYears { get; init; } = 25.0;

    /// <summary>Pontos de impulsividade a mais e de aversão a risco a menos no pico do impulso.</summary>
    public double AdolescentImpulsivityBoost { get; init; } = 30;
    public double AdolescentRiskAversionDrop { get; init; } = 25;

    /// <summary>Fração a mais da vontade social em <c>Socialize</c> no pico (sensibilidade aos pares).</summary>
    public double PeerSocialBonus { get; init; } = 0.3;

    /// <summary>Fração do bônus de continuidade perdida no pico: muda de ideia mais rápido.</summary>
    public double FickleContinuity { get; init; } = 0.3;

    /// <summary>Fração do efeito da antevisão ignorada no pico: desconta as consequências.</summary>
    public double ForesightDiscount { get; init; } = 0.3;

    /// <summary>Fração dos dias de trabalho em que o jovem no pico sai com os pares em vez de trabalhar.</summary>
    public double TruancyShare { get; init; } = 0.05;

    /// <summary>Fração da exigência para aceitar um par que o pico do impulso derruba (escolha precipitada).</summary>
    public double PrecipitousCourtship { get; init; } = 0.1;

    // --- testosterona ---

    /// <summary>Testosterona relativa do homem por idade (0..1); a mulher tem <see cref="FemaleTestosteroneShare"/> dela.</summary>
    public AgeCurve MaleTestosterone { get; init; } = AgeCurve.Of(
        (0, 0), (10, 0.03), (13, 0.35), (16, 0.85), (20, 1.0), (30, 0.95), (50, 0.75), (70, 0.55), (90, 0.45));
    public double FemaleTestosteroneShare { get; init; } = 0.08;

    /// <summary>Agressividade = base + ganho × testosterona; multiplica a chance de violência.</summary>
    public double AggressionBase { get; init; } = 0.6;
    public double AggressionGain { get; init; } = 0.8;

    // --- ciclo menstrual ---

    /// <summary>Duração individual do ciclo (dias): média, desvio e limites.</summary>
    public double CycleMeanDays { get; init; } = 28;
    public double CycleStdDevDays { get; init; } = 2.5;
    public int CycleMinDays { get; init; } = 21;
    public int CycleMaxDays { get; init; } = 35;

    /// <summary>Fase lútea (dias da ovulação à menstruação seguinte); a ovulação cai em <c>ciclo − lútea</c>.</summary>
    public int LutealDays { get; init; } = 14;

    /// <summary>Dias de menstruação, e os primeiros deles em que a cólica pesa.</summary>
    public int MensesDays { get; init; } = 5;
    public int CrampDays { get; init; } = 2;

    /// <summary>Mulheres com cólica que limita a atividade e a fração de trabalho que ela tira nesses dias.</summary>
    public double DysmenorrheaShare { get; init; } = 0.15;
    public double CrampWorkWithdrawal { get; init; } = 0.5;
    public double CrampWorkPenalty { get; init; } = 30;
    public double CrampIdleBonus { get; init; } = 20;

    /// <summary>Mulheres com tensão pré-menstrual moderada, os dias dela e o quanto afasta do convívio (irritação).</summary>
    public double PremenstrualShare { get; init; } = 0.2;
    public int PremenstrualDays { get; init; } = 4;
    public double IrritabilitySocialPenalty { get; init; } = 15;

    /// <summary>Desnutrição acima da qual o ciclo para (amenorreia da fome: sem ovulação nem menstruação).</summary>
    public double AmenorrheaUndernutrition { get; init; } = 0.5;

    /// <summary>Desnutrição a partir da qual cada menstruação tira saúde (perda de ferro, anemia de quem come mal).</summary>
    public double AnemiaUndernutrition { get; init; } = 0.2;
    public int MenstrualHealthLoss { get; init; } = 1;

    // --- gravidez e pós-parto ---

    /// <summary>Mães com tristeza pós-parto que dura meses e o luto (0..1) que ela pesa.</summary>
    public double PostpartumDepressionShare { get; init; } = 0.12;
    public double PostpartumLowMood { get; init; } = 0.5;

    /// <summary>Multiplicadores da chance de parto prematuro: gêmeos, mãe abaixo de 18 anos, desnutrição plena.</summary>
    public double PretermTwinMultiplier { get; init; } = 6.0;
    public double PretermTeenMultiplier { get; init; } = 1.5;
    public double PretermUndernutritionGain { get; init; } = 1.5;

    public static EndocrineRules Default { get; } = new();

    public static EndocrineRules Disabled { get; } = new() { Enabled = false };

    public string? Validate()
    {
        if (PubertyMinYears <= 0 || PubertyMaxYears < PubertyMinYears) return "Endocrine.Puberty: faixa inválida";
        if (MaturityAgeYears <= PubertyMaxYears || DriveRiseYears <= 0) return "Endocrine.MaturityAgeYears: deve passar da puberdade";
        if (CycleMinDays <= LutealDays || CycleMaxDays < CycleMinDays) return "Endocrine.Cycle: faixa inválida";
        if (MensesDays < CrampDays || CrampDays < 0) return "Endocrine.MensesDays: deve conter os dias de cólica";
        if (DysmenorrheaShare is < 0 or > 1 || PremenstrualShare is < 0 or > 1 || PostpartumDepressionShare is < 0 or > 1)
            return "Endocrine: frações fora de [0,1]";
        return null;
    }
}
