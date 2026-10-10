namespace LivingWorld.Domain.Population.Skills;

/// <summary>Habilidade, identificada por id vindo do cenário — mesmo contrato de
/// <see cref="ProfessionType"/>: o motor nunca conhece o nome, só o id. Já foi um enum fechado de
/// 13 valores; abrir pra id evita qualquer literal de identidade de habilidade em
/// <c>src/</c>.</summary>
public readonly record struct SkillType(int Id);
