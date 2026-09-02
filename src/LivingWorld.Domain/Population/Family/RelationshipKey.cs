using LivingWorld.Domain.Shared;

namespace LivingWorld.Domain.Population.Family;

/// <summary>Chave de <see cref="Relationship"/> — par ordenado <c>(From, To)</c>, nunca
/// normalizado (Fase 7, T3, AD-052). A assimetria é o próprio propósito do tipo (FAM-05):
/// <c>RelationshipKey(A, B)</c> e <c>RelationshipKey(B, A)</c> são chaves distintas por
/// construção, a igualdade estrutural de <c>record struct</c> já garante isso.</summary>
public readonly record struct RelationshipKey(NpcId From, NpcId To);
