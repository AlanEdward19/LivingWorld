using LivingWorld.Domain;

namespace LivingWorld.Api.Visual;

/// <summary>Fase 15.1, T2 (VTT2-11): diff puro entre duas fotos de posição de um escopo — nunca
/// recebe <c>WorldState</c> nem invoca <c>GlobalLayerBuilder</c>/<c>CityLayerBuilder</c>. Só o
/// snapshot inicial (T3) monta camadas; o caminho de delta nunca as recomputa.</summary>
public static class ScopeDeltaBuilder
{
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
}
