using LivingWorld.Domain;

namespace LivingWorld.Simulation;

// SPEC_DEVIATION (dynamic-city-growth, T1/T3): design.md declara este tipo em
// src/LivingWorld.Domain/Cities/ — mas LivingWorld.Domain não referencia LivingWorld.Simulation
// (é o inverso: Simulation -> Domain, ver os .csproj), e WorldState só existe em Simulation.
// Mesmo motivo/precedente já documentado em CityPopulationQuery.cs. Vive aqui, mesmo pacote.

/// <summary>Ocupação real de uma cidade (dynamic-city-growth, T1/T3): responde "esta célula/este
/// footprint está livre?" varrendo <see cref="WorldState.Buildings"/> filtrado por
/// <see cref="Building.City"/> — nenhum grid próprio persistido, mesma filosofia "bounds/posição
/// são sempre derivados" de <see cref="CityBoundsResolver"/>.</summary>
public static class CityOccupancy
{
    private const int LegacyRingRadius = 3;

    /// <summary>True quando nenhum prédio já existente na mesma cidade ocupa alguma célula do
    /// footprint candidato (já traduzido para coordenadas absolutas). <paramref name="placingId"/>
    /// é o <see cref="BuildingId"/> do prédio sendo posicionado agora, se houver um — T3/CITYGROW-01:
    /// sem ele, um vizinho ainda sem posição autorada seria contado pelo anel-hash legado em vez
    /// da posição real que <see cref="BuildingPlacementResolver.Resolve"/> escolheria pra ele,
    /// deixando dois prédios "derivados" na mesma cidade livres pra colidir entre si (bug real,
    /// pego pelo teste de não-colisão de T3).</summary>
    public static bool IsFree(
        WorldState world, City city, CityBounds bounds, IReadOnlyList<CellCoord> candidateFootprint, BuildingId? placingId = null)
    {
        var occupied = OccupiedCellsOfCity(world, city, bounds, placingId);
        return candidateFootprint.All(cell => !occupied.Contains(cell));
    }

    /// <summary>Primeira origem livre dentro de <paramref name="bounds"/>, varrida em ordem
    /// determinística (linha a linha, sem RNG) — mesmo <see cref="Building.Id"/> e mesmo estado
    /// do mundo sempre produzem a mesma origem. <c>null</c> quando os bounds estão totalmente
    /// ocupados.</summary>
    public static CellCoord? FindFreeCellInBounds(
        WorldState world, City city, CityBounds bounds, IReadOnlyList<CellCoord> footprintShape, BuildingId? placingId = null)
    {
        var occupied = OccupiedCellsOfCity(world, city, bounds, placingId);
        return ScanForFreeOrigin(occupied, bounds, footprintShape);
    }

