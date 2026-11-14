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

    /// <summary>AD-147: fome ou sede crítica tira o lazer da frente (o teto de Conversar e Ficar à toa).</summary>
    BodyOverride,
}

/// <summary>Quanto um termo somou (ou tirou) da utilidade de uma ação.</summary>
public readonly record struct UtilityContribution(ActionType Action, UtilityTerm Term, double Value);

/// <summary>Grupo de termos da utilidade (AD-142): a camada do meio da rede legível termo → grupo → ação. Cada grupo de cada
/// ação tem um ganho sináptico por NPC (<see cref="BrainState"/>). A ordem é o índice no vetor de ganhos.</summary>
public enum UtilityGroup : byte
{
    Base,
    Comfort,
    Personality,
    Context,
    Affect,
    Pressure,
    Foresight,
    Continuity,
}

// Parte de BehaviorDecisionSystem — a utilidade de cada ação como soma de termos nomeados.
public sealed partial class BehaviorDecisionSystem
{
    /// <summary>Utilidade de <paramref name="action"/>: <c>(base × (1 + conforto) × peso de personalidade + contexto + vida
    /// interior) × antevisão + continuidade</c>, termo a termo. Com <paramref name="sink"/> nulo (caminho quente) só soma; com
    /// sink, cada termo diferente de zero entra nele. <paramref name="knockout"/> zera um termo (teste de intervenção): o
    /// termo multiplicativo vira fator neutro. Com <paramref name="gains"/> (cérebro, AD-142) cada grupo de termos entra
    /// multiplicado pelo ganho que o NPC herdou e aprendeu, <c>Σ grupo × (1 + g/1000)</c>, e o rastro mostra o valor com ganho;
    /// <paramref name="groups"/> (8 posições, opcional) recebe quanto cada <see cref="UtilityGroup"/> somou, já com ganho.</summary>
    internal static double ScoreOf(
        DecisionContext ctx, ActionType action, NeedsRules rules, EconomyRules economy, ActionType? continuityAction,
        ActionEligibility eligibility, IReadOnlyDictionary<ActionType, ResolutionResult> foresight,
        List<UtilityContribution>? sink = null, UtilityTerm? knockout = null, short[]? gains = null, double[]? groups = null)
    {
        if (gains is null)
        {
            var none = default(NoBrain);
            return ScoreOf(ctx, action, rules, economy, continuityAction, eligibility, foresight, sink, knockout, ref none);
        }

        var lens = new WithBrain(gains, groups, 0);
        return ScoreOf(ctx, action, rules, economy, continuityAction, eligibility, foresight, sink, knockout, ref lens);
    }

    /// <summary>Como o cérebro enxerga a soma (AD-142). Especializado por tipo: com <see cref="NoBrain"/> o JIT apaga tudo e a
    /// conta é a de antes, na mesma ordem de operações e sem custo; com <see cref="WithBrain"/> cada grupo soma com o seu ganho.</summary>
    internal interface IBrainLens
    {
        void Begin(ActionType action);
        double Factor(UtilityGroup group);
        void Credit(UtilityGroup group, double value);
        double Total { get; }
    }

    internal struct NoBrain : IBrainLens
    {
        public readonly void Begin(ActionType action) { }
        public readonly double Factor(UtilityGroup group) => 1.0;
        public readonly void Credit(UtilityGroup group, double value) { }
        public readonly double Total => 0;
    }

    /// <summary>Ganhos da linha da ação; <c>groups[offset..offset+8]</c> (opcional) recebe a contribuição de cada grupo.</summary>
    internal struct WithBrain(short[] gains, double[]? groups, int offset) : IBrainLens
    {
        private int _row;
        private double _total;

        public void Begin(ActionType action)
        {
            _row = (int)action * BrainState.Groups;
            _total = 0;
            if (groups is not null) Array.Clear(groups, offset, BrainState.Groups);
        }

        public readonly double Factor(UtilityGroup group) => 1.0 + gains[_row + (int)group] * 0.001;

        public void Credit(UtilityGroup group, double value)
        {
            double v = value * Factor(group);
            _total += v;
            if (groups is not null) groups[offset + (int)group] += v;
        }

        public readonly double Total => _total;
    }

