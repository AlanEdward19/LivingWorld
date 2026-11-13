using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Cognition;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Extraordinary.Mechanics;

namespace LivingWorld.Simulation.Behavior.Decision;

/// <summary>Uma sinapse lida: o termo (neurônio de entrada) que passou pelo grupo (camada do meio) até a ação (saída), com o
/// valor que de fato somou e o ganho (milésimos) que o NPC tem nesse grupo dessa ação.</summary>
public readonly record struct BrainSynapse(ActionType Action, UtilityTerm Term, UtilityGroup Group, double Value, int GainMilli);

/// <summary>A rede de uma decisão, termo → grupo → ação: as sinapses e a nota de cada ação (a soma delas).</summary>
public sealed record BrainReading(IReadOnlyList<BrainSynapse> Synapses, IReadOnlyDictionary<ActionType, double> Scores);

/// <summary>"Ver o cérebro" (ADR-0046, opção b como visualização da c): projeção volátil (ADR-0014), recomputável do canônico,
/// que só existe quando alguém lê; não entra no tick. Usa a mesma <c>ScoreOf</c> do motor, então a soma das sinapses de uma
/// ação é exatamente a nota que ela teve.</summary>
public static class BrainReadout
{
    public static BrainReading Read(
        DecisionContext ctx, NeedsRules rules, EconomyRules economy, short[]? gains, ActionType? continuityAction = null,
        ActionEligibility eligibility = default)
    {
        var foresight = ctx.ForesightPreviews ?? ForesightMechanic.EmptyPreviews;
        var terms = new List<UtilityContribution>();
        var scores = new Dictionary<ActionType, double>();
        foreach (var action in Enum.GetValues<ActionType>())
        {
            if (action is ActionType.UsePower or ActionType.Steal || !eligibility.Allows(action)) continue;
            scores[action] = BehaviorDecisionSystem.ScoreOf(ctx, action, rules, economy, continuityAction, eligibility, foresight, terms, gains: gains);
        }

        var synapses = new List<BrainSynapse>(terms.Count);
        foreach (var t in terms)
        {
            var group = GroupOf(t.Term);
            int gain = gains is null ? 0 : gains[(int)t.Action * BrainState.Groups + (int)group];
            synapses.Add(new BrainSynapse(t.Action, t.Term, group, t.Value, gain));
        }

        return new BrainReading(synapses, scores);
    }

    /// <summary>Leitura de um NPC real com o contexto que o motor montaria agora. Leitura pura: nada muda no mundo.</summary>
    public static Result<BrainReading> Read(WorldState world, NpcId id)
    {
        var npc = world.FindNpc(id);
        if (npc is null || !npc.IsAlive) return Result<BrainReading>.Fail("NpcId: NPC ausente, morto ou ainda não materializado");
        var ctx = NpcDecisionSandbox.BuildContext(world, npc);
        var eligibility = LivingWorld.Simulation.Ontogeny.OntogenyMechanic.EligibilityFor(world.OntogenyRules, npc.Development);
        var gains = world.BrainOn ? LivingWorld.Simulation.Cognition.BrainMechanic.GainsOf(npc) : null;
        return Result<BrainReading>.Ok(Read(ctx, world.NeedsRules, world.EconomyRules, gains, npc.CurrentAction, eligibility));
    }

    /// <summary>Em que grupo da fórmula cada termo entra (a camada do meio da rede).</summary>
    public static UtilityGroup GroupOf(UtilityTerm term) => term switch
    {
        UtilityTerm.Comfort => UtilityGroup.Comfort,
        UtilityTerm.Personality => UtilityGroup.Personality,
        UtilityTerm.ThreatMemory or UtilityTerm.ScarcityBelief or UtilityTerm.StrongBond => UtilityGroup.Context,
        UtilityTerm.IncomeNeed or UtilityTerm.FamineMemory => UtilityGroup.Pressure,
        UtilityTerm.Grief or UtilityTerm.Fear or UtilityTerm.Joy or UtilityTerm.Shame or UtilityTerm.Solitude => UtilityGroup.Affect,
        UtilityTerm.Foresight => UtilityGroup.Foresight,
        UtilityTerm.Continuity => UtilityGroup.Continuity,
        _ => UtilityGroup.Base,
    };
}
