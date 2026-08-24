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
/// nenhum limiar novo. A cidade-mãe é excluída DESSE espaçamento mínimo (mesma convenção que
/// <see cref="OverflowClusterFinder.FindClusters"/> já usa pra excluir a própria cidade dona do
/// overflow): a nova cidade nasce como um desdobramento da mãe, ficar relativamente perto dela é
/// esperado (é a própria mãe que ancora a busca em anel); o bug relatado é a nova cidade colar numa
/// cidade JÁ EXISTENTE e não relacionada — daí só as OUTRAS cidades entrarem no espaçamento
/// mínimo.
///
/// ROOT-CAUSE post-ship fix (user-reported, 2026-08-23, "cidades coladas" round 2 — a causa raiz
/// real da saga do dia): excluir a mãe do espaçamento mínimo virou, na prática, excluí-la de QUALQUER
/// checagem — como a mãe é, quase por definição, a única cidade próxima no instante da fundação
/// (nenhuma outra cidade existe ainda perto), o anel 1 aceitava o primeiro candidato sem jamais
/// comparar contra os bounds da própria mãe. Confirmado ao vivo: mãe em Origin(4,4) 3x3, filha
/// nascendo em Origin(3,3) 3x3 — 4 células literalmente compartilhadas. "Ficar perto da mãe é
/// esperado" nunca significou "pode ocupar o mesmo território" — só o GAP mínimo (AbsorptionRingCells)
/// é dispensado pra mãe, não a garantia de não-overlap. <see cref="FoundingBoundsAt"/> calcula os
/// bounds que a filha teria ao nascer (população 0, mesma fórmula de <see cref="CityBoundsResolver.SideFor"/>
/// e mesmo clamp de borda de <see cref="CityBoundsResolver.Resolve"/>) e <see cref="Overlaps"/> os
/// compara aos bounds atuais da mãe — mesmo espírito de "busca em anel crescendo a partir da borda
/// até achar algo que não colide" que <see cref="OverflowPlacer.ResolveOverflowPositionGiven"/> já
/// usa pra posicionar prédios de overflow, só que aqui o "colide" é overlap de bounds, não célula
/// ocupada.</summary>
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
        var occupied = world.ActiveCities().Select(city => city.Location).ToHashSet();
        int absorptionRingCells = world.CityRules.AbsorptionRingCells;

        long motherPopulation = CityPopulationQuery.Population(world, motherCityId);
        var (motherBounds, _) = CityOccupancy.ResolveGrownBounds(world, mother, motherPopulation);
        int daughterSide = CityBoundsResolver.SideFor(0, world.Map.Width, world.Map.Height);

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

                    var daughterBounds = FoundingBoundsAt(candidate, daughterSide, world.Map.Width, world.Map.Height);
                    if (Overlaps(daughterBounds, motherBounds))
                        continue; // a mãe é isenta do GAP mínimo, nunca do não-overlap

                    return candidate;
                }
            }
        }

        return null; // mapa cheio -- nenhuma célula respeita o espaçamento mínimo de toda outra cidade
    }

    /// <summary>Os bounds que a cidade filha teria ao nascer em <paramref name="candidate"/> —
    /// mesma fórmula de <see cref="CityBoundsResolver.SideFor"/> (população 0) e mesmo clamp de
    /// borda de mapa que <see cref="CityBoundsResolver.Resolve"/> aplica via seu próprio
    /// <c>ClampOrigin</c> privado; duplicado aqui (2 linhas) porque aquele clamp não é exposto e
    /// uma cidade recém-fundada perto da borda do mapa também tem sua origem empurrada pra dentro.</summary>
    private static CityBounds FoundingBoundsAt(CellCoord candidate, int side, int mapWidth, int mapHeight)
    {
        int maxX = Math.Max(0, mapWidth - side);
        int maxY = Math.Max(0, mapHeight - side);
        var origin = new CellCoord(
            Math.Clamp(candidate.X - side / 2, 0, maxX),
            Math.Clamp(candidate.Y - side / 2, 0, maxY));
        return new CityBounds(origin, side, side);
    }

    /// <summary>Overlap de área (não Chebyshev) — <c>true</c> só quando os dois retângulos
    /// compartilham ao menos uma célula; encostar borda a borda sem compartilhar célula não conta
    /// (é exatamente o "perto mas nunca sobreposto" que este fix exige pra mãe).</summary>
    private static bool Overlaps(CityBounds a, CityBounds b) =>
        a.Origin.X < b.Origin.X + b.Width && b.Origin.X < a.Origin.X + a.Width &&
        a.Origin.Y < b.Origin.Y + b.Height && b.Origin.Y < a.Origin.Y + a.Height;

    private static bool IsValid(WorldState world, CellCoord coord) =>
        coord.X >= 0 && coord.Y >= 0 && coord.X < world.Map.Width && coord.Y < world.Map.Height;
}
