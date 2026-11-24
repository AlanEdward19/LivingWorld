using LivingWorld.Domain.Shared;

namespace LivingWorld.Simulation.Scheduling;

/// <summary>Fila de eventos futuros indexada pelo instante alvo em milissegundos (ADR-0050). Vence na ordem do instante; empate no
/// mesmo instante desempata por <see cref="ScheduledEvent.Id"/>, nunca por ordem de inserção (docs/domain/time-and-ticks.md).
/// Os baldes são por hora de fecho (<see cref="WorldMillis.ToHourCeiling"/>): um tick nunca atravessa a hora, então tudo que
/// vence num tick está num balde só, e com Δ = 1 h cada balde é o tick inteiro, como antes. AD-197c: o evento que vence dentro da
/// hora (fora da hora cheia, o caso do passo fino) vai para uma árvore ordenada por (instante, Id) da sua hora, com agendar, cancelar e
/// vencer em O(log k) mesmo com muitos eventos na mesma hora; o da hora cheia segue na lista, sem custo novo de hora em hora. Todo
/// evento de dentro da hora vence antes dos da hora cheia do mesmo balde, então a ordem de saída é a mesma.</summary>
public sealed class EventScheduler
{
    // Acesso é sempre por tick exato (Schedule/Cancel/PopDue); a ordem global só importa em
    // Snapshot(), que ordena. Árvore ordenada custava O(log T) por Schedule, no caminho mais quente.
    private readonly Dictionary<long, List<ScheduledEvent>> _byTick = new(); // hora de fecho → eventos

    // Só o tick é necessário pra achar o bucket em Cancel — o índice dentro do bucket nunca era
    // lido em lugar nenhum (só escrito), então recalculá-lo com Sort()+FindIndex() a cada
    // Schedule (e reindexar o bucket inteiro a cada Cancel) era trabalho puro sem consumidor.
    // Achado no profiling: com população grande,
    // muitos NPCs reagendam por hora e o bucket do mesmo tick cresce — o Sort() completo por
    // inserção dominava o custo do tick (O(k log k) por chamada, k = eventos nesse tick).
    private readonly Dictionary<long, long> _tickById = new(); // id → instante alvo em milissegundos

    private static readonly Comparer<ScheduledEvent> ByInstantThenId = Comparer<ScheduledEvent>.Create(static (a, b) =>
        a.TargetMs != b.TargetMs ? a.TargetMs.CompareTo(b.TargetMs) : a.Id.CompareTo(b.Id));
    private readonly Dictionary<long, SortedSet<ScheduledEvent>> _insideHour = new(); // hora de fecho → eventos de dentro da hora

    // Baldes devolvidos por quem já despachou o tick: reaproveitar a lista (e a capacidade que ela já cresceu) evita criar e
    // redimensionar uma lista nova a cada hora simulada (os baldes de tick têm centenas de acordares).
    private readonly Stack<List<ScheduledEvent>> _spare = new();
    private const int MaxSpareBuckets = 8;

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
        long key = WorldMillis.ToHourCeiling(evt.TargetMs);
        if (evt.TargetMs % WorldMillis.PerHour != 0)
        {
            if (!_insideHour.TryGetValue(key, out var tree)) _insideHour[key] = tree = new SortedSet<ScheduledEvent>(ByInstantThenId);
            tree.Add(evt);
            _tickById[evt.Id] = evt.TargetMs;
            return;
        }
        if (!_byTick.TryGetValue(key, out var bucket))
        {
            bucket = _spare.Count > 0 ? _spare.Pop() : [];
            _byTick[key] = bucket;
        }

