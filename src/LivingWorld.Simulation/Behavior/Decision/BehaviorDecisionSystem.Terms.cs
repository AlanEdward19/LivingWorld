using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Cognition;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Simulation.Behavior.Decision;

/// <summary>Termo nomeado da utilidade de uma ação (AD-138). O rastro cognitivo é feito só destes termos, com o peso que cada
/// um teve na soma: o que aparece como "por que fiz isso" é exatamente o que decidiu (achado 11).</summary>
public enum UtilityTerm : byte
{
    Hunger,
    Thirst,
    Sleep,
    Social,
    Provision,
    EmptyPantry,
    Baseline,
    Cry,
    Comfort,
    Personality,
    ThreatMemory,
    ScarcityBelief,
    StrongBond,
    IncomeNeed,
    FamineMemory,
    Grief,
    Fear,
    Joy,
    Shame,
    Solitude,
    Foresight,
    Continuity,
    Menstruation,
    Adolescence,
}

/// <summary>Quanto um termo somou (ou tirou) da utilidade de uma ação.</summary>
public readonly record struct UtilityContribution(ActionType Action, UtilityTerm Term, double Value);

// Parte de BehaviorDecisionSystem — a utilidade de cada ação como soma de termos nomeados.
public sealed partial class BehaviorDecisionSystem
{
    /// <summary>Utilidade de <paramref name="action"/>: <c>(base × (1 + conforto) × peso de personalidade + contexto + vida
    /// interior) × antevisão + continuidade</c>, termo a termo. Com <paramref name="sink"/> nulo (caminho quente) só soma; com
    /// sink, cada termo diferente de zero entra nele. <paramref name="knockout"/> zera um termo (teste de intervenção): o
    /// termo multiplicativo vira fator neutro.</summary>
    internal static double ScoreOf(
        DecisionContext ctx, ActionType action, NeedsRules rules, EconomyRules economy, ActionType? continuityAction,
        ActionEligibility eligibility, IReadOnlyDictionary<ActionType, ResolutionResult> foresight,
        List<UtilityContribution>? sink = null, UtilityTerm? knockout = null)
    {
        double baseValue = BaseTermOf(ctx, action, economy, eligibility, sink is not null || knockout is not null, out var baseTerm);
        if (knockout is { } k && k == baseTerm) baseValue = 0;
        Note(sink, action, baseTerm, baseValue);

        double comfortFactor = action switch
        {
            ActionType.Eat => ctx.Comfort.Eat,
            ActionType.Sleep => ctx.Comfort.Sleep,
            _ => 0.0,
        };
        if (knockout == UtilityTerm.Comfort) comfortFactor = 0;
        double comforted = baseValue * (1.0 + comfortFactor);
        Note(sink, action, UtilityTerm.Comfort, comforted - baseValue);

        double weight = knockout == UtilityTerm.Personality ? 1.0 : PersonalityWeighting.WeightOf(ctx.Personality, action);
        double score = comforted * weight;
        Note(sink, action, UtilityTerm.Personality, score - comforted);

        var (contextTerm, contextBonus) = ContextFactorBonus(ctx, action, economy);
        if (contextBonus != 0 && knockout != contextTerm)
        {
            score += contextBonus;
            Note(sink, action, contextTerm, contextBonus);
        }

        score += InnerLifeTerms(ctx, action, rules.Psyche, economy, baseValue, sink, knockout);
        score += HormoneTerms(ctx, action, baseValue, sink, knockout);

        // AD-139: o jovem no pico do impulso desconta parte das consequências que a antevisão mostra e muda de ideia mais rápido
        var hormones = knockout == UtilityTerm.Adolescence ? default : ctx.Hormones;
        if (foresight.TryGetValue(action, out var preview) && knockout != UtilityTerm.Foresight)
        {
            double factor = ForesightUtilityFactor(preview);
            double factored = score * factor;
            Note(sink, action, UtilityTerm.Foresight, factored - score);
            if (hormones.ForesightDiscount > 0)
            {
                double discounted = score * (factor + (1 - factor) * hormones.ForesightDiscount);
                Note(sink, action, UtilityTerm.Adolescence, discounted - factored);
                factored = discounted;
            }
            score = factored;
        }

        if (rules.HysteresisEnabled && continuityAction == action && knockout != UtilityTerm.Continuity)
        {
            score += rules.ContinuityBonus;
            Note(sink, action, UtilityTerm.Continuity, rules.ContinuityBonus);
            if (hormones.Fickleness > 0)
            {
                score -= rules.ContinuityBonus * hormones.Fickleness;
                Note(sink, action, UtilityTerm.Adolescence, -rules.ContinuityBonus * hormones.Fickleness);
            }
        }

        return score;
    }

