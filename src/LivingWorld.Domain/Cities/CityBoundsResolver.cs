namespace LivingWorld.Domain;

/// <summary>Extensão de uma cidade no grid do mundo (Fase 15.1, T45).</summary>
public readonly record struct CityBounds(CellCoord Origin, int Width, int Height);

/// <summary>Resolve os bounds de uma cidade (Fase 15.1, T45; G4/backend-gaps.md). Nenhum cenário
/// autora tamanho de cidade hoje — <see cref="IsDerived"/> é sempre <c>true</c> por ora, e o
/// fallback é exatamente a mesma fórmula fixa do placeholder client-side
/// (`cityGroundBounds`, `web/src/map-engine/worldVisuals.ts:44-53`), para que trocar o campo
/// derivado pelo campo real (T20/T34) não mova nada visualmente por si só.</summary>
public static class CityBoundsResolver
{
    private const int DerivedWidth = 34;
    private const int DerivedHeight = 24;

    public static (CityBounds Bounds, bool IsDerived) Resolve(City city)
    {
        var origin = new CellCoord(city.Location.X - DerivedWidth / 2, city.Location.Y - DerivedHeight / 2);
        return (new CityBounds(origin, DerivedWidth, DerivedHeight), true);
    }
}
