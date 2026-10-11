using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Cities.Spatial;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cities.Construction;
using LivingWorld.Simulation.Cities.Founding;
using LivingWorld.Simulation.Cities.Queries;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Cities;

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
    /// é o <see cref="BuildingId"/> do prédio sendo posicionado agora, se houver um —
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

    /// <summary>Uma <see cref="CityBounds"/> por
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
        // As caixas só dependem dos prédios da cidade e dos bounds por população — exatamente o que o
        // plano memoizado valida. Enquanto o plano não muda, a lista pronta vale.
        // Atalho exato: nenhum prédio criado ou trocado de cidade desde que as caixas foram calculadas para
        // estes mesmos bounds ⇒ o mesmo conjunto de prédios ⇒ o mesmo plano. Evita filtrar e ordenar todos os
        // prédios do mundo a cada chamada (uma por tick e cidade).
        if (Plans.GetOrCreateValue(world).TryGetValue(city.Id, out var known)
            && known.Boxes is { } fast && known.BoxesLayoutVersion == Building.LayoutVersion
            && known.Bounds == populationBounds
            && known.MapWidth == world.Map.Width && known.MapHeight == world.Map.Height)
            return fast;

        long layoutVersion = Building.LayoutVersion;
        var siblings = world.Buildings.Where(b => b.City == city.Id).OrderBy(b => b.Id.Value).ToArray();
        var plan = PlanFor(world, city, populationBounds, siblings);
        if (plan.Boxes is { } cached)
        {
            plan.BoxesLayoutVersion = layoutVersion;
            return cached;
        }

        var result = new List<(Building Building, CityBounds Box)>();
        foreach (var building in world.Buildings.Where(b => b.City == city.Id))
        {
            // null = escassez de terra pra este prédio -- excluído da lista de
            // footprints em vez de derrubar a resolução inteira dos outros prédios da cidade.
            CellCoord? position = building.Position
                ?? BuildingPlacementResolver.Resolve(building, city, world, populationBounds)?.Position;
            if (position is null) continue;

            var footprint = BuildingFootprintGenerator.Generate(building);
            if (footprint.Count == 0) continue;

            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            foreach (var footprintCell in footprint)
            {
                int x = footprintCell.Cell.X + position.Value.X, y = footprintCell.Cell.Y + position.Value.Y;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }

            result.Add((building, new CityBounds(new CellCoord(minX, minY), maxX - minX + 1, maxY - minY + 1)));
        }

        plan.Boxes = result;
        plan.BoxesLayoutVersion = layoutVersion;
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

        // Post-ship fix (cross-city bounds clamp): each other city's OWN un-clamped growth
        // (population box unioned with ITS OWN overflow boxes only, via OwnGrowthBoundsIgnoringOtherCities
        // below) -- deliberately NOT this same ResolveGrownBounds for the other city, which would
        // recurse into every OTHER other city's ResolveGrownBounds and reintroduce the exact
        // O(2^N) recursive blocker already fixed once in this feature (see the "blocker" doc
        // comment on OccupiedCellsOfCity above). One level of "how far has this neighbor grown
        // from its own buildings" is a cheap, non-recursive, still-correct-enough approximation to
        // stop THIS city's growth at the gap boundary -- it doesn't need to also know whether that
        // neighbor is itself being clamped by a third city.
        var otherCityBounds = world.ActiveCities()
            .Where(c => c.Id != city.Id)
            .Select(other => OwnGrowthBoundsIgnoringOtherCities(world, other).Bounds)
            .ToList();

        return SpatialBoundsResolver.ResolveCity(
            city, population, world.Map.Width, world.Map.Height, boxes, world.CityRules.AbsorptionRingCells, otherCityBounds);
    }

    /// <summary>dynamic-city-growth post-ship fix: one non-recursive level of a city's own growth
    /// (population box ∪ its own overflow boxes), with no cross-city clamp applied -- the building
    /// block <see cref="ResolveGrownBounds"/> uses to approximate a NEIGHBOR's extent without
    /// calling back into the full cross-clamped resolution (which would recurse across every
    /// city).</summary>
    private static (CityBounds Bounds, bool IsDerived) OwnGrowthBoundsIgnoringOtherCities(WorldState world, City city)
    {
        long population = CityPopulationQuery.Population(world, city.Id);
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
        var siblings = world.Buildings.Where(b => b.City == city.Id).OrderBy(b => b.Id.Value).ToArray();

        if (placingId is not { } selfId)
        {
            // Sem prédio em posicionamento: anel-hash legado para todos os que não têm posição autorada.
            var legacy = new HashSet<CellCoord>();
            foreach (var building in siblings)
                AddFootprint(legacy, building, building.Position ?? LegacyRingFallback(building.Id, city.Location));
            return legacy;
        }

        // A posição derivada de um prédio depende só dos de Id menor (passe ascendente) e dos bounds,
        // nunca de qual prédio está sendo posicionado agora — então o passe é memoizado por cidade e
        // só estendido quando entram prédios novos no fim. O conjunto devolvido é montado a partir das
        // posições já derivadas, sem repetir busca de célula livre / anel de overflow por vizinho.
        var plan = PlanFor(world, city, bounds, siblings);

        // Nenhum vizinho excluído (o prédio ainda não está na lista e não há "futuro" sem posição):
        // o conjunto é exatamente o do plano. Devolvido sem cópia — todo chamador só lê, e usa
        // antes de qualquer nova chamada que possa estender o plano.
        bool anyExcluded = false;
        foreach (var building in siblings)
        {
            if (building.Id.Value == selfId.Value || (building.Position is null && building.Id.Value > selfId.Value))
            {
                anyExcluded = true;
                break;
            }
        }

        if (!anyExcluded)
            return plan.Occupied;

        var occupied = new HashSet<CellCoord>();
        for (int i = 0; i < siblings.Length; i++)
        {
            var building = siblings[i];
            if (building.Id.Value == selfId.Value) continue; // nunca o próprio prédio sendo posicionado
            if (building.Position is null && building.Id.Value > selfId.Value) continue; // "futuro" sem posição autorada

            // Escassez de terra pra este vizinho: não entra no conjunto (nunca um crash).
            if ((building.Position ?? plan.Derived[i]) is not { } position) continue;
            AddFootprint(occupied, building, position);
        }
        return occupied;
    }

    private static void AddFootprint(HashSet<CellCoord> occupied, Building building, CellCoord position)
    {
        foreach (var footprintCell in BuildingFootprintGenerator.Generate(building))
            occupied.Add(new CellCoord(footprintCell.Cell.X + position.X, footprintCell.Cell.Y + position.Y));
    }

    private sealed class PlacementPlan
    {
        public CityBounds Bounds;
        public int MapWidth, MapHeight;
        public long[] Ids = [];
        public CellCoord?[] Derived = [];
        public HashSet<CellCoord> Occupied = new();
        /// <summary>Caixas de footprint dos prédios da cidade para estes bounds; descartadas quando o plano muda.</summary>
        public List<(Building Building, CityBounds Box)>? Boxes;
        /// <summary><see cref="Building.LayoutVersion"/> em que <see cref="Boxes"/> foi calculado.</summary>
        public long BoxesLayoutVersion;
        /// <summary>Por forma de prédio (tipo, orientação): marcos (id do passo, raio) — no passo desse id
        /// todo anel de raio menor já estava inteiramente ocupado para a forma. A ocupação só cresce ao
        /// longo do passe, então o marco continua valendo para qualquer resolução de id maior ou igual
        /// (cuja ocupação é superconjunto da do passo). Raios crescem junto com os ids.</summary>
        public readonly Dictionary<(int Type, int Orientation), List<(long Id, int Radius)>> OverflowMarks = new();

        public int StartRadiusFor((int Type, int Orientation) shapeKey, long id)
        {
            if (!OverflowMarks.TryGetValue(shapeKey, out var marks)) return 1;
            for (int i = marks.Count - 1; i >= 0; i--)
                if (marks[i].Id <= id) return marks[i].Radius;
            return 1;
        }

        public void Mark((int Type, int Orientation) shapeKey, long id, int radius)
        {
            if (!OverflowMarks.TryGetValue(shapeKey, out var marks))
                OverflowMarks[shapeKey] = marks = new();
            if (marks.Count == 0 || radius > marks[^1].Radius)
                marks.Add((id, radius));
        }
    }

    private static (int Type, int Orientation) ShapeKeyOf(Building building) =>
        (building.BuildingTypeId, building.Orientation ?? BuildingFootprintGenerator.DerivedOrientation(building.Id, building.BuildingTypeId));

    /// <summary>Busca de overflow de <paramref name="building"/> começando no marco do plano memoizado
    /// da cidade (anéis menores já falharam para a forma, com ocupação que é subconjunto da atual) e
    /// gravando o novo marco. Resultado idêntico ao da busca desde o raio 1.</summary>
    internal static CellCoord? ResolveOverflowWithMarks(
        WorldState world, City city, CityBounds bounds, Building building, IReadOnlyList<CellCoord> shape)
    {
        PlacementPlan? plan = null;
        if (Plans.TryGetValue(world, out var byCity) && byCity.TryGetValue(city.Id, out var cached)
            && cached.Bounds == bounds && cached.MapWidth == world.Map.Width && cached.MapHeight == world.Map.Height)
            plan = cached;

        var shapeKey = ShapeKeyOf(building);
        int start = plan?.StartRadiusFor(shapeKey, building.Id.Value) ?? 1;
        var position = OverflowPlacer.ResolveOverflowPosition(world, city, bounds, building.Id, shape, start, out int found);
        plan?.Mark(shapeKey, building.Id.Value, found);
        return position;
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<
        WorldState, Dictionary<CityId, PlacementPlan>> Plans = new();

    /// <summary>Passe ascendente por Id sobre os prédios da cidade: cada prédio sem posição autorada
    /// ocupa a primeira célula livre dos bounds, senão a do anel de overflow, contra o que os de Id
    /// menor já ocupam. Reaproveita o passe anterior quando só entraram prédios no fim da lista.</summary>
    private static PlacementPlan PlanFor(WorldState world, City city, CityBounds bounds, Building[] siblings)
    {
        var byCity = Plans.GetOrCreateValue(world);
        byCity.TryGetValue(city.Id, out var plan);

        int reusable = 0;
        if (plan is not null && plan.Bounds == bounds && plan.MapWidth == world.Map.Width && plan.MapHeight == world.Map.Height)
        {
            while (reusable < plan.Ids.Length && reusable < siblings.Length && plan.Ids[reusable] == siblings[reusable].Id.Value)
                reusable++;
            // Só vale se TODO o passe anterior continua válido (prédios novos apenas no fim).
            if (reusable != plan.Ids.Length)
                plan = null;
        }
        else
        {
            plan = null;
        }

        int start = 0;
        if (plan is null)
        {
            plan = new PlacementPlan { Bounds = bounds, MapWidth = world.Map.Width, MapHeight = world.Map.Height };
            byCity[city.Id] = plan;
        }
        else
        {
            start = plan.Ids.Length;
        }

        if (start == siblings.Length && plan.Ids.Length == siblings.Length)
            return plan;

        plan.Boxes = null;

        var ids = new long[siblings.Length];
        var derived = new CellCoord?[siblings.Length];
        Array.Copy(plan.Ids, ids, start);
        Array.Copy(plan.Derived, derived, start);

        for (int i = start; i < siblings.Length; i++)
        {
            var building = siblings[i];
            ids[i] = building.Id.Value;
            CellCoord? position;
            if (building.Position is { } authored)
                position = authored;
            else
            {
                var shape = BuildingFootprintGenerator.Generate(building).Select(c => c.Cell).ToList();
                position = ScanForFreeOrigin(plan.Occupied, bounds, shape);
                if (position is null)
                {
                    var shapeKey = ShapeKeyOf(building);
                    position = OverflowPlacer.ResolveOverflowPositionGiven(
                        plan.Occupied, bounds, building.Id, shape, world.Map.Width, world.Map.Height,
                        plan.StartRadiusFor(shapeKey, building.Id.Value), out int foundRadius);
                    plan.Mark(shapeKey, building.Id.Value, foundRadius);
                }

                derived[i] = position;
            }

            if (position is { } resolved)
                AddFootprint(plan.Occupied, building, resolved);
        }

        plan.Ids = ids;
        plan.Derived = derived;
        return plan;
    }

    private static HashSet<CellCoord> OccupiedCellsLegacy(IEnumerable<Building> buildings, WorldState world)
    {
        var occupied = new HashSet<CellCoord>();
        foreach (var building in buildings)
        {
            var ownerCity = world.FindCity(building.City);
            if (ownerCity is null) continue;

            var position = building.Position ?? LegacyRingFallback(building.Id, ownerCity.Location);
            var shape = BuildingFootprintGenerator.Generate(building).Select(c => c.Cell).ToList();
            foreach (var cell in Translate(shape, position))
                occupied.Add(cell);
        }
        return occupied;
    }
}
