using LivingWorld.Domain.Shared;

namespace LivingWorld.Domain.History.Distortion;

/// <summary>Metadata canônica compacta de um relato —
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
