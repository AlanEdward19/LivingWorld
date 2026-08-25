namespace LivingWorld.Domain;

/// <summary>RNG determinístico do mundo (splitmix64). Nunca o RNG do BCL — o algoritmo não
/// tem garantia de estabilidade entre versões do runtime, e o mundo precisa do mesmo
/// resultado em dois processos com a mesma seed.</summary>
public sealed class WorldRng
{
    private ulong _state;

    public WorldRng(ulong seed) => _state = seed;

    /// <summary>Estado interno atual — usado só para snapshot/rehidratação de streams
    /// (<see cref="WorldRngRegistry"/>), nunca para decisão de negócio.</summary>
    public ulong State => _state;

    /// <summary>Cópia no ponto atual da sequência. Sorteios no fork não avançam este stream.</summary>
    public WorldRng Fork() => new(_state);

    /// <summary>Deriva um stream independente para uma entidade/sistema (ADR-0005):
    /// mesma seed base + mesma stream key = mesma sequência, sem afetar outros streams.</summary>
    public WorldRng Derive(long streamKey)
    {
        ulong mixed = _state ^ unchecked((ulong)streamKey * 0x9E3779B97F4A7C15UL);
        return new WorldRng(SplitMix(ref mixed));
    }

    public double NextDouble()
    {
        ulong z = SplitMix(ref _state);
        return (z >> 11) * (1.0 / (1UL << 53));
    }

    private static ulong SplitMix(ref ulong state)
    {
        state += 0x9E3779B97F4A7C15UL;
        ulong z = state;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
}