        // Insere na posição ordenada por (instante, Id) via busca binária em vez de Add+Sort — mesmo
        // resultado final (bucket sempre ordenado, mesmo contrato de desempate documentado
        // na classe), custo O(k) em vez de O(k log k) por inserção.
        int pos = BinarySearch(bucket, evt.TargetMs, evt.Id);
        bucket.Insert(pos, evt);
        _tickById[evt.Id] = evt.TargetMs;
    }

    private static int BinarySearch(List<ScheduledEvent> bucket, long target, long id)
    {
        int lo = 0, hi = bucket.Count - 1;
        while (lo <= hi)
        {
            int mid = lo + (hi - lo) / 2;
            var m = bucket[mid];
            int order = m.TargetMs != target ? m.TargetMs.CompareTo(target) : m.Id.CompareTo(id);
            if (order == 0) return mid;
            if (order < 0) lo = mid + 1;
            else hi = mid - 1;
        }
        return lo;
    }

    /// <summary>True se algum evento agendado com este ID foi removido.</summary>
    public bool Cancel(long id)
    {
        if (!_tickById.TryGetValue(id, out var target)) return false;
        long key = WorldMillis.ToHourCeiling(target);
        if (target % WorldMillis.PerHour != 0)
        {
            if (!_insideHour.TryGetValue(key, out var tree) || !tree.Remove(new ScheduledEvent(id, target, ""))) return false;
            _tickById.Remove(id);
            if (tree.Count == 0) _insideHour.Remove(key);
            return true;
        }
        if (!_byTick.TryGetValue(key, out var bucket)) return false;

        int pos = BinarySearch(bucket, target, id);
        if (pos >= bucket.Count || bucket[pos].Id != id) return false;

        bucket.RemoveAt(pos);
        _tickById.Remove(id);
        if (bucket.Count == 0)
        {
            _byTick.Remove(key);
            if (_spare.Count < MaxSpareBuckets) _spare.Push(bucket); // balde esvaziado a cancelamentos: reaproveita a capacidade
        }

        return true;
    }

    /// <summary>Remove e devolve, em ordem de (instante, ID), os eventos da hora de <paramref name="nowMs"/> que já venceram
    /// (instante ≤ agora). Vazio custa O(1) — não existe entrada para horas sem evento.</summary>
    public IReadOnlyList<ScheduledEvent> PopDue(long nowMs)
    {
        long key = WorldMillis.ToHourCeiling(nowMs);
        if (_insideHour.Count > 0 && _insideHour.TryGetValue(key, out var tree) && tree.Min!.TargetMs <= nowMs)
            return PopInside(key, tree, nowMs); // os de dentro da hora primeiro; os da hora cheia saem no laço seguinte do relógio
        if (!_byTick.TryGetValue(key, out var bucket)) return [];
        int due = bucket.Count;
        if (bucket[^1].TargetMs > nowMs)
        {
            due = 0;
            while (due < bucket.Count && bucket[due].TargetMs <= nowMs) due++;
            if (due == 0) return [];
            var popped = _spare.Count > 0 ? _spare.Pop() : new List<ScheduledEvent>(due);
            popped.AddRange(bucket.GetRange(0, due));
            bucket.RemoveRange(0, due);
            foreach (var evt in popped)
                _tickById.Remove(evt.Id);
            return popped;
        }

        _byTick.Remove(key);
        foreach (var evt in bucket)
            _tickById.Remove(evt.Id);
        return bucket;
    }

    private List<ScheduledEvent> PopInside(long key, SortedSet<ScheduledEvent> tree, long nowMs)
    {
        var popped = _spare.Count > 0 ? _spare.Pop() : [];
        while (tree.Count > 0 && tree.Min!.TargetMs <= nowMs)
        {
            var evt = tree.Min;
            tree.Remove(evt);
            _tickById.Remove(evt.Id);
            popped.Add(evt);
        }
        if (tree.Count == 0) _insideHour.Remove(key);
        return popped;
    }

    /// <summary>Devolve o balde de <see cref="PopDue"/> depois de despachado, para o próximo tick reaproveitá-lo. Quem não
    /// chama continua certo (a lista só vira lixo); quem chama não pode mais ler a lista.</summary>
    public void Release(IReadOnlyList<ScheduledEvent> popped)
    {
        if (popped is not List<ScheduledEvent> list || _spare.Count >= MaxSpareBuckets) return;

        list.Clear();
        _spare.Push(list);
    }

    public bool HasDue(long nowMs) =>
        (_insideHour.Count > 0 && _insideHour.TryGetValue(WorldMillis.ToHourCeiling(nowMs), out var tree) && tree.Min!.TargetMs <= nowMs)
        || (_byTick.TryGetValue(WorldMillis.ToHourCeiling(nowMs), out var bucket) && bucket.Count > 0 && bucket[0].TargetMs <= nowMs);

    public IReadOnlyList<ScheduledEvent> PeekDue(long nowMs)
    {
        long key = WorldMillis.ToHourCeiling(nowMs);
        var inside = _insideHour.TryGetValue(key, out var tree) ? tree.Where(e => e.TargetMs <= nowMs) : [];
        var full = _byTick.TryGetValue(key, out var bucket) ? bucket.Where(e => e.TargetMs <= nowMs) : [];
        return inside.Concat(full).ToList();
    }

    /// <summary>Todos os eventos ainda pendentes, ordenados por instante e depois por ID —
    /// para snapshot.</summary>
    public IReadOnlyList<ScheduledEvent> Snapshot() =>
        _byTick.SelectMany(kv => kv.Value).Concat(_insideHour.SelectMany(kv => kv.Value))
            .OrderBy(e => e.TargetMs).ThenBy(e => e.Id)
            .ToList();
}
