using LivingWorld.Domain.Cities;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Cities.Streets;

/// <summary>O chão das cidades, <c>Monthly</c> (AD-176): as viagens do mês viram pisoteio nas células da rota que cada uma fez; o
/// pisoteio velho se desfaz (o mato volta); o chão sob prédio novo some e os prédios do mês passam a ser obstáculo da rota; o
/// calçamento se desgasta pelo tempo e pelas passagens (sem conserto, volta a terra); e, a cada revisão, a superfície em vigor
/// muda pelo pisoteio (trilha, terra batida) ou pelo calçamento — a rede de caminhos que as rotas seguintes usam. Depois, quem
/// tem a técnica decide calçar ou consertar (<see cref="PavingDecision"/>).</summary>
[LivingCapability("STREETS", Events = [WorldEventKind.StreetWorkStarted], Consumers = ["behavior.travel", "economy.market", "invention.haul"])]
public sealed class StreetGroundSystem : ISimulationSystem
{
    public const string SystemName = "street-ground";

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Monthly;

    public void Tick(WorldState world, TickContext ctx)
    {
        if (!world.StreetsOn) return;
        var ground = world.Ground!;
        var rules = world.StreetRules;

        Deposit(world, ground);
        for (int i = 0; i < ground.Count; i++)
        {
            int wear = ground.WearAt(i);
            if (wear > 0) ground.SetWear(i, (int)((long)wear * rules.WearRetainPm / 1000));
        }

        // os prédios concluídos até aqui viram obstáculo da rota; o chão debaixo deles some (construíram em cima do caminho)
        ground.ObstacleTick = ctx.CurrentTick;
        var state = StreetRoutes.StateOf(world);
        for (int i = 0; i < ground.Count; i++)
        {
            if (state.Owner[i] == 0) continue;
            if (ground.WearAt(i) != 0) ground.SetWear(i, 0);
            if (ground.PavingAt(i) != GroundSurface.None) ground.SetPaving(i, GroundSurface.None, 0);
        }

        Weather(ground, rules);
        ground.Months++;
        if (ground.Months % Math.Max(1, rules.ReviewMonths) == 0) Review(ground, rules);

        PavingDecision.Monthly(world, ctx);
        world.CanonicalHashCache.MarkPropertyDirty(nameof(WorldState.Ground));
    }

    /// <summary>AD-179 (08d item 180): o mundo começa no meio da história. A vila semeada existe há anos, então nasce com o chão
    /// que o uso de sempre deixou: o mês típico (cada empregado vai e volta do trabalho em todo dia do mês; cada casa vai
    /// <see cref="StreetRules.HistoryMarketTripsPerMonth"/> vezes ao mercado mais perto e volta) deposita as passagens pelas rotas
    /// de hoje, e o pisoteio vai ao valor estacionário do próprio processo mensal (passagens ÷ (1 − o que sobra por mês)); a
    /// revisão vira trilha e terra batida. Repetido <see cref="StreetRules.HistoryRounds"/> vezes (as rotas seguem a rede que acabou
    /// de nascer). Só lê o mundo e escreve o chão; em ordem de id, sem sorteio. 0 rodadas: nasce sem caminho (o modelo da AD-176).</summary>
    public static void SeedHistory(WorldState world)
    {
        if (!world.StreetsOn) return;
        var ground = world.Ground!;
        var rules = world.StreetRules;
        if (rules.HistoryRounds <= 0 || rules.WearRetainPm >= 1000) return;
        var marketTypes = world.EconomyCatalog.MarketLocationTypeIds;
        var markets = world.Workplaces.Where(w => !w.Closed && marketTypes.Contains(w.LocationType.Id)).OrderBy(w => w.Id.Value).ToList();
        int days = world.Calendar.DaysPerMonth;
        for (int round = 0; round < rules.HistoryRounds; round++)
        {
            foreach (var home in world.Households.OrderBy(h => h.Id.Value))
            {
                if (home.IsEmpty || home.IsHomeless) continue;
                foreach (var member in home.Members.OrderBy(m => m.Value))
                {
                    if (world.FindNpc(member) is not { IsAlive: true } npc || HistoricWorkplace(world, npc, home.Location) is not { } work) continue;
                    ground.RecordTrips(home.Location, work, days);
                    ground.RecordTrips(work, home.Location, days);
                }
                if (markets.Count == 0 || rules.HistoryMarketTripsPerMonth <= 0) continue;
                var market = markets.OrderBy(m => StreetRoutes.CostOf(world, home.Location, m.Location).Cost).ThenBy(m => m.Id.Value).First();
                ground.RecordTrips(home.Location, market.Location, rules.HistoryMarketTripsPerMonth);
                ground.RecordTrips(market.Location, home.Location, rules.HistoryMarketTripsPerMonth);
            }
            Deposit(world, ground);
            for (int i = 0; i < ground.Count; i++)
                ground.SetWear(i, (int)Math.Min(int.MaxValue, (long)ground.PassesAt(i) * 1000 / (1000 - rules.WearRetainPm)));
            Review(ground, rules);
        }
        ground.ClearPasses(); // o mês de verdade começa vazio: as passagens do primeiro mês são as do mundo
        world.CanonicalHashCache.MarkPropertyDirty(nameof(WorldState.Ground));
    }

