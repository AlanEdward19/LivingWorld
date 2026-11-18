using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Geography.Map;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cities.Layout;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Cities;

/// <summary>Ruas das cidades (AD-173): derivadas do mapa, do ponto de fundação e das regras, guardadas por mapa (nada no
/// snapshot). A rua não recebe prédio; a porta de cada prédio dá para ela.</summary>
public static class CityStreets
{
    private static readonly ConditionalWeakTable<WorldMap, ConcurrentDictionary<(CellCoord, CityLayoutRules), StreetNetwork>> ByMap = new();

    internal static StreetNetwork For(WorldMap map, CellCoord center, CityLayoutRules rules)
    {
        if (!rules.Enabled) return StreetNetwork.Empty;
        return ByMap.GetOrCreateValue(map).GetOrAdd((center, rules), key => StreetNetwork.Build(map, key.Item1, key.Item2));
    }

    internal static StreetNetwork Of(WorldState world, City city) => For(world.Map, city.Location, world.CityRules.EffectiveLayout());

    /// <summary>Todas as células de rua (traçadas, usadas ou não) das cidades ativas.</summary>
    public static HashSet<CellCoord> AllStreetCells(WorldState world)
    {
        var all = new HashSet<CellCoord>();
        foreach (var city in world.ActiveCities())
            all.UnionWith(Of(world, city).Set);
        return all;
    }

    /// <summary>Prédios da cidade com origem e quintal: a posição gravada, senão a que o motor deriva para os bounds por
    /// população (o mesmo caminho das caixas de <see cref="CityOccupancy.OwnedBuildingFootprintBoxes"/>).</summary>
    public static IReadOnlyList<(Building Building, CellCoord Origin, int Croft)> Placed(WorldState world, City city)
    {
        long population = Queries.CityPopulationQuery.Population(world, city.Id);
        var (bounds, _) = LivingWorld.Domain.Cities.Spatial.SpatialBoundsResolver.ResolveCity(city, population, world.Map.Width, world.Map.Height);
        var placed = new List<(Building, CellCoord, int)>();
        foreach (var building in world.Buildings.Where(b => b.City == city.Id).OrderBy(b => b.Id.Value))
            if (Construction.BuildingPlacementResolver.ResolveLot(building, city, world, bounds) is { } lot)
                placed.Add((building, lot.Position, lot.Croft));
        return placed;
    }

    /// <summary>As ruas que existem de fato (AD-176): as células do chão que o pisoteio fez trilha ou terra batida, as calçadas
    /// (cascalho, pedra) e as que estão em obra, da cidade mais perto (o limite crescido dela). <c>Users</c> são as passagens do
    /// último mês, <c>UseBand</c> a faixa delas, <c>Planned</c> se a célula é do traçado reservado (AD-173: largo, principal,
    /// travessas), que agora é só onde não se constrói. Com a rua de verdade desligada, o uso derivado de antes (AD-173).</summary>
    public static IReadOnlyList<StreetCellView> RoadsOf(WorldState world, City city)
    {
        if (!world.StreetsOn) return PlannedRoadsOf(world, city);
        return Streets.StreetViews.ByCity(world).GetValueOrDefault(city.Id) ?? [];
    }

    /// <summary>AD-173 (rua desligada): o largo e, para cada prédio cuja porta dá numa rua, o caminho dessa célula até o largo,
    /// com quantas pessoas passam por ela: trilha até <see cref="CityLayoutRules.TrodResidents"/>, terra batida daí para cima.</summary>
    private static IReadOnlyList<StreetCellView> PlannedRoadsOf(WorldState world, City city)
    {
        var network = Of(world, city);
        if (network.Count == 0) return [];
        var users = new int[network.Count];
        var used = new bool[network.Count];
        for (int i = 0; i < network.Count; i++)
            if (network.InSquare(i)) used[i] = true;

        var people = new Dictionary<BuildingId, int>();
        foreach (var household in world.Households)
            if (household.City == city.Id && household.Dwelling is { } home)
                people[home] = people.GetValueOrDefault(home) + household.Members.Count;
        foreach (var workplace in world.Workplaces)
            if (workplace.City == city.Id && workplace.Building is { } site)
                people[site] = people.GetValueOrDefault(site) + workplace.Employees.Count;

        foreach (var (building, origin, croft) in Placed(world, city))
        {
            if (LotOf(world, city, building, origin, croft).Street is not { } street) continue;
            int count = people.GetValueOrDefault(building.Id);
            for (int i = network.IndexOf(street); i >= 0; i = network.Parent[i])
            {
                used[i] = true;
                users[i] += count;
            }
        }

        int trod = world.CityRules.EffectiveLayout().TrodResidents;
        var result = new List<StreetCellView>();
        for (int i = 0; i < network.Count; i++)
            if (used[i])
                result.Add(new StreetCellView(
                    city.Id, network.Cells[i], users[i] >= trod ? StreetSurface.Dirt : StreetSurface.Trail, users[i],
                    network.IsMain[i], network.InSquare(i))
                { Planned = true });
        return result;
    }

