namespace LivingWorld.Domain.Population.Skills;

/// <summary>Habilidade, identificada por id vindo do cenário (Fase 13, T11b) — mesmo contrato de
/// <see cref="ProfessionType"/>: o motor nunca conhece o nome, só o id. Antes da Fase 13 este
/// tipo era um enum fechado de 13 valores (Fase 6); abrir pra id evita qualquer literal de
/// identidade de habilidade em <c>src/</c>.</summary>
public readonly record struct SkillType(int Id);
