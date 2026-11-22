using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Production;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Economy.Market;

/// <summary>Recalcula <see cref="Workplace.Prices"/> por oferta/demanda,
/// <c>Daily</c>. Respeita <see cref="EconomyRules.Enabled"/>.</summary>
[LivingCapability("MARKET", Consumers = ["inspector.market"])]
[RunsAfter(typeof(ProductionSystem))]
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
        var populationIndex = RegionPopulationIndex.BuildForTick(world);

        foreach (var workplace in world.Workplaces.OrderBy(w => w.Id.Value))
        {
            if (!catalog.MarketLocationTypeIds.Contains(workplace.LocationType.Id)) continue;

            var region = world.Map.RegionOf(workplace.Location);
            long populationInRegion = populationIndex.AliveInRegion(region);

            foreach (var (resource, currentPrice) in workplace.Prices.ToList())
            {
                if (!rules.DemandBaselinePerNpc.TryGetValue(resource.Id, out var demandBaseline)) continue;

                long supplyOffered = workplace.Stock.GetValueOrDefault(resource);
                double demand = Math.Max(demandBaseline * populationInRegion, 0.0001);
                double ratio = supplyOffered / demand;
                double factor = 1 + rules.PriceSensitivity * (1 - ratio);

                long floor = rules.PriceFloor.GetValueOrDefault(resource.Id, currentPrice);
                long ceiling = rules.PriceCeiling.GetValueOrDefault(resource.Id, currentPrice);
                // Escassez sobe pelo menos 1: com preço inteiro, round(1 x 1,2) = 1 prendia o preço no piso para sempre.
                long scaled = factor > 1 ? (long)Math.Ceiling(currentPrice * factor) : (long)Math.Round(currentPrice * factor);
                long newPrice = Math.Clamp(scaled, floor, ceiling);
                // AD-191: o grão da fazenda também é caro quando o celeiro da cidade não cobre até a colheita mais a reserva
                if (resource.Id == rules.FoodResourceId && BarnScarcityPrice(world, workplace, floor) is { } barnPrice)
                    newPrice = Math.Clamp(Math.Max(newPrice, barnPrice), floor, ceiling);

                workplace.SetPrice(resource, newPrice);
            }
        }
    }

    /// <summary>AD-191 (<see cref="AgricultureRules.BarnReserveYears"/>): o preço que o celeiro curto pede na venda da fazenda — o piso
    /// vezes a cobertura (celeiro e banca das fazendas da cidade ÷ o que os moradores comem até a próxima colheita, mais a margem)
    /// elevada a −<see cref="AgricultureRules.BarnPriceExponent"/>; nulo com o celeiro cobrindo, fora da fazenda ou com a regra desligada.</summary>
    internal static long? BarnScarcityPrice(WorldState world, Workplace farm, long floor)
    {
        var ag = world.AgricultureRules;
        if (!ag.Enabled || ag.BarnReserveYears <= 0 || farm.LocationType.Id != CropSystem.DefaultFarmLocationTypeId) return null;
        if (world.FindCropAt(farm.Location) is not { } field) return null;
        var food = new LivingWorld.Domain.Geography.ResourceType(world.EconomyRules.FoodResourceId);
        long stores = 0, residents = 0;
        foreach (var other in world.Workplaces)
            if (other.City == farm.City && other.LocationType.Id == CropSystem.DefaultFarmLocationTypeId)
                stores += other.Stock.GetValueOrDefault(food) + (world.FindCropAt(other.Location)?.Barn ?? 0);
        foreach (var household in world.Households)
            if (household.City == farm.City) residents += household.Members.Count;
        if (residents <= 0) return null;
        var cal = world.CurrentDate.Calendar;
        double perDay = ag.HandKeepPerYear / (double)(cal.DaysPerMonth * cal.MonthsPerYear);
        double need = residents * perDay * (CropSystem.DaysToNextHarvest(world, ag, field) * (1 + ag.BarnPriceMargin)
            + ag.BarnReserveYears * cal.DaysPerMonth * cal.MonthsPerYear);
        double cover = stores / need;
        if (cover >= 1) return null;
        double factor = DeterministicMath.Exp(ag.BarnPriceExponent * DeterministicMath.Log(1.0 / Math.Max(0.05, cover)));
        return (long)Math.Ceiling(Math.Max(1, floor) * factor);
    }
}