    /// <summary>True somente quando uma varredura do mapa inteiro (não só desta cidade — um
    /// prédio de outra cidade também ocupa a célula pra qualquer um) não encontra nenhuma célula
    /// livre para o footprint informado. Sem contexto de "quem está sendo posicionado" (não há um
    /// prédio específico aqui, é uma pergunta ambiental sobre o mapa) — usa o anel-hash legado
    /// pra vizinhos sem posição autorada, uma aproximação aceitável pra um sinal booleano de
    /// escassez (ao contrário de <see cref="IsFree"/>/<see cref="FindFreeCellInBounds"/>, que
    /// decidem a posição real de um prédio e por isso precisam de precisão).</summary>
    public static bool IsLandScarce(WorldState world, City city, IReadOnlyList<CellCoord> footprintShape)
    {
        var mapBounds = new CityBounds(new CellCoord(0, 0), world.Map.Width, world.Map.Height);
        var occupied = OccupiedCellsLegacy(world.Buildings, world);
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

    /// <summary>dynamic-city-growth, T4b (CITYGROW-03/05): uma <see cref="CityBounds"/> por
    /// prédio da cidade — sua posição real (<see cref="Building.Position"/>) quando autorada,
    /// senão re-derivada via <see cref="BuildingPlacementResolver.Resolve"/> (nunca persistida,
    /// mesma convenção de <see cref="Building"/>). É o insumo que <see cref="CityBoundsResolver.Resolve"/>
    /// precisa em <paramref name="populationBounds"/>'s caller pra crescer os bounds pra além do
    /// teto de população — sem isso, T4's parâmetro `ownedBuildingFootprintBoxes` só era exercitado
    /// por teste unitário, nunca pelo tick/API reais.</summary>
    public static IReadOnlyList<CityBounds> OwnedBuildingFootprintBoxes(WorldState world, City city, CityBounds populationBounds) =>
        OwnedBuildingFootprintBoxesWithOwners(world, city, populationBounds).Select(p => p.Box).ToList();

    /// <summary>dynamic-city-growth, T6: mesma coisa que <see cref="OwnedBuildingFootprintBoxes"/>,
    /// mas emparelhada com o <see cref="Building"/> de origem — <see cref="OverflowClusterFinder"/>
    /// precisa do prédio em si (pra marcar/reatribuir), não só do box.</summary>
    public static IReadOnlyList<(Building Building, CityBounds Box)> OwnedBuildingFootprintBoxesWithOwners(
        WorldState world, City city, CityBounds populationBounds)
    {
        var result = new List<(Building Building, CityBounds Box)>();
        foreach (var building in world.Buildings.Where(b => b.City == city.Id))
        {
            var position = building.Position
                ?? BuildingPlacementResolver.Resolve(building, city, world, populationBounds).Position;
            var shape = BuildingFootprintGenerator.Generate(building.Id, building.BuildingTypeId).Select(c => c.Cell).ToList();
            var cells = Translate(shape, position);
            if (cells.Count == 0) continue;

            int minX = cells.Min(c => c.X), minY = cells.Min(c => c.Y);
            int maxX = cells.Max(c => c.X), maxY = cells.Max(c => c.Y);
            result.Add((building, new CityBounds(new CellCoord(minX, minY), maxX - minX + 1, maxY - minY + 1)));
        }
        return result;
    }

    /// <summary>dynamic-city-growth, T4b: o padrão de duas chamadas que todo call site real de
    /// <see cref="SpatialBoundsResolver.ResolveCity"/> precisa agora — primeiro os bounds
    /// puramente por população (pra dar às buildings ainda sem posição autorada uma área de
    /// busca de célula livre), depois os boxes de <see cref="OwnedBuildingFootprintBoxes"/>
    /// alimentados de volta pra resolução final (potencialmente maior). Um único lugar em vez de
    /// duplicar as duas chamadas nos 6 call sites (mesmo conceito, per tasks.md).</summary>
    public static (CityBounds Bounds, bool IsDerived) ResolveGrownBounds(WorldState world, City city, long population)
    {
        var (populationBounds, _) = SpatialBoundsResolver.ResolveCity(city, population, world.Map.Width, world.Map.Height);
        var boxes = OwnedBuildingFootprintBoxes(world, city, populationBounds);
        return SpatialBoundsResolver.ResolveCity(
            city, population, world.Map.Width, world.Map.Height, boxes, world.CityRules.AbsorptionRingCells);
    }

    /// <summary>Posição de um prédio sem posição autorada, para fins só de cálculo de ocupação —
    /// mesma fórmula de anel/hash que existia sozinha em
    /// <c>BuildingPlacementResolver.DerivedPosition</c> antes desta feature. Único fallback
    /// disponível quando não há um <paramref name="placingId"/> de contexto (<see
    /// cref="IsLandScarce"/>) — sem isso, "onde esse vizinho estaria" ficaria indefinido.</summary>
    internal static CellCoord LegacyRingFallback(BuildingId id, CellCoord cityLocation)
    {
        ulong h = StableHash.Mix(id.Value);
        double angle = h % 3600 / 3600.0 * 2 * Math.PI;
        int dx = (int)Math.Round(Math.Cos(angle) * LegacyRingRadius);
        int dy = (int)Math.Round(Math.Sin(angle) * LegacyRingRadius);
        return new CellCoord(cityLocation.X + dx, cityLocation.Y + dy);
    }

    /// <summary>Ocupação de uma única cidade, coerente com a decisão real de posicionamento
    /// (T3): cada vizinho sem posição autorada é resolvido na mesma ordem causal que <see
    /// cref="BuildingPlacementResolver.Resolve"/> usaria (livre nos bounds, senão anel de
    /// overflow) — nunca o anel-hash legado sozinho, que ignoraria a busca por célula livre e
    /// deixaria dois prédios derivados colidirem.
    ///
    /// dynamic-city-growth, fix (blocker): antes, cada vizinho era resolvido chamando de volta
    /// <see cref="BuildingPlacementResolver.Resolve"/>, que por sua vez chamava este método outra
    /// vez pra CADA vizinho menor -- T(k) = soma de T(1..k-1), ou seja 2^(k-1) resoluções sem
    /// nenhuma memoização (187s medidos com 6 prédios). Agora é um único passe ascendente por
    /// <see cref="BuildingId"/>: o conjunto <c>occupied</c> é construído incrementalmente, e cada
    /// vizinho sem posição autorada é resolvido direto contra ele (nunca reentrando em
    /// <see cref="BuildingPlacementResolver.Resolve"/> nem em <see cref="IsFree"/>/<see
    /// cref="FindFreeCellInBounds"/>) -- por isso a ordem ascendente é obrigatória: ao chegar no
    /// prédio k, <c>occupied</c> já contém exatamente as células dos prédios com id menor, a
    /// mesma causalidade que a versão recursiva original pretendia.
    ///
    /// O filtro por <paramref name="placingId"/> é preservado exatamente como antes: exclui o
    /// próprio prédio sendo posicionado (evitaria recursão infinita nele mesmo, comum quando o
    /// prédio já está em <c>world.Buildings</c>, ex. <c>CityProjector</c> iterando a própria
    /// lista) e ignora prédios "futuros" sem posição autorada (id maior, que não existiam ainda
    /// quando <paramref name="placingId"/> foi posicionado). Sem <paramref name="placingId"/>
    /// (chamada fora de uma resolução em curso), cai no anel-hash legado pra todos.
    ///
    /// <c>internal</c> (era <c>private</c>) só pra <see cref="OverflowPlacer.ResolveOverflowPosition"/>
    /// poder computar o conjunto ocupado uma única vez em vez de reconstruí-lo por candidato de
    /// anel (a outra metade do mesmo bug).</summary>
    internal static HashSet<CellCoord> OccupiedCellsOfCity(WorldState world, City city, CityBounds bounds, BuildingId? placingId)
    {
        var occupied = new HashSet<CellCoord>();
        var siblings = world.Buildings.Where(b => b.City == city.Id);
        if (placingId is { } selfId)
        {
            siblings = siblings
                .Where(b => b.Id.Value != selfId.Value) // nunca o próprio prédio sendo posicionado
                .Where(b => b.Position is not null || b.Id.Value < selfId.Value); // exclui "futuro" sem posição autorada
        }

        foreach (var building in siblings.OrderBy(b => b.Id.Value))
        {
            var shape = BuildingFootprintGenerator.Generate(building.Id, building.BuildingTypeId).Select(c => c.Cell).ToList();

            CellCoord position;
            if (building.Position is { } authored)
                position = authored;
            else if (placingId is not null)
                position = ScanForFreeOrigin(occupied, bounds, shape)
                    ?? OverflowPlacer.ResolveOverflowPositionGiven(occupied, bounds, building.Id, shape);
            else
                position = LegacyRingFallback(building.Id, city.Location);

            foreach (var cell in Translate(shape, position))
                occupied.Add(cell);
        }
        return occupied;
    }

    private static HashSet<CellCoord> OccupiedCellsLegacy(IEnumerable<Building> buildings, WorldState world)
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
