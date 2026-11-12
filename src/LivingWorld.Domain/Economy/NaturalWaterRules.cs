namespace LivingWorld.Domain.Economy;

/// <summary>Água de beber da natureza (AD-135): a casa que mora a até <see cref="MaxWalkTicks"/> horas de caminhada de uma
/// célula com água (rio, lago, fonte; <c>MapCell.HasWater</c>) enche a própria despensa de água dali, sem mercado e sem
/// pagar, até <see cref="DaysOfSupply"/> dias por morador. A busca olha as células com água num raio de
/// <see cref="SearchRadiusCells"/> em volta da casa. <see cref="Disabled"/>: só a água do mercado e do poço da fazenda.</summary>
public sealed record NaturalWaterRules(bool Enabled, long MaxWalkTicks = 1, int DaysOfSupply = 7, int SearchRadiusCells = 3)
{
    public static NaturalWaterRules Disabled { get; } = new(false);

    public static NaturalWaterRules Default { get; } = new(true);
}
