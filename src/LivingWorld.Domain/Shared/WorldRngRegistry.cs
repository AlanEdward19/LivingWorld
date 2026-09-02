namespace LivingWorld.Domain;

/// <summary>Um stream de <see cref="WorldRng"/> por chave, derivado uma única vez da seed raiz
/// (ADR-0005). O stream persiste e avança ao longo da run; a raiz nunca é consumida
/// diretamente (só serve de fonte para <see cref="WorldRng.Derive"/>), então adicionar um
/// stream novo não desloca a sequência dos streams já existentes.</summary>
public sealed class WorldRngRegistry
{
    private readonly WorldRng _root;
    private readonly SortedDictionary<string, WorldRng> _streams = new(StringComparer.Ordinal);

    public WorldRngRegistry(ulong seed) => _root = new WorldRng(seed);

    /// <summary>Reconstrói um registry a partir de um snapshot (rehidratação).</summary>
    public WorldRngRegistry(ulong seed, IEnumerable<RngStreamState> streams) : this(seed)
    {
        foreach (var s in streams)
            _streams[s.Key] = new WorldRng(s.State);
    }

    public WorldRng Stream(string key)
    {
        if (_streams.TryGetValue(key, out var existing))
            return existing;

        var derived = _root.Derive(StableHash(key));
        _streams[key] = derived;
        return derived;
    }

    /// <summary>Stream de rolagem única derivado de <c>(seed, purpose, id)</c> sem persistir no
    /// snapshot (PERF-13) — mesma sequência inicial que <see cref="Stream"/> produziria.</summary>
    public WorldRng StreamFor(string purpose, long id) =>
        _root.Derive(StableHash($"{purpose}-{id}"));

    /// <summary>Ordenado por chave — nunca por ordem de inserção/hash de dicionário.</summary>
    public IReadOnlyList<RngStreamState> Snapshot() =>
        _streams.Select(kv => new RngStreamState(kv.Key, kv.Value.State)).ToList();

    /// <summary>Hash FNV-1a de 64 bits: estável entre processos, ao contrário de
    /// <see cref="string.GetHashCode()"/>, que .NET randomiza por processo. Exposto como
    /// <c>internal</c> (task 7) para código Domain puro sem <see cref="WorldRngRegistry"/> à
    /// mão (ex.: <c>PopulationGenerator</c>, que só recebe um <see cref="WorldRng"/> já
    /// derivado) montar sub-streams nomeados a partir dele, mesma convenção de chave usada
    /// aqui.</summary>
    internal static long StableHash(string key)
    {
        ulong hash = 14695981039346656037UL;
        foreach (byte b in System.Text.Encoding.UTF8.GetBytes(key))
        {
            hash ^= b;
            hash *= 1099511628211UL;
        }
        return unchecked((long)hash);
    }
}
