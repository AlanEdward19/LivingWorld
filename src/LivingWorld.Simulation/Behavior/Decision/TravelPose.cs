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
            index.StopTrackingTravellers();
            return;
        }
        long now = world.CurrentDate.TotalMilliseconds;
        var cache = world.PosePaths;
        if (!ReferenceEquals(cache.Index, index) || cache.Steps.Count > 4096)
        {
            cache.Steps.Clear(); // índice novo (mundo reaberto): as poses são postas de novo
            cache.Index = index;
        }
        MarketIndex? markets = null;
        foreach (var npc in index.Travellers()) // AD-197l: só quem viaja, sem varrer os vivos
        {
            if (npc.TravelLegTo is null)
            {
                // AD-197i: quem já viajava quando o mundo passou ao passo fino (saiu de hora em hora, sem perna) ganha a perna agora,
                // do estado canônico deste instante (destino e caminho em ms desde a saída); gravada, vale também depois de reabrir.
                // Sem destino fora daqui (ou andando por poder): perna parada no lugar, para não refazer a conta a cada tick
                markets ??= MarketIndex.BuildForTick(world);
                var leg = BehaviorDecisionSystem.TravelLegFresh(world, npc, markets) ?? (npc.CurrentLocation, npc.ActionStartedAtMs);
                npc.SetTravelLeg(leg.To, leg.EndMs);
            }
            var to = npc.TravelLegTo!.Value;
            long startMs = npc.ActionStartedAtMs, endMs = npc.TravelLegEndMs;
            long id = npc.Id.Value;
            // AD-197l: antes do próximo instante de troca a célula é a mesma: nada a refazer
            if (cache.Steps.TryGetValue(id, out var step) && now < step.NextMs && step.StartMs == startMs && step.EndMs == endMs
                && step.From == npc.CurrentLocation && step.To == to)
                continue;
            var (cell, next) = Step(world, npc, to, endMs, now);
            index.SetPose(npc, cell);
            cache.Steps[id] = new WorldState.PoseStep(startMs, npc.CurrentLocation, to, endMs, next);
        }
        index.PrunePoses();
    }

    /// <summary>A célula de agora (a mesma de <see cref="Along"/>) e o primeiro instante em que ela muda.</summary>
    private static (CellCoord Cell, long NextMs) Step(WorldState world, Npc npc, CellCoord to, long endMs, long atMs)
    {
        long startMs = npc.ActionStartedAtMs;
        if (to == npc.CurrentLocation || endMs <= startMs) return (npc.CurrentLocation, long.MaxValue);
        if (atMs <= startMs) return (npc.CurrentLocation, startMs + 1);
        if (atMs >= endMs) return (to, long.MaxValue);
        var path = PathOf(world, npc.CurrentLocation, to);
        if (path.Count < 2) return (path.Count == 0 ? npc.CurrentLocation : path[0], endMs);
        long span = endMs - startMs, segments = path.Count - 1;
        long index = Math.Clamp(segments * (atMs - startMs) / span, 0, segments);
        long next = index >= segments ? endMs : startMs + ((index + 1) * span + segments - 1) / segments;
        return (path[(int)index], Math.Min(next, endMs));
    }

    /// <summary>Distância de Chebyshev (em células) entre as poses de dois NPCs agora.</summary>
    public static int Reach(WorldState world, Npc a, Npc b)
    {
        CellCoord pa = world.AliveNpcIndex.PoseOf(a) ?? a.CurrentLocation;
        CellCoord pb = world.AliveNpcIndex.PoseOf(b) ?? b.CurrentLocation;
        return Math.Max(Math.Abs(pa.X - pb.X), Math.Abs(pa.Y - pb.Y));
    }
}
