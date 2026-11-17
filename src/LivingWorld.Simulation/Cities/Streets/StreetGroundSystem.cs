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
