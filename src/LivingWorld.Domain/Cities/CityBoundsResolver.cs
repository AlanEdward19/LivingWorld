namespace LivingWorld.Domain;

/// <summary>Extensão de uma cidade no grid do mundo (Fase 15.1, T45).</summary>
public readonly record struct CityBounds(CellCoord Origin, int Width, int Height);

/// <summary>Resolve os bounds de uma cidade (Fase 15.1, T45; G4/backend-gaps.md). Nenhum cenário
/// autora tamanho de cidade hoje — <see cref="IsDerived"/> é sempre <c>true</c> por ora.
///
/// Bugfix real (usuário, 2026-08-13): a fórmula original era um tamanho FIXO de 34×24 células —
/// herdada do placeholder client-side (`cityGroundBounds`) de quando cidade tinha seu próprio
/// grid local pequeno. Coordenadas de cidade viraram absolutas (mesma escala do mapa-múndi,
/// T46), então um tamanho fixo de 34×24 estourava qualquer mundo menor que isso (10×10/20×20 —
/// exatamente os presets Pequeno/Médio do World Creator), desenhando a muralha muito além da
/// borda do mapa. <see cref="population"/> (soma de materializados + pool agregado, mesma fonte
/// de <c>CityPopulationQuery.Population</c>) agora escala o lado do quadrado, com piso e teto —
/// o teto é o mesmo 34 de antes, então cidades grandes continuam do tamanho que já estavam.</summary>
public static class CityBoundsResolver
{
    private const int MinSize = 4;
    private const int MaxSize = 34;

    public static (CityBounds Bounds, bool IsDerived) Resolve(City city, long population)
    {
        int side = Math.Clamp((int)Math.Ceiling(Math.Sqrt(Math.Max(population, 0)) * 2), MinSize, MaxSize);
        var origin = new CellCoord(city.Location.X - side / 2, city.Location.Y - side / 2);
        return (new CityBounds(origin, side, side), true);
    }
}
