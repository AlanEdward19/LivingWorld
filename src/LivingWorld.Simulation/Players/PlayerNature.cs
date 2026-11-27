using System.Runtime.CompilerServices;
using LivingWorld.Domain.Cities.Spatial;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Geography.Map;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Players;

/// <summary>#239: onde há mata para o corpo conduzido buscar material, pela mesma leitura dos NPCs da obra (<c>WorkHaul.WoodsFor</c>):
/// a célula do mapa que tem o recurso; num mapa que não traz o recurso em lugar nenhum, a terra fora das cidades (a "borda" de onde os
/// NPCs cortam). Só lê; o limite das cidades e a pergunta "o mapa tem o recurso" ficam em memória por hora de mundo (função pura do mundo).</summary>
internal static class PlayerNature
{
    private sealed class Cache
    {
        public long Hour = -1;
        public int ResourceId = -1;
        public bool MapHas;
        public List<CityBounds> Cities = [];
    }

    private static readonly ConditionalWeakTable<WorldState, Cache> Caches = new();

    /// <summary>A célula é mata do recurso: terra seca, sem prédio, com o recurso no mapa (ou, sem ele no mapa, fora das cidades).</summary>
    public static bool IsWild(WorldState world, MapCell cell, ResourceType resource)
    {
        if (cell.HasWater) return false;
        var known = Known(world, resource);
        bool wild = known.MapHas ? cell.Resources.Contains(resource) : !known.Cities.Any(b => b.Contains(cell.Coord));
        return wild && PlayerGround.At(world, cell.Coord) is null;
    }

    private static Cache Known(WorldState world, ResourceType resource)
    {
        var cache = Caches.GetOrCreateValue(world);
        long hour = world.CurrentDate.TotalHours;
        if (cache.Hour == hour && cache.ResourceId == resource.Id) return cache;
        cache.Hour = hour;
        cache.ResourceId = resource.Id;
        cache.MapHas = world.Map.Cells.Any(c => c.Resources.Contains(resource));
        cache.Cities = [];
        foreach (var city in world.ActiveCities())
            cache.Cities.Add(LivingWorld.Simulation.Cities.CityOccupancy
                .ResolveGrownBounds(world, city, LivingWorld.Simulation.Cities.Queries.CityPopulationQuery.Population(world, city.Id)).Bounds);
        return cache;
    }
}
