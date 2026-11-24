using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Geography.Map;
using LivingWorld.Domain.Invention;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Invention;

/// <summary>Pesca como uso da peça (AD-143), mensal: a casa que tem uma peça que funciona e água perto de casa sai à água no tempo
/// livre de quem tem mais tempo. Nada diz "para pescar": o rendimento sai do que o avaliador mediu da peça — a velocidade leva a mais
/// água (até o dobro do passo de quem anda), a estabilidade deixa lançar de pé (até a margem mínima × 3), a carga útil limita o que
/// cabe por saída — e do peixe que a água perto da cidade tem: estoque por cidade que cresce sozinho (logístico) até o que as células
/// d'água sustentam e cai com o que se tira. O peixe vira comida na despensa (o que a casa come no mês, não mais).</summary>
public static class Fishing
{
    private static readonly ConditionalWeakTable<WorldMap, ConcurrentDictionary<(CellCoord, int), int>> WaterNear = new();

    /// <summary>Células d'água num raio (volátil: função pura do mapa).</summary>
    internal static int WaterCellsNear(WorldMap map, CellCoord at, int radius)
    {
        var memo = WaterNear.GetOrCreateValue(map);
        if (memo.TryGetValue((at, radius), out int count)) return count;
        count = 0;
        for (int dy = -radius; dy <= radius; dy++)
            for (int dx = -radius; dx <= radius; dx++)
                if (map.TryGetCell(new CellCoord(at.X + dx, at.Y + dy), out var cell) && cell.HasWater) count++;
        if (memo.Count > 100_000) memo.Clear();
        memo[(at, radius)] = count;
        return count;
    }

    internal static void Month(WorldState world, TickContext ctx)
    {
        var ledger = world.Invention!;
        var rules = world.InventionRules!;
        if (!world.EconomyRules.Enabled) return;
        Regrow(world);
        if (ledger.Items.Count == 0) return;

        var homes = new SortedSet<long>();
        foreach (var item in ledger.Items) if (!item.ForSale) homes.Add(item.HouseholdId);
        var food = new ResourceType(world.EconomyRules.FoodResourceId);
        int unitG = rules.UnitGramsOf(food.Id);
        int days = world.Calendar.DaysPerMonth;
        foreach (long id in homes)
        {
            if (world.FindHousehold(new HouseholdId(id)) is not { } home) continue;
            var craft = ledger.BestCraft(id, rules);
            if (craft.SpeedMmS <= 0) continue;
            if (WaterCellsNear(world.Map, home.Location, rules.WaterObservationRadius) == 0) continue;
            long want = (long)home.Members.Count * days - home.Stock.GetValueOrDefault(food);
            if (want <= 0) continue; // despensa cheia para o mês: ninguém sai à água por isso
            if (Fisher(world, home, rules) is not { } fisher) continue;

            int stockIndex = StockIndex(world, home.City);
            if (stockIndex < 0) continue;
            var stock = ledger.Fish[stockIndex];
            long capacity = Capacity(world, home.City);
            if (capacity <= 0 || stock.Grams <= 0) continue;

            int free = fisher.Employer is null ? rules.FreeHoursIdle : rules.FreeHoursEmployed;
            double reach = Math.Clamp((double)craft.SpeedMmS / rules.WalkPaceMmS, 0.5, 2.0);
            double steady = Math.Clamp((double)craft.MetacentricMm / (3.0 * rules.MinMetacentricHeightMm), 0.2, 1.0);
            double perHour = rules.FishGramsPerHour * reach * steady * stock.Grams / capacity;
            double luck = 0.75 + 0.5 * ctx.OneShot($"fish-{id}-{ctx.CurrentTick}").NextDouble();
            long perTrip = (long)Math.Min(perHour * free * luck, Math.Max(0, craft.PayloadG));
            long caught = Math.Min(stock.Grams, perTrip * rules.FishingDaysPerMonth);
            long units = Math.Min(want, caught / unitG);
            if (units <= 0) continue;
            caught = units * unitG;
            ledger.Fish[stockIndex] = stock with { Grams = stock.Grams - caught };
            home.Deposit(food, units);
            world.RecordResourceProduced(food, units);
            world.InventionTally.FishingMonths++;
            world.InventionTally.FishGrams += caught;
            world.InventionTally.FishFoodUnits += units;
        }
    }

    /// <summary>Quem da casa sai à água: o adulto com mais tempo livre (sem emprego antes), o de menor id no empate.</summary>
    private static Npc? Fisher(WorldState world, Household home, InventionRules rules)
    {
        Npc? best = null;
        foreach (var member in home.Members.OrderBy(m => m.Value))
        {
            if (world.FindNpc(member) is not { IsAlive: true } npc || npc.AgeYears(world.CurrentDate) < rules.MinInventorAgeYears) continue;
            if (best is null || (best.Employer is not null && npc.Employer is null)) best = npc;
        }
        return best;
    }

    /// <summary>O que a água perto da cidade sustenta (g): células d'água no raio de observação em torno do centro da cidade.</summary>
    private static long Capacity(WorldState world, CityId city)
    {
        if (world.FindCity(city) is not { } c) return 0;
        var rules = world.InventionRules!;
        return WaterCellsNear(world.Map, c.Location, rules.WaterObservationRadius * 2) * (long)rules.FishKgPerWaterCell * 1000;
    }

    /// <summary>Índice do estoque da cidade, criado cheio na primeira vez que alguém de lá pesca.</summary>
    private static int StockIndex(WorldState world, CityId city)
    {
        var fish = world.Invention!.Fish;
        for (int i = 0; i < fish.Count; i++) if (fish[i].City == city) return i;
        long capacity = Capacity(world, city);
        if (capacity <= 0) return -1;
        int at = 0;
        while (at < fish.Count && fish[at].City.Value.CompareTo(city.Value) < 0) at++;
        fish.Insert(at, new FishStock(city, capacity));
        return at;
    }

    /// <summary>Crescimento logístico mensal: S += r·S·(1 − S/K).</summary>
    private static void Regrow(WorldState world)
    {
        var ledger = world.Invention!;
        var rules = world.InventionRules!;
        for (int i = 0; i < ledger.Fish.Count; i++)
        {
            var s = ledger.Fish[i];
            long k = Capacity(world, s.City);
            if (k <= 0 || s.Grams >= k) continue;
            long grown = (long)((double)rules.FishRegrowthPm / 1000 * s.Grams * (1 - (double)s.Grams / k));
            ledger.Fish[i] = s with { Grams = Math.Min(k, s.Grams + Math.Max(1, grown)) };
        }
    }
}
