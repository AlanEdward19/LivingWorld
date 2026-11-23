using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Cities.Spatial;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cities.Construction;
using LivingWorld.Simulation.Cities.Founding;
using LivingWorld.Simulation.Cities.Layout;
using LivingWorld.Simulation.Cities.Queries;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Cities;

// SPEC_DEVIATION (dynamic-city-growth): design.md declara este tipo em
// src/LivingWorld.Domain/Cities/ — mas LivingWorld.Domain não referencia LivingWorld.Simulation
// (é o inverso: Simulation -> Domain, ver os .csproj), e WorldState só existe em Simulation.
// Mesmo motivo/precedente já documentado em CityPopulationQuery.cs. Vive aqui, mesmo pacote.

/// <summary>Ocupação real de uma cidade (dynamic-city-growth): responde "esta célula/este
/// footprint está livre?" varrendo <see cref="WorldState.Buildings"/> filtrado por
/// <see cref="Building.City"/> — nenhum grid próprio persistido, mesma filosofia "bounds/posição
/// são sempre derivados" de <see cref="CityBoundsResolver"/>.</summary>
public static class CityOccupancy
{
    private const int LegacyRingRadius = 3;

    /// <summary>Células livres exigidas entre o footprint de um prédio derivado e qualquer outro
    /// (city-building-separation): sem isso as casas se fundem num bloco só.</summary>
    public const int MinGap = 1;

    /// <summary>True quando nenhum prédio já existente na mesma cidade ocupa alguma célula do
    /// footprint candidato (já traduzido para coordenadas absolutas). <paramref name="placingId"/>
    /// é o <see cref="BuildingId"/> do prédio sendo posicionado agora, se houver um —
    /// sem ele, um vizinho ainda sem posição autorada seria contado pelo anel-hash legado em vez
    /// da posição real que <see cref="BuildingPlacementResolver.Resolve"/> escolheria pra ele,
    /// deixando dois prédios "derivados" na mesma cidade livres pra colidir entre si (bug real,
    /// pego pelo teste de não-colisão).</summary>
    public static bool IsFree(
        WorldState world, City city, CityBounds bounds, IReadOnlyList<CellCoord> candidateFootprint, BuildingId? placingId = null)
    {
        // Só sobreposição (regra antiga): quem pergunta aqui é a autoria, que tem precedência sobre folga e água.
        var occupied = OccupiedCellsOfCity(world, city, bounds, placingId);
        var policy = PlacementPolicy.None;
        return policy.Allows(candidateFootprint, policy.HaloOf(candidateFootprint), new CellCoord(0, 0), occupied);
    }

    /// <summary>Primeira origem livre dentro de <paramref name="bounds"/>, varrida em ordem
    /// determinística (linha a linha, sem RNG) — mesmo <see cref="Building.Id"/> e mesmo estado
    /// do mundo sempre produzem a mesma origem. <c>null</c> quando os bounds estão totalmente
    /// ocupados.</summary>
    public static CellCoord? FindFreeCellInBounds(
        WorldState world, City city, CityBounds bounds, IReadOnlyList<CellCoord> footprintShape, BuildingId? placingId = null,
        int level = 0)
    {
        var ground = GroundOfCity(world, city, bounds, placingId);
        var occupied = ground.Occupied;
        var network = CityStreets.Of(world, city);
        var guard = level < 2 && network.Count > 0 ? new GroundGuard(network.Set, ground.Passages, null) : null;
        // Só lê os marcos do passe (gravados com ocupação que é subconjunto desta): pular origens que já
        // falharam antes não muda o resultado, só evita varrer de novo bounds já cheios.
        int from = 0;
        if (placingId is { } id && Plans.TryGetValue(world, out var byCity) && byCity.TryGetValue(city.Id, out var plan)
            && plan.Bounds == bounds && plan.MapWidth == world.Map.Width && plan.MapHeight == world.Map.Height)
            from = plan.ScanStartFor(ShapeHash(footprintShape, level + PlacementPolicy.StreetLevels), id.Value);
        return ScanForFreeOriginFrom(occupied, bounds, footprintShape, PlacementPolicy.For(world.Map, level), from, out _, guard);
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
        return ScanForFreeOrigin(occupied, mapBounds, footprintShape, PlacementPolicy.For(world.Map, PlacementPolicy.LevelCount - PlacementPolicy.StreetLevels - 1)) is null;
    }

    // ponytail: varredura O(área dos bounds), sem otimização de grid espacial — bounds de cidade
    // hoje são no máximo ~map/2 de lado (CityBoundsResolver), e IsLandScarce só roda quando a
    // cidade já esgotou seu próprio scan; adicionar índice espacial só se isso aparecer no
    // profiling em mapas grandes de verdade.
    internal static CellCoord? ScanForFreeOrigin(
        HashSet<CellCoord> occupied, CityBounds bounds, IReadOnlyList<CellCoord> footprintShape, PlacementPolicy policy) =>
        ScanForFreeOriginFrom(occupied, bounds, footprintShape, policy, 0, out _);