    private static double ScoreOf<TLens>(
        DecisionContext ctx, ActionType action, NeedsRules rules, EconomyRules economy, ActionType? continuityAction,
        ActionEligibility eligibility, IReadOnlyDictionary<ActionType, ResolutionResult> foresight,
        List<UtilityContribution>? sink, UtilityTerm? knockout, ref TLens lens)
        where TLens : struct, IBrainLens
    {
        lens.Begin(action);
        double baseValue = BaseTermOf(ctx, action, economy, eligibility, sink is not null || knockout is not null, out var baseTerm);
        if (knockout is { } k && k == baseTerm) baseValue = 0;
        lens.Credit(UtilityGroup.Base, baseValue);
        Note(sink, action, baseTerm, baseValue, lens.Factor(UtilityGroup.Base));

        double comfortFactor = action switch
        {
            ActionType.Eat => ctx.Comfort.Eat,
            ActionType.Sleep => ctx.Comfort.Sleep,
            _ => 0.0,
        };
        if (knockout == UtilityTerm.Comfort) comfortFactor = 0;
        double comforted = baseValue * (1.0 + comfortFactor);
        lens.Credit(UtilityGroup.Comfort, comforted - baseValue);
        Note(sink, action, UtilityTerm.Comfort, comforted - baseValue, lens.Factor(UtilityGroup.Comfort));

        double weight = knockout == UtilityTerm.Personality ? 1.0 : PersonalityWeighting.WeightOf(ctx.Personality, action);
        double score = comforted * weight;
        lens.Credit(UtilityGroup.Personality, score - comforted);
        Note(sink, action, UtilityTerm.Personality, score - comforted, lens.Factor(UtilityGroup.Personality));

        var (contextTerm, contextBonus) = ContextFactorBonus(ctx, action, economy);
        if (contextBonus != 0 && knockout != contextTerm)
        {
            score += contextBonus;
            lens.Credit(UtilityGroup.Context, contextBonus);
            Note(sink, action, contextTerm, contextBonus, lens.Factor(UtilityGroup.Context));
        }

        score += InnerLifeTerms(ctx, action, rules.Psyche, economy, baseValue, sink, knockout, ref lens);
        score += HormoneTerms(ctx, action, baseValue, sink, knockout, ref lens);

        // AD-139: o jovem no pico do impulso desconta parte das consequências que a antevisão mostra e muda de ideia mais rápido
        var hormones = knockout == UtilityTerm.Adolescence ? default : ctx.Hormones;
        if (foresight.TryGetValue(action, out var preview) && knockout != UtilityTerm.Foresight)
        {
            double factor = ForesightUtilityFactor(preview);
            double factored = score * factor;
            lens.Credit(UtilityGroup.Foresight, factored - score);
            Note(sink, action, UtilityTerm.Foresight, factored - score, lens.Factor(UtilityGroup.Foresight));
            if (hormones.ForesightDiscount > 0)
            {
                double discounted = score * (factor + (1 - factor) * hormones.ForesightDiscount);
                lens.Credit(UtilityGroup.Foresight, discounted - factored);
                Note(sink, action, UtilityTerm.Adolescence, discounted - factored, lens.Factor(UtilityGroup.Foresight));
                factored = discounted;
            }
            score = factored;
        }

        if (rules.HysteresisEnabled && continuityAction == action && knockout != UtilityTerm.Continuity)
        {
            score += rules.ContinuityBonus;
            lens.Credit(UtilityGroup.Continuity, rules.ContinuityBonus);
            Note(sink, action, UtilityTerm.Continuity, rules.ContinuityBonus, lens.Factor(UtilityGroup.Continuity));
            if (hormones.Fickleness > 0)
            {
                double fickle = -rules.ContinuityBonus * hormones.Fickleness;
                score += fickle;
                lens.Credit(UtilityGroup.Continuity, fickle);
                Note(sink, action, UtilityTerm.Adolescence, fickle, lens.Factor(UtilityGroup.Continuity));
            }
        }

        // AD-147 (backlog 08, item 47): o lazer não passa na frente do corpo. Com fome ou sede crítica, Conversar e Ficar à toa
        // param no déficit crítico, abaixo de qualquer Comer que a despensa sirva. Antes o peso de personalidade (até 3×) e o vínculo
        // levavam Conversar a ~300 contra 160 de Comer com a sede no zero, e quem estava sozinho em casa (sem ninguém para aliviar a
        // solidão) conversava de hora em hora até morrer de sede com água na despensa.
        // Só quando a despensa serve a necessidade crítica (Comer passa do déficit crítico): sem o que comer em casa, o teto mudava
        // a escolha de quem não tinha saída e a fome subia (seeds 1–2 × 100 anos: 4,4% → 7,7%). Estendê-lo a Trabalhar e Comprar
        // (tentativa 3) levou a fome de 6,4% a 11,7% em 20 × 100.
        if (rules.Psyche.CriticalBodyOverridesLeisure && action is ActionType.Socialize or ActionType.Idle
            && knockout != UtilityTerm.BodyOverride && score > CriticalDeficit
            && EatUtilityOf(ctx, economy) > CriticalDeficit)
        {
            double cut = CriticalDeficit - score;
            score = CriticalDeficit;
            lens.Credit(UtilityGroup.Base, cut / lens.Factor(UtilityGroup.Base));
            Note(sink, action, UtilityTerm.BodyOverride, cut);
        }

        return typeof(TLens) == typeof(NoBrain) ? score : lens.Total;
    }

