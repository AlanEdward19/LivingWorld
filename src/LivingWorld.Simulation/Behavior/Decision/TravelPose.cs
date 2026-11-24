using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Market;

namespace LivingWorld.Simulation.Behavior.Decision;

/// <summary>AD-197c (ADR-0050, decisão 2): onde está quem viaja num instante qualquer. A viagem guarda saída (com a fase da hora)
/// e chegada; a pose entre as duas é calculada pelo tempo ao longo da rota real (a mesma da projeção), não guardada: o viajante
/// passa pelas células do caminho no ritmo do relógio, e quem está perto dele no meio do caminho o alcança (golpe do jogador).
/// <see cref="Npc.CurrentLocation"/> segue sendo a origem até a chegada, como a decisão sempre leu.</summary>
public static class TravelPose
{
    /// <summary>Célula do NPC no instante <paramref name="atMs"/>: a do lugar onde está, fora de viagem; a da rota, em viagem.</summary>
    public static CellCoord CellAt(WorldState world, Npc npc, long atMs)
    {
        if (npc.CurrentAction != ActionType.Travel) return npc.CurrentLocation;
        var markets = MarketIndex.BuildForTick(world);
        if (BehaviorDecisionSystem.TravelDestinationFor(world, npc, markets) is not { } to || to == npc.CurrentLocation)
            return npc.CurrentLocation;
        if (BehaviorDecisionSystem.TravelArrivalTick(world, npc, markets) is not { } arrival) return npc.CurrentLocation;

        long startMs = npc.ActionStartedAtMs;
        long endMs = arrival * WorldMillis.PerHour + npc.ActionPhaseMs;
        if (atMs <= startMs || endMs <= startMs) return npc.CurrentLocation;
        if (atMs >= endMs) return to;
        var path = LivingWorld.Simulation.Cities.Streets.StreetRoutes.PathOf(world, npc.CurrentLocation, to);
        if (path.Count == 0) return npc.CurrentLocation;
        long index = (path.Count - 1) * (atMs - startMs) / (endMs - startMs);
        return path[(int)Math.Clamp(index, 0, path.Count - 1)];
    }

    /// <summary>Distância de Chebyshev (em células) entre as poses de dois NPCs agora.</summary>
    public static int Reach(WorldState world, Npc a, Npc b)
    {
        long now = world.CurrentDate.TotalMilliseconds;
        CellCoord pa = world.FineTime ? CellAt(world, a, now) : a.CurrentLocation;
        CellCoord pb = world.FineTime ? CellAt(world, b, now) : b.CurrentLocation;
        return Math.Max(Math.Abs(pa.X - pb.X), Math.Abs(pa.Y - pb.Y));
    }
}