    /// <summary>Passagens (porta → rua) de todos os prédios da cidade, do mesmo chão que decide a colocação.</summary>
    internal static HashSet<CellCoord> PassagesOf(WorldState world, City city)
    {
        long population = Queries.CityPopulationQuery.Population(world, city.Id);
        var (bounds, _) = LivingWorld.Domain.Cities.Spatial.SpatialBoundsResolver.ResolveCity(city, population, world.Map.Width, world.Map.Height);
        return CityOccupancy.GroundOfCity(world, city, bounds, new BuildingId(long.MaxValue)).Passages;
    }

    /// <summary>O lote de um prédio (AD-173): quintal de trás, passagem da porta até a rua e a célula de rua da frente.</summary>
    public static LotView LotOf(WorldState world, City city, Building building, CellCoord origin, int croft)
    {
        var network = Of(world, city);
        var footprint = BuildingFootprintGenerator.Generate(building);
        var cells = footprint.Select(c => c.Cell).ToArray();
        var (door, facing) = LotGeometry.DoorOf(footprint);
        var croftCells = LotGeometry.CroftOf(cells, cells.ToHashSet(), facing, croft)
            .Select(c => new CellCoord(c.X + origin.X, c.Y + origin.Y)).ToList();
        var path = new List<CellCoord>();
        CellCoord? street = null;
        int reach = CityOccupancy.Reach(world);
        for (int j = 1; j <= reach; j++)
        {
            var cell = new CellCoord(origin.X + door.X + facing.X * j, origin.Y + door.Y + facing.Y * j);
            if (network.Contains(cell)) { street = cell; break; }
            path.Add(cell);
        }
        if (street is null)
        {
            // Lote de caminho: as passagens do chão da cidade ligadas à soleira, até a rua (o caminho que a obra abriu).
            var doorstep = path[0];
            path.Clear();
            var passages = PassagesOf(world, city);
            var seen = new HashSet<CellCoord> { doorstep };
            var queue = new Queue<CellCoord>([doorstep]);
            while (queue.Count > 0 && street is null)
            {
                var at = queue.Dequeue();
                path.Add(at);
                foreach (var n in (ReadOnlySpan<CellCoord>)[new(at.X, at.Y + 1), new(at.X + 1, at.Y), new(at.X, at.Y - 1), new(at.X - 1, at.Y)])
                {
                    if (network.Contains(n)) { street = n; break; }
                    if (passages.Contains(n) && seen.Add(n)) queue.Enqueue(n);
                }
            }
            if (street is null) path.RemoveRange(1, path.Count - 1);
        }
        var access = Streets.StreetRoutes.AccessOf(world, building, network.Set);
        if (Streets.StreetRoutes.AccessGeometryOf(world, building) is not { } geometry)
            return new LotView(croftCells, path, street) { Access = access };
        // AD-185: o acesso desemboca no traçado da cidade ainda sem uso (reserva), no traçado em uso, ou num caminho do pisoteio fora dele
        string? state = geometry.Outlet is { } outlet ? (network.Contains(outlet) ? "planned" : "trodden")
            : street is not null ? "reserved" : null; // sem rede de caminho nenhuma: a rua da frente é só reserva
        return new LotView(croftCells, path, street)
        {
            Access = access,
            AccessCells = geometry.Cells,
            AccessViaBuildingId = geometry.Via,
            AccessState = state,
        };
    }
}

/// <summary>Superfície da rua (AD-173), da mais pobre à mais cara. O motor hoje só produz trilha e terra batida.</summary>
public enum StreetSurface { Trail, Dirt, Gravel, Paved }

/// <summary>Uma célula de rua em uso: cidade, célula, superfície, quantas pessoas passam, se é a rua principal e se é o largo.
/// AD-176 (só acréscimo): pisoteio acumulado, faixa de uso, se é do traçado reservado, condição do calçamento e obra em curso.</summary>
public sealed record StreetCellView(CityId City, CellCoord Cell, StreetSurface Surface, int Users, bool Main, bool Square)
{
    /// <summary>Pisoteio acumulado (passagens que ainda não se desfizeram).</summary>
    public int Wear { get; init; }

