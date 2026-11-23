using LivingWorld.Domain.Shared;

namespace LivingWorld.Simulation.Behavior.Needs;

/// <summary>Fila de despertar dos NPCs (AD-129, #152): um despertar pendente por NPC, sem objeto nem string por
/// agendamento. O estado é <c>NPC → (instante em milissegundos, sequência)</c> (ADR-0050); os baldes por hora de fecho
/// (<see cref="WorldMillis.ToHourCeiling"/>) só aceleram a coleta e cancelam de forma preguiçosa: reagendar não procura a
/// entrada antiga, que fica no balde e é descartada quando a hora chega (só vale a entrada cuja sequência é a registrada para
/// o NPC). Sequência é única e crescente, então cada balde fica em ordem crescente de sequência só com acréscimos no fim. Com
/// Δ = 1 h todo despertar cai na hora cheia e o balde é o tick inteiro, como antes.</summary>
internal sealed class NpcWakeQueue
{
    private readonly record struct Slot(long Ms, long Sequence);
    private readonly record struct Entry(long NpcId, long Sequence);

    private readonly Dictionary<long, Slot> _byNpc = new();
    private readonly Dictionary<long, List<Entry>> _byTick = new(); // hora de fecho → entradas

    // Baldes já esvaziados: reaproveitar a lista (e a capacidade dela) evita uma lista nova por hora simulada.
    private readonly Stack<List<Entry>> _spare = new();
    private const int MaxSpareBuckets = 8;

    public int Count => _byNpc.Count;

    /// <summary>Agenda (ou move) o despertar do NPC para o instante <paramref name="ms"/>. O anterior, se houver, deixa de
    /// valer.</summary>
    public void Schedule(long npcId, long ms, long sequence)
    {
        _byNpc[npcId] = new Slot(ms, sequence);
        long key = WorldMillis.ToHourCeiling(ms);
        if (!_byTick.TryGetValue(key, out var bucket))
        {
            bucket = _spare.Count > 0 ? _spare.Pop() : [];
            _byTick[key] = bucket;
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

    /// <summary>Remove os despertares da hora de <paramref name="nowMs"/> que já venceram (instante ≤ agora) e devolve os
    /// NPCs em <paramref name="npcIds"/> por sequência DECRESCENTE (último agendado primeiro).</summary>
    public void TakeDue(long nowMs, List<long> npcIds)
    {
        npcIds.Clear();
        long key = WorldMillis.ToHourCeiling(nowMs);
        if (!_byTick.TryGetValue(key, out var bucket)) return;

        int pending = 0;
        for (int i = bucket.Count - 1; i >= 0; i--)
        {
            var entry = bucket[i];
            if (!IsCurrent(entry, out var slot)) continue;
            if (slot.Ms > nowMs)
            {
                pending++;
                continue;
            }
            _byNpc.Remove(entry.NpcId);
            npcIds.Add(entry.NpcId);
        }

        if (pending > 0)
        {
            // ainda há quem acorde mais tarde nesta hora (Δ menor que a hora): fica só o que vale, na mesma ordem
            bucket.RemoveAll(e => !IsCurrent(e, out _));
            return;
        }

        _byTick.Remove(key);
        bucket.Clear();
        if (_spare.Count < MaxSpareBuckets) _spare.Push(bucket);
    }

    /// <summary>Os NPCs que acordariam em <paramref name="nowMs"/>, na ordem de <see cref="TakeDue"/>, sem removê-los.</summary>
    public IReadOnlyList<long> PeekDue(long nowMs)
    {
        var result = new List<long>();
        if (!_byTick.TryGetValue(WorldMillis.ToHourCeiling(nowMs), out var bucket)) return result;
        for (int i = bucket.Count - 1; i >= 0; i--)
            if (IsCurrent(bucket[i], out var slot) && slot.Ms <= nowMs) result.Add(bucket[i].NpcId);
        return result;
    }

    /// <summary>Instante (milissegundos) do despertar pendente do NPC, ou nulo.</summary>
    public long? MsOf(long npcId) => _byNpc.TryGetValue(npcId, out var slot) ? slot.Ms : null;

    /// <summary>Estado canônico: um despertar por NPC, em ordem crescente de id.</summary>
    public IReadOnlyList<PendingNpcWake> Snapshot()
    {
        var result = new List<PendingNpcWake>(_byNpc.Count);
        foreach (var (npcId, slot) in _byNpc)
            result.Add(new PendingNpcWake(npcId, slot.Ms, slot.Sequence));
        result.Sort(static (a, b) => a.NpcId.CompareTo(b.NpcId));
        return result;
    }

    /// <summary>Reidrata do snapshot. Em ordem de sequência, para que um NPC repetido (snapshot antigo) fique com o
    /// agendamento mais recente, como ficaria se tivesse seguido rodando.</summary>
    public void Restore(IEnumerable<PendingNpcWake> wakes)
    {
        foreach (var wake in wakes.OrderBy(w => w.Sequence).ThenBy(w => w.NpcId))
            Schedule(wake.NpcId, wake.TargetMs, wake.Sequence);
    }

    /// <summary>A entrada vale se é o agendamento registrado do NPC (a sequência é única por agendamento).</summary>
    private bool IsCurrent(Entry entry, out Slot slot) =>
        _byNpc.TryGetValue(entry.NpcId, out slot) && slot.Sequence == entry.Sequence;
}
