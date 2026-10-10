using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;

namespace LivingWorld.Simulation.Core;

/// <summary>NPCs vivos em ordem de id, mais um índice por célula — derivado, fora do hash canônico.
/// O índice espacial é mantido por <see cref="Npc.LocationObserver"/> (único mutador de posição:
/// <see cref="Npc.MoveTo"/>), então consultas de vizinhança custam O(janela) em vez de O(população).</summary>
public sealed class AliveNpcIndex
{
    private readonly List<Npc> _alive;
    private readonly Dictionary<CellCoord, List<Npc>> _byCell = new();

    private AliveNpcIndex(List<Npc> alive)
    {
        _alive = alive;
        foreach (var npc in alive)
            Track(npc);
    }

    public IReadOnlyList<Npc> Alive => _alive;

    /// <summary>NPCs vivos na célula (ordem do bucket não especificada). Vazio se nenhum.</summary>
    public IReadOnlyList<Npc> At(CellCoord cell) =>
        _byCell.TryGetValue(cell, out var bucket) ? bucket : Array.Empty<Npc>();

    public static AliveNpcIndex RebuildFrom(WorldState world)
    {
        var alive = world.Npcs.Where(n => n.IsAlive).OrderBy(n => n.Id.Value).ToList();
        return new AliveNpcIndex(alive);
    }

    public void OnBorn(Npc npc)
    {
        if (!npc.IsAlive) return;
        int i = _alive.BinarySearch(npc, Comparer<Npc>.Create((a, b) => a.Id.Value.CompareTo(b.Id.Value)));
        if (i < 0)
        {
            _alive.Insert(~i, npc);
            Track(npc);
        }
    }

    public void OnDied(Npc npc)
    {
        for (int i = 0; i < _alive.Count; i++)
        {
            if (_alive[i].Id == npc.Id)
            {
                _alive.RemoveAt(i);
                Untrack(npc);
                return;
            }
        }
    }

    private void Track(Npc npc)
    {
        AddToBucket(npc, npc.CurrentLocation);
        npc.LocationObserver = OnMoved;
    }

    private void Untrack(Npc npc)
    {
        RemoveFromBucket(npc, npc.CurrentLocation);
        npc.LocationObserver = null;
    }

    private void OnMoved(Npc npc, CellCoord previous)
    {
        RemoveFromBucket(npc, previous);
        AddToBucket(npc, npc.CurrentLocation);
    }

    private void AddToBucket(Npc npc, CellCoord cell)
    {
        if (!_byCell.TryGetValue(cell, out var bucket))
            _byCell[cell] = bucket = new List<Npc>(2);
        bucket.Add(npc);
    }

    private void RemoveFromBucket(Npc npc, CellCoord cell)
    {
        if (!_byCell.TryGetValue(cell, out var bucket)) return;
        int i = bucket.IndexOf(npc);
        if (i < 0) return;
        bucket.RemoveAt(i);
        if (bucket.Count == 0)
            _byCell.Remove(cell);
    }
}
