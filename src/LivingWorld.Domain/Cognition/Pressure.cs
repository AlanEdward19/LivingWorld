namespace LivingWorld.Domain.Cognition;

/// <summary>Pressure derivada de contexto de decisão —
/// camada explicável "por que agir?", sem estado canônico novo.</summary>
public sealed record Pressure(
    string Kind,
    double Intensity,
    IReadOnlyList<string> Factors);
