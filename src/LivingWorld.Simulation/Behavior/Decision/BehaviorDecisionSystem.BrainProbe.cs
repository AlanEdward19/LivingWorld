using System.Globalization;
using System.Text;
using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Population;
using LivingWorld.Simulation.Cognition;
using LivingWorld.Simulation.Extraordinary.Mechanics;
using LivingWorld.Simulation.Observation;

namespace LivingWorld.Simulation.Behavior.Decision;

// Parte de BehaviorDecisionSystem — sonda do cérebro (AD-160): só roda com BrainDecisionTally.Diagnose, nunca no mundo normal.
public sealed partial class BehaviorDecisionSystem
{
    /// <summary>Pontua as mesmas ações no mesmo estado com e sem os ganhos do NPC e conta onde a escolha muda.</summary>
    private static void ProbeBrain(
        Npc npc, DecisionContext ctx, NeedsRules rules, EconomyRules economy, ActionType? continuityAction,
        ActionEligibility eligibility, ActionType brainChoice, long now)
    {
        var foresight = ctx.ForesightPreviews ?? ForesightMechanic.EmptyPreviews;
        var gains = BrainMechanic.GainsOf(npc);
        ActionType pure = ActionType.Eat;
        double pureScore = double.NegativeInfinity, pureWithGain = 0;
        foreach (var action in AllActions)
        {
            if (!eligibility.Allows(action)) continue;
            double s = ScoreOf(ctx, action, rules, economy, continuityAction, eligibility, foresight);
            if (s > pureScore) { pureScore = s; pure = action; }
        }

        pureWithGain = ScoreOf(ctx, pure, rules, economy, continuityAction, eligibility, foresight, gains: gains);
        var t = BrainDecisionTally.Current;
        t.Decisions++;
        t.MarginSum += Math.Abs(pureWithGain - pureScore);
        t.Matrix[(int)pure, (int)brainChoice]++;
        bool changed = pure != brainChoice;
        if (changed) t.Changed++;
        bool critical = SurvivalDeficit(ctx.Needs.Hunger) >= CriticalDeficit || SurvivalDeficit(ctx.Needs.Thirst) >= CriticalDeficit;
        if (critical)
        {
            t.Critical++;
            if (changed) t.CriticalChanged++;
            if (changed && pure is ActionType.Eat or ActionType.Buy && brainChoice is not (ActionType.Eat or ActionType.Buy))
                t.CriticalAwayFromSurvival++;
        }

        if (!changed || t.Samples.Count >= t.MaxSamples) return;
        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.Append(inv, $"npc={npc.Id.Value} t={now} fome={ctx.Needs.Hunger} sede={ctx.Needs.Thirst} sono={ctx.Needs.Sleep} social={ctx.Needs.Social} ");
        sb.Append(inv, $"carteira={ctx.WalletAmount} utilidade={pure}({pureScore:F1}->{pureWithGain:F1}) cerebro={brainChoice}(");
        sb.Append(inv, $"{ScoreOf(ctx, brainChoice, rules, economy, continuityAction, eligibility, foresight):F1}->");
        sb.Append(inv, $"{ScoreOf(ctx, brainChoice, rules, economy, continuityAction, eligibility, foresight, gains: gains):F1}) g[{brainChoice}]=");
        int row = (int)brainChoice * Domain.Cognition.BrainState.Groups, prow = (int)pure * Domain.Cognition.BrainState.Groups;
        for (int i = 0; i < Domain.Cognition.BrainState.Groups; i++) sb.Append(inv, $"{gains[row + i]},");
        sb.Append(inv, $" g[{pure}]=");
        for (int i = 0; i < Domain.Cognition.BrainState.Groups; i++) sb.Append(inv, $"{gains[prow + i]},");
        t.Samples.Add(sb.ToString());
    }
}
