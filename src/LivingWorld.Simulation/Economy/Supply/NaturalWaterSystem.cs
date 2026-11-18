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
            // AD-180: na seca o riacho e a fonte minguam; a casa leva só o que corre (o resto, no poço pago ou no mercado)
            double flow = LivingWorld.Simulation.Climate.WeatherNow.SpringYield(world, household.Location);
            if (flow < 1) missing = Math.Min(missing, (long)Math.Floor(household.Members.Count * flow) + 1);
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


    /// <summary>AD-176: horas pela rota real (rede de caminhos, prédios no caminho) até a água mais próxima no raio; sem a rua de
    /// verdade, a reta de antes. Memo por casa, refeito quando a rede ou os obstáculos mudam.</summary>
    public static long WalkToWater(WorldState world, CellCoord home, int radius) =>
        !world.StreetsOn ? WalkToWater(world.Map, home, radius) : NearestWater(world, home, radius)?.Ticks ?? long.MaxValue;

    /// <summary>AD-185: a célula d'água mais perto em linha reta no raio (empate: a de menor linha, depois coluna) e as horas a pé até
    /// ela pela rota real; nulo se não há. Memo por (célula, raio), refeito quando as rotas mudam: função pura do mundo.</summary>
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
        // AD-185: a água que se conhece é a mais perto em linha reta (o rio que se vê); a rota até ela dá as horas. Uma rota por casa
        // em vez de uma por célula d'água do raio (a busca pela rota mais curta entre todas custava metade do roteamento do mundo).
        (CellCoord Cell, long Ticks) best = (home, long.MaxValue);
        if (candidates.Count > 0) best = (candidates[0].Cell, LivingWorld.Simulation.Cities.Streets.StreetRoutes.Ticks(world, home, candidates[0].Cell));
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