    private static void Note(List<UtilityContribution>? sink, ActionType action, UtilityTerm term, double value)
    {
        if (sink is not null && value != 0) sink.Add(new UtilityContribution(action, term, value));
    }

    /// <summary>Base da ação e o nome do que a move (a necessidade que domina, a despensa, ou o valor de referência).</summary>
    private static double BaseTermOf(DecisionContext ctx, ActionType action, EconomyRules economy, ActionEligibility eligibility, bool named, out UtilityTerm term)
    {
        double value = UtilityBaseOf(ctx, action, economy, eligibility);
        term = UtilityTerm.Baseline;
        if (!named) return value; // caminho quente: o nome só importa para o rastro e o teste de intervenção
        term = action switch
        {
            ActionType.Eat => EatTermOf(ctx, economy),
            ActionType.Sleep => UtilityTerm.Sleep,
            ActionType.Socialize => UtilityTerm.Social,
            ActionType.Buy => BuyTermOf(ctx, economy),
            ActionType.Cry => UtilityTerm.Cry,
            _ => UtilityTerm.Baseline,
        };
        return value;
    }

    private static UtilityTerm EatTermOf(DecisionContext ctx, EconomyRules economy)
    {
        bool hungerLeads = SurvivalDeficit(ctx.Needs.Hunger) >= SurvivalDeficit(ctx.Needs.Thirst);
        if (!economy.Enabled || ctx.Household is not { } household) return hungerLeads ? UtilityTerm.Hunger : UtilityTerm.Thirst;
        bool hasFood = household.Stock.GetValueOrDefault(new ResourceType(economy.FoodResourceId)) >= 1;
        bool hasWater = household.Stock.GetValueOrDefault(new ResourceType(economy.WaterResourceId)) >= 1;
        if (!hasFood && !hasWater) return UtilityTerm.EmptyPantry;
        double hunger = hasFood ? SurvivalDeficit(ctx.Needs.Hunger) : 0, thirst = hasWater ? SurvivalDeficit(ctx.Needs.Thirst) : 0;
        return hunger >= thirst ? UtilityTerm.Hunger : UtilityTerm.Thirst;
    }

    private static UtilityTerm BuyTermOf(DecisionContext ctx, EconomyRules economy)
    {
        if (!economy.Enabled || ctx.Household is not { } household) return UtilityTerm.Baseline;
        long food = household.Stock.GetValueOrDefault(new ResourceType(economy.FoodResourceId));
        long water = household.Stock.GetValueOrDefault(new ResourceType(economy.WaterResourceId));
        double provision = ProvisionPressureOf(ctx, household, Math.Min(food, water));
        bool needsFood = food < 1, needsWater = water < 1;
        if (!needsFood && !needsWater) return provision > NonNeedBaselineUtility ? UtilityTerm.Provision : UtilityTerm.Baseline;
        double hunger = needsFood ? SurvivalDeficit(ctx.Needs.Hunger) : 0, thirst = needsWater ? SurvivalDeficit(ctx.Needs.Thirst) : 0;
        double own = Math.Max(hunger, thirst);
        if (provision > own) return UtilityTerm.Provision;
        return hunger >= thirst ? UtilityTerm.Hunger : UtilityTerm.Thirst;
    }

