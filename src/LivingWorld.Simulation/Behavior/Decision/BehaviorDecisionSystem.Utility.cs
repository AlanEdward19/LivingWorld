using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Cognition;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Extraordinary;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Market;
using LivingWorld.Simulation.Extraordinary.Mechanics;
using LivingWorld.Simulation.Extraordinary.Opportunity;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Behavior.Decision;

// Parte de BehaviorDecisionSystem — seleção por utilidade: bases, urgência de sobrevivência, trilha da decisão.
public sealed partial class BehaviorDecisionSystem
{
    /// <summary>O rastro é montado sob demanda: quase nenhuma decisão é gravada no
    /// <see cref="NpcCognitionLog"/>, e derivar pressões/oportunidades custava mais que a
    /// própria escolha da ação.</summary>
    internal readonly struct UtilityDecision(
        ActionType action, PendingPowerInvocation? pendingPower, Func<DecisionTrace> trace)
    {
        public ActionType Action { get; } = action;
        public PendingPowerInvocation? PendingPower { get; } = pendingPower;
        public DecisionTrace Trace => trace();
    }

    /// <summary>Escolhe ação por utility e expõe <see cref="DecisionTrace"/> volátil.
    /// Foresight entra via <see cref="DecisionContext.ForesightPreviews"/>
    /// — dicionário vazio compartilhado no caminho comum, sem parâmetro paralelo omnisciente.</summary>
    internal static UtilityDecision SelectByUtility(
        DecisionContext ctx, NeedsRules rules, EconomyRules economy, ActionType? continuityAction,
        PowerUtilityRules? powerRules = null,
        WakeReason wakeReason = WakeReason.UrgentNeed,
        ActionType? previousIntent = null)
    {
        var utilityRules = PowerUtilityRules.Resolve(powerRules);
        var foresight = ctx.ForesightPreviews ?? ForesightMechanic.EmptyPreviews;
        var best = ActionType.Eat;
        double bestScore = double.NegativeInfinity;
        PendingPowerInvocation? bestPending = null;
        var scored = new List<(ActionType Action, double Score)>(AllActions.Length + ctx.PowerOpportunities.Count);

        foreach (var action in AllActions)
        {
            double score = UtilityBaseOf(ctx, action, economy) * PersonalityWeighting.WeightOf(ctx.Personality, action)
                + ContextFactorBonus(ctx, action);
            if (foresight.TryGetValue(action, out var preview))
                score *= ForesightUtilityFactor(preview);
            if (rules.HysteresisEnabled && continuityAction == action)
                score += rules.ContinuityBonus;

            scored.Add((action, score));
            if (score > bestScore)
            {
                bestScore = score;
                best = action;
                bestPending = null;
            }
        }

        foreach (var opp in ctx.PowerOpportunities)
        {
            // Spec edge: mechanic/candidate scoring failure is isolated — exclude this
            // opportunity from comparison; other candidates and fixed ActionTypes continue.
            double score;
            try
            {
                score = PowerOpportunityUtility(opp, ctx, utilityRules);
            }
            catch
            {
                continue;
            }

            scored.Add((ActionType.UsePower, score));
            if (score > bestScore)
            {
                bestScore = score;
                best = ActionType.UsePower;
                bestPending = new PendingPowerInvocation(
                    opp.PowerId, opp.MechanicToken, opp.SuggestedTarget);
            }
        }

        return new UtilityDecision(
            best, bestPending,
            () => BuildDecisionTrace(ctx, wakeReason, previousIntent, best, bestScore, scored));
    }

    private static DecisionTrace BuildDecisionTrace(
        DecisionContext ctx,
        WakeReason wakeReason,
        ActionType? previousIntent,
        ActionType winner,
        double winningUtility,
        List<(ActionType Action, double Score)> scored)
    {
        var pressures = PressureModel.DerivePressures(ctx);
        var opportunities = OpportunityModel.DeriveOpportunities(ctx);

        var topPressures = pressures
            .OrderByDescending(p => p.Intensity)
            .ThenBy(p => p.Kind, StringComparer.Ordinal)
            .Take(3)
            .ToList();

        var ordered = scored
            .GroupBy(s => s.Action)
            .Select(g => (Action: g.Key, Score: g.Max(x => x.Score)))
            .OrderByDescending(s => s.Score)
            .ThenBy(s => s.Action)
            .ToList();

        var alternatives = ordered
            .Where(s => s.Action != winner)
            .Take(3)
            .Select(s => s.Action)
            .ToList();

        var positives = new List<string>();
        foreach (var pressure in topPressures)
        {
            positives.Add($"{pressure.Kind}:{pressure.Intensity:0.#}");
            foreach (var factor in pressure.Factors.Take(2))
                positives.Add(factor);
        }

        foreach (var opp in opportunities.Take(2))
            positives.Add(opp.Kind);

        var negatives = new List<string>();
        var blocking = new List<string>();
        foreach (var alt in ordered.Where(s => s.Action != winner).Take(3))
        {
            negatives.Add($"{alt.Action}:{alt.Score:0.#}");
            if (alt.Score < 0 || alt.Score + 25 < winningUtility)
                blocking.Add($"{alt.Action}:outscored");
        }

        if (ctx.Household is null && winner is ActionType.Eat or ActionType.Buy)
            blocking.Add("NoHousehold");

        return new DecisionTrace(
            WakeReason: wakeReason,
            PreviousIntent: previousIntent,
            TopPressures: topPressures,
            KnownOpportunities: opportunities,
            Winner: winner,
            WinningUtility: winningUtility,
            TopPositiveFactors: positives,
            TopNegativeFactors: negatives,
            BlockingFactors: blocking,
            KnownAlternatives: alternatives);
    }

