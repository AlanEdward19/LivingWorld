using LivingWorld.Domain.Cities;

namespace LivingWorld.Workers;

/// <summary>Medição do traçado da cidade (AD-173) e da rua de verdade (AD-176), só para os relatórios: <c>LW_CITY_LAYOUT=off</c>
/// volta à colocação de antes (sem ruas nem lotes); <c>LW_STREETS=off</c> volta ao deslocamento em linha reta, sem pisoteio nem
/// calçamento; <c>LW_AD179=off</c> tira o que a AD-179 mudou na vila de código (o chão da história e a carga que pesa no passo);
/// <c>LW_AD184=off</c> volta a obra da AD-179 (casa e oficina sem obra, fila sem turma, sem oferta que sobe);
/// vazio = padrão (ligado).</summary>
public static class CityLayoutSwitch
{
    public static CityLayoutRules? Rules()
    {
        bool layoutOff = Environment.GetEnvironmentVariable("LW_CITY_LAYOUT") == "off";
        bool streetsOff = Environment.GetEnvironmentVariable("LW_STREETS") == "off";
        bool ad179Off = Environment.GetEnvironmentVariable("LW_AD179") == "off";
        string? ad184 = Environment.GetEnvironmentVariable("LW_AD184");
        bool ad184Off = ad184 == "off";
        bool ad184Part = ad184 is "nohelpers" or "nobid" or "nohouse"; // AD-184: uma regra só desligada, para atribuir o efeito
        if (!layoutOff && !streetsOff && !ad179Off && !ad184Off && !ad184Part) return null;
        var rules = layoutOff ? CityLayoutRules.Off : CityLayoutRules.Default;
        if (streetsOff) return rules with { Streets = StreetRules.Off };
        if (ad184Off) rules = rules with { Streets = rules.Streets.WithoutAd184() }; // AD-184: obra como na AD-179
        if (ad184 == "nohelpers") rules = rules with { Streets = rules.Streets with { HelpersBuild = false } };
        if (ad184 == "nohouse") rules = rules with { Streets = rules.Streets with { HouseIsWork = false } };
        if (ad184 == "nobid") rules = rules with { Streets = rules.Streets with { QuitMargin = 1e9, QuitMarginSlack = 1e9, QuitMarginPeak = 1e9 } };
        return ad179Off ? rules with { Streets = rules.Streets with { HistoryRounds = 0, CarryPmPerKg = 0, SiteLoadKg = 0 } } : rules;
    }
}
