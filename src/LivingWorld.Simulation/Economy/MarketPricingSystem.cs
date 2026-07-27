using LivingWorld.Domain;

namespace LivingWorld.Simulation;

/// <summary>Recalcula <see cref="Workplace.Prices"/> por oferta/demanda (Fase 5, ECON-23/24),
/// <c>Daily</c>. Respeita <see cref="EconomyRules.Enabled"/> (ECON-05).</summary>
public sealed class MarketPricingSystem : ISimulationSystem
{
    public const string SystemName = "economy-market-pricing";

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Daily;

    public void Tick(WorldState world, TickContext ctx)
    {
        if (!world.EconomyRules.Enabled) return;

        var rules = world.EconomyRules;
        var catalog = world.EconomyCatalog;

        foreach (var workplace in world.Workplaces.OrderBy(w => w.Id.Value))
        {
            if (!catalog.MarketLocationTypeIds.Contains(workplace.LocationType.Id)) continue;

            var region = world.Map.RegionOf(workplace.Location);
            long populationInRegion = world.Npcs.Count(n => n.IsAlive && world.Map.RegionOf(n.CurrentLocation) == region);

            foreach (var (resource, currentPrice) in workplace.Prices.ToList())
            {
                if (!rules.DemandBaselinePerNpc.TryGetValue(resource.Id, out var demandBaseline)) continue;

                long supplyOffered = workplace.Stock.GetValueOrDefault(resource);
                double demand = Math.Max(demandBaseline * populationInRegion, 0.0001);
                double ratio = supplyOffered / demand;
                double factor = 1 + rules.PriceSensitivity * (1 - ratio);

                long floor = rules.PriceFloor.GetValueOrDefault(resource.Id, currentPrice);
                long ceiling = rules.PriceCeiling.GetValueOrDefault(resource.Id, currentPrice);
                long newPrice = Math.Clamp((long)Math.Round(currentPrice * factor), floor, ceiling);

                workplace.SetPrice(resource, newPrice);
            }
        }
    }
}
