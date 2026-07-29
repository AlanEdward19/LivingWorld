using System.Security.Cryptography;
using System.Text;

namespace LivingWorld.Simulation.Snapshot;

/// <summary>Hash canônico incremental por entidade (PERF-12).</summary>
public static class IncrementalHasher
{
    public static string CombineIncremental(IReadOnlyDictionary<long, string> perEntityHash)
    {
        var ordered = perEntityHash.OrderBy(kv => kv.Key).Select(kv => kv.Value);
        var payload = string.Join('\n', ordered);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }

    /// <summary>Verifica equivalência incremental vs hash completo (PERF-12). Hoje recomputa o
    /// canônico inteiro como oráculo — substituir por combinação de entidades sujas quando o
    /// rastreio de dirty estiver no <see cref="WorldState"/>.</summary>
    public static bool MatchesCanonical(WorldState world) =>
        WorldSnapshot.CanonicalHash(world) == WorldSnapshot.CanonicalHashFromEntityParts(world);
}
