using LivingWorld.Api.Visual.Observable;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Cities.Spatial;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cities;
using LivingWorld.Simulation.Cities.Queries;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Production;

namespace LivingWorld.Api.Visual.Scope;

/// <summary>AD-172: leitura do mundo num tick, montada uma vez e partilhada por todos os escopos assinados (Cidade e Mundo). Junta o
/// que cada escopo refazia sozinho: limites e população das cidades, o destino de mudança de cada casa (antes uma busca linear por
/// NPC), em que escopo cada NPC está, os índices do observável e as listas de processo que não dependem do escopo. Só leitura, nunca
/// guardada no mundo.</summary>
public sealed class ScopeProjectionFrame
{
    public WorldState World { get; }
    public long Tick { get; }
    public ObservableContext Observable { get; }
    public IReadOnlyDictionary<CityId, CityBounds> CityBounds => _bounds;
    public IReadOnlyList<ScopedNpc> Npcs => _npcs;

    private readonly Dictionary<CityId, CityBounds> _bounds = new();
    private readonly Dictionary<CityId, long> _population = new();
    private readonly List<ScopedNpc> _npcs = new();
    private readonly HashSet<long> _deliveringWater = new();
    private readonly Dictionary<long, NpcVisual> _npcVisuals = new();
    private readonly Dictionary<long, NpcVisual>? _previousVisuals;
    private readonly Dictionary<long, BuildingStateVisual> _buildingStates = new();
    private readonly Dictionary<long, BuildingStateVisual>? _previousBuildingStates;
    private List<ProcessVisual>? _resourceProcesses;
    private List<ProcessVisual>? _cropProcesses;
    private Dictionary<CityId, CityDensityVisual>? _densities;
    private IReadOnlyList<FaunaGroupVisual>? _faunaGroups;

    /// <param name="previous">A leitura do tick anterior (AD-172): o NPC que não mudou nada visível reaproveita o registro de lá,
    /// sem alocar, e o diff o reconhece por referência. Só a API guarda isso; o mundo não sabe.</param>
    public ScopeProjectionFrame(WorldState world, PresentationNames? names = null, ScopeProjectionFrame? previous = null)
    {
        _previousVisuals = previous?._npcVisuals;
        _previousBuildingStates = previous?._buildingStates;
        World = world;
        Tick = world.CurrentDate.TotalHours;
        Observable = new ObservableContext(world, names);
        // ResolveGrownBounds alimenta os boxes de overflow das próprias buildings da cidade de volta pra Resolve.
        foreach (var city in world.ActiveCities())
        {
            long population = CityPopulationQuery.Population(world, city.Id);
            _population[city.Id] = population;
            _bounds[city.Id] = CityOccupancy.ResolveGrownBounds(world, city, population).Bounds;
        }

        var relocations = new Dictionary<HouseholdId, CellCoord?>();
        foreach (var household in world.Households)
            if (!relocations.ContainsKey(household.Id))
                relocations[household.Id] = household.PendingRelocationCity is { } destination ? world.FindCity(destination)?.Location : null;

        foreach (var process in world.ResourceProcesses)
            if (process.Status == ProcessStatus.InProgress && process.Kind == ProcessKind.DeliverWater)
                _deliveringWater.Add(process.ActorId.Value);

        var npcs = world.Npcs;
        for (int i = 0; i < npcs.Count; i++)
        {
            var npc = npcs[i];
            if (!npc.IsAlive) continue;
            CellCoord? relocation = npc.Household is { } home ? relocations.GetValueOrDefault(home) : null;
            bool hasBounds = _bounds.TryGetValue(npc.City, out var bounds);
            bool insideCity = hasBounds && NpcScopeResolver.Resolve(npc, bounds).Kind == NpcScopeKind.City;
            _npcs.Add(new ScopedNpc(npc, relocation, hasBounds, insideCity));
        }

        // Mesma ordem da projeção anterior (por id); a lista do motor já vem assim, ordenar só quando não vier.
        for (int i = 1; i < _npcs.Count; i++)
            if (_npcs[i - 1].Npc.Id.Value > _npcs[i].Npc.Id.Value)
            {
                _npcs.Sort((a, b) => a.Npc.Id.Value.CompareTo(b.Npc.Id.Value));
                break;
            }
    }

