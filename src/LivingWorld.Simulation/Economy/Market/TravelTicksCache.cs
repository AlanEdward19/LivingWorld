using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Geography.Map;

namespace LivingWorld.Simulation.Economy.Market;

/// <summary>Ticks de deslocamento entre duas células de um mapa, memoizados por mapa. É função pura de (mapa, origem,
/// destino) (o terreno de uma célula não muda), então o memo nunca fica velho: mundo reaberto no meio do dia e execução
/// contínua veem o mesmo valor. Evita recalcular o trajeto a cada despertar de cada NPC.</summary>
internal static class TravelTicksCache
{
    private static readonly ConditionalWeakTable<WorldMap, ConcurrentDictionary<(CellCoord, CellCoord), long>> Memo = new();

    public static long Between(WorldMap map, CellCoord origin, CellCoord destination)
    {
        if (origin == destination) return 0;
        var byPair = Memo.GetOrCreateValue(map);
        if (byPair.TryGetValue((origin, destination), out long ticks)) return ticks;

        ticks = TravelResolution.TicksBetween(map, origin, destination);
        byPair[(origin, destination)] = ticks;
        return ticks;
    }
}
