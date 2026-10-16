namespace LivingWorld.Domain.Cognition;

/// <summary>Opportunity derivada de contexto de decisão —
/// "o que posso fazer?" filtrado pelo que o Agent conhece/percebe.</summary>
public sealed record Opportunity(
    string Kind,
    double Attractiveness,
    string? Detail = null);
