namespace LivingWorld.Domain;

/// <summary>Pressure derivada de contexto de decisão (Fase 16.3 P2b, COH-51/52 / doc#33-34) —
/// camada explicável "por que agir?", sem estado canônico novo.</summary>
public sealed record Pressure(
    string Kind,
    double Intensity,
    IReadOnlyList<string> Factors);
