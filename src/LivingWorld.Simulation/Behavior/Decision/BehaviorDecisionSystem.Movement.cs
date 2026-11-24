using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Cities.Spatial;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cities;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Behavior.Decision;

// Parte de BehaviorDecisionSystem — passos reais e passeio ambiente.
public sealed partial class BehaviorDecisionSystem
{
    private static void MoveTracked(Npc npc, CellCoord destination, long tick, HashSet<CellCoord> occupancy)
    {
        occupancy.Remove(npc.CurrentLocation);
        npc.MoveTo(destination, tick);
        occupancy.Add(destination);
    }

    private static string AmbientPurpose(ActionType action) => action switch
    {
        ActionType.Idle => "ambient-Idle",
        ActionType.Socialize => "ambient-Socialize",
        ActionType.Work => "ambient-Work",
        _ => "ambient",
    };

    /// <summary>AD-192 (08c 146): passos do passeio entre o começo e o fim da ação pelo relógio — os múltiplos de 1/<paramref name="perHour"/>
    /// horas que caem no intervalo —, então à toa (2 h) e conversar (3 h) andam o mesmo por hora; determinístico, sem sorteio.</summary>
    internal static int AmbientSteps(long startedAt, long now, double perHour)
    {
        if (perHour <= 0 || now <= startedAt) return 0;
        return (int)Math.Min(8, Math.Floor(now * perHour) - Math.Floor(startedAt * perHour));
    }

    private static void MoveOneAmbientStep(
        WorldState world, Npc npc, TickContext ctx, long tick, ActionType action, HashSet<CellCoord> occupancy,
        Dictionary<CityId, long> cityPopulationCache, Dictionary<CityId, CityBounds> cityBoundsCache, int stepIndex = 0)
    {
        // ResolveGrownBounds realimenta os boxes de overflow das
        // próprias buildings da cidade, então o passeio ambiente já respeita os bounds crescidos
        // em vez do teto só-população de antes.
        CityBounds? homeBounds = null;
        if (world.FindCity(npc.City) is { } city)
        {
            if (!cityBoundsCache.TryGetValue(city.Id, out var cached))
            {
                cached = CityOccupancy.ResolveCoreBounds(
                    world, city, PopulationOf(world, city.Id, cityPopulationCache)); // AD-192: o passeio fica no miolo
                cityBoundsCache[city.Id] = cached;
            }
            homeBounds = cached;
        }

        // Vizinhos válidos em ordem (Y, X) crescente — o próprio laço dy→dx já gera essa ordem.
        Span<CellCoord> neighbors = stackalloc CellCoord[8];
        int count = 0;
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                var cell = new CellCoord(npc.CurrentLocation.X + dx, npc.CurrentLocation.Y + dy);
                if (!world.Map.TryGetCell(cell, out _)) continue;
                if (world.IsExtraordinaryConstructCell(cell)) continue;
                if (homeBounds is { } bounds && !bounds.Contains(cell)) continue;
                neighbors[count++] = cell;
            }

        // Passeio ambiente é a única fonte de posições "livres" (sem destino declarado como
        // household/workplace, que legitimamente hospedam vários NPCs na mesma célula) — por
        // isso preferimos vizinhos desocupados aqui (LIVE-POLISH: NPCs se empilhando
        // visualmente). Em população densa isso pode esvaziar TODOS os 8 vizinhos de uma vez —
        // o NPC precisa continuar andando em vez de travar, então cai pra "qualquer vizinho
        // válido" quando não sobra nenhum livre.
        Span<CellCoord> free = stackalloc CellCoord[8];
        int freeCount = 0;
        for (int i = 0; i < count; i++)
            if (!occupancy.Contains(neighbors[i]))
                free[freeCount++] = neighbors[i];

        var candidates = freeCount > 0 ? free[..freeCount] : neighbors[..count];
        if (candidates.Length == 0) return;

        // Sorteio derivado de (ação, NPC, tick), sem stream guardado: o stream por NPC ficava no snapshot para sempre,
        // inclusive o de quem já morreu, e a chave em texto era montada a cada passo (#157, AD-127).
        double draw = ctx.DrawFor(AmbientPurpose(action), unchecked(npc.Id.Value * 1_000_003L + tick + stepIndex * 7_919L));
        int index = Math.Min((int)(draw * candidates.Length), candidates.Length - 1);
        // AD-157b: com a despensa em casa, o passeio fica ao alcance dela (quem passeia não sai sem comida para longe)
        if (LivingWorld.Simulation.Invention.PersonalCarry.StrollStep(world, npc, action, candidates, index) is not { } step) return;
        MoveTracked(npc, step, tick, occupancy);
    }
}
