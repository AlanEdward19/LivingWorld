using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Cognition;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Decision;
using LivingWorld.Simulation.Cognition;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Extraordinary.Mechanics;
using LivingWorld.Simulation.Population.Lifecycle;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Cognition;

/// <summary>AD-166: o luto tira do trabalho e do lazer, mas não da comida nem da compra da casa; e a morte por acidente ou velhice
/// enluta como as outras. Antes o apelo do luto ao ócio vencia o comer com a fome a 65 e a compra com a despensa vazia, e a
/// morte por acidente ou velhice não enlutava ninguém.</summary>
public class GriefYieldsToBodyTests
{
    private static readonly Personality Mid = Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    private static DecisionContext Mourner(int hunger, long food, long wallet, double grief, int members = 1) => new(
        new NpcId(1), 0, new NeedsSnapshot(hunger, 100, 100, 100), new BodySnapshot(1.7, 68, 28, 1, 1),
        new HouseholdSnapshot(new HouseholdId(1),
            new Dictionary<ResourceType, long> { [new ResourceType(ScenarioRunner.DefaultEconomyRules.FoodResourceId)] = food,
                [new ResourceType(ScenarioRunner.DefaultEconomyRules.WaterResourceId)] = 10 },
            Enumerable.Range(1, members).Select(i => new NpcId(i)).ToList()),
        [], [], [], [], Mid, ActionType.Idle, WalletAmount: wallet,
        Inner: new InnerLifeSnapshot(new Affect(grief, 0, 0, 0, 0, 0), 0));

    private static double Score(DecisionContext ctx, ActionType action, NeedsRules rules) => BehaviorDecisionSystem.ScoreOf(
        ctx, action, rules, ScenarioRunner.DefaultEconomyRules, ActionType.Idle, default, ForesightMechanic.EmptyPreviews);

    private static NeedsRules Rules(bool yields) =>
        ScenarioRunner.DefaultNeedsRules with { Psyche = ScenarioRunner.DefaultNeedsRules.Psyche with { GriefYieldsToBody = yields } };

    [Fact]
    public void A_mourner_hungry_with_food_at_home_still_eats_and_with_the_pantry_empty_still_shops()
    {
        var hungry = Mourner(hunger: 35, food: 5, wallet: 0, grief: 1.0);      // fome em 65, comida em casa
        var emptyPantry = Mourner(hunger: 60, food: 3, wallet: 100, grief: 1.0, members: 2); // 1,5 dia de 2 da meta, moeda para comprar

        Assert.True(Score(hungry, ActionType.Idle, Rules(false)) > Score(hungry, ActionType.Eat, Rules(false)), "antes o luto vencia o comer");
        Assert.True(Score(hungry, ActionType.Eat, Rules(true)) > Score(hungry, ActionType.Idle, Rules(true)));
        Assert.True(Score(emptyPantry, ActionType.Idle, Rules(false)) > Score(emptyPantry, ActionType.Buy, Rules(false)), "antes o luto vencia a compra");
        Assert.True(Score(emptyPantry, ActionType.Buy, Rules(true)) > Score(emptyPantry, ActionType.Idle, Rules(true)));
    }

    [Fact]
    public void Fed_and_stocked_the_mourner_still_withdraws_from_work_into_idleness()
    {
        var fed = Mourner(hunger: 90, food: 10, wallet: 100, grief: 1.0);
        var calm = fed with { Inner = null };

        Assert.True(Score(fed, ActionType.Idle, Rules(true)) > Score(calm, ActionType.Idle, Rules(true)));
        Assert.True(Score(fed, ActionType.Work, Rules(true)) < Score(calm, ActionType.Work, Rules(true)));
    }

    [Theory]
    [InlineData(WorldEventKind.Accident)]
    [InlineData(WorldEventKind.OldAge)]
    public void Death_by_accident_or_old_age_leaves_the_spouse_grieving(WorldEventKind kind)
    {
        var (world, _) = ScenarioRunner.Create(3);
        var ctx = new TickContext(world, world.Rng, world.Scheduler);
        var dead = world.Npcs.First(n => n.IsAlive && n.Spouse is { } s && world.Npcs.Any(o => o.Id == s && o.IsAlive));
        var spouse = world.Npcs.First(n => n.Id == dead.Spouse);
        long now = world.CurrentDate.TotalHours;

        NpcDeath.Apply(world, ctx, dead, kind);

        Assert.True(PsycheMechanic.EmotionOf(world, spouse, Emotion.Grief, now) > 0.5);
    }
}
