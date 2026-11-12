using System.Runtime.CompilerServices;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Geography.Map;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Market;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Economy.Supply;

/// <summary>Água de beber da natureza (AD-135), <c>Daily</c>: a casa a até <c>MaxWalkTicks</c> de caminhada de um rio, lago ou
/// fonte (célula com <see cref="MapCell.HasWater"/>) enche a despensa de água dali, sem mercado e sem pagar. Antes a água só
/// existia como mercadoria, e quem morava longe do mercado morria de sede ao lado de um rio. A água entra como produzida
/// (a conservação fecha) e é consumida por <c>Eat</c>; com a despensa cheia, a decisão de abastecimento não manda ninguém
/// comprar água. Uma passada por casa por dia; a distância até a água é calculada uma vez por célula do mapa.</summary>
[LivingCapability("NATURAL_WATER", Consumers = ["behavior.eat", "behavior.provisioning"])]
public sealed class NaturalWaterSystem : ISimulationSystem
{
    public const string SystemName = "natural-water";

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Daily;

    private static readonly ConditionalWeakTable<WorldMap, Dictionary<CellCoord, long>> WalkTicks = new();

    public void Tick(WorldState world, TickContext ctx)
    {
        var rules = world.NaturalWaterRules;
        if (!rules.Enabled || !world.EconomyRules.Enabled) return;

        var water = new ResourceType(world.EconomyRules.WaterResourceId);
        foreach (var household in world.Households.OrderBy(h => h.Id.Value))
        {
            if (household.IsEmpty || WalkToWater(world.Map, household.Location, rules.SearchRadiusCells) > rules.MaxWalkTicks)
                continue;
            long missing = household.Members.Count * (long)rules.DaysOfSupply - household.Stock.GetValueOrDefault(water);
            if (missing <= 0) continue;
            household.Deposit(water, missing);
            world.RecordResourceProduced(water, missing);
        }
    }

    /// <summary>Horas de caminhada da célula até a água mais próxima no raio; <see cref="long.MaxValue"/> se não houver.</summary>
    public static long WalkToWater(WorldMap map, CellCoord home, int radius)
    {
        var memo = WalkTicks.GetValue(map, _ => new Dictionary<CellCoord, long>());
        lock (memo)
            if (memo.TryGetValue(home, out long known)) return known;

        long best = long.MaxValue;
        for (int dy = -radius; dy <= radius; dy++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                var cell = new CellCoord(home.X + dx, home.Y + dy);
                if (!map.TryGetCell(cell, out var found) || !found.HasWater) continue;
                best = Math.Min(best, TravelTicksCache.Between(map, home, cell));
            }

        lock (memo)
            memo[home] = best;
        return best;
    }
}
