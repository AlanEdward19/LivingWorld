namespace LivingWorld.Domain;

/// <summary>Os 4 eixos numéricos de uma <see cref="Relationship"/> (Fase 7, T1) — nunca uma
/// flag booleana de "amizade" (FAM-01). Modelo de decisão do motor, não conteúdo de cenário
/// (mesmo motivo de <see cref="SkillType"/>).</summary>
public enum RelationshipAxis
{
    Trust,
    Affection,
    Respect,
    Debt,
}

/// <summary>Catálogo fechado dos eventos nomeados que alteram uma <see cref="Relationship"/>
/// (FAM-03) — cada um mapeia para um delta declarado em <see cref="FamilyRules"/>, nunca um
/// literal solto em C#. <see cref="Conversation"/> (Fase 11, retrofit T6, LLM-09 AC3) é o único
/// evento entre um NPC e o jogador — todos os demais são NPC↔NPC.</summary>
public enum RelationshipEventType
{
    Cohabitation,
    Betrayal,
    Help,
    Trade,
    Conversation,
}

/// <summary>Os 6 fatores que compõem o score de atração de cortejo (FAM-06) — cada um
/// normalizado <c>[0,1]</c> antes de aplicar o peso declarado em <see cref="FamilyRules"/>
/// (Risco do design: sem normalização, um fator de magnitude maior dominaria silenciosamente).</summary>
public enum AttractionFactor
{
    Age,
    Health,
    Status,
    Skill,
    CulturalAffinity,
    ExistingRelationship,
}
