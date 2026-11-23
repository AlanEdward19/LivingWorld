namespace LivingWorld.Domain.Cities;

/// <summary>AD-192: consequências do bairro denso (higiene, como cada um vê o bairro, onde morar, valor, migração) e as sobras de
/// cidade e rota (08c 146; 08d 165, 166, 170, 177, 182, 183). Toda chave nasce ligada; <see cref="WithoutAd192"/> desliga todas
/// (<c>LW_AD192=off</c>) para o pareado. Números escolhidos e fontes em <c>docs/domain/dense-quarter.md</c>.</summary>
public sealed partial record StreetRules
{
    // ---------------------------------------------------------------- bairro denso: o que o motor mede (sujeira da casa, 0–1)

    /// <summary>AD-192: pessoas por andar acima das quais a casa está apinhada (a família de 5 cabe num andar); dobro = apinhada de vez.</summary>
    public double CrowdPersonsPerFloor { get; init; } = 5;

    /// <summary>AD-192: pesos da sujeira da casa (somam 1): apinhamento, paredes coladas (a volta da casa tomada por outros prédios),
    /// escoamento (sem quintal que absorva, mais baixa que a vizinhança, sem calçamento na porta) e água (longe, ou fonte cercada de
    /// casas: quem tira e quem despeja no mesmo lugar).</summary>
    public double SqualorCrowdWeight { get; init; } = 0.3;
    public double SqualorCloseWeight { get; init; } = 0.3;
    public double SqualorDrainWeight { get; init; } = 0.2;
    public double SqualorWaterWeight { get; init; } = 0.2;

    /// <summary>AD-192: horas de ida à água a partir das quais a casa lava e limpa pouco (a água é toda de beber).</summary>
    public double SqualorWaterFarHours { get; init; } = 2;

    /// <summary>AD-192: casas a até <see cref="SourceCrowdRadius"/> células da fonte a partir das quais a água chega suja.</summary>
    public int SourceCrowdHouses { get; init; } = 8;
    public int SourceCrowdRadius { get; init; } = 3;

    // ---------------------------------------------------------------- higiene → doença (OutbreakSystem)

    /// <summary>AD-192: os casos do ambiente das doenças de água e comida e de vetor (piolho, pulga) pesam pela sujeira da casa de quem
    /// pega: fator = 1 + <see cref="HygieneGain"/> × (sujeira − <see cref="HygieneReference"/>), entre o piso e o teto.</summary>
    public bool QuarterHygiene { get; init; } = true;
    public double HygieneGain { get; init; } = 2.0;
    public double HygieneReference { get; init; } = 0.2;
    public double HygieneFloor { get; init; } = 0.6;
    public double HygieneMax { get; init; } = 2.5;

    /// <summary>AD-192: doença de respiração e de contato passa pela parede-meia: quem está contagioso numa casa colada encontra os
    /// moradores das casas que encostam nela, <see cref="WallContactsPerDay"/> encontros por dia × a parte da volta da casa tomada.</summary>
    public bool WallContagion { get; init; } = true;
    public double WallContactsPerDay { get; init; } = 2;

    // ---------------------------------------------------------------- como cada um vê o bairro denso

    /// <summary>AD-192: aversão ao bairro denso (−1 curiosidade, +1 medo e nojo), por pessoa: quem é da cidade parte de
    /// <see cref="RegardLocal"/>, quem veio de fora de <see cref="RegardOutsider"/>; aversão ao risco e pouca estabilidade somam, a
    /// abertura tira (cada traço a <see cref="RegardTraitWeight"/> por 50 pontos); morar na sujeira acostuma
    /// (<see cref="RegardHabit"/> × a sujeira da própria casa); cada crime conhecido na cidade de quem mora em casa suja soma
    /// <see cref="RegardPerCrime"/> até <see cref="RegardCrimeMax"/>.</summary>
    public double RegardLocal { get; init; } = 0.4;
    public double RegardOutsider { get; init; } = -0.2;
    public double RegardTraitWeight { get; init; } = 0.25;
    public double RegardHabit { get; init; } = 0.8;
    public double RegardPerCrime { get; init; } = 0.25;
    public double RegardCrimeMax { get; init; } = 0.75;

