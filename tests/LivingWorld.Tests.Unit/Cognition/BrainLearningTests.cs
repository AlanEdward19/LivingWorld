using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Cognition;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Decision;
using LivingWorld.Simulation.Cognition;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Cognition;

/// <summary>AD-142: o NPC aprende pela experiência dele (alívio contra o que a ação costuma dar), dentro dos limites; nasce com a
/// média dos pais e segregação; a criação puxa para quem criou. Nenhuma ação tem tratamento por nome.</summary>
public class BrainLearningTests
{
    private static readonly BrainRules Rules = BrainRules.Default;

    private static (WorldState World, Npc Npc) Adult()
    {
        var (world, _) = ScenarioRunner.Create(7);
        world.UseBrain(Rules);
        var npc = world.Npcs.Where(n => n.IsAlive).OrderBy(n => n.Id.Value).First();
        return (world, npc);
    }

    private static void Choose(Npc npc, ActionType action, UtilityGroup driver)
    {
        Span<double> groups = stackalloc double[BrainState.Groups];
        groups[(int)driver] = 80;
        groups[(int)UtilityGroup.Personality] = 20;
        BrainMechanic.Remember(npc, action, groups);
    }

    private static short Gain(Npc npc, ActionType action, UtilityGroup group) => npc.Brain!.GainOf(action, (int)group);

    [Fact]
    public void Relief_above_what_the_action_usually_gives_strengthens_the_groups_that_chose_it()
    {
        var (world, npc) = Adult();
        Choose(npc, ActionType.Buy, UtilityGroup.Base);
        BrainMechanic.Learn(world, npc, ActionType.Buy, reward: 120);

        Assert.True(Gain(npc, ActionType.Buy, UtilityGroup.Base) > 0);
    }

    [Fact]
    public void Relief_below_expectation_weakens_the_groups_that_chose_it()
    {
        var (world, npc) = Adult();
        for (int i = 0; i < 20; i++)
        {
            Choose(npc, ActionType.Eat, UtilityGroup.Base);
            BrainMechanic.Learn(world, npc, ActionType.Eat, reward: 60); // a expectativa sobe até ~60
        }
        short before = Gain(npc, ActionType.Eat, UtilityGroup.Base);
        Choose(npc, ActionType.Eat, UtilityGroup.Base);
        BrainMechanic.Learn(world, npc, ActionType.Eat, reward: 0);

        Assert.True(Gain(npc, ActionType.Eat, UtilityGroup.Base) < before);
    }

    [Fact]
    public void No_experience_ever_takes_a_group_below_half_or_above_double()
    {
        var (world, npc) = Adult();
        for (int i = 0; i < 2000; i++)
        {
            Choose(npc, ActionType.Eat, UtilityGroup.Base);
            BrainMechanic.Learn(world, npc, ActionType.Eat, reward: i % 2 == 0 ? -5000 : 5000);
        }

        Assert.All(npc.Brain!.Gains, g => Assert.InRange(g, Rules.MinGain, Rules.MaxGain));
    }

    [Fact]
    public void Learning_slows_with_age()
    {
        Assert.True(Rules.LearningRate(10) > Rules.LearningRate(45) && Rules.LearningRate(45) > Rules.LearningRate(70));
    }

    [Fact]
    public void Filling_an_empty_pantry_while_hungry_rewards_more_than_while_fed()
    {
        var hungry = new BrainMechanic.Felt(Needs: 200, Valence: 0, PantryDays: 0, SurvivalDeficit: 90);
        var fed = hungry with { SurvivalDeficit = 10 };
        var after = hungry with { PantryDays = 3 };

        Assert.True(BrainMechanic.Reward(hungry, after, 2) > BrainMechanic.Reward(fed, after with { SurvivalDeficit = 10 }, 2));
    }