    /// <summary>Vida interior na escolha (AD-138): luto tira do trabalho e do convívio e puxa para casa, vergonha recolhe,
    /// alegria aproxima, medo segura em casa, a solidão recente desconta a esperança de achar alguém, e quem não tem moeda com a
    /// despensa baixa pesa ganhar o pão; quem passou fome reforça a ida ao mercado.</summary>
    private static double InnerLifeTerms(
        DecisionContext ctx, ActionType action, PsycheRules psyche, EconomyRules economy, double baseValue,
        List<UtilityContribution>? sink, UtilityTerm? knockout)
    {
        if (!psyche.Enabled) return 0;
        var mood = ctx.Mood;
        double sum = 0;
        void Add(UtilityTerm term, double value)
        {
            if (value == 0 || knockout == term) return;
            sum += value;
            Note(sink, action, term, value);
        }

        switch (action)
        {
            case ActionType.Work:
                Add(UtilityTerm.IncomeNeed, IncomeNeedOf(ctx, economy, psyche));
                Add(UtilityTerm.Grief, -psyche.GriefWorkPenalty * mood.Grief);
                Add(UtilityTerm.Shame, -psyche.GriefWorkPenalty * psyche.ShameWithdrawal * mood.Shame);
                break;
            case ActionType.Idle:
                Add(UtilityTerm.Grief, psyche.GriefIdleBonus * mood.Grief);
                break;
            case ActionType.Socialize:
                Add(UtilityTerm.Grief, -psyche.GriefSocialPenalty * mood.Grief);
                Add(UtilityTerm.Shame, -psyche.ShameSocialPenalty * mood.Shame);
                Add(UtilityTerm.Joy, psyche.JoySocialBonus * mood.Joy);
                Add(UtilityTerm.Solitude, -psyche.SolitudeDiscount * baseValue * mood.Loneliness);
                break;
            case ActionType.Travel:
                Add(UtilityTerm.Fear, -psyche.FearTravelPenalty * mood.Fear);
                break;
            case ActionType.Buy when ctx.FamineMemory > 0 && ctx.WalletAmount >= 1 && PantryShortfall(ctx, economy) > 0:
                Add(UtilityTerm.FamineMemory, psyche.FamineProvisionBonus * ctx.FamineMemory / 100.0);
                break;
        }

        return sum;
    }

    /// <summary>Hormônios na escolha (AD-139), pelo mesmo caminho da vida interior: a cólica tira do trabalho e puxa para o
    /// descanso, a irritação pré-menstrual afasta do convívio e o jovem no pico do impulso procura os pares.</summary>
    private static double HormoneTerms(
        DecisionContext ctx, ActionType action, double baseValue, List<UtilityContribution>? sink, UtilityTerm? knockout)
    {
        var h = ctx.Hormones;
        double menstruation = action switch
        {
            ActionType.Work => -h.CrampWork,
            ActionType.Idle => h.CrampIdle,
            ActionType.Socialize => -h.IrritableSocial,
            _ => 0,
        };
        double adolescence = action == ActionType.Socialize ? h.PeerSocial * baseValue : 0; // reforça a vontade social, não a cria
        double sum = 0;
        if (menstruation != 0 && knockout != UtilityTerm.Menstruation)
        {
            sum += menstruation;
            Note(sink, action, UtilityTerm.Menstruation, menstruation);
        }
        if (adolescence != 0 && knockout != UtilityTerm.Adolescence)
        {
            sum += adolescence;
            Note(sink, action, UtilityTerm.Adolescence, adolescence);
        }
        return sum;
    }

    /// <summary>Sem moeda nenhuma e com a despensa da casa abaixo da meta, trabalhar é o caminho para comer: até
    /// <see cref="PsycheRules.IncomeNeedMax"/>, proporcional ao que falta.</summary>
    private static double IncomeNeedOf(DecisionContext ctx, EconomyRules economy, PsycheRules psyche) =>
        ctx.WalletAmount >= 1 ? 0 : psyche.IncomeNeedMax * PantryShortfall(ctx, economy);

    /// <summary>Quanto falta (0..1) para a despensa da casa chegar à meta de dias por morador.</summary>
    private static double PantryShortfall(DecisionContext ctx, EconomyRules economy)
    {
        if (!economy.Enabled || ctx.Household is not { } household || ctx.ProvisionTargetDays <= 0) return 0;
        long scarcest = Math.Min(
            household.Stock.GetValueOrDefault(new ResourceType(economy.FoodResourceId)),
            household.Stock.GetValueOrDefault(new ResourceType(economy.WaterResourceId)));
        double days = (double)scarcest / Math.Max(1, household.Members.Count);
        return Math.Clamp(1.0 - days / ctx.ProvisionTargetDays, 0, 1);
    }
}
