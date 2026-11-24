using System.Runtime.CompilerServices;
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
    private readonly Memo _memo;
    private readonly Workplace[] _markets;
    private readonly WorldState _world;

    private MarketIndex(Memo memo, Workplace[] markets, WorldState world)
    {
        _memo = memo;
        _markets = markets;
        _world = world;
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

            long ticks = LivingWorld.Simulation.Cities.Streets.StreetRoutes.Ticks(_world, origin, market.Location); // AD-176: pela rota
            if (ticks < bestTicks || (ticks == bestTicks && market.Id.Value < bestId))
            {
                bestTicks = ticks;
                bestId = market.Id.Value;
                best = market;
            }
        }
        return best ?? NearestTo(origin);
    }

    /// <summary>Mesma busca de <see cref="NearestSelling(CellCoord, ResourceType[])"/> para dois recursos, sem alocar o
    /// array de parâmetros (a reposição da despensa consulta isso a cada despertar).</summary>
    public Workplace? NearestSelling(CellCoord origin, ResourceType first, ResourceType second)
    {
        Workplace? best = null;
        long bestTicks = long.MaxValue;
        long bestId = long.MaxValue;
        foreach (var market in _markets)
        {
            if (!Sells(market, first) && !Sells(market, second)) continue;

            long ticks = LivingWorld.Simulation.Cities.Streets.StreetRoutes.Ticks(_world, origin, market.Location);
            if (ticks < bestTicks || (ticks == bestTicks && market.Id.Value < bestId))
            {
                bestTicks = ticks;
                bestId = market.Id.Value;
                best = market;
            }
        }

        return best ?? NearestTo(origin);
    }

    private static bool Sells(Workplace market, ResourceType resource) =>
        market.Stock.GetValueOrDefault(resource) > 0 && market.Prices.TryGetValue(resource, out var price) && price > 0;

    public Workplace? NearestTo(CellCoord origin)
    {
        var memo = _memo;
        int i = memo.CellOf(origin);
        if (i < 0) return memo.OffOrigins.Contains(origin) && memo.OffNearest.TryGetValue(origin, out var off) ? off : null;
        return memo.OriginMark[i] == memo.Mark && memo.Known[i] ? memo.Nearest[i] : null;
    }

    /// <summary>AD-187: a visão do tick e o mais perto por célula em vetores do tamanho do mapa (marca do tick por célula, sem hash:
    /// eram duas consultas de tabela por pessoa viva a cada hora); célula fora do mapa (não deveria haver) no conjunto de antes.</summary>
    private sealed class Memo
    {
        public WorldMap? Map;
        public (long, int, bool) Routes;
        public Workplace[] Markets = [];
        public CellCoord[] MarketLocations = [];
        public int W, H;
        public Workplace?[] Nearest = [];
        public bool[] Known = [];
        /// <summary>Reaproveitado a cada tick (a visão do tick anterior já foi descartada): a célula está na visão quando a marca
        /// dela é a do tick.</summary>
        public int[] OriginMark = [];
        public int Mark;
        public readonly Dictionary<CellCoord, Workplace?> OffNearest = new();
        public readonly HashSet<CellCoord> OffOrigins = new();

        public int CellOf(CellCoord c) => c.X >= 0 && c.Y >= 0 && c.X < W && c.Y < H ? c.Y * W + c.X : -1;
    }

    private static readonly ConditionalWeakTable<WorldState, Memo> Memos = new();

    public static MarketIndex BuildForTick(WorldState world)
    {
        var catalog = world.EconomyCatalog;
        var map = world.Map;
        var memo = Memos.GetOrCreateValue(world);
        var routes = LivingWorld.Simulation.Cities.Streets.StreetRoutes.Stamp(world);
        if (!ReferenceEquals(memo.Map, map) || memo.Routes != routes || !MarketsUnchanged(world, memo))
        {
            memo.Routes = routes;
            var markets = world.Workplaces
                .Where(w => catalog.MarketLocationTypeIds.Contains(w.LocationType.Id))
                .OrderBy(w => w.Id.Value)
                .ToArray();
            memo.Map = map;
            memo.Markets = markets;
            memo.MarketLocations = markets.Select(m => m.Location).ToArray();
            if (memo.W != map.Width || memo.H != map.Height)
            {
                memo.W = map.Width; memo.H = map.Height;
                int n = memo.W * memo.H;
                memo.Nearest = new Workplace?[n]; memo.Known = new bool[n]; memo.OriginMark = new int[n]; memo.Mark = 0;
            }
            else
            {
                Array.Clear(memo.Nearest);
                Array.Clear(memo.Known);
            }
            memo.OffNearest.Clear();
        }

        if (++memo.Mark == int.MaxValue) { Array.Clear(memo.OriginMark); memo.Mark = 1; }
        memo.OffOrigins.Clear();
        foreach (var npc in world.AliveNpcIndex.Alive)
            AddOrigin(world, memo, npc.CurrentLocation);
        foreach (var wp in world.Workplaces)
            AddOrigin(world, memo, wp.Location);

        return new MarketIndex(memo, memo.Markets, world);
    }

    private static void AddOrigin(WorldState world, Memo memo, CellCoord origin)
    {
        int i = memo.CellOf(origin);
        if (i < 0)
        {
            if (memo.OffOrigins.Add(origin) && !memo.OffNearest.ContainsKey(origin))
                memo.OffNearest[origin] = NearestMarket(world, origin, memo.Markets);
            return;
        }
        if (memo.OriginMark[i] == memo.Mark) return;
        memo.OriginMark[i] = memo.Mark;
        if (memo.Known[i]) return;
        memo.Nearest[i] = NearestMarket(world, origin, memo.Markets);
        memo.Known[i] = true;
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

    private static Workplace? NearestMarket(WorldState world, CellCoord origin, Workplace[] markets)
    {
        Workplace? best = null;
        long bestTicks = long.MaxValue;
        long bestId = long.MaxValue;
        foreach (var market in markets)
        {
            long ticks = LivingWorld.Simulation.Cities.Streets.StreetRoutes.Ticks(world, origin, market.Location);
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
