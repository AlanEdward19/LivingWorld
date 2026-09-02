namespace LivingWorld.Domain;

/// <summary>Mistura determinística (splitmix-style finalizer) para derivar geometria estável de
/// um id (Fase 15.1, T45) — nunca <c>HashCode.Combine</c>/<c>string.GetHashCode</c>: ambos são
/// re-semeados por processo no .NET moderno, o que quebraria a estabilidade entre processos que
/// este módulo promete (footprint/posição idênticos sempre que chamados com o mesmo id).</summary>
public static class StableHash
{
    public static ulong Mix(long value)
    {
        ulong h = unchecked((ulong)value);
        h = (h ^ (h >> 33)) * 0xFF51AFD7ED558CCDUL;
        h = (h ^ (h >> 33)) * 0xC4CEB9FE1A85EC53UL;
        return h ^ (h >> 33);
    }
}
