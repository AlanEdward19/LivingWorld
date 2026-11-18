using System.Text.Json.Serialization;
using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cities.Streets;

namespace LivingWorld.Simulation.Core;

// Parte de WorldState — o chão das cidades: pisoteio, superfície, calçamento e obras (AD-176).
public sealed partial class WorldState
{
    /// <summary>Chão pisado, superfície e obras de calçamento (AD-176). Nulo com a rua de verdade desligada: deslocamento em linha
    /// reta como antes, omitido do snapshot e do hash.</summary>
    [Canonical, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GroundLedger? Ground { get; private set; }

    /// <summary>Regras da rua em vigor (as do traçado da cidade).</summary>
    internal StreetRules StreetRules => CityRules.EffectiveLayout().Streets;

    internal bool StreetsOn => Ground is not null && StreetRules.Enabled;

    /// <summary>Cache volátil das rotas (função pura do chão, dos prédios e do mapa): nunca no snapshot.</summary>
    internal StreetRouteState? RouteState { get; set; }

    /// <summary>AD-185, só para o teste de equivalência: sem o cache de rotas (par e trecho da rede), toda rota é recalculada. O
    /// mundo tem de sair igual com e sem ele. Volátil, fora do snapshot.</summary>
    internal bool RouteCacheOff { get; set; }

    /// <summary>Liga o chão (criação do mundo) ou restaura o guardado (snapshot); com a regra desligada, fica nulo.</summary>
    public void UseGround(GroundLedger? ledger)
    {
        Ground = !StreetRules.Enabled ? null
            : ledger is { } kept && kept.Width == Map.Width && kept.Height == Map.Height ? kept
            : new GroundLedger(Map.Width, Map.Height, CurrentDate.TotalHours);
        RouteState = null;
        CanonicalHashCache.MarkPropertyDirty(nameof(Ground));
    }
}
