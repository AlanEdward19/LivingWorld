using LivingWorld.Domain.Shared;

namespace LivingWorld.Domain.Extraordinary;

/// <summary>Id monotônico de encontro de combate multi-round (Fase 16.4, REALISM-16).</summary>
public readonly record struct CombatEncounterId(long Value);

/// <summary>Estado de um encontro ativo ou encerrado.</summary>
public enum CombatEncounterStatus
{
    Active,
    Fled,
    Resolved,
}

/// <summary>Encontro persistente entre ticks — AD-010: criado por <c>combat.engage:</c>, não por
/// <c>combat.strike:</c>.</summary>
public sealed record CombatEncounter(
    CombatEncounterId Id,
    NpcId Attacker,
    NpcId Defender,
    int Magnitude,
    int RoundsElapsed,
    CombatEncounterStatus Status);

/// <summary>Regras de combate multi-round declaradas por cenário (teto + fuga).</summary>
public sealed record CombatRules(
    int MaxRounds,
    int FleeHealthThreshold,
    double FleeProbability)
{
    public static CombatRules Default { get; } = new(MaxRounds: 8, FleeHealthThreshold: 25, FleeProbability: 0.35);
}

/// <summary>Resultado de um round de encontro.</summary>
public enum CombatRoundOutcome
{
    Continuing,
    Fled,
    Resolved,
}
