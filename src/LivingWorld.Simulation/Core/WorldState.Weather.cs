using System.Text.Json.Serialization;
using LivingWorld.Simulation.Climate;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Simulation.Core;

// Parte de WorldState — o tempo do dia por região (AD-180).
public sealed partial class WorldState
{
    /// <summary>O tempo do mundo (AD-180). Nulo com o tempo desligado (e antes do primeiro dia gerado): omitido do snapshot e do hash.</summary>
    [Canonical, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public WeatherLedger? Weather { get; private set; }

    /// <summary>O tempo diário está ligado nas regras do mundo.</summary>
    internal bool WeatherOn => ClimateRules.WeatherOn();

    /// <summary>Restaura o tempo guardado (snapshot) ou o liga no primeiro dia; com o tempo desligado, fica nulo.</summary>
    public void UseWeather(WeatherLedger? ledger)
    {
        Weather = WeatherOn ? ledger ?? new WeatherLedger() : null;
        Weather?.Reindex();
        CanonicalHashCache.MarkPropertyDirty(nameof(Weather));
    }

    /// <summary>O dia mudou o tempo: volta o cache de temperatura e marca o hash.</summary>
    internal void TouchWeather()
    {
        if (Weather is null) return;
        Weather.Version++;
        TemperatureAdjustmentsVersion++;
        CanonicalHashCache.MarkPropertyDirty(nameof(Weather));
    }
}
