using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Production;

namespace LivingWorld.Simulation.Cities;

/// <summary>#223: onde cada trabalhador fica enquanto trabalha, pelo ofício e pelo lugar (nada de pilha no mesmo ladrilho). Quem
/// trabalha na lavoura fica nas células de cultivo em volta do prédio da fazenda (o campo, fora da planta); nos outros ofícios, nas
/// células de piso do térreo e, quando enchem, nas do chão em volta. Cada célula comporta <see cref="LivingWorld.Domain.Population.PlayerRules.CellCrowd"/> pessoas;
/// a escolha é a célula menos cheia (a mais perto no empate) e só depende da ordem dos NPCs, sem RNG. Só lê o mundo, nunca guarda.</summary>
internal static class WorkStands
{
    /// <summary>Até onde, em células a partir da planta, o campo e o chão em volta alcançam.</summary>
    internal const int MaxRing = 8;

    /// <summary>A lavoura (o ofício que cultiva): a oficina do tipo fazenda.</summary>
    internal static bool IsField(Workplace workplace) => workplace.LocationType.Id == CropSystem.DefaultFarmLocationTypeId;

    /// <summary>Derivado e descartável: vale enquanto o conjunto de prédios do mundo não muda (nunca persistido nem lido pelo hash).</summary>
    private sealed class Derived(long version, int buildings)
    {
        public long Version { get; } = version;
        public int Buildings { get; } = buildings;
        public HashSet<CellCoord>? Blocked { get; set; }
        public Dictionary<(long Building, int Wanted), CellCoord[]> Stands { get; } = [];
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<WorldState, Derived> Cache = new();

    private static Derived DerivedOf(WorldState world)
    {
        long version = Building.LayoutVersion;
        if (Cache.TryGetValue(world, out var known) && known.Version == version && known.Buildings == world.Buildings.Count) return known;
        var fresh = new Derived(version, world.Buildings.Count);
        Cache.AddOrUpdate(world, fresh);
        return fresh;
    }

    /// <summary>Postos do prédio (<see cref="Of"/>), guardados enquanto o conjunto de prédios não muda; a lotação pedida entra por
    /// faixas de 8 pessoas, para a lista não mudar a cada contratação.</summary>
    internal static CellCoord[] StandsOf(WorldState world, Building building, Workplace workplace)
    {
        var derived = DerivedOf(world);
        int wanted = (workplace.Employees.Count + 7) / 8 * 8;
        if (derived.Stands.TryGetValue((building.Id.Value, wanted), out var known)) return known;
        derived.Blocked ??= BlockedCells(world);
        return derived.Stands[(building.Id.Value, wanted)] = Of(world, building, workplace, derived.Blocked, wanted);
    }

    /// <summary>Células (mundo) cobertas por planta de prédio: ali não é campo.</summary>
    internal static HashSet<CellCoord> BlockedCells(WorldState world)
    {
        var blocked = new HashSet<CellCoord>();
        foreach (var building in world.Buildings)
        {
            if (building.Position is not { } origin) continue;
            foreach (var cell in BuildingFootprintGenerator.Generate(building))
                blocked.Add(new CellCoord(origin.X + cell.Cell.X, origin.Y + cell.Cell.Y));
        }

        return blocked;
    }

    /// <summary>Postos do prédio em coordenadas do mundo, do mais perto ao mais longe: o campo (lavoura) ou o piso do térreo e depois
    /// o chão em volta. Há células até caber <paramref name="wanted"/> pessoas na lotação, ou até <see cref="MaxRing"/>.</summary>
    internal static CellCoord[] Of(WorldState world, Building building, Workplace workplace, HashSet<CellCoord> blocked, int wanted)
    {
        if (building.Position is not { } origin) return [];
        int crowd = Math.Max(1, world.Players.CellCrowd);
        var footprint = BuildingFootprintGenerator.Generate(building);
        var result = new List<CellCoord>();
        if (!IsField(workplace))
            foreach (var cell in BuildingLayoutCache.Of(building).WalkableByFloor[0])
                result.Add(new CellCoord(origin.X + cell.Cell.X, origin.Y + cell.Cell.Y));

        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        foreach (var cell in footprint)
        {
            int x = origin.X + cell.Cell.X, y = origin.Y + cell.Cell.Y;
            minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
            minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
        }

        var streets = StreetCellsNear(world, building);
        var onStreet = new List<CellCoord>();
        for (int ring = 1; ring <= MaxRing; ring++)
        {
            for (int y = minY - ring; y <= maxY + ring; y++)
                for (int x = minX - ring; x <= maxX + ring; x++)
                {
                    if (x > minX - ring && x < maxX + ring && y > minY - ring && y < maxY + ring) continue; // só o anel
                    var cell = new CellCoord(x, y);
                    if (blocked.Contains(cell) || !world.Map.TryGetCell(cell, out var terrain) || terrain.HasWater) continue;
                    if (streets is not null && streets.Contains(cell)) onStreet.Add(cell);
                    else result.Add(cell);
                }

            if ((long)result.Count * crowd >= wanted) break;
        }

        // rua só como último recurso (ninguém fica no meio da passagem enquanto sobra campo)
        if ((long)result.Count * crowd < wanted) result.AddRange(onStreet);
        return [.. result];
    }

    /// <summary>A célula menos cheia (a mais perto no empate) com vaga na lotação; nula se todas estão cheias.</summary>
    internal static CellCoord? Pick(WorldState world, CellCoord[] stands, Dictionary<CellCoord, int> crowd)
    {
        int limit = Math.Max(1, world.Players.CellCrowd);
        CellCoord? best = null;
        int bestCount = int.MaxValue;
        for (int i = 0; i < stands.Length; i++)
        {
            int count = crowd.GetValueOrDefault(stands[i]);
            if (count >= limit || count >= bestCount) continue;
            best = stands[i];
            bestCount = count;
            if (count == 0) break;
        }

        return best;
    }

    private static HashSet<CellCoord>? StreetCellsNear(WorldState world, Building building)
    {
        var city = world.FindActiveCity(building.City);
        return city is null ? null : CityStreets.Of(world, city).Set;
    }
}
