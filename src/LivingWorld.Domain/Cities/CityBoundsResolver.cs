namespace LivingWorld.Domain;

/// <summary>Extensão de uma cidade no grid do mundo (Fase 15.1, T45).</summary>
public readonly record struct CityBounds(CellCoord Origin, int Width, int Height)
{
    public bool Contains(CellCoord cell) =>
        cell.X >= Origin.X && cell.X < Origin.X + Width &&
        cell.Y >= Origin.Y && cell.Y < Origin.Y + Height;
}

/// <summary>Resolve os bounds de uma cidade (Fase 15.1, T45; G4/backend-gaps.md). Nenhum cenário
/// autora tamanho de cidade hoje — <see cref="IsDerived"/> é sempre <c>true</c> por ora.
///
/// Bugfix real (usuário, 2026-08-13, rodada 1): a fórmula original era um tamanho FIXO de
/// 34×24 células — herdada do placeholder client-side (`cityGroundBounds`) de quando cidade
/// tinha seu próprio grid local pequeno. Coordenadas de cidade viraram absolutas (mesma escala
/// do mapa-múndi, T46), então um tamanho fixo de 34×24 estourava qualquer mundo menor que isso
/// (10×10/20×20 — exatamente os presets Pequeno/Médio do World Creator).
///
/// Bugfix real (usuário, 2026-08-13, rodada 2 — a rodada 1 ainda estourava): escalar só por
/// <see cref="population"/> não basta — um template "Cidade média" (mapa 20×20, população 150)
/// ainda produzia lado 25 num mapa de 20, confirmado ao vivo via `/visual/subscribe`. O lado
/// agora nunca excede metade da menor dimensão do mapa (<paramref name="mapWidth"/>/<paramref
/// name="mapHeight"/>). A raiz da população é dividida por dois e limitada a 12 células para a
/// cidade permanecer um marcador compacto no mapa-múndi, não dominar a paisagem inteira.</summary>
public static class CityBoundsResolver
{
    private const int MinSize = 3;
    private const int MaxSize = 12;

    /// <summary>Só o cálculo do lado (sem depender de uma <see cref="City"/> já existir) — usado
    /// também por <see cref="LivingWorld.Simulation.PopulationSeeder"/> pra decidir o raio de
    /// espalhamento das famílias na semeadura inicial; sem isso o raio era fixo (2 células) e
    /// descasava do footprint real assim que a população era pequena o bastante pra produzir um
    /// lado menor que 5 — famílias nasciam fora dos próprios limites da cidade (LIVE-POLISH).</summary>
    public static int SideFor(long population, int mapWidth, int mapHeight)
    {
        int populationSide = Math.Clamp((int)Math.Ceiling(Math.Sqrt(Math.Max(population, 0)) / 2.0), MinSize, MaxSize);
        int mapLimit = Math.Max(1, Math.Min(mapWidth, mapHeight) / 2);
        return Math.Min(populationSide, mapLimit);
    }

