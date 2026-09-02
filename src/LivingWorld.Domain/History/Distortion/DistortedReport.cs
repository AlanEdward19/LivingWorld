using LivingWorld.Domain.Shared;

namespace LivingWorld.Domain.History.Distortion;

/// <summary>Relato distorcido materializado sob demanda (Fase 10, Opção C) — volátil, nunca
/// serializado no snapshot canônico.</summary>
public sealed record DistortedReport(
    ReportId ReportId,
    IReadOnlyList<NpcId> AttributedParticipants,
    double DistortedMagnitude,
    long DistortedTick,
    string MoralizedNarrativeSeed,
    double DistanceFromFact);
