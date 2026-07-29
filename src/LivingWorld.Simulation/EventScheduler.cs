namespace LivingWorld.Simulation;

/// <summary>Fila de eventos futuros indexada por tick alvo. Empate no mesmo tick desempata por
/// <see cref="ScheduledEvent.Id"/>, nunca por ordem de inserção (docs/domain/time-and-ticks.md).</summary>
public sealed class EventScheduler
{
    private readonly SortedDictionary<long, List<ScheduledEvent>> _byTick = new();
    private readonly Dictionary<long, (long Tick, int Index)> _indexById = new();

    public EventScheduler()
    {
    }

    public EventScheduler(IEnumerable<ScheduledEvent> pending)
    {
        foreach (var evt in pending)
            Schedule(evt);
    }

    public void Schedule(ScheduledEvent evt)
    {
        if (!_byTick.TryGetValue(evt.TargetTick, out var bucket))
        {
            bucket = [];
            _byTick[evt.TargetTick] = bucket;
        }
        bucket.Add(evt);
        bucket.Sort((a, b) => a.Id.CompareTo(b.Id));
        _indexById[evt.Id] = (evt.TargetTick, bucket.FindIndex(e => e.Id == evt.Id));
    }

    /// <summary>True se algum evento agendado com este ID foi removido.</summary>
    public bool Cancel(long id)
    {
        if (!_indexById.TryGetValue(id, out var location)) return false;

        if (!_byTick.TryGetValue(location.Tick, out var bucket)) return false;

        int removed = bucket.RemoveAll(e => e.Id == id);
        if (removed == 0) return false;

        _indexById.Remove(id);
        if (bucket.Count == 0)
            _byTick.Remove(location.Tick);
        else
        {
            for (int i = 0; i < bucket.Count; i++)
                _indexById[bucket[i].Id] = (location.Tick, i);
        }

        return true;
    }

    /// <summary>Remove e devolve, em ordem de ID, todos os eventos agendados para este tick.
    /// Vazio custa O(1) — não existe entrada para ticks sem evento.</summary>
    public IReadOnlyList<ScheduledEvent> PopDue(long tick)
    {
        if (!_byTick.TryGetValue(tick, out var bucket)) return [];
        _byTick.Remove(tick);
        foreach (var evt in bucket)
            _indexById.Remove(evt.Id);
        return bucket;
    }

    public bool HasDue(long tick) => _byTick.ContainsKey(tick) && _byTick[tick].Count > 0;

    public IReadOnlyList<ScheduledEvent> PeekDue(long tick) =>
        _byTick.TryGetValue(tick, out var bucket) ? bucket : [];

    /// <summary>Todos os eventos ainda pendentes, ordenados por tick e depois por ID —
    /// para snapshot (task 7).</summary>
    public IReadOnlyList<ScheduledEvent> Snapshot() =>
        _byTick.OrderBy(kv => kv.Key)
            .SelectMany(kv => kv.Value.OrderBy(e => e.Id))
            .ToList();
}
