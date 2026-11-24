namespace LivingWorld.Domain.Shared;

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

    /// <summary>Deriva um stream independente para uma entidade/sistema:
    /// mesma seed base + mesma stream key = mesma sequência, sem afetar outros streams.</summary>
    public WorldRng Derive(long streamKey)
    {
        ulong mixed = _state ^ unchecked((ulong)streamKey * 0x9E3779B97F4A7C15UL);
        return new WorldRng(SplitMix(ref mixed));
    }

    /// <summary>Estado inicial do stream que <see cref="Derive"/> criaria, sem alocar: para sorteios fixos lidos no caminho
    /// quente (AD-139). <c>NextDouble(ref estado)</c> dá a mesma sequência que o stream derivado.</summary>
    public ulong DeriveState(long streamKey)
    {
        ulong mixed = _state ^ unchecked((ulong)streamKey * 0x9E3779B97F4A7C15UL);
        return SplitMix(ref mixed);
    }

    public double NextDouble()
    {
        ulong z = SplitMix(ref _state);
        return (z >> 11) * (1.0 / (1UL << 53));
    }

    /// <summary>Mesmo sorteio de <see cref="NextDouble()"/> sobre um estado guardado pelo chamador (sem objeto).</summary>
    public static double NextDouble(ref ulong state)
    {
        ulong z = SplitMix(ref state);
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
