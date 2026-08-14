using LivingWorld.Domain;

namespace LivingWorld.Simulation;

public enum NpcInspectionLod
{
    Materialized = 0,
    Archived = 1,
}

public sealed record NpcActionTargetDto(string Kind, string Id);

/// <summary>Retrato somente-leitura de um NPC vivo para inspeção (Fase 8, T14, CITY-06) —
/// identidade, família, profissão, atributos e rotina. "Memórias" é sempre lista vazia nesta
/// fase: sistema de memória individual é Fase 10/11 (AD-068, design.md Tech Decisions); o campo
/// existe como contrato futuro-compatível, sem inventar dado que o motor ainda não produz.</summary>
public sealed record NpcInspectionDto(
    NpcId Id,
    string Name,
    Sex Sex,
    int AgeYears,
    CultureId Culture,
    CityId City,
    HouseholdId? Household,
    NpcId? MotherId,
    NpcId? FatherId,
    NpcId? Spouse,
    ProfessionType Profession,
    WorkplaceId? Employer,
    int Health,
    int Hunger,
    int Thirst,
    int Sleep,
    int Social,
    Personality Personality,
    SkillSet Skills,
    CellCoord CurrentLocation,
    ActionType? CurrentAction,
    long ActionStartedAtTick,
    NpcActionTargetDto? ActionTarget,
    NpcInspectionLod Lod,
    IReadOnlyList<string> Beliefs,
    IReadOnlyList<string> Memories);
