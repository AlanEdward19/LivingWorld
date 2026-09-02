using LivingWorld.Domain.Shared;

namespace LivingWorld.Domain.History.Distortion;

/// <summary>Metadata canônica compacta de um relato (Fase 10, HIST-01 AC4 / Opção C do design) —
/// fato de origem, meio, hops, peso de cânone. Payload distorcido é volátil (<see
/// cref="DistortedReport"/>).</summary>
public sealed record ReportState(
    ReportId Id,
    FactId OriginFactId,
    CityId CommunityId,
    TransmissionMediumType Medium,
    int HopCount,
    double Weight,
    long CreatedAtTick,
    long LastHopTick);