    /// <summary>Varredura linha a linha a partir do índice <paramref name="startIndex"/> (linha * largura + coluna).
    /// <paramref name="foundIndex"/> é o índice achado, ou a área dos bounds quando nenhuma origem serve.</summary>
    internal static CellCoord? ScanForFreeOriginFrom(
        HashSet<CellCoord> occupied, CityBounds bounds, IReadOnlyList<CellCoord> footprintShape, PlacementPolicy policy,
        int startIndex, out int foundIndex, GroundGuard? guard = null)
    {
        var halo = policy.HaloOf(footprintShape);
        int area = bounds.Width * bounds.Height;
        for (int index = Math.Max(0, startIndex); index < area; index++)
        {
            int x = bounds.Origin.X + index % bounds.Width;
            int y = bounds.Origin.Y + index / bounds.Width;
            bool inside = true;
            for (int i = 0; i < footprintShape.Count && inside; i++)
                inside = bounds.Contains(new CellCoord(footprintShape[i].X + x, footprintShape[i].Y + y));
            var origin = new CellCoord(x, y);
            if (inside && policy.Allows(footprintShape, halo, origin, occupied, guard))
            {
                foundIndex = index;
                return origin;
            }
        }
        foundIndex = area;
        return null;
    }

    /// <summary>Identidade da forma (células) para os marcos de varredura; independe de tipo e orientação.</summary>
    internal static ulong ShapeHash(IReadOnlyList<CellCoord> shape, int level)
    {
        ulong hash = 1469598103934665603UL ^ ((ulong)level * 0x9E3779B97F4A7C15UL);
        foreach (var cell in shape)
        {
            hash = (hash ^ (uint)cell.X) * 1099511628211UL;
            hash = (hash ^ (uint)cell.Y) * 1099511628211UL;
        }
        return hash;
    }

    internal static List<CellCoord> Translate(IReadOnlyList<CellCoord> shape, CellCoord origin) =>
        shape.Select(c => new CellCoord(c.X + origin.X, c.Y + origin.Y)).ToList();

    /// <summary>Uma <see cref="CityBounds"/> por
    /// prédio da cidade — sua posição real (<see cref="Building.Position"/>) quando autorada,
    /// senão re-derivada via <see cref="BuildingPlacementResolver.Resolve"/> (nunca persistida,
    /// mesma convenção de <see cref="Building"/>). É o insumo que <see cref="CityBoundsResolver.Resolve"/>
    /// precisa em <paramref name="populationBounds"/>'s caller pra crescer os bounds pra além do
    /// teto de população — sem isso, o parâmetro `ownedBuildingFootprintBoxes` só era exercitado
    /// por teste unitário, nunca pelo tick/API reais.</summary>
    public static IReadOnlyList<CityBounds> OwnedBuildingFootprintBoxes(WorldState world, City city, CityBounds populationBounds) =>
        OwnedBuildingFootprintBoxesWithOwners(world, city, populationBounds).Select(p => p.Box).ToList();

    /// <summary>dynamic-city-growth: mesma coisa que <see cref="OwnedBuildingFootprintBoxes"/>,
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
        var siblings = SiblingsOf(world, city);

        // Caixas anteriores, se o plano continuar o mesmo objeto (só entraram prédios no fim) e todos os
        // prédios do plano tiverem posição autorada: a caixa de cada um depende só da própria posição e
        // forma, então as já calculadas continuam valendo e só os acrescentados no mundo são medidos.
        var previous = Plans.GetOrCreateValue(world).TryGetValue(city.Id, out var before) ? before : null;
        var previousBoxes = previous?.Boxes;
        int previousWorldCount = previous?.BoxesWorldCount ?? 0;
        long previousVersion = previous?.BoxesLayoutVersion ?? -1;

        var plan = PlanFor(world, city, populationBounds, siblings);
        if (plan.Boxes is { } cached)
        {
            plan.BoxesLayoutVersion = layoutVersion;
            plan.BoxesWorldCount = world.Buildings.Count;
            return cached;
        }

        var all = world.Buildings;
        bool incremental = previousBoxes is not null && ReferenceEquals(plan, previous) && !plan.HasDerived
            && previousWorldCount <= all.Count && layoutVersion - previousVersion == all.Count - previousWorldCount;