    /// <summary>Onde o adulto trabalhou na história: o emprego, se já tem; senão a oficina aberta do ofício dele mais perto de casa
    /// pela rota (no mundo recém-criado ninguém foi contratado ainda: o primeiro dia contrata).</summary>
    private static LivingWorld.Domain.Geography.CellCoord? HistoricWorkplace(WorldState world, LivingWorld.Domain.Population.Npc npc,
        LivingWorld.Domain.Geography.CellCoord home)
    {
        if (npc.Employer is { } employer) return world.FindWorkplace(employer)?.Location;
        if (world.LifeStageRules.LifeStageOf(npc.AgeYears(world.CurrentDate)) != LivingWorld.Domain.Population.Body.LifeStage.Adult) return null;
        if (!world.EconomyCatalog.LocationTypeByProfession.TryGetValue(npc.Profession.Id, out int type)) return null;
        LivingWorld.Domain.Geography.CellCoord? best = null;
        double bestCost = double.MaxValue;
        foreach (var place in world.Workplaces)
        {
            if (place.Closed || place.LocationType.Id != type || place.City != npc.City) continue;
            double cost = StreetRoutes.CostOf(world, home, place.Location).Cost;
            if (cost < bestCost || cost == bestCost && best is { } b && (place.Location.Y, place.Location.X).CompareTo((b.Y, b.X)) < 0)
            {
                bestCost = cost;
                best = place.Location;
            }
        }
        return best;
    }

    /// <summary>Cada viagem do mês deixa uma passagem em cada célula da rota que fez (com a rede e os obstáculos do mês).</summary>
    internal static void Deposit(WorldState world, GroundLedger ground)
    {
        ground.ClearPasses();
        var trips = ground.DrainTrips();
        if (trips.Count == 0) return;
        var state = StreetRoutes.StateOf(world);
        var cells = new List<int>();
        foreach (var (from, to, count) in trips)
        {
            var route = StreetRoutes.Get(world, ground.CellOf(from), ground.CellOf(to));
            cells.Clear();
            StreetRoutes.CellsOf(state, route, cells);
            foreach (int c in cells) ground.AddPasses(c, count);
        }
    }

    /// <summary>Desgaste do calçamento: o tempo (cascalho mais que pedra) e as passagens do mês; a zero, o calçamento acabou.</summary>
    private static void Weather(GroundLedger ground, StreetRules rules)
    {
        for (int i = 0; i < ground.Count; i++)
        {
            var paving = ground.PavingAt(i);
            if (paving == GroundSurface.None) continue;
            int loss = (paving == GroundSurface.Stone ? rules.StoneDecayPm : rules.GravelDecayPm)
                + ground.PassesAt(i) / Math.Max(1, rules.PassesPerDecayPoint);
            int condition = ground.ConditionAt(i) - loss;
            ground.SetPaving(i, condition <= 0 ? GroundSurface.None : paving, Math.Max(0, condition));
        }
    }

    /// <summary>Superfície em vigor pela revisão: calçamento com condição; senão terra batida ou trilha pelo pisoteio, com folga
    /// (só some abaixo da metade do limiar que a criou). Mudou alguma, a rede de caminhos muda de versão.</summary>
    internal static void Review(GroundLedger ground, StreetRules rules)
    {
        bool changed = false;
        for (int i = 0; i < ground.Count; i++)
        {
            var current = ground.SurfaceAt(i);
            var next = SurfaceFor(current, ground.WearAt(i), ground.PavingAt(i), ground.ConditionAt(i), rules);
            if (next == current) continue;
            ground.SetSurface(i, next);
            changed = true;
        }
        if (changed) ground.SurfaceVersion++;
    }

    internal static GroundSurface SurfaceFor(GroundSurface current, int wear, GroundSurface paving, int condition, StreetRules rules)
    {
        if (paving != GroundSurface.None && condition > 0) return paving;
        bool wasDirt = current >= GroundSurface.Dirt, wasTrail = current >= GroundSurface.Trail;
        if (wear >= rules.DirtWear || (wasDirt && wear >= rules.DirtWear / 2)) return GroundSurface.Dirt;
        if (wear >= rules.TrailWear || (wasTrail && wear >= rules.TrailWear / 2)) return GroundSurface.Trail;
        return GroundSurface.None;
    }
}
