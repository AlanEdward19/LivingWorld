using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Cognition;
using LivingWorld.Domain.Population;
using LivingWorld.Simulation.Cognition;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Cognition;

/// <summary>AD-160: com fome ou sede crítica o corpo decide pela utilidade pura (o ganho aprendido não pesa); fora disso, o cérebro
/// pesa. Antes (AD-142) um ganho de −500 em Comer e +1000 em Conversar valia também com a sede no zero.</summary>
public class BrainBodyDecidesTests
{
    private static (WorldState World, Npc Npc) Adult(BrainRules rules)
    {
        var (world, _) = ScenarioRunner.Create(7);
        world.UseBrain(rules);
        var npc = world.Npcs.Where(n => n.IsAlive).OrderBy(n => n.Id.Value).First();
        var brain = BrainState.Neutral();
        for (int g = 0; g < BrainState.Groups; g++)
        {
            brain.Gains[(int)ActionType.Eat * BrainState.Groups + g] = -500;
            brain.Gains[(int)ActionType.Socialize * BrainState.Groups + g] = 1000;
        }
        npc.SetBrain(brain);
        return (world, npc);
    }

    [Theory]
    [InlineData(30, 90, true)]
    [InlineData(90, 5, true)]
    [InlineData(31, 31, false)] // AD-160b: o corpo decide com fome ou sede urgente (30)
    public void Critical_hunger_or_thirst_hands_the_choice_to_the_body(int hunger, int thirst, bool bodyDecides)
    {
        Assert.Equal(bodyDecides, BrainRules.Default.BodyDecides(hunger, thirst));
    }

    [Fact]
    public void With_critical_thirst_the_learned_gains_do_not_weigh()
    {
        var (world, npc) = Adult(BrainRules.Default);
        long now = world.CurrentDate.TotalHours;
        npc.SetHunger(90, now);
        npc.SetThirst(5, now);

        Assert.Null(BrainMechanic.GainsFor(world.BrainRules!, npc, now)); // AD-160b: nulo = a conta da utilidade pura
    }

    [Fact]
    public void Without_critical_need_the_learned_gains_weigh()
    {
        var (world, npc) = Adult(BrainRules.Default);
        long now = world.CurrentDate.TotalHours;
        npc.SetHunger(60, now);
        npc.SetThirst(60, now);

        Assert.Same(npc.Brain!.Gains, BrainMechanic.GainsFor(world.BrainRules!, npc, now));
    }

    [Fact]
    public void The_ad142_experiment_without_the_rule_lets_gains_weigh_even_at_critical_thirst()
    {
        var (world, npc) = Adult(BrainRules.Default with { CriticalNeed = 0 });
        long now = world.CurrentDate.TotalHours;
        npc.SetThirst(5, now);

        Assert.Same(npc.Brain!.Gains, BrainMechanic.GainsFor(world.BrainRules!, npc, now));
    }
}
