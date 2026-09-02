using LivingWorld.Domain.Shared;

namespace LivingWorld.Domain.History;

/// <summary>Visão enviesada de um <see cref="Fact"/> enquanto há testemunha viva (Fase 10,
/// HIST-01 AC3) — fidelidade alta, sem operador de distorção (distorção só começa no relato).</summary>
public sealed record WitnessedAccount(
    FactId FactId,
    IReadOnlyList<NpcId> Participants,
    WorldEventKind Kind,
    long Tick,
    CityId? Location,
    double PerceivedSignificance,
    string Payload);
