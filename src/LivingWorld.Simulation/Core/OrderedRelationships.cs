using System.Collections;
using System.Diagnostics.CodeAnalysis;
using LivingWorld.Domain.Population.Family;

namespace LivingWorld.Simulation.Core;

/// <summary>Visão das relações na ordem em que foram criadas, estável a remoções (AD-128). O <c>Dictionary</c> reaproveita
/// a vaga de quem saiu, então depois de uma remoção a ordem de enumeração dependia do histórico em memória: o mundo
/// reaberto do snapshot enumerava (e fazia o hash) diferente do que seguiu rodando. Aqui quem sai deixa um buraco e quem
/// entra vai para o fim; sem remoção nenhuma a ordem é a mesma do dicionário, então os hashes antigos não mudam. As buscas
/// continuam no dicionário.</summary>
internal sealed class OrderedRelationships : IReadOnlyDictionary<RelationshipKey, Relationship>
{
    private readonly List<(RelationshipKey Key, Relationship? Value)> _order = [];
    private int _holes;

    internal Dictionary<RelationshipKey, Relationship> Map { get; set; } = [];

    public void Append(RelationshipKey key, Relationship value)
    {
        value.OrderSlot = _order.Count;
        _order.Add((key, value));
    }

    public void Remove(Relationship value)
    {
        _order[value.OrderSlot] = (default, null);
        _holes++;
        if (_holes < 64 || _holes * 2 < _order.Count) return;

        // Compacta sem mudar a ordem relativa.
        int write = 0;
        for (int read = 0; read < _order.Count; read++)
        {
            if (_order[read].Value is not { } kept) continue;
            kept.OrderSlot = write;
            _order[write++] = _order[read];
        }

        _order.RemoveRange(write, _order.Count - write);
        _holes = 0;
    }

    public int Count => Map.Count;
    public Relationship this[RelationshipKey key] => Map[key];
    public bool ContainsKey(RelationshipKey key) => Map.ContainsKey(key);
    public bool TryGetValue(RelationshipKey key, [MaybeNullWhen(false)] out Relationship value) => Map.TryGetValue(key, out value);

    public IEnumerable<RelationshipKey> Keys
    {
        get
        {
            foreach (var (key, value) in _order)
                if (value is not null) yield return key;
        }
    }

    public IEnumerable<Relationship> Values
    {
        get
        {
            foreach (var (_, value) in _order)
                if (value is not null) yield return value;
        }
    }

    public IEnumerator<KeyValuePair<RelationshipKey, Relationship>> GetEnumerator()
    {
        foreach (var (key, value) in _order)
            if (value is not null) yield return new KeyValuePair<RelationshipKey, Relationship>(key, value);
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
