using LivingWorld.Domain;
using LivingWorld.Domain.Llm;

namespace LivingWorld.Simulation;

/// <summary>Snapshot efêmero por (NpcId, wake) para scoring de decisão (Fase 16.3 P1b,
/// COH-11) — nunca persistido, nunca <c>[Canonical]</c>, nunca carrega referência a
/// <see cref="WorldState"/>. Coleções são listas vazias quando sem fatores, nunca <c>null</c>.</summary>
public sealed record DecisionContext(
    NpcId NpcId,
    long Tick,
    NeedsSnapshot Needs,
    BodySnapshot Body,
    HouseholdSnapshot? Household,
    IReadOnlyList<NpcMemory> RelevantMemories,
    IReadOnlyList<string> RelevantBeliefs,
    IReadOnlyList<RelationshipFact> KnownRelationships,
    IReadOnlyList<PowerOpportunity> PowerOpportunities,
    Personality Personality,
    ActionType? CurrentAction);

public readonly record struct NeedsSnapshot(int Hunger, int Thirst, int Sleep, int Social);

public readonly record struct BodySnapshot(
    double Height,
    double Weight,
    double MuscleMass,
    double WorkCapacityMultiplier,
    double MovementCostMultiplier);

public sealed record HouseholdSnapshot(
    HouseholdId Id,
    IReadOnlyDictionary<ResourceType, long> Stock,
    IReadOnlyList<NpcId> Members);

/// <summary>4 eixos de <see cref="Relationship"/> expostos ao scoring — <c>Familiarity</c>
/// espelha o eixo Debt (4º eixo canônico) no shape do DecisionContext (design P1b).</summary>
public readonly record struct RelationshipFact(
    NpcId With,
    int Trust,
    int Affection,
    int Respect,
    int Familiarity);

/// <summary>Candidato dinâmico de poder no utility loop (shape P1d; lista vazia em P1b até
/// <c>PowerOpportunityProvider</c> na Phase 4).</summary>
public sealed record PowerOpportunity(
    string MechanicToken,
    NpcId? SuggestedTarget,
    decimal EstimatedCost,
    double EstimatedRisk,
    string Reliability);
