using LivingWorld.Domain.Climate;

namespace LivingWorld.Workers;

/// <summary>AD-180, só para os relatórios pareados: <c>LW_WEATHER=off</c> desliga o tempo diário (volta o sorteio anual da safra e a
/// estação em degrau de trimestre); <c>nomud</c> tira o efeito do chão no passo, <c>nodrought</c> o da seca na fonte, <c>nobody</c> o
/// do frio no corpo (relento e risco do mês), <c>nowork</c> a parada do trabalho ao ar livre (ablação); vazio = padrão (ligado).</summary>
public static class WeatherSwitch
{
    public static ClimateRules? ClimateRules()
    {
        string? mode = Environment.GetEnvironmentVariable("LW_WEATHER");
        var d = LivingWorld.Domain.Climate.ClimateRules.Default;
        var w = WeatherRules.Default;
        return mode switch
        {
            "off" => d with { Weather = null },
            "nomud" => d with { Weather = w with { WetTravelFactor = 1, MudTravelFactor = 1, HeavyMudTravelFactor = 1, SnowTravelPerCm = 0 } },
            "nodrought" => d with { Weather = w with { DroughtSpringFloor = 1 } },
            "nobody" => d with { Weather = w with { ColdHazardPerC = 0, ColdHazardPerCOthers = 0, HeatHazardPerC = 0, RoughSleepDeathPerDegNight = 0, RoughSleepHealthPerDegNight = 0 } },
            "nowork" => d with { Weather = w with { HeavyRainMm = 10_000, FieldSnowCm = 10_000, FrozenGroundMeanC = -100, ReapStopsAtOrAboveMm = 10_000, FieldDayHours = 1 } },
            _ => null,
        };
    }
}