    public long PopulationOf(CityId city) => _population.TryGetValue(city, out long population) ? population : CityPopulationQuery.Population(World, city);

    public bool IsDeliveringWater(NpcId npc) => _deliveringWater.Contains(npc.Value);

    /// <summary>O visual do NPC, montado uma vez por tick mesmo quando ele aparece em dois escopos (quem muda de cidade).</summary>
    public NpcVisual NpcVisualOf(in ScopedNpc scoped, Func<ScopeProjectionFrame, ScopedNpc, NpcVisual?, NpcVisual> build)
    {
        long id = scoped.Npc.Id.Value;
        if (_npcVisuals.TryGetValue(id, out var cached)) return cached;
        var visual = build(this, scoped, _previousVisuals?.GetValueOrDefault(id));
        _npcVisuals[id] = visual;
        return visual;
    }

    /// <summary>Estado dos prédios visíveis do escopo, reaproveitando o do tick anterior quando nada mudou (AD-172).</summary>
    public IReadOnlyList<BuildingStateVisual> BuildingStates(IReadOnlyList<Building> visible)
    {
        var states = ObservableProjector.BuildingStates(
            Observable, visible, _previousBuildingStates is { } previous ? id => previous.GetValueOrDefault(id) : null);
        foreach (var state in states) _buildingStates[state.BuildingId] = state;
        return states;
    }

    /// <summary>Processos de material em andamento: não dependem do escopo, montados uma vez.</summary>
    public List<ProcessVisual> ResourceProcesses => _resourceProcesses ??= World.ResourceProcesses
        .Where(process => process.Status == ProcessStatus.InProgress)
        .OrderBy(process => process.Id.Value)
        .Select(process =>
        {
            var snapshot = ResourceProcessPresentation.ToProcess(World, process);
            return new ProcessVisual(
                snapshot.Id, snapshot.Kind, snapshot.TargetId, snapshot.Progress, snapshot.DescriptorKey,
                Quality: null, snapshot.RemainingHours, snapshot.Location)
            { ResourceId = process.Output.Id, ResourceName = Observable.Names.Resource(process.Output.Id) };
        }).ToList();

    /// <summary>Lavouras como processo: não dependem do escopo, montadas uma vez.</summary>
    public List<ProcessVisual> CropProcesses => _cropProcesses ??= World.CropBatches
        .Where(crop => crop.Status != CropStatus.Harvested)
        .OrderBy(crop => crop.Id.Value)
        .Select(crop =>
        {
            var snapshot = ResourceProcessPresentation.ToCrop(World, crop);
            return new ProcessVisual(
                snapshot.Id, snapshot.Kind, snapshot.TargetId, snapshot.Progress, snapshot.DescriptorKey,
                Quality: null, snapshot.RemainingHours, snapshot.Location)
            { ResourceId = crop.CropResourceId, ResourceName = Observable.Names.Resource(crop.CropResourceId) };
        }).ToList();

    public Dictionary<CityId, CityDensityVisual> Densities => _densities ??= ObservableProjector.Densities(World, _npcs);

    public IReadOnlyList<FaunaGroupVisual> FaunaGroups => _faunaGroups ??= ObservableProjector.FaunaGroups(World);
}

/// <summary>Um NPC vivo e onde ele está: destino de mudança da casa (se houver), se a cidade dele tem limites e se está dentro deles.</summary>
public readonly record struct ScopedNpc(Npc Npc, CellCoord? Relocation, bool HasCityBounds, bool InsideCity)
{
    public bool InCity(CityId city) => HasCityBounds && InsideCity && Npc.City == city;

    public bool InWorld => Relocation is not null || (HasCityBounds && !InsideCity);
}
