namespace LivingWorld.Domain;

/// <summary>Opportunity derivada de contexto de decisão (Fase 16.3 P2b, COH-53 / doc#38-39) —
/// "o que posso fazer?" filtrado pelo que o Agent conhece/percebe.</summary>
public sealed record Opportunity(
    string Kind,
    double Attractiveness,
    string? Detail = null);
