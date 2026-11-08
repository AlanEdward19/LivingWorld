namespace LivingWorld.Tests.Shared;

/// <summary>Roda um corpo por seed em paralelo (mundos e RNG são por instância; ver .specs/features/parallel-scenario-seeds/plan.md)
/// com grau limitado e erro que sempre nomeia a seed — nunca uma <see cref="AggregateException"/> opaca.
/// O resultado sai na ordem das seeds, então a agregação feita depois pelo teste continua determinística.</summary>
public static class ParallelSeeds
{
    /// <summary>Grau único de paralelismo dos testes de cenário: não satura a máquina/CI.</summary>
    public static int Degree { get; } = Math.Clamp(Environment.ProcessorCount, 1, 8);

    /// <summary>Executa <paramref name="body"/> para cada seed de <paramref name="first"/> a <paramref name="last"/> (inclusive).
    /// Se alguma seed falhar, lança uma exceção com a menor seed que falhou (o original vai em InnerException).</summary>
    public static T[] Map<T>(ulong first, ulong last, Func<ulong, T> body)
    {
        int count = checked((int)(last - first + 1));
        var results = new T[count];
        var failures = new Exception?[count];
        var options = new ParallelOptions { MaxDegreeOfParallelism = Degree };
        Parallel.For(0, count, options, index =>
        {
            try { results[index] = body(first + (ulong)index); }
            catch (Exception ex) { failures[index] = ex; }
        });

        for (int index = 0; index < count; index++)
        {
            if (failures[index] is { } ex)
                throw new InvalidOperationException($"seed {first + (ulong)index} falhou: {ex.Message}", ex);
        }

        return results;
    }

    /// <summary>Como <see cref="Map{T}"/>, sem valor de retorno.</summary>
    public static void ForEach(ulong first, ulong last, Action<ulong> body) =>
        Map<bool>(first, last, seed => { body(seed); return true; });
}
