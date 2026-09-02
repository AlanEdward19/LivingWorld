using LivingWorld.Domain;

namespace LivingWorld.Simulation;

/// <summary>
/// Escolha pura e reproduzível: <c>hash(seed, npcId, salt) → [0,1)</c> (EVO-16).
/// Usado nos rolls de herança e em escolhas internas do caminho "mistura".
/// Nunca System.Random, <c>HashCode.Combine</c> ou <c>string.GetHashCode</c>.
/// </summary>
public static class DeterministicChoice
{
    /// <summary>Mesma tripleta (seed, npcId, salt) → mesmo valor em todo processo/execução.</summary>
    public static double InUnitInterval(ulong seed, NpcId npcId, string salt)
    {
        ulong mixed = seed;
        mixed ^= StableHash.Mix(npcId.Value);
        mixed ^= StableHash.Mix(unchecked((long)Fnv1a64(salt ?? "")));
        mixed = Finalize(mixed);
        // Mesma conversão de WorldRng.NextDouble: 53 bits → [0,1).
        return (mixed >> 11) * (1.0 / (1UL << 53));
    }

    private static ulong Fnv1a64(string key)
    {
        ulong hash = 14695981039346656037UL;
        foreach (byte b in System.Text.Encoding.UTF8.GetBytes(key))
        {
            hash ^= b;
            hash *= 1099511628211UL;
        }
        return hash;
    }

    private static ulong Finalize(ulong h)
    {
        h = (h ^ (h >> 33)) * 0xFF51AFD7ED558CCDUL;
        h = (h ^ (h >> 33)) * 0xC4CEB9FE1A85EC53UL;
        return h ^ (h >> 33);
    }
}
