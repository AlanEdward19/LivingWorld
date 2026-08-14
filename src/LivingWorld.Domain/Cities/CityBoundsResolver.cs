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

    public static (CityBounds Bounds, bool IsDerived) Resolve(City city, long population, int mapWidth, int mapHeight)
    {
        int populationSide = Math.Clamp((int)Math.Ceiling(Math.Sqrt(Math.Max(population, 0)) / 2.0), MinSize, MaxSize);
        int mapLimit = Math.Max(1, Math.Min(mapWidth, mapHeight) / 2);
        int side = Math.Min(populationSide, mapLimit);
        var origin = new CellCoord(city.Location.X - side / 2, city.Location.Y - side / 2);
        return (new CityBounds(origin, side, side), true);
    }
}
