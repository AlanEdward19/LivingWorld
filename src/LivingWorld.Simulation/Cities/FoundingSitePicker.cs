using LivingWorld.Domain;

namespace LivingWorld.Simulation;

/// <summary>Escolhe um sítio de fundação distinto e válido de forma determinística (Fase 15.1,
/// Stage 4, T11, LWV-04.2) — nunca reutiliza a célula de uma cidade existente.
///
/// Post-ship fix (user-reported, 2026-08-23, "cidades coladas"): a checagem original só excluía a
/// célula EXATA de outra cidade (<c>occupied.Contains(candidate)</c>) — nenhum espaçamento mínimo,
/// diferente do que <c>dynamic-city-growth</c> já garante pro sistema de overflow (<see
/// cref="OverflowClusterFinder"/>/<see cref="CityBoundsResolver"/>). Como households ainda não têm
/// prédios de fato posicionados (gap separado, real-household-workplace-buildings), este picker —
/// não o de overflow — é o caminho dominante de fundação hoje, e é ele que produzia cidades
/// literalmente encostadas. Reusa a MESMA constante/métrica (<see cref="CityRules.AbsorptionRingCells"/>,
/// distância de Chebyshev via <see cref="OverflowClusterFinder.IsWithinAbsorptionRangeOfAnyOtherCity"/>),
/// nenhum limiar novo. A cidade-mãe é excluída da checagem (mesma convenção que
/// <see cref="OverflowClusterFinder.FindClusters"/> já usa pra excluir a própria cidade dona do
/// overflow): a nova cidade nasce como um desdobramento da mãe, ficar relativamente perto dela é
/// esperado (é a própria mãe que ancora a busca em anel); o bug relatado é a nova cidade colar numa
/// cidade JÁ EXISTENTE e não relacionada — daí só as OUTRAS cidades entrarem no espaçamento
/// mínimo.</summary>
public static class FoundingSitePicker
{
    /// <summary>Falha honesta (mapa cheio, nenhuma célula respeita o espaçamento mínimo de toda
    /// OUTRA cidade existente) devolve <c>null</c> — o chamador decide não fundar em vez de forçar
    /// uma cidade colada (mesmo espírito de "sem placement possível" em
    /// <c>BuildingPlacementResolver</c>/<c>CityOccupancy.IsLandScarce</c>).</summary>
    public static CellCoord? Pick(WorldState world, CityId motherCityId)
    {
        var mother = world.FindCity(motherCityId)
            ?? throw new InvalidOperationException("cidade-mãe inexistente ao escolher sítio de fundação");
        var occupied = world.Cities.Select(city => city.Location).ToHashSet();
        int absorptionRingCells = world.CityRules.AbsorptionRingCells;

        for (int ring = 1; ring <= Math.Max(world.Map.Width, world.Map.Height); ring++)
        {
            for (int dx = -ring; dx <= ring; dx++)
            {
                for (int dy = -ring; dy <= ring; dy++)
                {
                    if (Math.Abs(dx) != ring && Math.Abs(dy) != ring) continue;
                    var candidate = new CellCoord(mother.Location.X + dx, mother.Location.Y + dy);
                    if (!IsValid(world, candidate) || occupied.Contains(candidate)) continue;

                    var candidateBox = new CityBounds(candidate, 1, 1);
                    if (OverflowClusterFinder.IsWithinAbsorptionRangeOfAnyOtherCity(
                            world, motherCityId, candidateBox, absorptionRingCells))
                        continue; // dentro do espaçamento mínimo de outra cidade existente

                    return candidate;
                }
            }
        }

        return null; // mapa cheio -- nenhuma célula respeita o espaçamento mínimo de toda outra cidade
    }

    private static bool IsValid(WorldState world, CellCoord coord) =>
        coord.X >= 0 && coord.Y >= 0 && coord.X < world.Map.Width && coord.Y < world.Map.Height;
}
