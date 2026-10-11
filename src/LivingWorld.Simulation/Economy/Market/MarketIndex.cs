using System.Runtime.CompilerServices;
using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Geography.Map;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Economy.Market;

/// <summary>Mercado mais próximo por célula de origem. A visão do tick cobre só as origens existentes
/// no início do tick (NPCs vivos e workplaces); o cálculo em si é função pura de (mapa, origem,
/// conjunto de mercados) e fica memoizado entre ticks enquanto o conjunto de mercados não muda —
/// recalcular origens × mercados a cada hora era o maior custo do tick de decisão.</summary>
public sealed class MarketIndex
{
    private readonly HashSet<CellCoord> _origins;
    private readonly Dictionary<CellCoord, Workplace?> _nearestByOrigin;
    private readonly Workplace[] _markets;
    private readonly WorldMap _map;

    private MarketIndex(
        HashSet<CellCoord> origins, Dictionary<CellCoord, Workplace?> nearestByOrigin, Workplace[] markets, WorldMap map)
    {
        _origins = origins;
        _nearestByOrigin = nearestByOrigin;
        _markets = markets;
        _map = map;
    }

    /// <summary>Mercado mais próximo que de fato tem algo a vender (estoque e preço de pelo menos um dos
    /// <paramref name="resources"/>; estoque sem preço é só sobra de outra coisa, não é venda); ir ao mercado mais perto que está vazio não alimenta ninguém.
    /// Sem nenhum com estoque, cai no mais próximo.</summary>
    public Workplace? NearestSelling(CellCoord origin, params ResourceType[] resources)
    {
        Workplace? best = null;
        long bestTicks = long.MaxValue;
        long bestId = long.MaxValue;
        foreach (var market in _markets)
        {
            bool sells = false;
            foreach (var resource in resources)
                if (market.Stock.GetValueOrDefault(resource) > 0
                    && market.Prices.TryGetValue(resource, out var price) && price > 0) { sells = true; break; }
            if (!sells) continue;

            long ticks = TravelResolution.TicksBetween(_map, origin, market.Location);
            if (ticks < bestTicks || (ticks == bestTicks && market.Id.Value < bestId))
            {
                bestTicks = ticks;
                bestId = market.Id.Value;
                best = market;
            }
        }
        return best ?? NearestTo(origin);
    }

    public Workplace? NearestTo(CellCoord origin) =>
        _origins.Contains(origin) && _nearestByOrigin.TryGetValue(origin, out var market) ? market : null;

    private sealed class Memo
    {
        public WorldMap? Map;
        public Workplace[] Markets = [];
        public CellCoord[] MarketLocations = [];
        public readonly Dictionary<CellCoord, Workplace?> Nearest = new();
        /// <summary>Reaproveitado a cada tick (a visão do tick anterior já foi descartada).</summary>
        public readonly HashSet<CellCoord> Origins = new();
    }

    private static readonly ConditionalWeakTable<WorldState, Memo> Memos = new();

    public static MarketIndex BuildForTick(WorldState world)
    {
        var catalog = world.EconomyCatalog;
        var map = world.Map;
        var memo = Memos.GetOrCreateValue(world);
        if (!ReferenceEquals(memo.Map, map) || !MarketsUnchanged(world, memo))
        {
            var markets = world.Workplaces
                .Where(w => catalog.MarketLocationTypeIds.Contains(w.LocationType.Id))
                .OrderBy(w => w.Id.Value)
                .ToArray();
            memo.Map = map;
            memo.Markets = markets;
            memo.MarketLocations = markets.Select(m => m.Location).ToArray();
            memo.Nearest.Clear();
        }

        var origins = memo.Origins;
        origins.Clear();
        foreach (var npc in world.AliveNpcIndex.Alive)
            origins.Add(npc.CurrentLocation);
        foreach (var wp in world.Workplaces)
            origins.Add(wp.Location);

        foreach (var origin in origins)
        {
            if (!memo.Nearest.ContainsKey(origin))
                memo.Nearest[origin] = NearestMarket(map, origin, memo.Markets);
        }

        return new MarketIndex(origins, memo.Nearest, memo.Markets, map);
    }

    /// <summary>Compara o conjunto atual de mercados com o memo sem alocar. Só vale quando
    /// <see cref="WorldState.Workplaces"/> está em ordem de Id (o normal); do contrário devolve false
    /// e o chamador reconstrói pelo caminho que ordena.</summary>
    private static bool MarketsUnchanged(WorldState world, Memo memo)
    {
        var marketTypes = world.EconomyCatalog.MarketLocationTypeIds;
        int found = 0;
        long lastId = long.MinValue;
        foreach (var workplace in world.Workplaces)
        {
            if (workplace.Id.Value < lastId) return false;
            lastId = workplace.Id.Value;
            if (!marketTypes.Contains(workplace.LocationType.Id)) continue;
            if (found >= memo.Markets.Length
                || !ReferenceEquals(memo.Markets[found], workplace)
                || memo.MarketLocations[found] != workplace.Location)
                return false;
            found++;
        }
        return found == memo.Markets.Length;
    }

    private static Workplace? NearestMarket(WorldMap map, CellCoord origin, Workplace[] markets)
    {
        Workplace? best = null;
        long bestTicks = long.MaxValue;
        long bestId = long.MaxValue;
        foreach (var market in markets)
        {
            long ticks = TravelResolution.TicksBetween(map, origin, market.Location);
            if (ticks < bestTicks || (ticks == bestTicks && market.Id.Value < bestId))
            {
                bestTicks = ticks;
                bestId = market.Id.Value;
                best = market;
            }
        }
        return best;
    }
}
