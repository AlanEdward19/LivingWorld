namespace LivingWorld.Domain.Cities;

/// <summary>Rua de verdade (AD-176): o chão pisado vira trilha e terra batida pelo uso, a superfície muda o passo, o NPC anda
/// pela rota mais rápida (rede de caminhos ou atravessando), e calçar é obra com técnica, material, mão de obra, quem paga e
/// manutenção. Fatores em milésimos; números escolhidos estão em <c>docs/domain/city-layout.md</c> com a fonte de cada um.</summary>
/// <param name="Enabled">Desligado: o deslocamento em linha reta de antes, sem pisoteio, sem rede e sem calçamento.</param>
/// <param name="TrailWear">Pisoteio acumulado a partir do qual a célula vira trilha (some abaixo da metade).</param>
/// <param name="DirtWear">Pisoteio acumulado a partir do qual a trilha vira terra batida (volta a trilha abaixo da metade).</param>
/// <param name="WearRetainPm">Quanto do pisoteio sobra de um mês para o outro (o mato volta), em milésimos.</param>
/// <param name="ReviewMonths">De quantos em quantos meses a superfície é revista (o mato cresce e o chão endurece devagar).</param>
/// <param name="TrailPm">Custo de andar na trilha, em milésimos do terreno sem caminho.</param>
/// <param name="DirtPm">Custo de andar na terra batida.</param>
/// <param name="GravelPm">Custo de andar no cascalho.</param>
/// <param name="StonePm">Custo de andar na pedra.</param>
/// <param name="DetourPm">Custo extra (milésimos de célula) de cada trecho de prédio solto na reta: contornar. 830 = a volta em
/// grade de 8 vizinhos em torno de um obstáculo (2√2 − 2 células).</param>
/// <param name="OverPm">Custo extra (milésimos de célula) de cada trecho de casas coladas na reta: escada, laje ou telhado.</param>
/// <param name="HaulerOffRoadCells">Células sem caminho que o veículo de terra (trenó, padiola, carroça) aguenta na rota; mais
/// que isso, a carga volta aos braços.</param>
/// <param name="PavingDirtCells">Células de terra batida da cidade que pressionam a descobrir como calçar.</param>
/// <param name="PavingPrototypeTicks">Duração do primeiro trecho de teste (a descoberta).</param>
/// <param name="StoneSkillAxis">Nível do eixo de habilidade da técnica de calçar a partir do qual ela calça com pedra.</param>
/// <param name="GravelHoursPerCell">Horas de trabalho para calçar uma célula com cascalho (fora o transporte do material).</param>
/// <param name="StoneHoursPerCell">Horas de trabalho para calçar uma célula com pedra.</param>
/// <param name="LoadsPerCell">Viagens de material (ida e volta da fonte) por célula.</param>
/// <param name="StoneTerrainId">Terreno de onde sai pedra (o mais pesado do catálogo padrão); cascalho sai de célula com água
/// (leito do rio, vau).</param>
/// <param name="StoneResourceId">Recurso da economia que é pedra (o da pedreira no cenário padrão, AD-090). Se algum mercado o
/// vende, a pedra da obra é comprada lá e trazida pela rota; se ninguém no mundo lida com ele, sai do terreno pedregoso.</param>
/// <param name="StoneUnitsPerCell">Unidades de pedra por célula calçada.</param>
/// <param name="CrewMax">Diaristas por obra.</param>
/// <param name="CrewHoursPerDay">Horas de trabalho de cada diarista por dia.</param>
/// <param name="PaybackYears">Horizonte em que o tempo poupado por quem passa tem de pagar a obra.</param>
/// <param name="MaxCellsPerWork">Células numa obra só.</param>
/// <param name="GravelDecayPm">Desgaste mensal do cascalho sem uso (milésimos da condição).</param>
/// <param name="StoneDecayPm">Desgaste mensal da pedra sem uso.</param>
/// <param name="PassesPerDecayPoint">Passagens que tiram um milésimo da condição do calçamento.</param>
/// <param name="RepairBelowPm">Condição abaixo da qual quem pagou manda consertar.</param>
/// <param name="FoundingCommuteHours">Horas de caminho (trabalho + mercado) a partir das quais o aglomerado sem lote perto funda
/// cidade-filha; mais perto pelo caminho, continua parte da mãe.</param>
public sealed record StreetRules(
    bool Enabled = true,
    int TrailWear = 150,
    int DirtWear = 1500,
    int WearRetainPm = 850,
    int ReviewMonths = 3,
    int TrailPm = 850,
    int DirtPm = 800,
    int GravelPm = 750,
    int StonePm = 700,
    int DetourPm = 830,
    int OverPm = 1500,
    int HaulerOffRoadCells = 2,
    int PavingDirtCells = 12,
    long PavingPrototypeTicks = 720,
    int StoneSkillAxis = 4,
    int GravelHoursPerCell = 40,
    int StoneHoursPerCell = 120,
    int LoadsPerCell = 4,
    int StoneTerrainId = 3,
    int StoneResourceId = 5,
    int StoneUnitsPerCell = 4,
    int CrewMax = 6,
    int CrewHoursPerDay = 8,
    int PaybackYears = 10,
    int MaxCellsPerWork = 8,
    int GravelDecayPm = 8,
    int StoneDecayPm = 3,
    int PassesPerDecayPoint = 200,
    int RepairBelowPm = 600,
    double FoundingCommuteHours = 2.0)
{
    /// <summary>AD-179: peso máximo da oferta de trabalho na obra na utilidade de trabalhar (o termo <c>SiteWork</c>), na escala
    /// da necessidade de renda (<c>PsycheRules.IncomeNeedMax</c>).</summary>
    public double SiteOfferMax { get; init; } = 30;

    /// <summary>AD-179: oferta a partir da qual o adulto sem emprego aceita a obra (sai abaixo da metade disto).</summary>
    public double SiteJoinMin { get; init; } = 3;

    /// <summary>AD-179: reserva de moeda da casa, em dias de jornal, abaixo da qual a obra atrai (a despensa conta um terço disto).</summary>
    public int SiteReserveDays { get; init; } = 30;

    /// <summary>AD-179 (item 175): quilos de carga a pé que custam um milésimo a mais de tempo por quilo (ver
    /// <c>docs/domain/site-work.md</c>); a carga pessoal (<c>PersonalCarry</c>) e o material de obra pesam assim.</summary>
    public int CarryPmPerKg { get; init; } = 5;

    /// <summary>AD-179: carga a partir da qual o passo não piora mais (quem não aguenta mais não leva mais).</summary>
    public int CarryMaxKg { get; init; } = 40;

    /// <summary>AD-179: quilos de material numa viagem de obra (um cesto ou padiola de cascalho, pedra), a ida carregada.</summary>
    public int SiteLoadKg { get; init; } = 25;

    /// <summary>AD-179 (08d item 180): rodadas do chão da história da vila na criação do mundo (0 = nasce sem caminho).</summary>
    public int HistoryRounds { get; init; } = 2;

    /// <summary>AD-179: idas ao mercado por casa num mês típico da história (e as voltas). Medido na vila de código: 0,4 a 4,5 idas
    /// ao mercado por pessoa-mês; escolhido o meio por casa.</summary>
    public int HistoryMarketTripsPerMonth { get; init; } = 4;

    /// <summary>AD-179: a casa ou o prédio da fila da cidade só anda pelas horas de uma turma no lote. Desligado por padrão: na vila
    /// de pleno emprego (cenário <c>default.json</c>) ninguém sem emprego aceita, e a cidade parava de construir; ligar pede antes a
    /// família e os vizinhos construírem (ajuda mútua) ou o salário da obra disputar a mão (issue no relatório da AD-179).</summary>
    public bool BuildNeedsCrew { get; init; }

    /// <summary>AD-179: meses sem nenhuma hora de turma depois dos quais a obra de calçamento é desfeita (o material comprado fica
    /// gasto): numa vila sem mão livre ela não fica aberta para sempre, segurando a cidade.</summary>
    public int SiteStaleMonths { get; init; } = 6;

    /// <summary>AD-179: unidades de material por viagem do estoque da cidade ao lote (um cesto ou padiola).</summary>
    public int BuildUnitsPerLoad { get; init; } = 2;

    /// <summary>AD-185: quem procura emprego vai à oficina do ofício com vaga mais perto de casa pela rota (antes: a de menor id,
    /// e na metrópole todos iam à primeira fazenda, a 2 h de casa); desligado, a de menor id.</summary>
    public bool NearestJob { get; init; } = true;

    public static StreetRules Default { get; } = new();

    /// <summary>A regra de antes do AD-176 (linha reta), para medir pareado.</summary>
    public static StreetRules Off { get; } = new(Enabled: false);
}
