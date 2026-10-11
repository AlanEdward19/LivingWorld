using System.Collections;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Behavior.Decision;

/// <summary>Relações conhecidas do NPC (<c>A→*</c>), montadas só se alguém as ler por inteiro. A decisão
/// de todo despertar só precisa saber se existe vínculo forte — <see cref="AnyBondAtLeast"/> responde
/// sem alocar e com saída antecipada; a lista completa (rastro cognitivo, pressões) é materializada
/// uma vez, sob demanda, na mesma decisão em que foi pedida (o estado não muda no meio dela).</summary>
internal sealed class LazyRelationshipFacts(WorldState world, NpcId owner) : IReadOnlyList<RelationshipFact>
{
    private RelationshipFact[]? _facts;

    private RelationshipFact[] Facts => _facts ??= DecisionContextBuilder.MaterializeKnownRelationships(world, owner);

    public int Count => Facts.Length;

    public RelationshipFact this[int index] => Facts[index];

    public IEnumerator<RelationshipFact> GetEnumerator() => ((IEnumerable<RelationshipFact>)Facts).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>Existe relação com confiança ou afeto (arredondados como em <see cref="RelationshipFact"/>)
    /// de pelo menos <paramref name="minimum"/>?</summary>
    public bool AnyBondAtLeast(int minimum)
    {
        if (_facts is not null)
        {
            foreach (var fact in _facts)
                if (fact.Trust >= minimum || fact.Affection >= minimum) return true;
            return false;
        }

        if (world.Relationships.Count == 0) return false;
        foreach (var to in world.RelationshipTargetsOf(owner))
        {
            var rel = world.Relationships[new RelationshipKey(owner, to)];
            if ((int)Math.Round(rel.Trust) >= minimum || (int)Math.Round(rel.Affection) >= minimum)
                return true;
        }
        return false;
    }
}
