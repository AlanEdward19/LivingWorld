using LivingWorld.Domain.Shared;

namespace LivingWorld.Infrastructure.Records;

/// <summary>Interning aditivo de <see cref="EventLogRecord.Kind"/> na fronteira de persistência
/// (Fase 28, T17). Primeira ocorrência de um kind permanece literal (compatível com linhas
/// pré-interning); repetições gravam <c>i:{id}</c>.</summary>
public static class EventLogKindEncoding
{
    public const string InternedPrefix = "i:";

    public static string Encode(string kind, StringInternPool pool)
    {
        ArgumentNullException.ThrowIfNull(kind);
        ArgumentNullException.ThrowIfNull(pool);

        int before = pool.Count;
        int id = pool.Intern(kind);
        return id < before ? $"{InternedPrefix}{id}" : kind;
    }

    public static string Decode(string stored, StringInternPool pool)
    {
        ArgumentNullException.ThrowIfNull(stored);
        ArgumentNullException.ThrowIfNull(pool);

        if (stored.StartsWith(InternedPrefix, StringComparison.Ordinal)
            && int.TryParse(stored.AsSpan(InternedPrefix.Length), out int id))
            return pool.Resolve(id);

        pool.Intern(stored);
        return stored;
    }

    public static void SeedPool(StringInternPool pool, IEnumerable<string> storedKindsInOrder)
    {
        ArgumentNullException.ThrowIfNull(pool);
        ArgumentNullException.ThrowIfNull(storedKindsInOrder);

        foreach (string stored in storedKindsInOrder)
            Decode(stored, pool);
    }
}
