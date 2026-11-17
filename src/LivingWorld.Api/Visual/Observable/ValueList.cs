using System.Collections;

namespace LivingWorld.Api.Visual.Observable;

/// <summary>Lista com igualdade por conteúdo (AD-170): os registros da projeção são comparados pelo diff de escopo, e uma lista comum
/// compara por referência (todo tick viraria "mudou"). Serializa como array, igual a qualquer <see cref="IReadOnlyList{T}"/>.</summary>
public sealed class ValueList<T> : IReadOnlyList<T>, IEquatable<ValueList<T>>
{
    private readonly T[] _items;

    public static ValueList<T> Empty { get; } = new([]);

    public ValueList(T[] items) => _items = items;

    public static ValueList<T> Of(IEnumerable<T> items)
    {
        if (items is ValueList<T> v) return v;
        var array = items.ToArray();
        return array.Length == 0 ? Empty : new(array);
    }

    public int Count => _items.Length;
    public T this[int index] => _items[index];
    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)_items).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();

    public bool Equals(ValueList<T>? other) =>
        other is not null && (ReferenceEquals(this, other) || _items.AsSpan().SequenceEqual(other._items, EqualityComparer<T>.Default));

    public override bool Equals(object? obj) => obj is ValueList<T> other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var item in _items) hash.Add(item);
        return hash.ToHashCode();
    }
}
