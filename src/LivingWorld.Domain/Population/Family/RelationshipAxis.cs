using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Population.Skills;

namespace LivingWorld.Domain.Population.Family;

/// <summary>Os 4 eixos numéricos de uma <see cref="Relationship"/> — nunca uma
/// flag booleana de "amizade". Modelo de decisão do motor, não conteúdo de cenário
/// (mesmo motivo de <see cref="ActionType"/> — <see cref="SkillType"/> deixou de ser fechado
/// depois).</summary>
public enum RelationshipAxis
{
    Trust,
    Affection,
    Respect,
    Debt,
}

/// <summary>Catálogo fechado dos eventos nomeados que alteram uma <see cref="Relationship"/>
/// — cada um mapeia para um delta declarado em <see cref="FamilyRules"/>, nunca um
/// literal solto em C#. <see cref="Conversation"/> é o único
/// evento entre um NPC e o jogador — todos os demais são NPC↔NPC.</summary>
public enum RelationshipEventType
{
    Cohabitation,
    Betrayal,
    Help,
    Trade,
    Conversation,
}

/// <summary>Os 6 fatores que compõem o score de atração de cortejo — cada um
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
