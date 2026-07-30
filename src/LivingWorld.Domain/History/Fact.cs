namespace LivingWorld.Domain;

/// <summary>Esqueleto imutável Tier A de um fato histórico (Fase 10, HIST-01) — quem, o quê,
/// onde, quando, significância. Nunca mutado após a escrita; entra no hash via
/// <c>WorldState.Facts</c>.</summary>
public sealed record Fact(
    FactId Id,
    long Tick,
    WorldEventKind Kind,
    List<NpcId> Participants,
    CityId? Location,
    double Significance,
    string Payload);
