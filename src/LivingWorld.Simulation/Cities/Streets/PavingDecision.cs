using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cities.Queries;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Cities.Streets;

/// <summary>Quem decide e paga o calçamento (AD-176), uma vez por mês e por cidade que sabe calçar (tecnologia de
/// <see cref="PavingTechnique.Category"/> adotada): o trecho de terra batida mais passado da cidade vira obra quando o tempo que
/// ele poupa a quem passa, no horizonte de <see cref="StreetRules.PaybackYears"/>, paga a mão de obra (material tirado do rio ou
/// do terreno pedregoso e trazido pela rota). Paga quem mora de frente (as casas cuja soleira dá no trecho), se puderem; senão
/// o tesouro da cidade (a <i>pavage</i>). Calçamento gasto abaixo de <see cref="StreetRules.RepairBelowPm"/> pede conserto pela
/// mesma regra; sem quem pague, ele se gasta até acabar.</summary>
internal static class PavingDecision
{
    internal static void Monthly(WorldState world, TickContext ctx)
    {
        var ground = world.Ground!;
        if (world.Technologies.Count == 0) return;
        foreach (var city in world.ActiveCities().OrderBy(c => c.Id.Value))
        {
            if (ground.Works.Any(w => w.City == city.Id.Value)) continue; // uma obra por cidade por vez
            var best = PavingTechnique.BestSurface(world, city.Id);
            if (best == GroundSurface.None) continue;
            TryRepair(world, ctx, city) ;
            if (ground.Works.Any(w => w.City == city.Id.Value)) continue;
            TryPave(world, ctx, city, best);
        }
    }

    /// <summary>Células da cidade: dentro do limite crescido dela.</summary>
    private static LivingWorld.Domain.Cities.Spatial.CityBounds BoundsOf(WorldState world, City city) =>
        CityOccupancy.ResolveGrownBounds(world, city, CityPopulationQuery.Population(world, city.Id)).Bounds;

    private static void TryRepair(WorldState world, TickContext ctx, City city)
    {
        var ground = world.Ground!;
        var rules = world.StreetRules;
        var bounds = BoundsOf(world, city);
        var cells = new List<int>();
        for (int i = 0; i < ground.Count && cells.Count < rules.MaxCellsPerWork; i++)
        {
            if (ground.PavingAt(i) == GroundSurface.None || ground.ConditionAt(i) >= rules.RepairBelowPm) continue;
            if (!bounds.Contains(ground.CellOf(i))) continue;
            cells.Add(i);
        }
        if (cells.Count == 0) return;
        var surface = ground.PavingAt(cells[0]);
        cells.RemoveAll(c => ground.PavingAt(c) != surface);
        long hours = 0;
        foreach (int c in cells)
        {
            long full = HoursFor(world, ground.CellOf(c), surface);
            if (full < 0) return;
            hours += Math.Max(1, full * (1000 - ground.ConditionAt(c)) / 1000);
        }
        Open(world, ctx, city, surface, cells, hours, repair: true);
    }

    private static void TryPave(WorldState world, TickContext ctx, City city, GroundSurface best)
    {
        var ground = world.Ground!;
        var rules = world.StreetRules;
        var bounds = BoundsOf(world, city);
        var candidates = new List<(int Index, int Passes)>();
        for (int i = 0; i < ground.Count; i++)
        {
            if (ground.SurfaceAt(i) != GroundSurface.Dirt || ground.PavingAt(i) != GroundSurface.None) continue;
            int passes = ground.PassesAt(i);
            if (passes <= 0 || !bounds.Contains(ground.CellOf(i))) continue;
            candidates.Add((i, passes));
        }
        if (candidates.Count == 0) return;
        candidates.Sort((a, b) => a.Passes != b.Passes ? b.Passes.CompareTo(a.Passes) : a.Index.CompareTo(b.Index));
        var chosen = candidates.Take(rules.MaxCellsPerWork).ToList();

        // pedra se a técnica e o terreno dão, senão cascalho; sem fonte de material ao alcance, não calça
        var surface = best;
        if (surface == GroundSurface.Stone && HoursFor(world, world.Ground!.CellOf(chosen[0].Index), surface) < 0) surface = GroundSurface.Gravel;
        long hours = 0;
        double hoursSavedPerYear = 0;
        var state = StreetRoutes.StateOf(world);
        double gain = StreetRoutes.FactorOf(rules, GroundSurface.Dirt) - StreetRoutes.FactorOf(rules, surface);
        foreach (var (index, passes) in chosen)
        {
            long h = HoursFor(world, ground.CellOf(index), surface);
            if (h < 0) return;
            hours += h;
            hoursSavedPerYear += passes * 12.0 * state.Base * state.Terrain[index] * gain;
        }
        long dayWage = DayWage(world);
        long cost = CostOf(hours, dayWage, rules);
        double hourValue = (double)dayWage / Math.Max(1, rules.CrewHoursPerDay);
        if (hoursSavedPerYear * rules.PaybackYears * hourValue < cost) return; // o tempo poupado não paga a obra
        Open(world, ctx, city, surface, chosen.Select(c => c.Index).ToList(), hours, repair: false);
    }

    internal static long CostOf(long hours, long dayWage, StreetRules rules) =>
        (hours + rules.CrewHoursPerDay - 1) / Math.Max(1, rules.CrewHoursPerDay) * dayWage;