        var result = incremental ? new List<(Building Building, CityBounds Box)>(previousBoxes!) : new List<(Building Building, CityBounds Box)>();
        for (int index = incremental ? previousWorldCount : 0; index < all.Count; index++)
        {
            var building = all[index];
            if (building.City != city.Id) continue;

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
        plan.BoxesWorldCount = all.Count;
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
            city, population, world.Map.Width, world.Map.Height, boxes, world.CityRules.AbsorptionRingCells, otherCityBounds,
            chainReachCells: ChainReach(world), anchorLoneBuilding: AnchorLone(world));
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
            city, population, world.Map.Width, world.Map.Height, boxes, world.CityRules.AbsorptionRingCells,
            chainReachCells: ChainReach(world), anchorLoneBuilding: AnchorLone(world));
    }

    /// <summary>AD-192 (08d 165): com o traçado ligado, a cidade sem prédio perto do miolo encadeia o limite a partir do mais perto.</summary>
    private static bool AnchorLone(WorldState world) => ChainReach(world) > 0 && world.StreetRules.AnchorLoneHouse;

    /// <summary>Posição de um prédio sem posição autorada, para fins só de cálculo de ocupação —
    /// mesma fórmula de anel/hash que existia sozinha em
    /// <c>BuildingPlacementResolver.DerivedPosition</c> antes desta feature. Único fallback
    /// disponível quando não há um <paramref name="placingId"/> de contexto (<see
    /// cref="IsLandScarce"/>) — sem isso, "onde esse vizinho estaria" ficaria indefinido.</summary>
    internal static CellCoord LegacyRingFallback(BuildingId id, CellCoord cityLocation)
    {
        ulong h = StableHash.Mix(id.Value);
        double turns = h % 3600 / 3600.0;
        int dx = (int)Math.Round(DeterministicMath.CosTurns(turns) * LegacyRingRadius);
        int dy = (int)Math.Round(DeterministicMath.SinTurns(turns) * LegacyRingRadius);
        return new CellCoord(cityLocation.X + dx, cityLocation.Y + dy);
    }

    /// <summary>Ocupação de uma única cidade, coerente com a decisão real de posicionamento:
    /// cada vizinho sem posição autorada é resolvido na mesma ordem causal que <see
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
    internal static HashSet<CellCoord> OccupiedCellsOfCity(WorldState world, City city, CityBounds bounds, BuildingId? placingId) =>
        GroundOfCity(world, city, bounds, placingId).Occupied;

    /// <summary>AD-173: o chão inteiro (plantas, passagens de porta, quintais) na mesma ordem causal de
    /// <see cref="OccupiedCellsOfCity"/>.</summary>
    internal static CityGround GroundOfCity(WorldState world, City city, CityBounds bounds, BuildingId? placingId)
    {
        var siblings = SiblingsOf(world, city);
        var network = CityStreets.Of(world, city);
        var layout = world.CityRules.EffectiveLayout();

        if (placingId is not { } selfId)
        {
            // Sem prédio em posicionamento: anel-hash legado para todos os que não têm posição autorada.
            var legacy = new CityGround();
            foreach (var building in siblings)
                legacy.Add(BuildingFootprintGenerator.Generate(building), building.Position ?? LegacyRingFallback(building.Id, city.Location),
                    building.CroftCells ?? 0, network, world.Map, layout);
            return legacy;
        }

        // A posição derivada de um prédio depende só dos de Id menor (passe ascendente) e dos bounds,
        // nunca de qual prédio está sendo posicionado agora — então o passe é memoizado por cidade e
        // só estendido quando entram prédios novos no fim. O chão devolvido é montado a partir das
        // posições já derivadas, sem repetir busca de lote / célula livre / anel de overflow por vizinho.
        var plan = PlanFor(world, city, bounds, siblings);

        // Nenhum vizinho excluído (o prédio ainda não está na lista e não há "futuro" sem posição):
        // o chão é exatamente o do plano. Devolvido sem cópia — todo chamador só lê, e usa
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
            return plan.Ground;

        var ground = new CityGround();
        for (int i = 0; i < siblings.Length; i++)
        {
            var building = siblings[i];
            if (building.Id.Value == selfId.Value) continue; // nunca o próprio prédio sendo posicionado
            if (building.Position is null && building.Id.Value > selfId.Value) continue; // "futuro" sem posição autorada

            // Escassez de terra pra este vizinho: não entra no conjunto (nunca um crash).
            if ((building.Position ?? plan.Derived[i]) is not { } position) continue;
            ground.Add(BuildingFootprintGenerator.Generate(building), position, building.CroftCells ?? plan.DerivedCroft[i], network, world.Map, layout);
        }
        return ground;
    }

    /// <summary>AD-173: até onde (células da caixa por população) o limite crescido encadeia a absorção; 0 = não encadeia.
    /// Ver <see cref="CityLayoutRules.ChainedBoundsReachCells"/>.</summary>
    internal static int ChainReach(WorldState world) =>
        world.CityRules.EffectiveLayout() is { Enabled: true } layout ? Math.Max(0, layout.ChainedBoundsReachCells) : 0;

    /// <summary>Alcance da porta até a rua: o maior quintal da frente mais a própria célula de rua.</summary>
    internal static int Reach(WorldState world) => world.CityRules.EffectiveLayout().MaxFrontYardCells() + 1;

    /// <summary>Casa (abriga família, sem ser local de trabalho) ou não: decide o lote que o motor escolhe quando não há
    /// família conhecida.</summary>
    internal static bool IsDwelling(WorldState world, int buildingTypeId) =>
        buildingTypeId == LivingWorld.Simulation.Population.PopulationSeeder.InitialHouseBuildingTypeId
        || (world.CityCatalog.BuildingRecipes.TryGetValue(buildingTypeId, out var recipe) && recipe.HousingCapacityProvided > 0 && recipe.Workplace is null);

    internal static LotProfile DerivedProfile(WorldState world, int buildingTypeId) =>
        IsDwelling(world, buildingTypeId) ? LotProfile.UnknownHousehold : LotProfile.Workshop;

    /// <summary>A colocação de um prédio novo (AD-173): do degrau <paramref name="firstLevel"/> ao <paramref name="lastLevel"/>,
    /// contra o chão de quem tem Id menor (e os autorados). Devolve a origem e o quintal escolhido.</summary>
    internal static (CellCoord Origin, int Croft, int Orientation)? FindPlacement(
        WorldState world, City city, CityBounds bounds, BuildingId id, int buildingTypeId, int orientation, LotProfile profile,
        int firstLevel, int lastLevel, bool chooseOrientation = false)
    {
        var ground = GroundOfCity(world, city, bounds, id);
        PlacementPlan? plan = null;
        if (Plans.TryGetValue(world, out var byCity) && byCity.TryGetValue(city.Id, out var cached)
            && cached.Bounds == bounds && cached.MapWidth == world.Map.Width && cached.MapHeight == world.Map.Height)
            plan = cached;
        return PlaceAcrossLevels(world, city, bounds, ground, plan, id, buildingTypeId, orientation, profile, firstLevel, lastLevel,
            writeScanMarks: false, chooseOrientation);
    }

    /// <summary>AD-184: até <paramref name="max"/> lotes de rua possíveis no degrau <paramref name="level"/>, do mais perto do largo
    /// para fora (a mesma ordem de <see cref="FindPlacement"/>), para quem constrói escolher entre eles pelo que pesa para ela
    /// (água, mercado, trabalho, foro). Só lê o chão: não marca o passe, então a busca de sempre não muda.</summary>
    internal static List<(CellCoord Origin, int Croft, int Orientation)> CandidateLots(
        WorldState world, City city, CityBounds bounds, BuildingId id, int buildingTypeId, int orientation, LotProfile profile,
        int level, int max, IReadOnlyList<CellCoord>? anchors = null)
    {
        var found = new List<(CellCoord Origin, int Croft, int Orientation)>();
        var network = CityStreets.Of(world, city);
        if (network.Count == 0 || level >= PlacementPolicy.StreetLevels) return found;
        var ground = Barred(world, city, network, GroundOfCity(world, city, bounds, id));
        var options = OptionsOf(id, buildingTypeId, orientation, choose: true);
        var layout = world.CityRules.EffectiveLayout();
        // um candidato por trecho da rede, espalhados do largo para fora (o primeiro é o lote de sempre)
        int perStreet = (layout.MaxFrontYardCells() + 1) * options.Length;
        int total = network.Count * perStreet;
        int stride = Math.Max(1, network.Count / Math.Max(1, max)) * perStreet;
        int ring = world.CityRules.AbsorptionRingCells; // a borda que a cidade absorve ao crescer (não vira aglomerado à parte)
        bool founds = world.CityRules.Enabled; // só onde aglomerado de fora funda cidade-filha
        void Sample(int[]? order, int limit)
        {
            for (int start = 0, taken = 0; start < total && taken < limit;)
            {
                if (LotPlanner.Find(ground, network, world.Map, layout, options, profile, level, start, out int at, order) is not { } lot) break;
                // só dentro da cidade de agora (e da borda que ela absorve): lote além vira aglomerado e funda cidade-filha
                if (founds && !NearBounds(bounds, lot.Origin, ring)) { start = Math.Max(at + 1, start + (order is null ? stride : perStreet)); continue; }
                if (!found.Exists(l => Math.Max(Math.Abs(l.Origin.X - lot.Origin.X), Math.Abs(l.Origin.Y - lot.Origin.Y)) < 3)) { found.Add(lot); taken++; }
                start = Math.Max(at + 1, start + (order is null ? stride : perStreet));
            }
        }
        Sample(null, max);
        // perto de cada lugar que pesa para quem constrói (o trabalho, a fonte): os lotes de rua mais perto dele primeiro
        foreach (var anchor in anchors ?? [])
        {
            var near = Enumerable.Range(0, network.Count)
                .OrderBy(i => Math.Max(Math.Abs(network.Cells[i].X - anchor.X), Math.Abs(network.Cells[i].Y - anchor.Y))).ThenBy(i => i).ToArray();
            Sample(near, Math.Max(1, max / 2));
        }
        return found;
    }

    /// <summary>AD-192 (08d 166, 182): o chão de quem grava a posição de um prédio novo, com o que ele não pode cobrir: o caminho
    /// pisado (trilha ou mais, passagem de todos) e o chão de outra cidade (o limite dela; a rua do traçado que cai ali fica cortada).
    /// O mesmo chão de sempre sem as chaves ou sem nada a barrar.</summary>
    internal static CityGround Barred(WorldState world, City city, StreetNetwork network, CityGround ground)
    {
        var rules = world.StreetRules;
        HashSet<CellCoord>? barred = null, cut = null;
        if (rules.LotsAvoidTrodden && world.StreetsOn)
        {
            var ledger = world.Ground!;
            for (int i = 0; i < ledger.Count; i++)
                if (ledger.SurfaceAt(i) != Streets.GroundSurface.None) (barred ??= []).Add(ledger.CellOf(i));
        }
        if (rules.CutAtNeighbourCity)
            foreach (var other in world.ActiveCities())
            {
                if (other.Id == city.Id) continue;
                var box = OwnGrowthBoundsIgnoringOtherCities(world, other).Bounds;
                for (int y = box.Origin.Y; y < box.Origin.Y + box.Height; y++)
                    for (int x = box.Origin.X; x < box.Origin.X + box.Width; x++)
                    {
                        var cell = new CellCoord(x, y);
                        (barred ??= []).Add(cell);
                        if (network.Contains(cell)) (cut ??= []).Add(cell);
                    }
            }
        return barred is null && cut is null ? ground : ground.WithBarred(barred ?? [], cut ?? []);
    }

    private static bool NearBounds(CityBounds bounds, CellCoord cell, int ring) =>
        cell.X >= bounds.Origin.X - ring && cell.Y >= bounds.Origin.Y - ring
        && cell.X < bounds.Origin.X + bounds.Width + ring && cell.Y < bounds.Origin.Y + bounds.Height + ring;

    /// <summary>As plantas que quem constrói pode escolher: a da identidade primeiro, depois as outras três rotações em
    /// ordem; só a da identidade quando a orientação não será gravada (prédio derivado).</summary>
    private static LotPlanner.Option[] OptionsOf(BuildingId id, int buildingTypeId, int orientation, bool choose)
    {
        if (!choose) return [new LotPlanner.Option(orientation, BuildingFootprintGenerator.Generate(id, buildingTypeId, orientation))];
        var options = new LotPlanner.Option[4];
        for (int r = 0; r < 4; r++)
        {
            int o = (orientation + r * 90) % 360;
            options[r] = new LotPlanner.Option(o, BuildingFootprintGenerator.Generate(id, buildingTypeId, o));
        }
        return options;
    }

    private static (CellCoord Origin, int Croft, int Orientation)? FindLot(
        WorldState world, CityGround ground, PlacementPlan? plan, StreetNetwork network, CityLayoutRules layout,
        LotPlanner.Option[] options, LotProfile profile, BuildingId id, int level, out int depth)
    {
        // De frente para a rua, ou mais perto do largo com um caminho a pé curto até ela: fica o mais perto do largo
        // (empate: o de frente para a rua). A distância é Chebyshev da origem do lote ao ponto de fundação.
        ulong key = LotPlanner.Key(options, layout, profile, level);
        int start = plan?.LotStartFor(key, id.Value) ?? 0;
        var lot = LotPlanner.Find(ground, network, world.Map, layout, options, profile, level, start, out int found);
        plan?.MarkLot(key, id.Value, found);
        ulong pathKey = key * 0x9E3779B97F4A7C15UL + 1;
        int pathStart = plan?.LotStartFor(pathKey, id.Value) ?? 0;
        var viaPath = LotPlanner.FindViaPath(ground, network, world.Map, layout, options, profile, level, pathStart, out int pathFound);
        plan?.MarkLot(pathKey, id.Value, pathFound);
        int Distance((CellCoord Origin, int Croft, int Orientation)? l) =>
            l is { } v ? Math.Max(Math.Abs(v.Origin.X - network.Center.X), Math.Abs(v.Origin.Y - network.Center.Y)) : int.MaxValue;
        if (Distance(viaPath) < Distance(lot)) lot = viaPath;
        depth = Distance(lot);
        return lot;
    }

    private static (CellCoord Origin, int Croft, int Orientation)? PlaceAcrossLevels(
        WorldState world, City city, CityBounds bounds, CityGround ground, PlacementPlan? plan, BuildingId id, int buildingTypeId,
        int orientation, LotProfile profile, int firstLevel, int lastLevel, bool writeScanMarks, bool chooseOrientation = false)
    {
        var options = OptionsOf(id, buildingTypeId, orientation, chooseOrientation);
        var footprint = options[0].Footprint;
        var layout = world.CityRules.EffectiveLayout();
        var network = CityStreets.Of(world, city);
        if (chooseOrientation)
        {
            // AD-192: quem grava a posição não cobre caminho pisado nem chão de outra cidade; essa busca não marca o passe (os marcos
            // valem só para o chão que só cresce, o das posições derivadas)
            var barred = Barred(world, city, network, ground);
            if (!ReferenceEquals(barred, ground)) { ground = barred; plan = null; }
        }
        List<CellCoord>? shape = null;
        CellCoord? doorstep = null;
        for (int level = Math.Max(0, firstLevel); level <= Math.Min(lastLevel, PlacementPolicy.LevelCount - 1); level++)
        {
            if (level < PlacementPolicy.StreetLevels)
            {
                if (network.Count == 0) continue;
                var lot = FindLot(world, ground, plan, network, layout, options, profile, id, level, out int depth);
                // O quintal é escolha, não exigência: a família só se afasta CroftDetourCells células a mais do largo por ele.
                if (lot is { Croft: > 0 } && level == 0 && lastLevel >= 1
                    && FindLot(world, ground, plan, network, layout, options, profile, id, 1, out int bareDepth) is { } bare
                    && bareDepth + layout.CroftDetourCells < depth)
                    return bare;
                if (lot is not null) return lot;
                continue;
            }

            shape ??= footprint.Select(c => c.Cell).ToList();
            if (doorstep is null && network.Count > 0)
            {
                var (door, facing) = LotGeometry.DoorOf(footprint);
                doorstep = new CellCoord(door.X + facing.X, door.Y + facing.Y);
            }
            int loose = level - PlacementPolicy.StreetLevels;
            var policy = PlacementPolicy.For(world.Map, loose);
            var guard = loose < 2 && network.Count > 0 ? new GroundGuard(network.Set, ground.Passages, doorstep, world.Map.Width, world.Map.Height)
                : doorstep is { } step && world.StreetsOn ? GroundGuard.DoorOnMap(step, world.Map.Width, world.Map.Height) : null;
            ulong shapeHash = ShapeHash(shape, level);
            var position = ScanForFreeOriginFrom(
                ground.Occupied, bounds, shape, policy, plan?.ScanStartFor(shapeHash, id.Value) ?? 0, out int scanIndex, guard);
            if (writeScanMarks) plan?.MarkScan(shapeHash, id.Value, scanIndex);
            if (position is null)
            {
                var shapeKey = (buildingTypeId, orientation, level);
                position = OverflowPlacer.ResolveOverflowPositionGiven(
                    ground.Occupied, bounds, id, shape, world.Map.Width, world.Map.Height,
                    plan?.StartRadiusFor(shapeKey, id.Value) ?? 1, out int foundRadius, policy, guard);
                plan?.Mark(shapeKey, id.Value, foundRadius);
            }
            if (position is { } p) return (p, 0, orientation);
        }
        return null;
    }

    private sealed class PlacementPlan
    {
        public CityBounds Bounds;
        public int MapWidth, MapHeight;
        public long[] Ids = [];
        public CellCoord?[] Derived = [];
        /// <summary>Quintal escolhido por cada prédio derivado (AD-173); 0 para os autorados (vale o deles).</summary>
        public int[] DerivedCroft = [];
        public CityGround Ground = new();
        public HashSet<CellCoord> Occupied => Ground.Occupied;
        /// <summary>Caixas de footprint dos prédios da cidade para estes bounds; descartadas quando o plano muda.</summary>
        public List<(Building Building, CityBounds Box)>? Boxes;
        /// <summary><see cref="Building.LayoutVersion"/> em que <see cref="Boxes"/> foi calculado.</summary>
        public long BoxesLayoutVersion;
        /// <summary>Quantos prédios o mundo tinha quando <see cref="Boxes"/> foi calculado.</summary>
        public int BoxesWorldCount;
        /// <summary>Algum prédio do plano não tinha posição autorada (a posição vem da busca, não do dado).</summary>
        public bool HasDerived;
        /// <summary>Por forma de prédio (tipo, orientação): marcos (id do passo, raio) — no passo desse id
        /// todo anel de raio menor já estava inteiramente ocupado para a forma. A ocupação só cresce ao
        /// longo do passe, então o marco continua valendo para qualquer resolução de id maior ou igual
        /// (cuja ocupação é superconjunto da do passo). Raios crescem junto com os ids.</summary>
        public readonly Dictionary<(int Type, int Orientation, int Level), List<(long Id, int Radius)>> OverflowMarks = new();

        /// <summary>Por forma: marcos (id do passo, índice de varredura) — todo índice menor já falhou para a forma
        /// naquele passo, e a ocupação só cresce ao longo do passe, então continua falhando para ids maiores.
        /// Índice == área dos bounds significa varredura esgotada (cai direto no overflow).</summary>
        private readonly Dictionary<ulong, List<(long Id, int Index)>> _scanMarks = new();

        /// <summary>AD-173: marcos da busca de lote na rua, mesma lógica dos de varredura (o chão só cresce no passe), por
        /// planta, termos do lote e degrau. Não dependem dos bounds.</summary>
        private readonly Dictionary<ulong, List<(long Id, int Index)>> _lotMarks = new();

        public int LotStartFor(ulong key, long id)
        {
            if (!_lotMarks.TryGetValue(key, out var marks)) return 0;
            for (int i = marks.Count - 1; i >= 0; i--)
                if (marks[i].Id <= id) return marks[i].Index;
            return 0;
        }

        public void MarkLot(ulong key, long id, int index)
        {
            if (!_lotMarks.TryGetValue(key, out var marks))
                _lotMarks[key] = marks = new();
            if (marks.Count == 0 || index > marks[^1].Index)
                marks.Add((id, index));
        }

        public int ScanStartFor(ulong shape, long id)
        {
            if (!_scanMarks.TryGetValue(shape, out var marks)) return 0;
            for (int i = marks.Count - 1; i >= 0; i--)
                if (marks[i].Id <= id) return marks[i].Index;
            return 0;
        }

        public void MarkScan(ulong shape, long id, int index)
        {
            if (!_scanMarks.TryGetValue(shape, out var marks))
                _scanMarks[shape] = marks = new();
            if (marks.Count == 0 || index > marks[^1].Index)
                marks.Add((id, index));
        }

        /// <summary>Bounds novos: os marcos de varredura (índices dos bounds antigos) caem, mas os de anel
        /// sobrevivem encolhidos. Um anel de raio r' em torno dos bounds novos fica a no máximo r' + E de
        /// distância dos bounds antigos (E = quanto os novos passam dos antigos em algum lado), então todo
        /// anel de raio menor que (r − E) continua inteiramente ocupado.</summary>
        public void ShiftMarksToBounds(CityBounds oldBounds, CityBounds newBounds)
        {
            _scanMarks.Clear();
            int exceed = Math.Max(0, Math.Max(
                Math.Max(oldBounds.Origin.X - newBounds.Origin.X, oldBounds.Origin.Y - newBounds.Origin.Y),
                Math.Max(
                    newBounds.Origin.X + newBounds.Width - (oldBounds.Origin.X + oldBounds.Width),
                    newBounds.Origin.Y + newBounds.Height - (oldBounds.Origin.Y + oldBounds.Height))));
            foreach (var key in OverflowMarks.Keys.ToList())
            {
                var shifted = new List<(long Id, int Radius)>();
                foreach (var (id, radius) in OverflowMarks[key])
                {
                    int next = Math.Max(1, radius - exceed);
                    if (shifted.Count == 0 || next > shifted[^1].Radius) shifted.Add((id, next));
                }
                OverflowMarks[key] = shifted;
            }
        }

        public int StartRadiusFor((int Type, int Orientation, int Level) shapeKey, long id)
        {
            if (!OverflowMarks.TryGetValue(shapeKey, out var marks)) return 1;
            for (int i = marks.Count - 1; i >= 0; i--)
                if (marks[i].Id <= id) return marks[i].Radius;
            return 1;
        }

        public void Mark((int Type, int Orientation, int Level) shapeKey, long id, int radius)
        {
            if (!OverflowMarks.TryGetValue(shapeKey, out var marks))
                OverflowMarks[shapeKey] = marks = new();
            if (marks.Count == 0 || radius > marks[^1].Radius)
                marks.Add((id, radius));
        }
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<
        WorldState, Dictionary<CityId, PlacementPlan>> Plans = new();

    private sealed class SiblingCache
    {
        public Building[] Siblings = [];
        public int WorldCount;
        public long LayoutVersion = -1;
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<
        WorldState, Dictionary<CityId, SiblingCache>> SiblingCaches = new();

    /// <summary>Prédios da cidade ordenados por Id. Os prédios só são acrescentados ao mundo, e qualquer
    /// troca de cidade ou prédio de outro mundo move <see cref="Building.LayoutVersion"/> além do número
    /// de acréscimos; então, quando a versão andou exatamente o que a lista cresceu e os novos têm Id
    /// maior que o último, basta anexar os novos da cidade. Qualquer outro caso refaz a ordenação.</summary>
    private static Building[] SiblingsOf(WorldState world, City city)
    {
        var byCity = SiblingCaches.GetOrCreateValue(world);
        if (!byCity.TryGetValue(city.Id, out var cache))
            byCity[city.Id] = cache = new SiblingCache();

        var all = world.Buildings;
        long version = Building.LayoutVersion;
        if (cache.LayoutVersion == version && cache.WorldCount == all.Count)
            return cache.Siblings;

        int added = all.Count - cache.WorldCount;
        if (cache.LayoutVersion >= 0 && added > 0 && version - cache.LayoutVersion == added)
        {
            long lastId = cache.Siblings.Length == 0 ? long.MinValue : cache.Siblings[^1].Id.Value;
            var extra = new List<Building>();
            bool ordered = true;
            for (int i = cache.WorldCount; i < all.Count; i++)
            {
                var building = all[i];
                if (building.City != city.Id) continue;
                if (building.Id.Value <= lastId) { ordered = false; break; }
                lastId = building.Id.Value;
                extra.Add(building);
            }

            if (ordered)
            {
                var merged = new Building[cache.Siblings.Length + extra.Count];
                cache.Siblings.CopyTo(merged, 0);
                extra.CopyTo(merged, cache.Siblings.Length);
                cache.Siblings = merged;
                cache.WorldCount = all.Count;
                cache.LayoutVersion = version;
                return merged;
            }
        }

        cache.Siblings = all.Where(b => b.City == city.Id).OrderBy(b => b.Id.Value).ToArray();
        cache.WorldCount = all.Count;
        cache.LayoutVersion = version;
        return cache.Siblings;
    }

    /// <summary>Passe ascendente por Id sobre os prédios da cidade: cada prédio sem posição autorada
    /// ocupa a primeira célula livre dos bounds, senão a do anel de overflow, contra o que os de Id
    /// menor já ocupam. Reaproveita o passe anterior quando só entraram prédios no fim da lista.</summary>
    private static PlacementPlan PlanFor(WorldState world, City city, CityBounds bounds, Building[] siblings)
    {
        var byCity = Plans.GetOrCreateValue(world);
        byCity.TryGetValue(city.Id, out var plan);

        int reusable = 0;
        // Com todos os prédios do plano autorados, a ocupação não depende dos bounds (só as posições
        // derivadas dependem): o plano é reaproveitado sob bounds novos, só descartando os marcos de anel,
        // que valem para uns bounds específicos. Sem isso, alternar entre os bounds por população e os
        // bounds crescidos refazia o passe inteiro a cada chamada.
        if (plan is not null && plan.MapWidth == world.Map.Width && plan.MapHeight == world.Map.Height
            && (plan.Bounds == bounds || !plan.HasDerived))
        {
            if (plan.Bounds != bounds)
            {
                plan.ShiftMarksToBounds(plan.Bounds, bounds);
                plan.Bounds = bounds;
            }

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
        var derivedCroft = new int[siblings.Length];
        Array.Copy(plan.Ids, ids, start);
        Array.Copy(plan.Derived, derived, start);
        Array.Copy(plan.DerivedCroft, derivedCroft, start);

        for (int i = start; i < siblings.Length; i++)
        {
            var building = siblings[i];
            ids[i] = building.Id.Value;
            CellCoord? position;
            int croft = building.CroftCells ?? 0;
            var footprint = BuildingFootprintGenerator.Generate(building);
            if (building.Position is { } authored)
                position = authored;
            else
            {
                // Degraus de relaxamento (ver PlacementPolicy.LevelCount): só desce quando o degrau
                // anterior não achou terra em lugar nenhum do mapa.
                int orientation = building.Orientation ?? BuildingFootprintGenerator.DerivedOrientation(building.Id, building.BuildingTypeId);
                var lot = PlaceAcrossLevels(world, city, bounds, plan.Ground, plan, building.Id, building.BuildingTypeId, orientation,
                    DerivedProfile(world, building.BuildingTypeId), 0, PlacementPolicy.LevelCount - 1, writeScanMarks: true);
                position = lot?.Origin;
                croft = lot?.Croft ?? 0;
                derived[i] = position;
                derivedCroft[i] = croft;
                plan.HasDerived = true;
            }

            if (position is { } resolved)
                plan.Ground.Add(footprint, resolved, croft, CityStreets.Of(world, city), world.Map, world.CityRules.EffectiveLayout());
        }

        plan.Ids = ids;
        plan.Derived = derived;
        plan.DerivedCroft = derivedCroft;
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
