namespace LivingWorld.Api.Visual;

/// <summary>Fase 15, T1: posição de replay dentro de um escopo — usado para reidratar snapshot
/// e retomar deltas após reconexão, sem escrita de mundo.</summary>
public sealed record VisualCursor(long Tick, string ScopeKey, long Sequence);
