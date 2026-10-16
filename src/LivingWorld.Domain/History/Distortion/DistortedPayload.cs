using LivingWorld.Domain.Shared;

namespace LivingWorld.Domain.History.Distortion;

/// <summary>Payload estruturado intermediário para operadores de distorção —
/// transformado hop a hop por <c>DistortionEngine</c>.</summary>
public sealed record DistortedPayload(
    IReadOnlyList<NpcId> Participants,
    double Magnitude,
    long Tick,
    string Payload,
    string MoralSeed,
    double DistanceFromFact);
