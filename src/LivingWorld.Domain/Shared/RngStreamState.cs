namespace LivingWorld.Domain.Shared;

/// <summary>Estado persistido de um stream de RNG (ADR-0005), identificado pela chave que o
/// sistema/entidade usou em <c>ctx.Rng(streamKey)</c>.</summary>
public readonly record struct RngStreamState(string Key, ulong State);
