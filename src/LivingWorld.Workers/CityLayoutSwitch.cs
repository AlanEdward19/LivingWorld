using LivingWorld.Domain.Cities;

namespace LivingWorld.Workers;

/// <summary>Medição do traçado da cidade (AD-173) e da rua de verdade (AD-176), só para os relatórios: <c>LW_CITY_LAYOUT=off</c>
/// volta à colocação de antes (sem ruas nem lotes); <c>LW_STREETS=off</c> volta ao deslocamento em linha reta, sem pisoteio nem
/// calçamento; vazio = padrão (ligado).</summary>
public static class CityLayoutSwitch
{
    public static CityLayoutRules? Rules()
    {
        bool layoutOff = Environment.GetEnvironmentVariable("LW_CITY_LAYOUT") == "off";
        bool streetsOff = Environment.GetEnvironmentVariable("LW_STREETS") == "off";
        if (!layoutOff && !streetsOff) return null;
        var rules = layoutOff ? CityLayoutRules.Off : CityLayoutRules.Default;
        return streetsOff ? rules with { Streets = StreetRules.Off } : rules;
    }
}
