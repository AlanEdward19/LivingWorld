using System.Globalization;
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

/// <summary>AD-138 (achado 11): o pensamento registrado é o motivo. Todo termo do rastro entrou na soma com o peso mostrado, e
/// tirá-lo (intervenção) mexe na nota da vencedora na direção do sinal e, quando ele era decisivo, muda a escolha.</summary>
public class ThoughtIsMotiveTests
{
    private static readonly NeedsRules Rules = ScenarioRunner.DefaultNeedsRules;
    private static readonly EconomyRules Economy = ScenarioRunner.DefaultEconomyRules;

    private static DecisionContext Ctx(
        int hunger, int thirst, int sleep, int social, long food, long water, long wallet, Affect mood, double famine, int ambition)
    {
        var household = new HouseholdSnapshot(new HouseholdId(1),
            new Dictionary<ResourceType, long>
            {
                [new ResourceType(Economy.FoodResourceId)] = food,
                [new ResourceType(Economy.WaterResourceId)] = water,
            },
            [new NpcId(1), new NpcId(2)]);
        var personality = Personality.Create(50, 50, 50, 50, 50, ambition, 50, 50, 50, 50).Value!;
        return new DecisionContext(new NpcId(1), 10, new NeedsSnapshot(hunger, thirst, sleep, social),
            new BodySnapshot(1.75, 70, 30, 1.0, 1.0), household, [], [], [], [], personality, CurrentAction: null,
            WalletAmount: wallet, ProvisionTargetDays: 4, Inner: new InnerLifeSnapshot(mood, famine));
    }

    /// <summary>Uma grade determinística de situações: necessidades, despensa, carteira, humor e lembrança de fome.</summary>
    private static IEnumerable<DecisionContext> Situations()
    {
        int[] needs = [5, 35, 70, 95];
        Affect[] moods =
        [
            Affect.Calm,
            new(Grief: 0.9, Fear: 0, Anger: 0, Joy: 0, Shame: 0, Loneliness: 0),
            new(Grief: 0, Fear: 0.7, Anger: 0.3, Joy: 0, Shame: 0.5, Loneliness: 0),
            new(Grief: 0, Fear: 0, Anger: 0, Joy: 0.8, Shame: 0, Loneliness: 0.6),
        ];
        int k = 0;
        foreach (int hunger in needs)
            foreach (int sleep in needs)
                foreach (int social in needs)
                    foreach (var mood in moods)
                    {
                        k++;
                        long food = k % 3 == 0 ? 0 : k % 3 * 3;
                        long wallet = k % 4 == 0 ? 0 : 40;
                        yield return Ctx(hunger, 100 - (k * 7 % 60), sleep, social, food, food + 1, wallet, mood,
                            famine: k % 5 == 0 ? 70 : 0, ambition: 20 + k * 13 % 70);
                    }
    }

    private static double Score(DecisionContext ctx, ActionType action, UtilityTerm? knockout = null) =>
        BehaviorDecisionSystem.ScoreOf(ctx, action, Rules, Economy, null, default, ForesightMechanic.EmptyPreviews, knockout: knockout);

    private static readonly ActionType[] Actions = Enum.GetValues<ActionType>().Where(a => a is not (ActionType.UsePower or ActionType.Steal)).ToArray();

    private static (UtilityTerm Term, double Value)[] WinnerTerms(DecisionTrace trace) =>
        trace.TopPositiveFactors.Concat(trace.TopNegativeFactors)
            .Select(f => f.Split(':'))
            .Where(p => Enum.TryParse<UtilityTerm>(p[0], out _))
            .Select(p => (Enum.Parse<UtilityTerm>(p[0]), double.Parse(p[1], CultureInfo.InvariantCulture)))
            .ToArray();

    [Fact]
    public void Every_term_in_the_trace_is_part_of_the_winning_sum()
    {
        foreach (var ctx in Situations())
        {
            var decision = BehaviorDecisionSystem.SelectByUtility(ctx, Rules, Economy, continuityAction: null);
            var trace = decision.Trace;
            var terms = WinnerTerms(trace);
            Assert.NotEmpty(terms);
            // o rastro arredonda a 0,1: a soma mostrada bate com a nota dentro do arredondamento
            Assert.InRange(terms.Sum(t => t.Value), trace.WinningUtility - 0.06 * terms.Length, trace.WinningUtility + 0.06 * terms.Length);
            Assert.All(trace.TopPressures, p => Assert.All(p.Factors, f => Assert.True(Enum.TryParse<UtilityTerm>(f, out _))));
        }
    }