    /// <summary>O rastro guarda o valor que de fato somou: o termo vezes o fator do grupo (1 sem cérebro, x × 1 = x).</summary>
    private static void Note(List<UtilityContribution>? sink, ActionType action, UtilityTerm term, double value, double factor = 1.0)
    {
        if (sink is not null && value != 0) sink.Add(new UtilityContribution(action, term, value * factor));
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
    private static double InnerLifeTerms<TLens>(
        DecisionContext ctx, ActionType action, PsycheRules psyche, EconomyRules economy, double baseValue,
        List<UtilityContribution>? sink, UtilityTerm? knockout, ref TLens lens)
        where TLens : struct, IBrainLens
    {
        if (!psyche.Enabled) return 0;
        var mood = ctx.Mood;
        double sum = 0;
        switch (action)
        {
            case ActionType.Work:
                AddInner(ref sum, ref lens, sink, action, knockout, UtilityTerm.IncomeNeed, IncomeNeedOf(ctx, economy, psyche));
                AddInner(ref sum, ref lens, sink, action, knockout, UtilityTerm.Grief, -psyche.GriefWorkPenalty * mood.Grief);
                AddInner(ref sum, ref lens, sink, action, knockout, UtilityTerm.Shame, -psyche.GriefWorkPenalty * psyche.ShameWithdrawal * mood.Shame);
                break;
            case ActionType.Idle:
                AddInner(ref sum, ref lens, sink, action, knockout, UtilityTerm.Grief, psyche.GriefIdleBonus * mood.Grief);
                break;
            case ActionType.Socialize:
                AddInner(ref sum, ref lens, sink, action, knockout, UtilityTerm.Grief, -psyche.GriefSocialPenalty * mood.Grief);
                AddInner(ref sum, ref lens, sink, action, knockout, UtilityTerm.Shame, -psyche.ShameSocialPenalty * mood.Shame);
                AddInner(ref sum, ref lens, sink, action, knockout, UtilityTerm.Joy, psyche.JoySocialBonus * mood.Joy);
                AddInner(ref sum, ref lens, sink, action, knockout, UtilityTerm.Solitude, -psyche.SolitudeDiscount * baseValue * mood.Loneliness);
                break;
            case ActionType.Travel:
                AddInner(ref sum, ref lens, sink, action, knockout, UtilityTerm.Fear, -psyche.FearTravelPenalty * mood.Fear);
                break;
            case ActionType.Buy when ctx.FamineMemory > 0 && ctx.WalletAmount >= 1 && PantryShortfall(ctx, economy) > 0:
                AddInner(ref sum, ref lens, sink, action, knockout, UtilityTerm.FamineMemory, psyche.FamineProvisionBonus * ctx.FamineMemory / 100.0);
                break;
        }

        return sum;
    }

    /// <summary>Um termo da vida interior: entra na soma de sempre, na ordem de sempre, e no grupo (Affect ou Pressure).</summary>
    private static void AddInner<TLens>(
        ref double sum, ref TLens lens, List<UtilityContribution>? sink, ActionType action, UtilityTerm? knockout,
        UtilityTerm term, double value)
        where TLens : struct, IBrainLens
    {
        if (value == 0 || knockout == term) return;
        sum += value;
        var group = term is UtilityTerm.IncomeNeed or UtilityTerm.FamineMemory ? UtilityGroup.Pressure : UtilityGroup.Affect;
        lens.Credit(group, value);
        Note(sink, action, term, value, lens.Factor(group));
    }

    /// <summary>Hormônios na escolha (AD-139), pelo mesmo caminho da vida interior: a cólica tira do trabalho e puxa para o
    /// descanso, a irritação pré-menstrual afasta do convívio e o jovem no pico do impulso procura os pares.</summary>
    private static double HormoneTerms<TLens>(
        DecisionContext ctx, ActionType action, double baseValue, List<UtilityContribution>? sink, UtilityTerm? knockout,
        ref TLens lens)
        where TLens : struct, IBrainLens
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
        AddInner(ref sum, ref lens, sink, action, knockout, UtilityTerm.Menstruation, menstruation);
        AddInner(ref sum, ref lens, sink, action, knockout, UtilityTerm.Adolescence, adolescence);
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
