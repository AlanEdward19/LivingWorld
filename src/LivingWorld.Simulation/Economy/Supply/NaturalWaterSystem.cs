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
        // AD-185: com a ida de verdade, a despensa só enche com o que alguém trouxe (WaterFetch); aqui só se limpa o razão das idas
        if (WaterFetch.On(world)) { WaterFetch.Prune(world); return; }

        var water = new ResourceType(world.EconomyRules.WaterResourceId);
        foreach (var household in world.Households.OrderBy(h => h.Id.Value))
        {
            if (household.IsEmpty || WalkToWater(world, household.Location, rules.SearchRadiusCells) > rules.MaxWalkTicks)
                continue;
            long missing = household.Members.Count * (long)rules.DaysOfSupply - household.Stock.GetValueOrDefault(water);
            if (missing <= 0) continue;
            household.Deposit(water, missing);
            world.RecordResourceProduced(water, missing);
        }
    }

    private sealed class RoutedWalk
    {
        public (long, int, bool) Stamp;
        public readonly Dictionary<(CellCoord, int), (CellCoord Cell, long Ticks)> ByHome = [];
    }

    private static readonly ConditionalWeakTable<WorldState, RoutedWalk> RoutedWalks = new();

    /// <summary>Células entre a célula da casa e a soleira por onde a rota sai (folga do limite inferior da busca).</summary>
    private const int DoorSlackCells = 4;

    /// <summary>AD-176: horas pela rota real (rede de caminhos, prédios no caminho) até a água mais próxima no raio; sem a rua de
    /// verdade, a reta de antes. Memo por casa, refeito quando a rede ou os obstáculos mudam.</summary>
    public static long WalkToWater(WorldState world, CellCoord home, int radius) =>
        !world.StreetsOn ? WalkToWater(world.Map, home, radius) : NearestWater(world, home, radius)?.Ticks ?? long.MaxValue;

    /// <summary>AD-185: a célula d'água mais perto pela rota real no raio e as horas a pé até ela (empate: a de menor linha, depois
    /// coluna); nulo se não há. As células são vistas da mais perto para a mais longe em linha reta, e a busca para quando nem a
    /// reta no passo mais rápido do mapa bate a melhor rota achada (o limite inferior). Memo por (célula, raio), refeito quando a
    /// rede ou os obstáculos mudam: função pura do mundo, o reaberto acha o mesmo.</summary>
    public static (CellCoord Cell, long Ticks)? NearestWater(WorldState world, CellCoord home, int radius)
    {
        var map = world.Map;
        if (!world.StreetsOn)
        {
            (CellCoord, long)? straight = null;
            for (int dy = -radius; dy <= radius; dy++)
                for (int dx = -radius; dx <= radius; dx++)
                {
                    var cell = new CellCoord(home.X + dx, home.Y + dy);
                    if (!map.TryGetCell(cell, out var found) || !found.HasWater) continue;
                    long t = TravelTicksCache.Between(map, home, cell);
                    if (straight is not { } s || t < s.Item2) straight = (cell, t);
                }
            return straight;
        }
        var memo = RoutedWalks.GetOrCreateValue(world);
        var stamp = LivingWorld.Simulation.Cities.Streets.StreetRoutes.Stamp(world);
        if (memo.Stamp != stamp) { memo.ByHome.Clear(); memo.Stamp = stamp; }
        if (memo.ByHome.TryGetValue((home, radius), out var known)) return known.Ticks == long.MaxValue ? null : known;
        var candidates = new List<(int D2, CellCoord Cell)>();
        for (int dy = -radius; dy <= radius; dy++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                var cell = new CellCoord(home.X + dx, home.Y + dy);
                if (map.TryGetCell(cell, out var found) && found.HasWater) candidates.Add((dx * dx + dy * dy, cell));
            }
        candidates.Sort((a, b) => a.D2 != b.D2 ? a.D2.CompareTo(b.D2) : (a.Cell.Y, a.Cell.X).CompareTo((b.Cell.Y, b.Cell.X)));
        double floor = LivingWorld.Simulation.Cities.Streets.StreetRoutes.CheapestCostPerCell(world);
        (CellCoord Cell, long Ticks) best = (home, long.MaxValue);
        foreach (var (d2, cell) in candidates)
        {
            // a rota sai da soleira, que fica a poucas células da célula da casa: a folga mantém o limite abaixo de toda rota
            if (best.Ticks != long.MaxValue && Math.Ceiling(floor * Math.Max(0, Math.Sqrt(d2) - DoorSlackCells)) > best.Ticks) break;
            long t = LivingWorld.Simulation.Cities.Streets.StreetRoutes.Ticks(world, home, cell);
            if (t < best.Ticks) best = (cell, t);
        }
        memo.ByHome[(home, radius)] = best;
        return best.Ticks == long.MaxValue ? null : best;
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
