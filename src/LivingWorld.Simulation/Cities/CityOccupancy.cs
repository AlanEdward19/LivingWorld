using LivingWorld.Domain;

namespace LivingWorld.Simulation;

// SPEC_DEVIATION (dynamic-city-growth, T1): design.md declara este tipo em
// src/LivingWorld.Domain/Cities/ — mas LivingWorld.Domain não referencia LivingWorld.Simulation
// (é o inverso: Simulation -> Domain, ver os .csproj), e WorldState só existe em Simulation.
// Mesmo motivo/precedente já documentado em CityPopulationQuery.cs. Vive aqui, mesmo pacote.

/// <summary>Ocupação real de uma cidade (dynamic-city-growth, T1): responde "esta célula/este
/// footprint está livre?" varrendo <see cref="WorldState.Buildings"/> filtrado por
/// <see cref="Building.City"/> — nenhum grid próprio persistido, mesma filosofia "bounds/posição
/// são sempre derivados" de <see cref="CityBoundsResolver"/>.</summary>
public static class CityOccupancy
{
    private const int LegacyRingRadius = 3;

    /// <summary>True quando nenhum prédio já existente na mesma cidade ocupa alguma célula do
    /// footprint candidato (já traduzido para coordenadas absolutas).</summary>
    public static bool IsFree(WorldState world, City city, IReadOnlyList<CellCoord> candidateFootprint)
    {
        var occupied = OccupiedCellsOf(world.Buildings.Where(b => b.City == city.Id), world);
        return candidateFootprint.All(cell => !occupied.Contains(cell));
    }

    /// <summary>Primeira origem livre dentro de <paramref name="bounds"/>, varrida em ordem
    /// determinística (linha a linha, sem RNG) — mesmo <see cref="Building.Id"/> e mesmo estado
    /// do mundo sempre produzem a mesma origem. <c>null</c> quando os bounds estão totalmente
    /// ocupados.</summary>
    public static CellCoord? FindFreeCellInBounds(
        WorldState world, City city, CityBounds bounds, IReadOnlyList<CellCoord> footprintShape)
    {
        var occupied = OccupiedCellsOf(world.Buildings.Where(b => b.City == city.Id), world);
        return ScanForFreeOrigin(occupied, bounds, footprintShape);
    }

    /// <summary>True somente quando uma varredura do mapa inteiro (não só desta cidade — um
    /// prédio de outra cidade também ocupa a célula pra qualquer um) não encontra nenhuma célula
    /// livre para o footprint informado.</summary>
    public static bool IsLandScarce(WorldState world, City city, IReadOnlyList<CellCoord> footprintShape)
    {
        var mapBounds = new CityBounds(new CellCoord(0, 0), world.Map.Width, world.Map.Height);
        var occupied = OccupiedCellsOf(world.Buildings, world);
        return ScanForFreeOrigin(occupied, mapBounds, footprintShape) is null;
    }

    // ponytail: varredura O(área dos bounds), sem otimização de grid espacial — bounds de cidade
    // hoje são no máximo ~map/2 de lado (CityBoundsResolver), e IsLandScarce só roda quando a
    // cidade já esgotou seu próprio scan; adicionar índice espacial só se isso aparecer no
    // profiling em mapas grandes de verdade.
    private static CellCoord? ScanForFreeOrigin(
        HashSet<CellCoord> occupied, CityBounds bounds, IReadOnlyList<CellCoord> footprintShape)
    {
        for (int y = bounds.Origin.Y; y < bounds.Origin.Y + bounds.Height; y++)
            for (int x = bounds.Origin.X; x < bounds.Origin.X + bounds.Width; x++)
            {
                var origin = new CellCoord(x, y);
                var candidate = Translate(footprintShape, origin);
                if (candidate.All(bounds.Contains) && candidate.All(cell => !occupied.Contains(cell)))
                    return origin;
            }
        return null;
    }

    internal static List<CellCoord> Translate(IReadOnlyList<CellCoord> shape, CellCoord origin) =>
        shape.Select(c => new CellCoord(c.X + origin.X, c.Y + origin.Y)).ToList();

    /// <summary>Posição de um prédio sem posição autorada, para fins só de cálculo de ocupação de
    /// vizinho — mesma fórmula de anel/hash que existia sozinha em
    /// <c>BuildingPlacementResolver.DerivedPosition</c> antes desta feature. T1 não depende de T3
    /// (tasks.md) e por isso não pode chamar o resolver occupancy-aware (que só nasce em T3, e
    /// que por sua vez chama <see cref="FindFreeCellInBounds"/>) — chamar de volta criaria
    /// recursão mútua sem base de parada. Isso é só o "onde esse vizinho já estaria" para marcar
    /// células ocupadas, nunca reexecuta busca por célula livre para o vizinho.</summary>
    internal static CellCoord LegacyRingFallback(BuildingId id, CellCoord cityLocation)
    {
        ulong h = StableHash.Mix(id.Value);
        double angle = h % 3600 / 3600.0 * 2 * Math.PI;
        int dx = (int)Math.Round(Math.Cos(angle) * LegacyRingRadius);
        int dy = (int)Math.Round(Math.Sin(angle) * LegacyRingRadius);
        return new CellCoord(cityLocation.X + dx, cityLocation.Y + dy);
    }

    private static HashSet<CellCoord> OccupiedCellsOf(IEnumerable<Building> buildings, WorldState world)
    {
        var occupied = new HashSet<CellCoord>();
        foreach (var building in buildings)
        {
            var ownerCity = world.FindCity(building.City);
            if (ownerCity is null) continue;

            var position = building.Position ?? LegacyRingFallback(building.Id, ownerCity.Location);
            var shape = BuildingFootprintGenerator.Generate(building.Id, building.BuildingTypeId).Select(c => c.Cell).ToList();
            foreach (var cell in Translate(shape, position))
                occupied.Add(cell);
        }
        return occupied;
    }
}