    /// <summary>Faixa das passagens do último mês: 0 (&lt;10), 1 (&lt;30), 2 (&lt;100), 3 (&lt;300), 4 (300 ou mais).</summary>
    public int UseBand { get; init; }

    /// <summary>A célula é do traçado reservado da cidade (largo, principal, travessa ou viela planejada).</summary>
    public bool Planned { get; init; }

    /// <summary>Condição do calçamento (0–1000); nulo em trilha e terra batida.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public int? Condition { get; init; }

    /// <summary>Obra de calçamento em curso nesta célula; nulo sem obra.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public StreetWorkView? Work { get; init; }

    /// <summary>AD-180: o chão da célula pelo tempo — <c>dry</c>, <c>wet</c>, <c>mud</c>, <c>heavy-mud</c>, <c>snow</c>, <c>frozen</c>
    /// (cascalho e pedra só molham: nunca lama); nulo com o tempo desligado.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? Ground { get; init; }

    /// <summary>AD-180: neve no chão (cm) e terra molhada (0–1000) da célula; nulos com o tempo desligado.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public int? SnowCm { get; init; }

    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public int? WetPm { get; init; }

    /// <summary>AD-180: a célula com o tempo do dia (o mesmo que o passo lê: lama na terra, neve em tudo).</summary>
    public StreetCellView WithWeather(WorldState world)
    {
        if (Climate.WeatherNow.RowAt(world, Cell) is not { } row) return this;
        var rules = world.ClimateRules.Weather!;
        var ground = Climate.WeatherNow.Ground(rules, row);
        bool paved = Surface is StreetSurface.Gravel or StreetSurface.Paved;
        if (paved && ground is Climate.GroundWeather.Mud or Climate.GroundWeather.HeavyMud) ground = Climate.GroundWeather.Wet;
        string name = ground switch
        {
            Climate.GroundWeather.Wet => "wet",
            Climate.GroundWeather.Mud => "mud",
            Climate.GroundWeather.HeavyMud => "heavy-mud",
            Climate.GroundWeather.Snow => "snow",
            Climate.GroundWeather.Frozen => "frozen",
            _ => "dry",
        };
        return this with { Ground = name, SnowCm = row.SnowMm, WetPm = row.SoilPm };
    }
}

/// <summary>Obra de calçamento numa célula (AD-176): o que vira, quanto já foi feito (0–100), se é conserto e quem paga
/// (<c>treasury</c> = tesouro da cidade, <c>frontage</c> = as casas de frente).</summary>
public sealed record StreetWorkView(StreetSurface Surface, int ProgressPct, bool Repair, string Payer)
{
    /// <summary>AD-179: id da obra (o mesmo do payload dos eventos de obra).</summary>
    public long WorkId { get; init; }

    /// <summary>AD-179: diaristas da turma da obra (ids de NPC); muda no máximo uma vez por dia.</summary>
    public IReadOnlyList<long> WorkerIds { get; init; } = [];
}

/// <summary>Lote de um prédio: células do quintal de trás (cercado), passagem da porta à rua e a rua da frente (nula se não dá
/// para rua: prédio de degrau sem rua).</summary>
public sealed record LotView(IReadOnlyList<CellCoord> Croft, IReadOnlyList<CellCoord> FrontPath, CellCoord? Street)
{
    /// <summary>AD-176: como se chega à porta — <c>street</c>, <c>path</c>, <c>alley</c> (viela), <c>stair</c> (escada do sobrado
    /// vizinho), <c>roof</c> (laje ou telhado do vizinho), <c>none</c>; nulo sem a rua de verdade.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? Access { get; init; }

    /// <summary>AD-185: a geometria do acesso — o que se anda da soleira até a primeira célula de caminho, em ordem (soleira primeiro;
    /// na escada, laje ou telhado a soleira é célula do vizinho). Vazia sem a rua de verdade ou com a porta para fora do mapa.</summary>
    public IReadOnlyList<CellCoord> AccessCells { get; init; } = [];

    /// <summary>AD-185: o prédio vizinho por onde se chega à porta (escada do sobrado, laje ou telhado); nulo nos outros acessos.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public long? AccessViaBuildingId { get; init; }

    /// <summary>AD-185: onde o acesso desemboca — <c>reserved</c> (rua do traçado da cidade ainda sem uso: só reserva de espaço),
    /// <c>planned</c> (rua do traçado já pisada ou calçada), <c>trodden</c> (caminho aberto pelo pisoteio fora do traçado); nulo sem a
    /// rua de verdade.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? AccessState { get; init; }
}
