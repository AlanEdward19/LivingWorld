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

    /// <summary>As ruas que existem de fato (AD-173): o largo e, para cada prédio cuja porta dá numa rua, o caminho dessa
    /// célula até o largo, com quantas pessoas (moradores e quem trabalha ali) passam por cada célula e a superfície que
    /// esse uso dá: trilha até <see cref="CityLayoutRules.TrodResidents"/>, terra batida a partir daí. Cascalho e pedra
    /// pedem uma técnica de calçar que o motor ainda não tem (dívida no 08c). Rua traçada sem uso não aparece.</summary>
    public static IReadOnlyList<StreetCellView> RoadsOf(WorldState world, City city)
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

        int reach = CityOccupancy.Reach(world);
        foreach (var (building, origin, _) in Placed(world, city))
        {
            if (LotGeometry.FrontStreetCell(network, BuildingFootprintGenerator.Generate(building), origin, reach) is not { } street) continue;
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
                    network.IsMain[i], network.InSquare(i)));
        return result;
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
        if (street is null && path.Count > 1) path.RemoveRange(1, path.Count - 1);
        return new LotView(croftCells, path, street);
    }
}

/// <summary>Superfície da rua (AD-173), da mais pobre à mais cara. O motor hoje só produz trilha e terra batida.</summary>
public enum StreetSurface { Trail, Dirt, Gravel, Paved }

/// <summary>Uma célula de rua em uso: cidade, célula, superfície, quantas pessoas passam, se é a rua principal e se é o largo.</summary>
public sealed record StreetCellView(CityId City, CellCoord Cell, StreetSurface Surface, int Users, bool Main, bool Square);

/// <summary>Lote de um prédio: células do quintal de trás (cercado), passagem da porta à rua e a rua da frente (nula se não dá
/// para rua: prédio de degrau sem rua).</summary>
public sealed record LotView(IReadOnlyList<CellCoord> Croft, IReadOnlyList<CellCoord> FrontPath, CellCoord? Street);
