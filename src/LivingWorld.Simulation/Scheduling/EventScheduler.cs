namespace LivingWorld.Simulation;

/// <summary>Fila de eventos futuros indexada por tick alvo. Empate no mesmo tick desempata por
/// <see cref="ScheduledEvent.Id"/>, nunca por ordem de inserção (docs/domain/time-and-ticks.md).</summary>
public sealed class EventScheduler
{
    private readonly SortedDictionary<long, List<ScheduledEvent>> _byTick = new();

    // Só o tick é necessário pra achar o bucket em Cancel — o índice dentro do bucket nunca era
    // lido em lugar nenhum (só escrito), então recalculá-lo com Sort()+FindIndex() a cada
    // Schedule (e reindexar o bucket inteiro a cada Cancel) era trabalho puro sem consumidor.
    // Achado no profiling do PERF-06/07 (baseline-timings.md T2 revisado): com população grande,
    // muitos NPCs reagendam por hora e o bucket do mesmo tick cresce — o Sort() completo por
    // inserção dominava o custo do tick (O(k log k) por chamada, k = eventos nesse tick).
    private readonly Dictionary<long, long> _tickById = new();

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

        // Insere na posição ordenada por Id via busca binária em vez de Add+Sort — mesmo
        // resultado final (bucket sempre ordenado por Id, mesmo contrato de desempate documentado
        // na classe), custo O(k) em vez de O(k log k) por inserção.
        int pos = BinarySearchById(bucket, evt.Id);
        bucket.Insert(pos, evt);
        _tickById[evt.Id] = evt.TargetTick;
    }

    private static int BinarySearchById(List<ScheduledEvent> bucket, long id)
    {
        int lo = 0, hi = bucket.Count - 1;
        while (lo <= hi)
        {
            int mid = lo + (hi - lo) / 2;
            long midId = bucket[mid].Id;
            if (midId == id) return mid;
            if (midId < id) lo = mid + 1;
            else hi = mid - 1;
        }
        return lo;
    }

    /// <summary>True se algum evento agendado com este ID foi removido.</summary>
    public bool Cancel(long id)
    {
        if (!_tickById.TryGetValue(id, out var tick)) return false;
        if (!_byTick.TryGetValue(tick, out var bucket)) return false;

        int pos = BinarySearchById(bucket, id);
        if (pos >= bucket.Count || bucket[pos].Id != id) return false;

        bucket.RemoveAt(pos);
        _tickById.Remove(id);
        if (bucket.Count == 0)
            _byTick.Remove(tick);

        return true;
    }

    /// <summary>Remove e devolve, em ordem de ID, todos os eventos agendados para este tick.
    /// Vazio custa O(1) — não existe entrada para ticks sem evento.</summary>
    public IReadOnlyList<ScheduledEvent> PopDue(long tick)
    {
        if (!_byTick.TryGetValue(tick, out var bucket)) return [];
        _byTick.Remove(tick);
        foreach (var evt in bucket)
            _tickById.Remove(evt.Id);
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