    // SPEC_DEVIATION (dynamic-city-growth, T4): design.md suggested this method take a
    // WorldState (or IReadOnlyList<Building>) to inspect overflow buildings directly — but
    // unlike CityOccupancy/OverflowPlacer/BuildingPlacementResolver (T1-T3), that would force
    // this file to move to LivingWorld.Simulation (WorldState only exists there). Resolving an
    // engine-built building's actual position already requires WorldState (Building.Position
    // stays null for those, per Building.cs's own doc comment — position is always re-derived on
    // demand via BuildingPlacementResolver, never persisted). Rather than repeat that ripple here,
    // this stays a pure Domain function: the caller (which does have WorldState) resolves each
    // candidate overflow building's own absolute footprint box first, and passes plain
    // <see cref="CityBounds"/> boxes in. No behavior change for the many existing callers, since
    // both new parameters are optional and default to "no overflow buildings" (identical output).
    // Post-ship fix (user-reported, 2026-08-23, "MorNorHol" founded off-map): this method clamped
    // WIDTH/HEIGHT to the map (mapLimit above) but never the ORIGIN -- a city near a map edge (or
    // one whose overflow-driven growth pushed it there) could report in-range dimensions while its
    // box was still entirely or partially off-map, because city.Location - side/2 (and the
    // growth-widened minX/minY below) were never re-clamped into [0, mapWidth) x [0, mapHeight).
    // ClampOrigin fixes both the population-only box and the grown box the same way.
    // Post-ship fix (user-reported, 2026-08-23): growth from a city's own overflow buildings had
    // no relationship at all to any OTHER city's bounds -- two cities founded at a safe distance
    // could each grow toward each other, tick after tick, until their walls touched/overlapped.
    // <paramref name="otherCityBoundsToAvoid"/> is the fix: any owned building box that would pull
    // the resulting bounds within <paramref name="absorptionRingCells"/> of one of these boxes is
    // simply not merged in -- bounds stop growing toward that neighbor at the gap boundary, they
    // never jump/warp/overlap. Defaults to null (no other cities to avoid) so every existing
    // single-city caller/test is unaffected.
    public static (CityBounds Bounds, bool IsDerived) Resolve(
        City city, long population, int mapWidth, int mapHeight,
        IReadOnlyList<CityBounds>? ownedBuildingFootprintBoxes = null, int absorptionRingCells = 3,
        IReadOnlyList<CityBounds>? otherCityBoundsToAvoid = null)
    {
        int side = SideFor(population, mapWidth, mapHeight);
        var origin = new CellCoord(city.Location.X - side / 2, city.Location.Y - side / 2);
        var populationBox = new CityBounds(origin, side, side);

        if (ownedBuildingFootprintBoxes is null || ownedBuildingFootprintBoxes.Count == 0)
            return (new CityBounds(ClampOrigin(origin, side, side, mapWidth, mapHeight), side, side), true);

        int minX = populationBox.Origin.X, minY = populationBox.Origin.Y;
        int maxX = populationBox.Origin.X + populationBox.Width - 1;
        int maxY = populationBox.Origin.Y + populationBox.Height - 1;

        foreach (var box in ownedBuildingFootprintBoxes)
        {
            if (ChebyshevGap(populationBox, box) > absorptionRingCells) continue;

            int candidateMinX = Math.Min(minX, box.Origin.X);
            int candidateMinY = Math.Min(minY, box.Origin.Y);
            int candidateMaxX = Math.Max(maxX, box.Origin.X + box.Width - 1);
            int candidateMaxY = Math.Max(maxY, box.Origin.Y + box.Height - 1);

            if (otherCityBoundsToAvoid is { Count: > 0 })
            {
                var candidateBounds = new CityBounds(
                    new CellCoord(candidateMinX, candidateMinY),
                    candidateMaxX - candidateMinX + 1, candidateMaxY - candidateMinY + 1);
                if (otherCityBoundsToAvoid.Any(other => ChebyshevGap(candidateBounds, other) < absorptionRingCells))
                    continue; // growing to include this box would close the gap to another city -- skip it
            }

            minX = candidateMinX; minY = candidateMinY; maxX = candidateMaxX; maxY = candidateMaxY;
        }

        int mapLimit = Math.Max(1, Math.Min(mapWidth, mapHeight) / 2);
        int width = Math.Min(maxX - minX + 1, mapLimit);
        int height = Math.Min(maxY - minY + 1, mapLimit);
        var grownOrigin = ClampOrigin(new CellCoord(minX, minY), width, height, mapWidth, mapHeight);
        return (new CityBounds(grownOrigin, width, height), true);
    }

    /// <summary>Empurra a origem de volta pra dentro de <c>[0, mapWidth) x [0, mapHeight)</c> sem
    /// alterar width/height -- a caixa inteira fica no mapa, não só o tamanho dela.</summary>
    private static CellCoord ClampOrigin(CellCoord origin, int width, int height, int mapWidth, int mapHeight)
    {
        int maxX = Math.Max(0, mapWidth - width);
        int maxY = Math.Max(0, mapHeight - height);
        return new CellCoord(Math.Clamp(origin.X, 0, maxX), Math.Clamp(origin.Y, 0, maxY));
    }

    /// <summary>Distância de Chebyshev (mesma métrica em anel de <c>OverflowPlacer</c>) entre as
    /// bordas de dois retângulos — 0 quando se sobrepõem ou se tocam.</summary>
    private static int ChebyshevGap(CityBounds a, CityBounds b)
    {
        int aRight = a.Origin.X + a.Width - 1, aBottom = a.Origin.Y + a.Height - 1;
        int bRight = b.Origin.X + b.Width - 1, bBottom = b.Origin.Y + b.Height - 1;

        int dx = Math.Max(0, Math.Max(a.Origin.X - bRight, b.Origin.X - aRight));
        int dy = Math.Max(0, Math.Max(a.Origin.Y - bBottom, b.Origin.Y - aBottom));
        return Math.Max(dx, dy);
    }
}
