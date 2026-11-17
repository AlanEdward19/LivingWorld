using LivingWorld.Domain.Cities;

namespace LivingWorld.Workers;

/// <summary>Medição do traçado da cidade (AD-173), só para os relatórios: <c>LW_CITY_LAYOUT=off</c> volta à colocação de antes
/// (sem ruas nem lotes); vazio = padrão (ligado).</summary>
public static class CityLayoutSwitch
{
    public static CityLayoutRules? Rules() =>
        Environment.GetEnvironmentVariable("LW_CITY_LAYOUT") == "off" ? CityLayoutRules.Off : null;
}
