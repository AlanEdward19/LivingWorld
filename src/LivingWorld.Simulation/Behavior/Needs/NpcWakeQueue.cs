namespace LivingWorld.Simulation.Behavior.Needs;

/// <summary>Fila de despertar dos NPCs (AD-129, #152): um despertar pendente por NPC, sem objeto nem string por
/// agendamento. O estado é <c>NPC → (tick, sequência)</c>; os baldes por tick só aceleram a coleta e cancelam de
/// forma preguiçosa: reagendar não procura a entrada antiga, que fica no balde e é descartada quando o tick chega
/// (só vale a entrada cuja sequência é a registrada para o NPC). Sequência é única e crescente, então cada balde
/// fica em ordem crescente de sequência só com acréscimos no fim.</summary>
internal sealed class NpcWakeQueue
{
    private readonly record struct Slot(long Tick, long Sequence);
    private readonly record struct Entry(long NpcId, long Sequence);

    private readonly Dictionary<long, Slot> _byNpc = new();
    private readonly Dictionary<long, List<Entry>> _byTick = new();

    // Baldes já esvaziados: reaproveitar a lista (e a capacidade dela) evita uma lista nova por hora simulada.
    private readonly Stack<List<Entry>> _spare = new();
    private const int MaxSpareBuckets = 8;

    public int Count => _byNpc.Count;

    /// <summary>Agenda (ou move) o despertar do NPC. O anterior, se houver, deixa de valer.</summary>
    public void Schedule(long npcId, long tick, long sequence)
    {
        _byNpc[npcId] = new Slot(tick, sequence);
        if (!_byTick.TryGetValue(tick, out var bucket))
        {
            bucket = _spare.Count > 0 ? _spare.Pop() : [];
            _byTick[tick] = bucket;
        }

        var entry = new Entry(npcId, sequence);
        if (bucket.Count == 0 || bucket[^1].Sequence < sequence)
        {
            bucket.Add(entry); // caminho normal: a sequência acabou de sair do contador
            return;
        }

        int lo = 0, hi = bucket.Count;
        while (lo < hi)
        {
            int mid = (lo + hi) >>> 1;
            if (bucket[mid].Sequence < sequence) lo = mid + 1; else hi = mid;
        }
        bucket.Insert(lo, entry);
    }

    /// <summary>Remove os despertares que vencem em <paramref name="tick"/> e devolve os NPCs em
    /// <paramref name="npcIds"/> por sequência DECRESCENTE (último agendado primeiro).</summary>
    public void TakeDue(long tick, List<long> npcIds)
    {
        npcIds.Clear();
        if (!_byTick.Remove(tick, out var bucket)) return;

        for (int i = bucket.Count - 1; i >= 0; i--)
        {
            var entry = bucket[i];
            if (!IsCurrent(entry, tick)) continue;
            _byNpc.Remove(entry.NpcId);
            npcIds.Add(entry.NpcId);
        }

        bucket.Clear();
        if (_spare.Count < MaxSpareBuckets) _spare.Push(bucket);
    }

    /// <summary>Os NPCs que acordariam em <paramref name="tick"/>, na ordem de <see cref="TakeDue"/>, sem removê-los.</summary>
    public IReadOnlyList<long> PeekDue(long tick)
    {
        var result = new List<long>();
        if (!_byTick.TryGetValue(tick, out var bucket)) return result;
        for (int i = bucket.Count - 1; i >= 0; i--)
            if (IsCurrent(bucket[i], tick)) result.Add(bucket[i].NpcId);
        return result;
    }

    public long? TickOf(long npcId) => _byNpc.TryGetValue(npcId, out var slot) ? slot.Tick : null;

    /// <summary>Estado canônico: um despertar por NPC, em ordem crescente de id.</summary>
    public IReadOnlyList<PendingNpcWake> Snapshot()
    {
        var result = new List<PendingNpcWake>(_byNpc.Count);
        foreach (var (npcId, slot) in _byNpc)
            result.Add(new PendingNpcWake(npcId, slot.Tick, slot.Sequence));
        result.Sort(static (a, b) => a.NpcId.CompareTo(b.NpcId));
        return result;
    }

    /// <summary>Reidrata do snapshot. Em ordem de sequência, para que um NPC repetido (snapshot antigo) fique com o
    /// agendamento mais recente, como ficaria se tivesse seguido rodando.</summary>
    public void Restore(IEnumerable<PendingNpcWake> wakes)
    {
        foreach (var wake in wakes.OrderBy(w => w.Sequence).ThenBy(w => w.NpcId))
            Schedule(wake.NpcId, wake.TargetTick, wake.Sequence);
    }

    private bool IsCurrent(Entry entry, long tick) =>
        _byNpc.TryGetValue(entry.NpcId, out var slot) && slot.Sequence == entry.Sequence && slot.Tick == tick;
}
