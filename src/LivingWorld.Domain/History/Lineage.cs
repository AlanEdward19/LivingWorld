using LivingWorld.Domain.Shared;

namespace LivingWorld.Domain.History;

/// <summary>Linhagem derivada do esqueleto (Fase 10, HIST-22) — nunca tabela paralela.</summary>
public sealed record Lineage(
    NpcId Descendant,
    IReadOnlyList<LineageGeneration> Generations);

/// <summary>Uma geração na cadeia ancestral — índice 0 é o descendente consultado.</summary>
public sealed record LineageGeneration(
    NpcId Self,
    NpcId? MotherId,
    NpcId? FatherId,
    long? BirthTick,
    long? DeathTick);
