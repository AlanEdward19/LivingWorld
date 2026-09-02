using LivingWorld.Domain;

namespace LivingWorld.Simulation;

public enum NpcInspectionLod
{
    Materialized = 0,
    Archived = 1,
    // T50: membro do pool agregado com NpcId reservado (City.PoolNpcIds) mas ainda não
    // materializado — sem atributos reais pra devolver (não existem até sortear), o DTO só
    // carrega id/cidade de verdade; o resto vem com valor placeholder (mesmo espírito de
    // FromNpcSummary pra Archived). Cliente usa este Lod pra oferecer "Materializar" em vez de
    // mostrar um erro genérico de "não encontrado".
    Pooled = 2,
}

public sealed record NpcActionTargetDto(string Kind, string Id);

public sealed record NpcRestStatusDto(
    RestPlaceKind Kind,
    double Quality,
    CellCoord Location,
    long RemainingHours,
    bool Blocked);

public sealed record NpcFoodStatusDto(
    int ResourceId,
    PreparationState Preparation,
    long RemainingHours,
    bool Blocked);

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
    IReadOnlyList<string> Memories,
    IReadOnlyList<string> PowerIds,
    // T50 (bug "seguir NPC entre escopos"): mesmo critério geométrico de
    // GlobalProjector/LivingScopeProjector (NpcScopeResolver), agora também aqui — cliente usa
    // pra saber que o NPC seguido cruzou de cidade pro mundo (ou vice-versa) e trocar de tela.
    NpcScope CurrentScope,
    // Fase 28 T10 (COG-10, COG-12, COG-13): rastro de decisão do side-store — leitura pura,
    // lista vazia explícita quando não há entradas; nunca recalculado aqui.
    IReadOnlyList<TraceEntry> CognitionTrace,
    NpcRestStatusDto? Rest = null,
    NpcFoodStatusDto? Food = null);