    /// <summary>Utility de candidato dinâmico de poder:
    /// <c>Urgency×w − Cost×w − Risk×w + Reliability×w</c>.</summary>
    internal static double PowerOpportunityUtility(
        PowerOpportunity opp, DecisionContext ctx, PowerUtilityRules rules)
    {
        double urgency = PowerUrgencyOf(ctx, opp);
        double reliabilityBonus = string.Equals(opp.Reliability, "Guaranteed", StringComparison.Ordinal)
            ? 1.0
            : string.Equals(opp.Reliability, "ResolutionCheck", StringComparison.Ordinal)
                ? 0.35
                : 0.1;

        return rules.UrgencyWeight * urgency
            - rules.CostWeight * (double)opp.EstimatedCost * 10.0
            - rules.RiskWeight * opp.EstimatedRisk * 100.0
            + rules.ReliabilityWeight * reliabilityBonus * 25.0;
    }

    /// <summary>Urgência contextual: déficit máximo de needs + boost ReachDestinationUrgently
    /// para teleport/movimento quando memória de ameaça ou necessidade de deslocamento.</summary>
    internal static double PowerUrgencyOf(DecisionContext ctx, PowerOpportunity opp)
    {
        double urgency = Math.Max(
            Math.Max(Deficit(ctx.Needs.Hunger), Deficit(ctx.Needs.Thirst)),
            Math.Max(Deficit(ctx.Needs.Sleep), Deficit(ctx.Needs.Social)));

        bool reachUrgent = ctx.RelevantMemories.Any(m =>
            ContainsAny(m.Content, "traído", "traido", "betray", "threat", "perigo", "danger"));
        bool locomotion = opp.MechanicToken.StartsWith("npc.teleport", StringComparison.Ordinal)
            || opp.MechanicToken.StartsWith("movement.", StringComparison.Ordinal);

        if (reachUrgent && locomotion)
            urgency = Math.Max(urgency, 95.0);

        return urgency;
    }

    /// <summary>Fator de utility a partir do preview de foresight:
    /// desfecho ruim → fator &lt; 1; sucesso → fator ≥ 1; PartialSuccess neutro.</summary>
    internal static double ForesightUtilityFactor(ResolutionResult preview) => preview switch
    {
        ResolutionResult.CriticalSuccess => 1.5,
        ResolutionResult.Success => 1.25,
        ResolutionResult.PartialSuccess => 1.0,
        ResolutionResult.Failure => 0.35,
        ResolutionResult.CriticalFailure => 0.1,
        _ => 1.0,
    };

    /// <summary>Bônus só quando memória/crença/relação estão presentes (COH-13/14) —
    /// listas vazias → 0, comportamento idêntico ao pré-DecisionContext (golden preservado).</summary>
    private static double ContextFactorBonus(DecisionContext ctx, ActionType action)
    {
        const double factorBonus = 40.0;
        return action switch
        {
            ActionType.Travel when ctx.RelevantMemories.Any(m =>
                ContainsAny(m.Content, "traído", "traido", "betray", "threat", "perigo", "danger")) => factorBonus,
            ActionType.Buy when ctx.RelevantBeliefs.Any(b =>
                ContainsAny(b, "scarcity", "escassez", "fome", "hunger", "food")) => factorBonus,
            ActionType.Socialize when HasStrongBond(ctx.KnownRelationships) => factorBonus,
            _ => 0.0,
        };
    }

    private static bool HasStrongBond(IReadOnlyList<RelationshipFact> known) =>
        known is LazyRelationshipFacts lazy
            ? lazy.AnyBondAtLeast(60)
            : known.Any(r => r.Trust >= 60 || r.Affection >= 60);

