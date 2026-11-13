namespace LivingWorld.Domain.Economy;

/// <summary>Ano agrícola de cereal de inverno (AD-135): semeia no outono, a lavoura acumula grau-dia acima de
/// <see cref="BaseTemperature"/> até <see cref="DevelopmentToMaturity"/>, amadurece no verão e é ceifada à mão numa janela
/// curta; o que fica em pé debulha e se perde. A colheita repõe primeiro a semente do ano seguinte; até <see cref="ReaperShare"/> do resto vai em espécie
/// para a tulha da casa de quem ceifou (até <see cref="HomeStorePerMember"/> por morador), e o resto vai ao celeiro, que perde <see cref="BarnLossPerYear"/> para ratos, mofo e caruncho e abastece a venda da fazenda até a próxima
/// safra. Unidades: a mesma unidade de alimento que <c>Eat</c> consome (uma ração; ~320 por pessoa por ano no cenário
/// padrão). A vila semeia para as bocas que tem (<see cref="SeedPerResident"/> por morador), até o que os braços da fazenda
/// lavram (<see cref="MaxSeedPerWorker"/> por empregado). A espiga só amadurece a partir de <see cref="EarliestRipeningMonth"/> (o dia longo do verão, fotoperíodo), então o
/// inverno ameno não adianta a colheita para a primavera. O celeiro raciona o que solta por dia para durar até o meio de
/// <see cref="ExpectedHarvestMonth"/>. Meses são índices 0..11 do calendário do mundo. <see cref="Disabled"/> mantém o cultivo antigo (lote diário).</summary>
public sealed record AgricultureRules(
    bool Enabled,
    int SowStartMonth = 9,
    int SowEndMonth = 10,
    int FailMonth = 9,
    double BaseTemperature = 0,
    double DevelopmentToMaturity = 1300,
    int EarliestRipeningMonth = 6,
    int ExpectedHarvestMonth = 7,
    double MinFieldWorkTemperature = 2,
    double YieldPerSeed = 4.0,
    double SkillYieldWeight = 0.3,
    long SeedPerResident = 150, // AD-140: 120 x (3,75 - 1) x 0,85 ~ 280 = consumo, sem margem para ano ruim e crescimento
    long MaxSeedPerWorker = 500,
    long SowPerWorkerDay = 30,
    long ReapPerWorkerDay = 100,
    double ShatterLossPerDay = 0.01,
    int MaxHarvestDays = 40,
    double BarnLossPerYear = 0.15,
    int ShopStockDaysPerPerson = 7,
    double InitialDevelopment = 500,
    long InitialProvisionPerPerson = 260,
    double ReaperShare = 0.5,
    long HomeStorePerMember = 10)
{
    /// <summary>Lote diário antigo (trigo em 24 h): padrão de mundo construído à mão e de snapshot antigo.</summary>
    public static AgricultureRules Disabled { get; } = new(false);

    /// <summary>Padrão dos cenários (código e JSON).</summary>
    public static AgricultureRules Default { get; } = new(true);

    /// <summary>Mês dentro da janela de semeadura (aceita janela que vira o ano).</summary>
    public bool IsSowingMonth(int month) =>
        SowStartMonth <= SowEndMonth ? month >= SowStartMonth && month <= SowEndMonth : month >= SowStartMonth || month <= SowEndMonth;
}
