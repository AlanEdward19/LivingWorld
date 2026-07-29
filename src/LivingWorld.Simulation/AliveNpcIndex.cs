using LivingWorld.Domain;

namespace LivingWorld.Simulation;

/// <summary>NPCs vivos em ordem de id — derivado, fora do hash canônico (PERF-07).</summary>
public sealed class AliveNpcIndex
{
    private readonly List<Npc> _alive;

    private AliveNpcIndex(List<Npc> alive) => _alive = alive;

    public IReadOnlyList<Npc> Alive => _alive;

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
            _alive.Insert(~i, npc);
    }

    public void OnDied(Npc npc)
    {
        for (int i = 0; i < _alive.Count; i++)
        {
            if (_alive[i].Id == npc.Id)
            {
                _alive.RemoveAt(i);
                return;
            }
        }
    }
}
