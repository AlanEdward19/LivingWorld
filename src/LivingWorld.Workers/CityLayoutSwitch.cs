using LivingWorld.Domain.Cities;

namespace LivingWorld.Workers;

/// <summary>Medição do traçado da cidade (AD-173) e da rua de verdade (AD-176), só para os relatórios: <c>LW_CITY_LAYOUT=off</c>
/// volta à colocação de antes (sem ruas nem lotes); <c>LW_STREETS=off</c> volta ao deslocamento em linha reta, sem pisoteio nem
/// calçamento; <c>LW_AD179=off</c> tira o que a AD-179 mudou na vila de código (o chão da história e a carga que pesa no passo);
/// vazio = padrão (ligado).</summary>
public static class CityLayoutSwitch
{
    public static CityLayoutRules? Rules()
    {
        bool layoutOff = Environment.GetEnvironmentVariable("LW_CITY_LAYOUT") == "off";
        bool streetsOff = Environment.GetEnvironmentVariable("LW_STREETS") == "off";
        bool ad179Off = Environment.GetEnvironmentVariable("LW_AD179") == "off";
        if (!layoutOff && !streetsOff && !ad179Off) return null;
        var rules = layoutOff ? CityLayoutRules.Off : CityLayoutRules.Default;
        if (streetsOff) return rules with { Streets = StreetRules.Off };
        return ad179Off ? rules with { Streets = rules.Streets with { HistoryRounds = 0, CarryPmPerKg = 0, SiteLoadKg = 0 } } : rules;
    }
}
