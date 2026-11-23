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
public sealed partial record StreetRules(
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

    /// <summary>AD-179/AD-184: a casa ou o prédio da fila da cidade só anda pelas horas de uma turma no lote. Ligado por padrão desde
    /// a AD-184: o jornal da obra disputa a mão com o ofício (<see cref="WageBidding"/>), então a cidade de pleno emprego também
    /// constrói, mais devagar.</summary>
    public bool BuildNeedsCrew { get; init; } = true;

    /// <summary>AD-184: casa nova é obra. A casa que precisa de moradia (sem casa, ou o casal morando de favor) abre uma obra num lote;
    /// a casa só existe quando ela termina. Desligado: o casal mora na célula onde estava, como antes.</summary>
    public bool HouseIsWork { get; init; } = true;

    /// <summary>AD-184: a oficina de quem empreende é obra; o negócio abre quando o prédio fica pronto. Desligado: abre no mês.</summary>
    public bool VentureIsWork { get; init; } = true;

    /// <summary>AD-184: a família, os parentes e os vizinhos de afeto trabalham na obra da casa nas horas livres, sem jornal.</summary>
    public bool HelpersBuild { get; init; } = true;

    /// <summary>AD-184: quem encomenda sobe a oferta quando não acha braço, até o que pode pagar; o empregado compara com o salário.</summary>
    public bool WageBidding { get; init; } = true;

    /// <summary>AD-184: horas-homem de levantar a casa de uma família (madeira, taipa e palha), sem o quintal nem o material.</summary>
    public int HouseHours { get; init; } = 480;

    /// <summary>AD-184: horas de buscar o material da casa na natureza quando ninguém o vende na cidade.</summary>
    public int HouseGatherHours { get; init; } = 160;

    /// <summary>AD-184: recurso da economia que é o material da casa (madeira no <c>default.json</c>) e quantas unidades ela leva; se
    /// alguém o vende na cidade e a casa pode pagar, ela compra e só carrega (sem as horas de buscar na natureza).</summary>
    public int HouseMaterialResourceId { get; init; } = 3;
    public int HouseMaterialUnits { get; init; } = 20;

    /// <summary>AD-194: o material é estoque do canteiro. As horas de quem está na obra vão primeiro a trazer carga (comprada de quem
    /// vende, carga por carga, ou buscada na natureza), <see cref="BuildUnitsPerLoad"/> unidades por viagem pela rota; a parede só sobe
    /// com material no lote; sem material, sem quem venda ou sem como pagar, e sem como buscar, a obra trava; o que sobra volta a quem
    /// pagou. Vale para a casa e a oficina. Desligado (<c>LW_AD194=off</c>): o material vira horas na abertura, como na AD-184.</summary>
    public bool MaterialOnSite { get; init; } = true;

    /// <summary>AD-194: a obra registra com lugar quando perde o dia pelo tempo e quando volta (uma vez por episódio).</summary>
    public bool WorkDayEvents { get; init; } = true;

    /// <summary>AD-195: quem traz o material anda de verdade: sai do lote, vai pela rota à despensa da casa, à banca de quem vende ou à
    /// mata, pega a carga nas mãos (o que aguenta pelo peso), volta e a deposita no lote; vários da turma carregam ao mesmo tempo
    /// enquanto o lote não tem material para a próxima jornada. Desligado (<c>LW_AD195=nowalk</c>): as horas pagam a viagem sem sair
    /// do lote, como na AD-194.</summary>
    public bool HaulersWalk { get; init; } = true;

    /// <summary>AD-195: a casa leva primeiro o material que já tem na despensa (sem comprar nem buscar); desligado: só banca e mata.</summary>
    public bool MaterialFromHome { get; init; } = true;

    /// <summary>AD-195: o corte na mata não conta de novo a caminhada que já é andada (o total da AD-184 menos as viagens pela rota).</summary>
    public bool CutWithoutWalk { get; init; } = true;

    /// <summary>AD-195: com moeda e a banca com estoque, a casa compra o que pode da carga em vez de ir cortar.</summary>
    public bool BuyPartialLoads { get; init; } = true;

    /// <summary>AD-195: dia de mutirão: o ajudante de vontade forte (família, parente, vizinho de afeto) dá um dia inteiro por semana à
    /// obra, fora da semeadura e da ceifa de quem lavra; quem lavra dá dois na entressafra.</summary>
    public bool MutiraoDays { get; init; } = true;

    /// <summary>AD-195: vontade mínima de ajudar (a mesma conta de <c>HelpMin</c>) para dar o dia inteiro e não só as horas livres.</summary>
    public double MutiraoDayMin { get; init; } = 0.4;

    /// <summary>AD-184: quanto vale à família ter a casa própria em vez de morar de favor ou apertada, em dias de jornal por morador
    /// por ano: a casa nova só compensa se as idas de cada dia de lá não custam mais que isso a mais que as de onde ela está.</summary>
    public int OwnHomeDaysPerYear { get; init; } = 30;

    /// <summary>AD-184: lotes de rua que a família compara ao escolher onde levantar a casa (o de sempre é o primeiro).</summary>
    public int LotChoices { get; init; } = 8;

    /// <summary>AD-184: idas por morador por dia à água e ao mercado de comida, o peso de cada lugar na escolha da casa.</summary>
    public double WaterTripsPerMemberDay { get; init; } = 0.25;
    public double MarketTripsPerMemberDay { get; init; } = 0.15;

    /// <summary>AD-184: horas de cercar, valar e limpar cada célula de quintal.</summary>
    public int CroftHoursPerCell { get; init; } = 6;

    /// <summary>AD-184: foro da terra do quintal, em dias de jornal por célula, pago ao tesouro da cidade ao abrir a obra.</summary>
    public int CroftFeeDaysPerCell { get; init; } = 2;

    /// <summary>AD-184: hora do dia até a qual se trabalha na obra nas horas livres (sem luz, ninguém levanta parede).</summary>
    public int BuildDaylightEndHour { get; init; } = 20;

    /// <summary>AD-184: fome e sede (0–100, cheio = 100) abaixo das quais a hora livre não vai à obra (come e bebe em casa).</summary>
    public int HelperNeedFloor { get; init; } = 40;

    /// <summary>AD-184: ajudantes sem jornal (parentes e vizinhos) numa obra, fora a própria casa.</summary>
    public int HelpersMax { get; init; } = 6;

    /// <summary>AD-184: afeto (0–100) a partir do qual o vizinho entra no mutirão.</summary>
    public double MutiraoAffection { get; init; } = 50;

    /// <summary>AD-184: vontade de ajudar (parentesco ou afeto × generosidade × a parte do dia que a ida deixa) a partir da qual ajuda.</summary>
    public double HelpMin { get; init; } = 0.25;

    /// <summary>AD-184: quanto a oferta sobe (milésimos do jornal) a cada semana sem braço bastante.</summary>
    public int OfferRaisePm { get; init; } = 100;

    /// <summary>AD-184: teto da oferta, em milésimos do jornal.</summary>
    public int OfferMaxPm { get; init; } = 3000;

    /// <summary>AD-184: o empregado só larga o ofício pela obra se o jornal dela vale esta razão do salário (a da troca de emprego).</summary>
    public double QuitMargin { get; init; } = 1.25;

    /// <summary>AD-184: quantas reservas (<see cref="SiteReserveDays"/> dias de jornal) a casa precisa ter guardadas para o jornal da
    /// obra não atrair mais quem está sem trabalho.</summary>
    public int SiteComfortReserves { get; init; } = 10;

    /// <summary>AD-184: a margem do lavrador na entressafra (fora da semeadura e da ceifa: a ceifa o toma de volta) e no pico (na
    /// semeadura e na ceifa ninguém larga a lavoura por obra nenhuma: com 2,0, a oficina em obra tirava os ceifeiros e a fazenda
    /// quebrava, cidade média seed 2 de 221 para 124 moradores).</summary>
    public double QuitMarginSlack { get; init; } = 1.1;
    public double QuitMarginPeak { get; init; } = 1e9;

    /// <summary>AD-184: dias sem hora de obra depois dos quais ela conta como parada (evento); volta a contar ao retomar.</summary>
    public int StallDays { get; init; } = 7;

    /// <summary>AD-184: o trecho de teste que descobre como calçar é obra com turma (horas de gente no trecho, pagas pelo tesouro), não
    /// um prazo que corre sozinho; <see cref="PrototypeHours"/> horas de trabalho (tentativa e erro numa célula).</summary>
    public bool PrototypeIsWork { get; init; } = true;
    public int PrototypeHours { get; init; } = 120;

    /// <summary>AD-184: enquanto a casa não fica pronta, o casal mora de favor na casa de um dos pais (desligado: fica onde estava,
    /// como antes da AD-184; só para medir).</summary>
    public bool LodgeWithParents { get; init; } = true;

    /// <summary>AD-184: quem mora de favor e quem o acolhe dividem o pote de água da casa: quem vai à fonte enche para os dois, e a
    /// água que chega fica no pote de todos (a mesma casa, sem viagem entre despensas). Desligado: cada casa enche o seu.</summary>
    public bool LodgersSharePot { get; init; } = true;

    /// <summary>AD-184: e dividem a mesa: sem comida na despensa da casa, quem come em casa come da de quem divide o teto (a mesma
    /// cozinha). Desligado: cada casa come só do seu.</summary>
    public bool LodgersShareTable { get; init; } = true;

    /// <summary>AD-184: o casal mora sozinho (casa própria) e não com os pais: costume do noroeste europeu (neolocal).</summary>
    public bool NeolocalCouples { get; init; } = true;

    /// <summary>AD-179: meses sem nenhuma hora de turma depois dos quais a obra de calçamento é desfeita (o material comprado fica
    /// gasto): numa vila sem mão livre ela não fica aberta para sempre, segurando a cidade.</summary>
    public int SiteStaleMonths { get; init; } = 6;

    /// <summary>AD-179: unidades de material por viagem do estoque da cidade ao lote (um cesto ou padiola).</summary>
    public int BuildUnitsPerLoad { get; init; } = 2;

    /// <summary>AD-185: quem procura emprego vai à oficina do ofício com vaga mais perto de casa pela rota (antes: a de menor id,
    /// e na metrópole todos iam à primeira fazenda, a 2 h de casa); desligado, a de menor id.</summary>
    public bool NearestJob { get; init; } = true;

    public static StreetRules Default { get; } = new();

    /// <summary>AD-184 desligada (<c>LW_AD184=off</c>), para medir pareado: a obra da AD-179 como era.</summary>
    /// <summary>AD-194 desligada (<c>LW_AD194=off</c>): material em horas e sem eventos de dia perdido, como na AD-184.</summary>
    public StreetRules WithoutAd194() => this with { MaterialOnSite = false, WorkDayEvents = false, HaulersWalk = false, MaterialFromHome = false, CutWithoutWalk = false, BuyPartialLoads = false, MutiraoDays = false };

    /// <summary>AD-195 desligada (<c>LW_AD195=off</c>, ou só uma regra: <c>nowalk</c>, <c>nohome</c>).</summary>
    public StreetRules WithAd195Mode(string mode) => mode switch
    {
        "off" => this with { HaulersWalk = false, MaterialFromHome = false, CutWithoutWalk = false, BuyPartialLoads = false, MutiraoDays = false },
        "nowalk" => this with { HaulersWalk = false },
        "nohome" => this with { MaterialFromHome = false },
        "nocut" => this with { CutWithoutWalk = false },
        "nopartial" => this with { BuyPartialLoads = false },
        "nomutirao" => this with { MutiraoDays = false },
        _ => this,
    };

    public StreetRules WithoutAd184() => this with
    {
        BuildNeedsCrew = false,
        HouseIsWork = false,
        VentureIsWork = false,
        HelpersBuild = false,
        WageBidding = false,
        PrototypeIsWork = false,
        MaterialOnSite = false,
        WorkDayEvents = false,
        HaulersWalk = false,
        MaterialFromHome = false,
        CutWithoutWalk = false,
        BuyPartialLoads = false,
        MutiraoDays = false,
    };

    /// <summary>A regra de antes do AD-176 (linha reta), para medir pareado.</summary>
    public static StreetRules Off { get; } = new(Enabled: false);
}
