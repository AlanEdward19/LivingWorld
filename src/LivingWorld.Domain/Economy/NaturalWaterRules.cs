namespace LivingWorld.Domain.Economy;

/// <summary>Água de beber da natureza (AD-135): a casa que mora a até <see cref="MaxWalkTicks"/> horas de caminhada de uma
/// célula com água (rio, lago, fonte; <c>MapCell.HasWater</c>) enche a própria despensa de água dali, sem mercado e sem
/// pagar, até <see cref="DaysOfSupply"/> dias por morador. A busca olha as células com água num raio de
/// <see cref="SearchRadiusCells"/> em volta da casa. <see cref="Disabled"/>: só a água do mercado e do poço da fazenda.
/// AD-185: com <see cref="Fetch"/> ligado (e a carga pessoal da invenção), a despensa só enche com o que alguém da casa foi buscar
/// a pé e trouxe (<see cref="WaterFetchRules"/>); o enchimento sem viagem fica só para o mundo sem a carga.</summary>
public sealed record NaturalWaterRules(bool Enabled, long MaxWalkTicks = 1, int DaysOfSupply = 7, int SearchRadiusCells = 3)
{
    public static NaturalWaterRules Disabled { get; } = new(false);

    public static NaturalWaterRules Default { get; } = new(true);

    /// <summary>AD-185: buscar água é uma viagem de verdade. Ausente no snapshot antigo: o padrão (ligado).</summary>
    public WaterFetchRules Fetch { get; init; } = WaterFetchRules.Default;
}

/// <summary>Buscar água andando (AD-185). Alguém da casa com tempo livre e idade (<see cref="MinAgeYears"/>) vai à água mais perto
/// pela rota (até <see cref="MaxWalkHours"/> de ida, num raio de <see cref="RadiusCells"/> células) quando a água da casa cai abaixo
/// de <see cref="RefillBelowDays"/> dias por morador (abaixo de <see cref="EmployedBelowDays"/>, vai mais de um); quem tem emprego só vai abaixo de <see cref="EmployedBelowDays"/> dias ou se
/// ninguém livre da casa pode ir (o costume: vão primeiro as crianças crescidas, os sem ofício e os velhos). Bebe na fonte, enche
/// o cântaro da casa (<see cref="VesselMl"/>, ou o recipiente inventado se for maior; o veículo de terra se a rota o comporta), até
/// <see cref="NaturalWaterRules.DaysOfSupply"/> dias, no limite do peso a pé, e volta carregado: a despensa só recebe o que chegou.
/// Números (precisam de conferência): cântaro, balde ou odre de 10–20 L por viagem; crianças a partir de 6–10 anos já buscam água
/// em sociedades agrárias; ida de até 1–3 h é comum onde não há poço (OMS/UNICEF: mais de 30 min ida e volta já é "acesso
/// limitado").</summary>
public sealed record WaterFetchRules(
    bool Enabled = true, long MaxWalkHours = 3, int RadiusCells = 6, int RefillBelowDays = 3, int EmployedBelowDays = 1,
    int MinAgeYears = 8, int VesselMl = 20000)
{
    public static WaterFetchRules Default { get; } = new();

    public static WaterFetchRules Off { get; } = new(Enabled: false);
}
