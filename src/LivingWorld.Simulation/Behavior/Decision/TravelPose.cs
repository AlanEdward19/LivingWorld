using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Market;

namespace LivingWorld.Simulation.Behavior.Decision;

/// <summary>AD-197c (ADR-0050, decisão 2): onde está quem viaja num instante qualquer. A viagem guarda saída (com a fase da hora)
/// e chegada; a pose entre as duas é calculada pelo tempo ao longo da rota real (a mesma da projeção), não guardada: o viajante
/// passa pelas células do caminho no ritmo do relógio. <see cref="Npc.CurrentLocation"/> segue sendo a origem até a chegada, como a
/// decisão sempre leu. AD-197h: no passo fino o relógio põe a pose de cada viajante no índice dos vivos (<see cref="Refresh"/>), e
/// percepção, testemunha e encontro (<see cref="AliveNpcIndex.At"/>) o veem na célula do caminho.</summary>
public static class TravelPose
{
    /// <summary>Célula do NPC no instante <paramref name="atMs"/>: a do lugar onde está, fora de viagem; a da rota, em viagem.</summary>
    public static CellCoord CellAt(WorldState world, Npc npc, long atMs) => CellAt(world, npc, atMs, MarketIndex.BuildForTick(world));

    private static CellCoord CellAt(WorldState world, Npc npc, long atMs, MarketIndex markets)
    {
        if (npc.CurrentAction != ActionType.Travel) return npc.CurrentLocation;
        if (npc.TravelLegTo is { } planned) return Along(world, npc, planned, npc.TravelLegEndMs, atMs); // AD-197h: a perna gravada
        if (BehaviorDecisionSystem.TravelDestinationFor(world, npc, markets) is not { } to || to == npc.CurrentLocation)
            return npc.CurrentLocation;
        long? end = world.FineTime
            ? BehaviorDecisionSystem.TravelArrivalMs(world, npc, markets) // AD-197h: o caminho em ms
            : BehaviorDecisionSystem.TravelArrivalTick(world, npc, markets) is { } arrival ? arrival * WorldMillis.PerHour + npc.ActionPhaseMs : null;
        if (end is not { } endMs) return npc.CurrentLocation;

        return Along(world, npc, to, endMs, atMs);
    }

    /// <summary>A célula da rota de <see cref="Npc.CurrentLocation"/> a <paramref name="to"/> na fração do tempo entre a saída e
    /// <paramref name="endMs"/>.</summary>
    private static CellCoord Along(WorldState world, Npc npc, CellCoord to, long endMs, long atMs)
    {
        long startMs = npc.ActionStartedAtMs;
        if (to == npc.CurrentLocation || atMs <= startMs || endMs <= startMs) return npc.CurrentLocation;
        if (atMs >= endMs) return to;
        var path = PathOf(world, npc.CurrentLocation, to);
        if (path.Count == 0) return npc.CurrentLocation;
        long index = (path.Count - 1) * (atMs - startMs) / (endMs - startMs);
        return path[(int)Math.Clamp(index, 0, path.Count - 1)];
    }

    /// <summary>A rota real entre dois lugares, guardada por par (depende só do mapa e da rede de ruas, que o carimbo de rotas
    /// acompanha): o viajante não refaz o caminho a cada tick fino.</summary>
    private static IReadOnlyList<CellCoord> PathOf(WorldState world, CellCoord from, CellCoord to)
    {
        var stamp = LivingWorld.Simulation.Cities.Streets.StreetRoutes.Stamp(world);
        var cache = world.PosePaths;
        if (cache.Stamp != stamp || cache.Paths.Count > 4096)
        {
            cache.Paths.Clear();
            cache.Stamp = stamp;
        }
        if (!cache.Paths.TryGetValue((from, to), out var path))
            cache.Paths[(from, to)] = path = LivingWorld.Simulation.Cities.Streets.StreetRoutes.PathOf(world, from, to);
        return path;
    }

    /// <summary>AD-197h: põe no índice dos vivos a pose de agora de cada viajante (no passo fino) ou tira todas (de hora em hora). O
    /// relógio chama antes de qualquer sistema do tick. Sai da perna gravada no viajante quando ele decidiu (destino e chegada,
    /// canônicos), não de destino e caminho refeitos a cada tick: barato, e o mesmo depois de reabrir o mundo. Fora do hash.</summary>
    public static void Refresh(WorldState world)
    {
        var index = world.AliveNpcIndex;
        if (!world.FineTime)
        {
            if (index.HasPoses) index.ClearPoses();
            return;
        }
        long now = world.CurrentDate.TotalMilliseconds;
        MarketIndex? markets = null;
        foreach (var npc in index.Alive)
        {
            if (npc.CurrentAction != ActionType.Travel)
            {
                if (index.HasPoses) index.SetPose(npc, null);
                continue;
            }
            if (npc.TravelLegTo is null)
            {
                // AD-197i: quem já viajava quando o mundo passou ao passo fino (saiu de hora em hora, sem perna) ganha a perna agora,
                // do estado canônico deste instante (destino e caminho em ms desde a saída); gravada, vale também depois de reabrir
                markets ??= MarketIndex.BuildForTick(world);
                if (BehaviorDecisionSystem.TravelLegFresh(world, npc, markets) is not { } leg)
                {
                    if (index.HasPoses) index.SetPose(npc, null);
                    continue;
                }
                npc.SetTravelLeg(leg.To, leg.EndMs);
            }
            index.SetPose(npc, Along(world, npc, npc.TravelLegTo!.Value, npc.TravelLegEndMs, now));
        }
    }

    /// <summary>Distância de Chebyshev (em células) entre as poses de dois NPCs agora.</summary>
    public static int Reach(WorldState world, Npc a, Npc b)
    {
        CellCoord pa = world.AliveNpcIndex.PoseOf(a) ?? a.CurrentLocation;
        CellCoord pb = world.AliveNpcIndex.PoseOf(b) ?? b.CurrentLocation;
        return Math.Max(Math.Abs(pa.X - pb.X), Math.Abs(pa.Y - pb.Y));
    }
}
