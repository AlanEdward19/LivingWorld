using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Geography.Spatial;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Snapshot;

namespace LivingWorld.Simulation.Core;

// Parte de WorldState — operações sobre cidades, prédios e portais. O estado (campos e propriedades) fica em WorldState.cs.
public sealed partial class WorldState
{
    /// <summary>Cidades que ainda participam de decisões e projeções. Tombstones continuam em
    /// <see cref="Cities"/> para preservar referências históricas.</summary>
    public IEnumerable<City> ActiveCities() => _cities.Where(city => city.MergedIntoCityId is null);

    /// <summary>Edifício concluído — mesmo molde de <see cref="Cities"/>.</summary>
    [Canonical] public IReadOnlyList<Building> Buildings => _buildings;

    /// <summary><see cref="BuildingId"/> é monotônico como <see cref="WorkplaceId"/> — só este
    /// contador precisa sobreviver ao snapshot. <see cref="CityId"/>/<see cref="LocationId"/> não
    /// têm contador: nascem do stream de RNG dedicado (<see cref="NextCityId"/>), já coberto pelo
    /// snapshot de <see cref="RngStreams"/>.</summary>
    [Canonical] public long NextBuildingId => _nextBuildingId;

    internal BuildingId NextBuildingIdAndAdvance() => new(_nextBuildingId++);

    public void AddCity(City city)
    {
        _cities.Add(city);
        _cityById[city.Id] = city;
    }

    public void AddBuilding(Building building)
    {
        _buildings.Add(building);
        _buildingById[building.Id] = building;
    }

    public City? FindCity(CityId id) => _cityById.GetValueOrDefault(id);
    public City? FindActiveCity(CityId id)
    {
        var city = FindCity(id);
        var visited = new HashSet<CityId>();
        while (city?.MergedIntoCityId is { } target && visited.Add(city.Id))
            city = FindCity(target);
        return city is { MergedIntoCityId: null } ? city : null;
    }

    internal void MergeCityInto(City daughter, City mother)
    {
        foreach (var building in _buildings.Where(building => building.City == daughter.Id).OrderBy(building => building.Id.Value))
            building.JoinCity(mother.Id);
        foreach (var workplace in _workplaces.Where(workplace => workplace.City == daughter.Id).OrderBy(workplace => workplace.Id.Value))
            workplace.JoinCity(mother.Id);
        foreach (var household in _households.OrderBy(household => household.Id.Value))
            household.ReplaceCityReference(daughter.Id, mother.Id);
        foreach (var npc in _npcs.Where(npc => npc.City == daughter.Id).OrderBy(npc => npc.Id.Value))
            npc.JoinCity(mother.Id);

        foreach (var stock in daughter.ExtractEntireStock().OrderBy(entry => entry.Key.Id))
            mother.DepositStock(stock.Key, stock.Value);

        foreach (var project in daughter.ConstructionQueue.ToList())
        {
            daughter.RemoveConstructionProject(project);
            project.JoinCity(mother.Id);
            mother.EnqueueConstruction(project);
        }

        var daughterTreasury = daughter.Treasury;
        if (daughterTreasury.Amount > 0 && daughter.TryDebitTreasury(daughterTreasury).IsSuccess)
            mother.CreditTreasury(daughterTreasury);
        var (pool, poolNpcIds) = daughter.ExtractEntirePool();
        mother.AbsorbPool(pool, poolNpcIds);
        daughter.MarkMergedInto(mother.Id);
    }
    public Building? FindBuilding(BuildingId id) => _buildingById.GetValueOrDefault(id);

    /// <summary>Único ponto de autoria de <see cref="SpatialPortal"/> — só
    /// <c>ScenarioLoaderV2</c> chama, no mesmo momento em que autora <see cref="City"/>/
    /// <see cref="Building"/>. Sem <c>FindPortal</c>/remoção: portal é dado descritivo estático do
    /// cenário, nenhum sistema o edita depois de carregado.</summary>
    public void AddPortal(SpatialPortal portal)
    {
        _portals.Add(portal);
        // Portals is cold (not HotProperties): must invalidate fragment cache after mutation
        // or CanonicalHash after AddPortal stays stale.
        CanonicalHashCache.MarkPropertyDirty(nameof(Portals));
    }

    /// <summary>Deriva um <see cref="CityId"/> novo a partir do stream de RNG dedicado
    /// <c>"city-founding"</c> — <c>Guid.NewGuid()</c> é banido em Domain/Simulation
    /// (rules/simulation-determinism.md); mesma seed produz sempre o mesmo <see cref="CityId"/>
    /// na mesma posição do stream.</summary>
    public CityId NextCityId() => new(NextGuidFromRng());

    /// <summary>Espelha <see cref="NextCityId"/> para <see cref="LocationId"/> — mesmo stream
    /// dedicado, próximo par de sorteios.</summary>
    public LocationId NextLocationId() => new(NextGuidFromRng());
}