    [Fact]
    public void Removing_a_traced_term_moves_the_winner_in_its_direction_and_flips_the_choice_when_decisive()
    {
        int terms = 0, passed = 0, decisive = 0, flipped = 0;
        foreach (var ctx in Situations())
        {
            var trace = BehaviorDecisionSystem.SelectByUtility(ctx, Rules, Economy, continuityAction: null).Trace;
            var winner = trace.Winner;
            double runnerUp = Actions.Where(a => a != winner).Max(a => Score(ctx, a));
            foreach (var (term, value) in WinnerTerms(trace))
            {
                if (Math.Abs(value) < 0.05) continue; // arredondado a zero no rastro
                terms++;
                double without = Score(ctx, winner, term);
                bool rightDirection = value > 0 ? without < trace.WinningUtility : without > trace.WinningUtility;
                var newWinner = Actions.OrderByDescending(a => Score(ctx, a, term)).ThenBy(a => a).First();
                bool shouldFlip = value > 0 && without < Actions.Where(a => a != winner).Max(a => Score(ctx, a, term));
                if (shouldFlip) decisive++;
                if (shouldFlip && newWinner != winner) flipped++;
                if (rightDirection && (!shouldFlip || newWinner != winner)) passed++;
                _ = runnerUp;
            }
        }

        Assert.True(terms > 200, $"poucos termos exercitados: {terms}");
        Assert.Equal(terms, passed);
        Assert.True(decisive > 0 && flipped == decisive, $"decisivos {decisive}, mudaram {flipped}");
    }

    [Fact]
    public void Grief_lowers_work_and_raises_staying_home_and_fear_lowers_travel()
    {
        var calm = Ctx(90, 90, 90, 90, 10, 10, 40, Affect.Calm, 0, 50);
        var grieving = calm with { Inner = new InnerLifeSnapshot(new Affect(1, 0, 0, 0, 0, 0), 0) };
        var afraid = calm with { Inner = new InnerLifeSnapshot(new Affect(0, 1, 0, 0, 0, 0), 0) };
        Assert.True(Score(grieving, ActionType.Work) < Score(calm, ActionType.Work));
        Assert.True(Score(grieving, ActionType.Idle) > Score(calm, ActionType.Idle));
        Assert.True(Score(grieving, ActionType.Socialize) < Score(calm, ActionType.Socialize));
        Assert.True(Score(afraid, ActionType.Travel) < Score(calm, ActionType.Travel));
    }

    [Fact]
    public void Work_is_no_longer_a_constant_the_broke_household_with_an_empty_pantry_needs_income()
    {
        var stocked = Ctx(90, 90, 90, 90, 20, 20, 0, Affect.Calm, 0, 50);
        var empty = Ctx(90, 90, 90, 90, 0, 1, 0, Affect.Calm, 0, 50);
        var withCoins = Ctx(90, 90, 90, 90, 0, 1, 40, Affect.Calm, 0, 50);
        Assert.True(Score(empty, ActionType.Work) > Score(stocked, ActionType.Work));
        Assert.Equal(Score(stocked, ActionType.Work), Score(withCoins, ActionType.Work)); // com moeda, quem pesa é a compra

        var trace = BehaviorDecisionSystem.SelectByUtility(empty, Rules, Economy, null).Trace;
        if (trace.Winner == ActionType.Work)
            Assert.Contains(trace.TopPressures, p => p.Kind == PressureModel.EarnIncome);
    }

    [Fact]
    public void A_famine_memory_strengthens_provisioning()
    {
        var plain = Ctx(80, 80, 90, 90, 2, 2, 40, Affect.Calm, 0, 50);
        var scarred = plain with { Inner = new InnerLifeSnapshot(Affect.Calm, 80) };
        Assert.True(Score(scarred, ActionType.Buy) > Score(plain, ActionType.Buy));
    }
}
