using LivingWorld.Domain.Shared;

namespace LivingWorld.Domain.History.Distortion;

/// <summary>Correção do passado como evento novo anexado — a linha original
/// nunca é reescrita.</summary>
public sealed record CompensatingCorrection(
    FactId CorrectsFactId,
    FactId CorrectionFactId,
    long Tick,
    string Reason);

/// <summary>Entrada na linha do tempo com marcação de papel (original vs correção).</summary>
public sealed record MarkedFactEntry(Fact Fact, FactLineRole Role);

public enum FactLineRole
{
    Original,
    Correction,
}
