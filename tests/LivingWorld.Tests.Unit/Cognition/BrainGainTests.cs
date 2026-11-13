using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Cognition;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Decision;
using LivingWorld.Simulation.Extraordinary.Mechanics;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Cognition;

/// <summary>AD-142 (ADR-0046): ganhos sinápticos sobre a utilidade. Neutro decide igual à utilidade sem cérebro; um ganho muda a
/// nota na fração do grupo; o rastro com ganho soma a nota; a intervenção do AD-138 continua valendo.</summary>
public class BrainGainTests
{
    private static readonly NeedsRules Rules = ScenarioRunner.DefaultNeedsRules;
    private static readonly EconomyRules Economy = ScenarioRunner.DefaultEconomyRules;

    internal static DecisionContext Ctx(int hunger, int sleep, int social, long food, long wallet, Affect mood)
    {
        var household = new HouseholdSnapshot(new HouseholdId(1),
            new Dictionary<ResourceType, long>
            {
                [new ResourceType(Economy.FoodResourceId)] = food,
                [new ResourceType(Economy.WaterResourceId)] = food + 1,
            },
            [new NpcId(1), new NpcId(2)]);
        var personality = Personality.Create(50, 50, 50, 50, 50, 60, 50, 50, 50, 50).Value!;
        return new DecisionContext(new NpcId(1), 10, new NeedsSnapshot(hunger, 80, sleep, social),
            new BodySnapshot(1.75, 70, 30, 1.0, 1.0), household, [], [], [], [], personality, CurrentAction: null,
            WalletAmount: wallet, ProvisionTargetDays: 4, Inner: new InnerLifeSnapshot(mood, 0));
    }

    private static IEnumerable<DecisionContext> Situations()
    {
        int[] needs = [5, 40, 75, 95];
        Affect[] moods = [Affect.Calm, new(0.8, 0.3, 0, 0, 0.4, 0), new(0, 0, 0, 0.7, 0, 0.5)];
        int k = 0;
        foreach (int hunger in needs)
            foreach (int sleep in needs)
                foreach (int social in needs)
                    foreach (var mood in moods)
                    {
                        k++;
                        yield return Ctx(hunger, sleep, social, food: k % 3 * 2, wallet: k % 4 == 0 ? 0 : 30, mood);
                    }
    }

    private static double Score(DecisionContext ctx, ActionType action, short[]? gains, UtilityTerm? knockout = null) =>
        BehaviorDecisionSystem.ScoreOf(ctx, action, Rules, Economy, null, default, ForesightMechanic.EmptyPreviews, knockout: knockout, gains: gains);

    private static short[] Neutral() => new short[BrainState.Actions * BrainState.Groups];

    [Fact]
    public void Neutral_gains_choose_exactly_what_the_utility_without_brain_chooses()
    {
        var off = Situations().Select(c => BehaviorDecisionSystem.SelectByUtility(c, Rules, Economy, null, captureTrace: false).Action);
        var on = Situations().Select(c => BehaviorDecisionSystem.SelectByUtility(c, Rules, Economy, null, captureTrace: false, brainGains: Neutral()).Action);

        Assert.Equal(off, on);
    }

    [Fact]
    public void A_gain_on_a_group_scales_only_that_group_of_that_action()
    {
        var gains = Neutral();
        gains[(int)ActionType.Eat * BrainState.Groups + (int)UtilityGroup.Base] = 500;
        var ctx = Ctx(20, 90, 90, food: 3, wallet: 30, Affect.Calm);
        var groups = new double[BrainState.Groups];
        BehaviorDecisionSystem.ScoreOf(ctx, ActionType.Eat, Rules, Economy, null, default, ForesightMechanic.EmptyPreviews, gains: Neutral(), groups: groups);
        double baseGroup = groups[(int)UtilityGroup.Base];

        Assert.Equal(Score(ctx, ActionType.Eat, Neutral()) + 0.5 * baseGroup, Score(ctx, ActionType.Eat, gains), 9);
    }

    [Fact]
    public void Gains_of_one_action_do_not_touch_the_score_of_another()
    {
        var gains = Neutral();
        for (int g = 0; g < BrainState.Groups; g++) gains[(int)ActionType.Eat * BrainState.Groups + g] = 1000;
        var ctx = Ctx(20, 30, 50, food: 3, wallet: 30, new(0.5, 0, 0, 0, 0, 0));

        Assert.Equal(Score(ctx, ActionType.Sleep, Neutral()), Score(ctx, ActionType.Sleep, gains), 12);
    }

    [Fact]
    public void Readout_synapses_of_each_action_sum_to_the_score_the_engine_gives()
    {
        var gains = Neutral();
        for (int i = 0; i < gains.Length; i++) gains[i] = (short)(i * 37 % 1500 - 500);
        foreach (var ctx in Situations())
        {
            var reading = BrainReadout.Read(ctx, Rules, Economy, gains);
            foreach (var (action, score) in reading.Scores)
                Assert.Equal(score, reading.Synapses.Where(s => s.Action == action).Sum(s => s.Value), 9);
        }
    }

    [Fact]
    public void Knocking_out_a_term_with_learned_gain_moves_the_score_in_the_direction_of_its_sign()
    {
        var gains = Neutral();
        for (int i = 0; i < gains.Length; i++) gains[i] = (short)(i * 53 % 1500 - 500);
        int checkedTerms = 0;
        foreach (var ctx in Situations())
        {
            foreach (var synapse in BrainReadout.Read(ctx, Rules, Economy, gains).Synapses)
            {
                // base e conforto multiplicam os grupos seguintes (a forma da fórmula): o corte em cascata é do AD-138, sem ganho
                if (Math.Abs(synapse.Value) < 1e-6 || synapse.Group is UtilityGroup.Base or UtilityGroup.Comfort) continue;
                double delta = Score(ctx, synapse.Action, gains) - Score(ctx, synapse.Action, gains, synapse.Term);
                Assert.True(Math.Sign(delta) == Math.Sign(synapse.Value), $"{synapse.Action}/{synapse.Term}: {synapse.Value} vs Δ {delta}");
                checkedTerms++;
            }
        }

        Assert.True(checkedTerms > 100);
    }
}
