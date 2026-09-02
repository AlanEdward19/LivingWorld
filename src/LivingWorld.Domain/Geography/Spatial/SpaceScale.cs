namespace LivingWorld.Domain;

/// <summary>Escala entre níveis de espaço como dado de domínio (Fase 15.1, T46/ADR-0018) — porta
/// `SCALE`/`localToParent`/`parentToLocal` de `web/src/map-engine/space.ts:20-48` pro servidor;
/// mesmos valores, para não mudar nada visualmente quando o cliente trocar a fonte. Nenhuma
/// unidade física real por trás (mesmo motivo do comentário original): valores de produto.</summary>
public static class SpaceScale
{
    /// <summary>Quantos tiles de <see cref="SpaceKind.City"/> cabem em 1 tile de <see cref="SpaceKind.World"/>.</summary>
    public const int WorldTilesPerCityTile = 20;

    /// <summary>Quantos tiles de <see cref="SpaceKind.Building"/> cabem em 1 tile de <see cref="SpaceKind.City"/>.</summary>
    public const int CityTilesPerBuildingTile = 6;

    private static int ChildScaleFactor(SpaceKind space) => space switch
    {
        SpaceKind.World => throw new ArgumentException("WorldSpace não tem pai", nameof(space)),
        SpaceKind.City => WorldTilesPerCityTile,
        SpaceKind.Building => CityTilesPerBuildingTile,
        _ => throw new ArgumentOutOfRangeException(nameof(space), space, null),
    };

    /// <summary>Converte uma coordenada local de <paramref name="space"/> pra coordenada
    /// correspondente no espaço pai.</summary>
    public static CellCoord ToParent(SpaceKind space, CellCoord local)
    {
        int factor = ChildScaleFactor(space);
        return new CellCoord(FloorDiv(local.X, factor), FloorDiv(local.Y, factor));
    }

    /// <summary>Inversa de <see cref="ToParent"/>: converte uma coordenada do espaço pai pra
    /// local de <paramref name="space"/>.</summary>
    public static CellCoord ToChild(SpaceKind space, CellCoord parentLocal)
    {
        int factor = ChildScaleFactor(space);
        return new CellCoord(parentLocal.X * factor, parentLocal.Y * factor);
    }

    private static int FloorDiv(int value, int divisor) => (int)Math.Floor((double)value / divisor);
}
