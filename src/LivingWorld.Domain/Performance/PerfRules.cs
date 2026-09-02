using LivingWorld.Domain.Shared;

namespace LivingWorld.Domain.Performance;

/// <summary>Tetos de custo e janela de arquivamento frio (Fase 9, PERF-03) — cenário-driven,
/// nunca literal em teste de sensor.</summary>
public sealed record PerfRules(
    double MaxMicrosPerAliveNpcTick,
    long MaxBytesAllocPerTick,
    long MaxBytesPerAliveNpcPerYear,
    int ColdArchiveAfterYears)
{
    public static Result<PerfRules> Create(
        double maxMicrosPerAliveNpcTick,
        long maxBytesAllocPerTick,
        long maxBytesPerAliveNpcPerYear,
        int coldArchiveAfterYears)
    {
        if (maxMicrosPerAliveNpcTick <= 0)
            return Result<PerfRules>.Fail("MaxMicrosPerAliveNpcTick: deve ser > 0");
        if (maxBytesAllocPerTick <= 0)
            return Result<PerfRules>.Fail("MaxBytesAllocPerTick: deve ser > 0");
        if (maxBytesPerAliveNpcPerYear <= 0)
            return Result<PerfRules>.Fail("MaxBytesPerAliveNpcPerYear: deve ser > 0");
        if (coldArchiveAfterYears <= 0)
            return Result<PerfRules>.Fail("ColdArchiveAfterYears: deve ser > 0");

        return Result<PerfRules>.Ok(new PerfRules(
            maxMicrosPerAliveNpcTick,
            maxBytesAllocPerTick,
            maxBytesPerAliveNpcPerYear,
            coldArchiveAfterYears));
    }

    /// <summary>Default do cenário medieval — tetos do sensor (PERF-16). Bytes/NPC/ano medido
    /// com fauna/flora horária + mapa default (~62k–76k); 4000 era pré-ecologia (16.4).</summary>
    public static readonly PerfRules Default = Create(
        maxMicrosPerAliveNpcTick: 15.0,
        maxBytesAllocPerTick: 400_000,
        maxBytesPerAliveNpcPerYear: 80_000,
        coldArchiveAfterYears: 10).Value
        ?? throw new InvalidOperationException("PerfRules.Default inválida — bug no cenário");

    /// <summary>Tetos iniciais do sensor de escala (PERF-02) antes de apertar com PERF-16 — medidos
    /// no cenário estável, não no default colapsado.</summary>
    public static readonly PerfRules ScaleSensorInitial = Create(
        maxMicrosPerAliveNpcTick: 500.0,
        maxBytesAllocPerTick: 200_000_000,
        maxBytesPerAliveNpcPerYear: 2_000_000,
        coldArchiveAfterYears: 10).Value
        ?? throw new InvalidOperationException("PerfRules.ScaleSensorInitial inválida");
}
