namespace LivingWorld.Tests.Shared;

/// <summary>Roda um corpo por seed em paralelo (mundos e RNG são por instância; ver .specs/features/parallel-scenario-seeds/plan.md)
/// com grau limitado e erro que sempre nomeia a seed real — nunca uma <see cref="AggregateException"/> opaca.
/// O resultado sai na ordem da lista de seeds, então a agregação feita depois pelo teste continua determinística.</summary>
public static class ParallelSeeds
{
    /// <summary>Grau único de paralelismo dos testes de cenário: não satura a máquina/CI.</summary>
    public static int Degree { get; } = Math.Clamp(Environment.ProcessorCount, 1, 8);

    /// <summary>Executa <paramref name="body"/> para cada seed da lista, na ordem dela no resultado.
    /// Se alguma seed falhar, lança uma exceção com a primeira seed da lista que falhou (o original vai em InnerException).</summary>
    public static T[] Map<T>(IReadOnlyList<ulong> seeds, Func<ulong, T> body)
    {
        var results = new T[seeds.Count];
        var failures = new Exception?[seeds.Count];
        var options = new ParallelOptions { MaxDegreeOfParallelism = Degree };
        Parallel.For(0, seeds.Count, options, index =>
        {
            try { results[index] = body(seeds[index]); }
            catch (Exception ex) { failures[index] = ex; }
        });

        for (int index = 0; index < seeds.Count; index++)
        {
            if (failures[index] is { } ex)
                throw new InvalidOperationException($"seed {seeds[index]} falhou: {ex.Message}", ex);
        }

        return results;
    }

    /// <summary>Faixa inclusiva <paramref name="first"/>..<paramref name="last"/>; vazia se <paramref name="last"/> &lt; <paramref name="first"/>.</summary>
    public static T[] Map<T>(ulong first, ulong last, Func<ulong, T> body) => Map(Range(first, last), body);

    /// <summary>Como <see cref="Map{T}(IReadOnlyList{ulong}, Func{ulong, T})"/>, sem valor de retorno.</summary>
    public static void ForEach(IReadOnlyList<ulong> seeds, Action<ulong> body) =>
        Map<bool>(seeds, seed => { body(seed); return true; });

    /// <summary>Como <see cref="Map{T}(ulong, ulong, Func{ulong, T})"/>, sem valor de retorno.</summary>
    public static void ForEach(ulong first, ulong last, Action<ulong> body) => ForEach(Range(first, last), body);

    private static ulong[] Range(ulong first, ulong last)
    {
        if (last < first) return [];
        var seeds = new ulong[checked((int)(last - first + 1))];
        for (int index = 0; index < seeds.Length; index++) seeds[index] = first + (ulong)index;
        return seeds;
    }
}
