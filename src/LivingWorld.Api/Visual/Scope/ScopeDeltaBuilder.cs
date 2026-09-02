using LivingWorld.Domain;

namespace LivingWorld.Api.Visual;

/// <summary>Fase 15.1, T2 (VTT2-11): diff puro entre duas fotos de posição de um escopo — nunca
/// recebe <c>WorldState</c> nem invoca <c>GlobalLayerBuilder</c>/<c>CityLayerBuilder</c>. Só o
/// snapshot inicial (T3) monta camadas; o caminho de delta nunca as recomputa.</summary>
public static class ScopeDeltaBuilder
{
    public static ScopeTickDelta Diff(long tick, LivingScopeState before, LivingScopeState after) =>
        new(
            tick,
            Changed(before.Npcs, after.Npcs, item => item.Id),
            Removed(before.Npcs, after.Npcs, item => item.Id),
            Changed(before.Cities, after.Cities, item => item.Id),
            Removed(before.Cities, after.Cities, item => item.Id),
            Changed(before.Buildings, after.Buildings, item => item.Id),
            Removed(before.Buildings, after.Buildings, item => item.Id),
            Changed(before.Processes, after.Processes, item => item.Id),
            Removed(before.Processes, after.Processes, item => item.Id),
            after.Indicators,
            after.Events);

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
            Merge(state.Npcs, delta.NpcUpserts, delta.NpcRemoved, item => item.Id),
            Merge(state.Cities, delta.CityUpserts, delta.CityRemoved, item => item.Id),
            Merge(state.Buildings, delta.BuildingUpserts, delta.BuildingRemoved, item => item.Id),
            Merge(state.Processes, delta.ProcessUpserts, delta.ProcessRemoved, item => item.Id),
            delta.Indicators,
            delta.Events);

    private static IReadOnlyList<T> Merge<T, TId>(
        IReadOnlyList<T> current,
        IReadOnlyList<T> upserts,
        IReadOnlyList<TId> removed,
        Func<T, TId> id) where TId : notnull
    {
        var result = current.ToDictionary(id);
        foreach (var itemId in removed)
            result.Remove(itemId);
        foreach (var item in upserts)
            result[id(item)] = item;
        return result.Values.OrderBy(item => id(item)).ToList();
    }
}