    [Fact]
    public void An_npc_who_went_hungry_and_bought_values_buying_more_than_a_sibling_who_never_did()
    {
        var (world, npc) = Adult();
        var sibling = world.Npcs.Where(n => n.IsAlive && n.Id != npc.Id).OrderBy(n => n.Id.Value).First();
        sibling.SetBrain(BrainState.Neutral());
        var hungry = new BrainMechanic.Felt(Needs: 150, Valence: 0, PantryDays: 0, SurvivalDeficit: 95);
        for (int i = 0; i < 5; i++)
        {
            Choose(npc, ActionType.Buy, UtilityGroup.Base);
            BrainMechanic.Learn(world, npc, ActionType.Buy, BrainMechanic.Reward(hungry, hungry with { PantryDays = 4 }, 2));
        }

        Assert.True(Gain(npc, ActionType.Buy, UtilityGroup.Base) > sibling.Brain!.GainOf(ActionType.Buy, (int)UtilityGroup.Base));
    }

    [Fact]
    public void Remembered_shares_add_up_to_the_whole_choice()
    {
        var (_, npc) = Adult();
        Span<double> groups = stackalloc double[BrainState.Groups];
        groups[0] = 70; groups[2] = -20; groups[4] = 10;
        BrainMechanic.Remember(npc, ActionType.Work, groups);

        Assert.Equal(ActionType.Work, npc.Brain!.PendingAction);
        Assert.InRange(npc.Brain.PendingShares!.Sum(s => Math.Abs(s)), 999, 1001);
    }

    [Fact]
    public void Children_inherit_gains_correlated_with_the_parents()
    {
        var rng = new WorldRng(11);
        double sxy = 0, sxx = 0, syy = 0;
        for (int family = 0; family < 200; family++)
        {
            var mother = BrainState.Neutral();
            var father = BrainState.Neutral();
            for (int i = 0; i < mother.Gains.Length; i++)
            {
                mother.Gains[i] = (short)((rng.NextDouble() * 2 - 1) * 400);
                father.Gains[i] = (short)((rng.NextDouble() * 2 - 1) * 400);
            }
            var child = BrainMechanic.Inherit(Rules, mother, father, rng, heritability: 0.5);
            double parents = (mother.Gains[0] + father.Gains[0]) / 2.0;
            sxy += parents * child.Gains[0]; sxx += parents * parents; syy += (double)child.Gains[0] * child.Gains[0];
        }

        Assert.True(sxy / Math.Sqrt(sxx * syy) > 0.3);
    }

    [Fact]
    public void Siblings_raised_together_end_up_closer_than_siblings_raised_apart()
    {
        var (world, _) = Adult();
        var npcs = world.Npcs.Where(n => n.IsAlive).OrderBy(n => n.Id.Value).Take(4).ToList();
        var (a, b, carerA, carerB) = (npcs[0], npcs[1], npcs[2], npcs[3]);
        carerA.SetBrain(BrainState.Neutral());
        carerB.SetBrain(BrainState.Neutral());
        for (int i = 0; i < carerA.Brain!.Gains.Length; i++) { carerA.Brain.Gains[i] = 600; carerB.Brain!.Gains[i] = -400; }

        double Distance(Npc x, Npc y) => x.Brain!.Gains.Zip(y.Brain!.Gains, (p, q) => Math.Abs(p - q)).Sum();
        a.SetBrain(BrainState.Neutral()); b.SetBrain(BrainState.Neutral());
        for (int s = 0; s < 180; s++) { BrainMechanic.Rear(Rules, a, [carerA]); BrainMechanic.Rear(Rules, b, [carerB]); }
        double apart = Distance(a, b);
        a.SetBrain(BrainState.Neutral()); b.SetBrain(BrainState.Neutral());
        for (int s = 0; s < 180; s++) { BrainMechanic.Rear(Rules, a, [carerA]); BrainMechanic.Rear(Rules, b, [carerA]); }

        Assert.True(Distance(a, b) < apart);
    }

    [Fact]
    public void Neural_sandbox_policy_is_a_pure_function_of_weights_and_inputs()
    {
        var first = NeuralPolicySandbox.Random(new WorldRng(5));
        var second = NeuralPolicySandbox.Random(new WorldRng(5));
        var ctx = BrainGainTests.Ctx(30, 60, 20, food: 2, wallet: 10, Affect.Calm);

        Assert.Equal(first.Decide(ctx), second.Decide(ctx));
        Assert.True(Enum.IsDefined(first.Decide(ctx)));
    }
}
