namespace LivingWorld.Workers;

/// <summary>AD-180, só para os relatórios pareados: <c>LW_WEATHER=off</c> desliga o tempo diário (volta o sorteio anual da safra e a
/// estação em degrau de trimestre); vazio = padrão (ligado).</summary>
public static class WeatherSwitch
{
    public static LivingWorld.Domain.Climate.ClimateRules? ClimateRules() =>
        Environment.GetEnvironmentVariable("LW_WEATHER") == "off"
            ? LivingWorld.Domain.Climate.ClimateRules.Default with { Weather = null }
            : null;
}
