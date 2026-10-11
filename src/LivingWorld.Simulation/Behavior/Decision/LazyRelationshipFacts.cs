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
        var targets = world.RelationshipTargetsOf(owner);

        // A resposta só depende dos eixos das relações do dono e de quais elas são: enquanto nenhum eixo
        // de nenhuma relação mudou (AxesVersion) e o dono tem os mesmos alvos, vale a última resposta.
        // As relações mudam em lote no tick diário, então a maioria dos despertares do dia reaproveita.
        var memo = Memos.GetOrCreateValue(world);
        var key = (owner, minimum);
        long version = Relationship.AxesVersion;
        if (memo.TryGetValue(key, out var known) && known.Version == version && known.Targets == targets.Count)
            return known.Result;

        bool result = false;
        foreach (var to in targets)
        {
            var rel = world.Relationships[new RelationshipKey(owner, to)];
            if ((int)Math.Round(rel.Trust) >= minimum || (int)Math.Round(rel.Affection) >= minimum)
            {
                result = true;
                break;
            }
        }

        memo[key] = new BondMemo(version, targets.Count, result);
        return result;
    }

    private readonly record struct BondMemo(long Version, int Targets, bool Result);

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<
        WorldState, Dictionary<(NpcId Owner, int Minimum), BondMemo>> Memos = new();
}