    /// <summary>AD-192: sujeira a partir da qual a casa conta como de bairro denso (para o crime que "é de lá" e a parte suja da cidade).</summary>
    public double DenseSqualor { get; init; } = 0.45;

    /// <summary>AD-192: a família pesa a sujeira do lote ou da casa vazia pela aversão dos adultos: dias de jornal por morador por ano ×
    /// aversão × sujeira (curiosidade não leva ninguém a morar na sujeira: aversão abaixo de 0 conta 0).</summary>
    public bool QuarterInLotChoice { get; init; } = true;
    public double QuarterDaysPerYear { get; init; } = 20;

    /// <summary>AD-192: o valor do imóvel (e o aluguel e a diária que saem dele) cai com a sujeira do lugar: fator de local − este × sujeira.</summary>
    public bool QuarterInValue { get; init; } = true;
    public double QuarterValuePenalty { get; init; } = 0.4;

    /// <summary>AD-192: a segurança de cada cidade na escolha de para onde migrar deixa de ser 0,5 fixo: cai com os crimes que a casa
    /// conhece lá e com a aversão dela à parte suja da cidade.</summary>
    public bool QuarterInMigration { get; init; } = true;

    // ---------------------------------------------------------------- sobras de cidade e rota

    /// <summary>AD-192 (08c 146): o passeio e a conversa andam pelo tempo, não pela ação concluída: um passo a cada
    /// 1/<see cref="AmbientStepsPerHour"/> horas do relógio, igual para os dois (antes, um passo por ação; à toa dura 2 h, conversar 3 h,
    /// e quem trocava uma pela outra se afastava mais).</summary>
    public bool AmbientStepsByHour { get; init; } = true;
    public double AmbientStepsPerHour { get; init; } = 1.0 / 3;

    /// <summary>AD-192 (08d 165): a cidade sem prédio perto do miolo (a casa solitária da cidade nova, com água em volta do largo)
    /// encadeia o limite a partir do prédio dela mais perto.</summary>
    public bool AnchorLoneHouse { get; init; } = true;

    /// <summary>AD-192 (08d 166): o lote novo não dá para a rua de uma cidade dentro do limite de outra (o traçado é cortado ali), nem
    /// cai dentro dele; posição derivada não muda.</summary>
    public bool CutAtNeighbourCity { get; init; } = true;

    /// <summary>AD-192 (08d 170): prédio do cenário sem posição grava, na carga, o lote e a orientação com a porta para a rua.</summary>
    public bool RecordAuthoredLots { get; init; } = true;

    /// <summary>AD-192 (08d 177): entrada e saída da rede pela melhor combinação entre a célula de caminho mais perto, a primeira que a
    /// reta encontra e a mais perto do mesmo pedaço de rede da outra ponta.</summary>
    public bool NetEntryBestPair { get; init; } = true;

    /// <summary>AD-192 (08d 182): o prédio pronto vira obstáculo da rota no ato (e o chão debaixo some), não na virada do mês.</summary>
    public bool ObstacleOnCompletion { get; init; } = true;

    /// <summary>AD-192 (08d 182): o lote novo não cobre caminho pisado (trilha ou mais): é passagem de todos.</summary>
    public bool LotsAvoidTrodden { get; init; } = true;

    /// <summary>AD-192 (08d 183): o trecho de teste da técnica de calçar traz o cascalho do leito pela rota (as idas de material contam
    /// nas horas da turma); sem fonte ao alcance, não há como testar.</summary>
    public bool PrototypeHaulsMaterial { get; init; } = true;

    /// <summary>AD-192 desligada (<c>LW_AD192=off</c>), para medir pareado.</summary>
    public StreetRules WithoutAd192() => this with
    {
        QuarterHygiene = false,
        WallContagion = false,
        QuarterInLotChoice = false,
        QuarterInValue = false,
        QuarterInMigration = false,
        AmbientStepsByHour = false,
        AnchorLoneHouse = false,
        CutAtNeighbourCity = false,
        RecordAuthoredLots = false,
        NetEntryBestPair = false,
        ObstacleOnCompletion = false,
        LotsAvoidTrodden = false,
        PrototypeHaulsMaterial = false,
    };
}
