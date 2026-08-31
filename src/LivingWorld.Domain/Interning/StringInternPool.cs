namespace LivingWorld.Domain;

/// <summary>Deduplica strings repetidas (profissão, traço, tag de evento) com ids inteiros
/// estáveis — Fase 28, CMP-03. Determinístico na ordem de primeiro uso; não thread-safe.</summary>
public sealed class StringInternPool
{
    private readonly Dictionary<string, int> _indexByValue = new(StringComparer.Ordinal);
    private readonly List<string> _values = [];

    public int Count => _values.Count;

    public int Intern(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (_indexByValue.TryGetValue(value, out int existing))
            return existing;

        int id = _values.Count;
        _values.Add(value);
        _indexByValue[value] = id;
        return id;
    }

    public string Resolve(int id)
    {
        if ((uint)id >= (uint)_values.Count)
            throw new ArgumentOutOfRangeException(nameof(id));

        return _values[id];
    }
}