    private static bool ContainsAny(string text, params string[] tokens)
    {
        foreach (var token in tokens)
        {
            if (text.Contains(token, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static double UtilityBaseOf(DecisionContext ctx, ActionType action, EconomyRules economy) => action switch
    {
        ActionType.Eat => EatUtilityOf(ctx, economy),
        ActionType.Sleep => SurvivalDeficit(ctx.Needs.Sleep),
        ActionType.Socialize => Deficit(ctx.Needs.Social),
        ActionType.Buy => BuyUtilityOf(ctx, economy),
        ActionType.Work or ActionType.Travel or ActionType.Idle => NonNeedBaselineUtility,
        ActionType.UsePower => 0.0, // só vence via candidatos PowerOpportunity (T22)
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, "ActionType desconhecido"),
    };

    private static double EatUtilityOf(DecisionContext ctx, EconomyRules economy)
    {
        double deficit = Math.Max(SurvivalDeficit(ctx.Needs.Hunger), SurvivalDeficit(ctx.Needs.Thirst));
        if (!economy.Enabled) return deficit;
        if (ctx.Household is not { } household) return deficit;

        bool hasFood = household.Stock.GetValueOrDefault(new ResourceType(economy.FoodResourceId)) >= 1;
        bool hasWater = household.Stock.GetValueOrDefault(new ResourceType(economy.WaterResourceId)) >= 1;
        return hasFood || hasWater ? deficit : NonNeedBaselineUtility / 2;
    }

    private static double BuyUtilityOf(DecisionContext ctx, EconomyRules economy)
    {
        if (!economy.Enabled) return NonNeedBaselineUtility;
        if (ctx.Household is not { } household)
            return NonNeedBaselineUtility;

        long food = household.Stock.GetValueOrDefault(new ResourceType(economy.FoodResourceId));
        long water = household.Stock.GetValueOrDefault(new ResourceType(economy.WaterResourceId));
        bool needsFood = food < 1;
        bool needsWater = water < 1;
        double provision = ProvisionPressureOf(ctx, household, Math.Min(food, water));
        if (!needsFood && !needsWater) return Math.Max(NonNeedBaselineUtility, provision);

        double own = Math.Max(needsFood ? SurvivalDeficit(ctx.Needs.Hunger) : 0, needsWater ? SurvivalDeficit(ctx.Needs.Thirst) : 0);
        return Math.Max(own, provision);
    }

    /// <summary>Quem tem dinheiro abastece a casa antes de ela esvaziar, e não só quando sente fome
    /// (crianças sem renda dependem disso): sobe de ~55 com 2 dias de estoque por membro até 130 com
    /// a despensa vazia — acima do que o peso de personalidade dá a Work.</summary>
    private static double ProvisionPressureOf(DecisionContext ctx, HouseholdSnapshot household, long scarcestStock)
    {
        if (ctx.WalletAmount < 1) return 0;
        int members = Math.Max(1, household.Members.Count);
        double daysPerMember = (double)scarcestStock / members;
        double shortfall = 1.0 - daysPerMember / ProvisionTargetDays;
        return shortfall <= 0 ? 0 : 55 + 75 * Math.Min(1.0, shortfall);
    }

    private static int Deficit(int need) => 100 - need;

    /// <summary>Déficit linear até <see cref="CriticalDeficit"/>; acima dele cada ponto vale
    /// <c>1 + CriticalEscalation</c> — a urgência dispara quando a necessidade vira crítica.</summary>
    private static double SurvivalDeficit(int need)
    {
        int deficit = Deficit(need);
        return deficit <= CriticalDeficit ? deficit : deficit + (deficit - CriticalDeficit) * CriticalEscalation;
    }

    internal static ActionType ResolveWithStepCap(
        long npcId, ActionType initial, WorldState world, Npc npc, MarketIndex marketIndex, int maxSteps)
    {
        var current = initial;
        for (int step = 0; step < maxSteps; step++)
        {
            var next = RefineForLocation(world, npc, current, marketIndex);
            if (next == current) return current;
            current = next;
        }

        var final = RefineForLocation(world, npc, current, marketIndex);
        throw new TickBudgetExceededException(
            $"npc {npcId}: seleção de ação não convergiu em {maxSteps} passos — ações empatadas em ciclo ({current} <-> {final})",
            maxSteps);
    }

    /// <summary>Overload for tests that supply a custom refine step without capturing world state.</summary>
    internal static ActionType ResolveWithStepCap(long npcId, ActionType initial, Func<ActionType, ActionType> refine, int maxSteps)
    {
        var current = initial;
        for (int step = 0; step < maxSteps; step++)
        {
            var next = refine(current);
            if (next == current) return current;
            current = next;
        }

        var final = refine(current);
        throw new TickBudgetExceededException(
            $"npc {npcId}: seleção de ação não convergiu em {maxSteps} passos — ações empatadas em ciclo ({current} <-> {final})",
            maxSteps);
    }
}
