using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Api.Visual.Scope;

/// <summary>Diff puro entre duas fotos de posição de um escopo — nunca
/// recebe <c>WorldState</c> nem invoca <c>GlobalLayerBuilder</c>/<c>CityLayerBuilder</c>. Só o
/// snapshot inicial monta camadas; o caminho de delta nunca as recomputa.</summary>
public static class ScopeDeltaBuilder
{
    public static ScopeTickDelta Diff(long tick, LivingScopeState before, LivingScopeState after) =>
        new(
            tick,
            ChangedNpcs(before.Npcs, after.Npcs),
            Removed(before.Npcs, after.Npcs, item => item.Id),
            Changed(before.Cities, after.Cities, item => item.Id),
            Removed(before.Cities, after.Cities, item => item.Id),
            Changed(before.Buildings, after.Buildings, item => item.Id),
            Removed(before.Buildings, after.Buildings, item => item.Id),
            Changed(before.Processes, after.Processes, item => item.Id),
            Removed(before.Processes, after.Processes, item => item.Id),
            after.Indicators,
            after.Events)
        {
            BuildingStateUpserts = Changed(before.BuildingStates, after.BuildingStates, item => item.BuildingId),
            BuildingStateRemoved = Removed(before.BuildingStates, after.BuildingStates, item => item.BuildingId),
            CropUpserts = Changed(before.Crops, after.Crops, item => item.Id),
            CropRemoved = Removed(before.Crops, after.Crops, item => item.Id),
            AnimalUpserts = Changed(before.Animals, after.Animals, item => item.Id),
            AnimalRemoved = Removed(before.Animals, after.Animals, item => item.Id),
            FaunaGroupUpserts = Changed(before.FaunaGroups, after.FaunaGroups, FaunaKey),
            FaunaGroupRemoved = Removed(before.FaunaGroups, after.FaunaGroups, FaunaKey),
            LeavingUpserts = Changed(before.Leavings, after.Leavings, item => item.Id),
            LeavingRemoved = Removed(before.Leavings, after.Leavings, item => item.Id),
            ObjectUpserts = Changed(before.Objects, after.Objects, item => item.Id),
            ObjectRemoved = Removed(before.Objects, after.Objects, item => item.Id),
            Climate = Equals(before.Climate, after.Climate) ? null : after.Climate,
        };

    /// <summary>Chave de um bando no delta: espécie e região (<c>espécie:região</c>).</summary>
    public static string FaunaKey(LivingWorld.Api.Visual.Observable.FaunaGroupVisual group) => $"{group.Species}:{group.RegionId}";

    public static ScopeTickDelta Diff(long tick, IReadOnlyDictionary<NpcId, CellCoord> before, IReadOnlyDictionary<NpcId, CellCoord> after)
    {
        var moved = new List<NpcPositionDelta>();
        foreach (var (id, location) in after)
        {
            if (!before.TryGetValue(id, out var previousLocation) || previousLocation != location)
                moved.Add(new NpcPositionDelta(id, location));
        }

        var removed = before.Keys.Where(id => !after.ContainsKey(id)).ToList();

        return new ScopeTickDelta(tick, moved, removed);
    }

    /// <summary>AD-170: NPC que só andou vai sem o observável (nulo = igual ao anterior); quem mudou de sinal visível leva o
    /// observável inteiro. NPC novo no escopo sempre leva.</summary>
    private static IReadOnlyList<NpcVisual> ChangedNpcs(IReadOnlyList<NpcVisual> before, IReadOnlyList<NpcVisual> after)
    {
        var previous = before.ToDictionary(item => item.Id);
        var changed = new List<NpcVisual>();
        foreach (var item in after)
        {
            if (!previous.TryGetValue(item.Id, out var old)) { changed.Add(item); continue; }
            if (old.Equals(item)) continue;
            changed.Add(Equals(old.Observable, item.Observable) ? item with { Observable = null } : item);
        }
        return changed;
    }

    private static IReadOnlyList<T> Changed<T, TId>(
        IReadOnlyList<T> before,
        IReadOnlyList<T> after,
        Func<T, TId> id) where TId : notnull
    {
        var previous = before.ToDictionary(id);
        return after.Where(item => !previous.TryGetValue(id(item), out var old) || !EqualityComparer<T>.Default.Equals(old, item)).ToList();
    }

    private static IReadOnlyList<TId> Removed<T, TId>(
        IReadOnlyList<T> before,
        IReadOnlyList<T> after,
        Func<T, TId> id) where TId : notnull
    {
        var current = after.Select(id).ToHashSet();
        return before.Select(id).Where(itemId => !current.Contains(itemId)).ToList();
    }
}

public static class LivingDeltaReducer
{
    public static LivingScopeState Apply(LivingScopeState state, ScopeTickDelta delta) =>
        new(
            // AD-170: ids tipados não são comparáveis; ordena pelo valor, na mesma ordem da projeção
            Merge(state.Npcs, KeepObservable(state.Npcs, delta.NpcUpserts), delta.NpcRemoved, item => item.Id, item => item.Id.Value),
            Merge(state.Cities, delta.CityUpserts, delta.CityRemoved, item => item.Id, item => item.Id.Value),
            Merge(state.Buildings, delta.BuildingUpserts, delta.BuildingRemoved, item => item.Id, item => item.Id.Value),
            Merge(state.Processes, delta.ProcessUpserts, delta.ProcessRemoved, item => item.Id),
            delta.Indicators,
            delta.Events)
        {
            BuildingStates = Merge(state.BuildingStates, delta.BuildingStateUpserts, delta.BuildingStateRemoved, item => item.BuildingId),
            Crops = Merge(state.Crops, delta.CropUpserts, delta.CropRemoved, item => item.Id),
            Animals = Merge(state.Animals, delta.AnimalUpserts, delta.AnimalRemoved, item => item.Id),
            FaunaGroups = Merge(state.FaunaGroups, delta.FaunaGroupUpserts, delta.FaunaGroupRemoved, ScopeDeltaBuilder.FaunaKey),
            Leavings = Merge(state.Leavings, delta.LeavingUpserts, delta.LeavingRemoved, item => item.Id),
            Objects = Merge(state.Objects, delta.ObjectUpserts, delta.ObjectRemoved, item => item.Id),
            Climate = delta.Climate ?? state.Climate,
        };

    /// <summary>Upsert de NPC sem observável mantém o que o estado já tinha (AD-170).</summary>
    private static IReadOnlyList<NpcVisual> KeepObservable(IReadOnlyList<NpcVisual> current, IReadOnlyList<NpcVisual> upserts)
    {
        if (upserts.All(u => u.Observable is not null)) return upserts;
        var known = current.ToDictionary(item => item.Id);
        return upserts.Select(u => u.Observable is null && known.TryGetValue(u.Id, out var old) ? u with { Observable = old.Observable } : u).ToList();
    }

    private static IReadOnlyList<T> Merge<T, TId>(
        IReadOnlyList<T> current,
        IReadOnlyList<T> upserts,
        IReadOnlyList<TId> removed,
        Func<T, TId> id) where TId : notnull => Merge(current, upserts, removed, id, id);

    private static IReadOnlyList<T> Merge<T, TId, TOrder>(
        IReadOnlyList<T> current,
        IReadOnlyList<T> upserts,
        IReadOnlyList<TId> removed,
        Func<T, TId> id,
        Func<T, TOrder> order) where TId : notnull
    {
        var result = current.ToDictionary(id);
        foreach (var itemId in removed)
            result.Remove(itemId);
        foreach (var item in upserts)
            result[id(item)] = item;
        return result.Values.OrderBy(order).ToList();
    }
}
