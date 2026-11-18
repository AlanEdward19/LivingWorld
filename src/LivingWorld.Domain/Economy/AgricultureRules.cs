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
    long HomeStorePerMember = 10,
    double SownHectaresPerFounder = 1.4,
    double SeedPerHectare = 220,
    double HandIntensityGain = 0.25,
    long HandKeepPerYear = 320,
    bool WholeVillageReaps = true,
    bool PayForCityGrain = true)
{
    // AD-190: PayForCityGrain — o grão (venda ou semente) que uma fazenda tira do celeiro de outra da cidade é pago a ela ao preço
    // de hoje (falso = o de antes, de graça).
    // AD-149, ceifa (backlog 08 item 41): com a lavoura madura a fazenda toma todo adulto do ofício sem trabalho na cidade
    // (WholeVillageReaps), cada um ceifa ReapPerWorkerDay por dia de foice; passada a ceifa, solta quem a terra não paga.

    // AD-149, terra finita (backlog 08 item 49). SownHectaresPerFounder: hectares semeados por ano que a vila tem por morador
    // fundador (0 = terra sem limite, o antigo); o cenário de código dá 1,4, o dobro do que os fundadores semeiam (150 de
    // semente ÷ 220 por hectare ≈ 0,68 ha). A fonte pede 0,7–1,0 (correções #8); AD-163 mediu 1,0 (vila de 131 em 50 anos,
    // natalidade −8%, mortalidade igual) mas o custo por NPC-tick a 1.000 sobe 3–8% (gente sem lugar procurando trabalho):
    // fica 1,4 até o custo do desemprego cair. SeedPerHectare: ~135 kg de trigo por hectare, a ração ≈ 0,6 kg. Com a terra toda
    // semeada, mais braços só capinam, estercam e lavram de novo: o rendimento por semente sobe até HandIntensityGain, com
    // retorno decrescente (1 − braços necessários ÷ braços). A fazenda só toma mais uma mão se ela colhe ao menos o que come
    // num ano (HandKeepPerYear). Números sem fonte firme, precisam de conferência.

    /// <summary>Braços que a terra pede para ser toda semeada (<see cref="MaxSeedPerWorker"/> por braço).</summary>
    public double HandsForLand(long arableSeed) => arableSeed / (double)Math.Max(1, MaxSeedPerWorker);

    /// <summary>Rendimento a mais por semente quando há mais braços que a terra pede (capina, esterco, lavra repetida).</summary>
    public double LandIntensity(long arableSeed, int hands)
    {
        if (arableSeed <= 0 || HandIntensityGain <= 0) return 1.0;
        double need = HandsForLand(arableSeed);
        return hands <= need ? 1.0 : 1.0 + HandIntensityGain * (1.0 - need / hands);
    }

    /// <summary>Colheita esperada do ano com <paramref name="hands"/> braços na terra (sem clima nem habilidade).</summary>
    public double ExpectedHarvest(long arableSeed, int hands) =>
        YieldPerSeed * Math.Min(arableSeed, (double)hands * MaxSeedPerWorker) * LandIntensity(arableSeed, hands);

    /// <summary>A terra ainda paga mais uma mão: o que ela acrescenta à colheita ≥ o que ela come no ano. Sem limite de terra, sim.</summary>
    public bool LandTakesAnotherHand(long arableSeed, int hands) =>
        arableSeed <= 0 || ExpectedHarvest(arableSeed, hands + 1) - ExpectedHarvest(arableSeed, hands) >= HandKeepPerYear;

    /// <summary>AD-182 (08c item 148): na carestia (o celeiro solta por dia menos do que a venda pede), o vendedor reparte: cada compra
    /// de comida leva no máximo esta ração de dias por morador da casa, para o pouco do dia chegar a mais casas em vez de a primeira
    /// levar a semana. Era a regra das cidades na carestia (venda em pouca quantidade, contra o açambarcamento) [precisa de
    /// conferência]. 0 desliga (a compra leva a meta inteira, como antes).</summary>
    public int DearthSaleDays { get; init; } = 2;

    /// <summary>AD-182: a semente do ano seguinte não vai à venda; o dono do celeiro a guarda mesmo na fome (antes, celeiro vazio,
    /// a venda comia a semente). Falso = o de antes.</summary>
    public bool SeedKeptFromSale { get; init; } = true;

    /// <summary>Lote diário antigo (trigo em 24 h): padrão de mundo construído à mão e de snapshot antigo.</summary>
    public static AgricultureRules Disabled { get; } = new(false);

    /// <summary>Padrão dos cenários (código e JSON).</summary>
    public static AgricultureRules Default { get; } = new(true);

    /// <summary>Mês dentro da janela de semeadura (aceita janela que vira o ano).</summary>
    public bool IsSowingMonth(int month) =>
        SowStartMonth <= SowEndMonth ? month >= SowStartMonth && month <= SowEndMonth : month >= SowStartMonth || month <= SowEndMonth;
}
