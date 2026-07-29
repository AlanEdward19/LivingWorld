using LivingWorld.Domain;

namespace LivingWorld.Simulation.Economy;

/// <summary>Mercado mais próximo por célula de origem, recomputado uma vez por tick (PERF-05).</summary>
public sealed class MarketIndex
{
    private readonly Dictionary<CellCoord, Workplace?> _nearestByOrigin;

    private MarketIndex(Dictionary<CellCoord, Workplace?> nearestByOrigin) =>
        _nearestByOrigin = nearestByOrigin;

    public Workplace? NearestTo(CellCoord origin) =>
        _nearestByOrigin.TryGetValue(origin, out var market) ? market : null;

    public static MarketIndex BuildForTick(WorldState world)
    {
        var catalog = world.EconomyCatalog;
        var markets = world.Workplaces
            .Where(w => catalog.MarketLocationTypeIds.Contains(w.LocationType.Id))
            .OrderBy(w => w.Id.Value)
            .ToList();

        var map = world.Map;
        var origins = new HashSet<CellCoord>();
        foreach (var npc in world.AliveNpcIndex.Alive)
            origins.Add(npc.CurrentLocation);
        foreach (var wp in world.Workplaces)
            origins.Add(wp.Location);

        var nearest = new Dictionary<CellCoord, Workplace?>();
        foreach (var origin in origins.OrderBy(c => c.X).ThenBy(c => c.Y))
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
            nearest[origin] = best;
        }

        return new MarketIndex(nearest);
    }
}