    /// <summary>Jornal de um dia de diarista: a mediana do salário mensal dos ofícios do cenário pelos dias do mês.</summary>
    internal static long DayWage(WorldState world)
    {
        var wages = world.EconomyRules.WageByProfession.Values.OrderBy(v => v).ToList();
        long median = wages.Count == 0 ? 0 : wages[wages.Count / 2];
        return Math.Max(1, median / Math.Max(1, world.Calendar.DaysPerMonth));
    }

    /// <summary>Horas de trabalho de uma célula: assentar mais buscar o material (ida e volta pela rota, <see cref="StreetRules.LoadsPerCell"/>
    /// viagens) na fonte mais perto — água (cascalho do leito) ou terreno pedregoso (pedra). -1 sem fonte ao alcance.</summary>
    internal static long HoursFor(WorldState world, CellCoord cell, GroundSurface surface)
    {
        var rules = world.StreetRules;
        var source = NearestSource(world, cell, surface, radius: 40);
        if (source is not { } s) return -1;
        double haul = StreetRoutes.CostOf(world, s, cell).Cost + StreetRoutes.CostOf(world, cell, s).Cost;
        long lay = surface == GroundSurface.Stone ? rules.StoneHoursPerCell : rules.GravelHoursPerCell;
        return lay + (long)Math.Ceiling(haul * rules.LoadsPerCell);
    }

    private static CellCoord? NearestSource(WorldState world, CellCoord cell, GroundSurface surface, int radius)
    {
        var map = world.Map;
        int stone = world.StreetRules.StoneTerrainId;
        for (int r = 0; r <= radius; r++)
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                    var at = new CellCoord(cell.X + dx, cell.Y + dy);
                    if (!map.TryGetCell(at, out var found)) continue;
                    if (surface == GroundSurface.Stone ? found.Terrain.Id == stone : found.HasWater) return at;
                }
        return null;
    }

    /// <summary>Abre a obra: as casas de frente pagam se todas podem a parte delas; senão o tesouro, se pode; senão não abre.</summary>
    private static void Open(WorldState world, TickContext ctx, City city, GroundSurface surface, List<int> cells, long hours, bool repair)
    {
        var rules = world.StreetRules;
        long cost = CostOf(hours, DayWage(world), rules);
        var frontage = FrontageHouseholds(world, cells);
        List<long> payers = [];
        if (frontage.Count > 0 && frontage.All(h => MeansOf(world, h) >= cost / frontage.Count + 1)) payers = frontage;
        else if (city.Treasury.Amount < cost) return;
        var ground = world.Ground!;
        var work = new PavingWork
        {
            Id = ground.NextWorkId++, City = city.Id.Value, Surface = surface, Repair = repair, Cells = cells, HoursTotal = hours,
            HoursLeft = hours, StartTick = ctx.CurrentTick, Payers = payers,
        };
        ground.Works.Add(work);
        ctx.LogEvent(WorldEventKind.StreetWorkStarted,
            $"{work.Id}|{city.Id.Value}|{surface}|{(repair ? 1 : 0)}|{cells.Count}|{hours}|{(payers.Count == 0 ? "tesouro" : "frente")}",
            sourceSystem: StreetGroundSystem.SystemName);
    }

    /// <summary>Casas cuja soleira dá numa das células (ou ao lado): quem mora de frente para o trecho.</summary>
    internal static List<long> FrontageHouseholds(WorldState world, List<int> cells)
    {
        var state = StreetRoutes.StateOf(world);
        var near = new HashSet<int>();
        foreach (int c in cells)
        {
            near.Add(c);
            int x = c % state.W, y = c / state.W;
            if (x > 0) near.Add(c - 1);
            if (x + 1 < state.W) near.Add(c + 1);
            if (y > 0) near.Add(c - state.W);
            if (y + 1 < state.H) near.Add(c + state.W);
        }
        var buildings = new HashSet<long>();
        for (int k = 1; k < state.Door.Length; k++)
            if (state.Door[k] >= 0 && near.Contains(state.Door[k])) buildings.Add(state.OwnerId[k]);
        return world.Households
            .Where(h => !h.IsEmpty && h.Dwelling is { } d && buildings.Contains(d.Value))
            .Select(h => h.Id.Value).OrderBy(id => id).ToList();
    }

    internal static long MeansOf(WorldState world, long household)
    {
        if (world.FindHousehold(new HouseholdId(household)) is not { } home) return 0;
        long sum = 0;
        foreach (var member in home.Members)
            if (world.FindNpc(member) is { IsAlive: true } npc) sum += npc.Wallet.Amount;
        return sum;
    }

    /// <summary>Adultos da cidade sem emprego (diaristas da obra), por id.</summary>
    internal static List<Npc> DayLaborers(WorldState world, CityId city, int max)
    {
        var list = new List<Npc>();
        foreach (var npc in world.AliveNpcIndex.Alive.OrderBy(n => n.Id.Value))
        {
            if (list.Count >= max) break;
            if (npc.City != city || npc.Employer is not null) continue;
            if (world.LifeStageRules.LifeStageOf(npc.AgeYears(world.CurrentDate)) != LifeStage.Adult) continue;
            list.Add(npc);
        }
        return list;
    }
}
