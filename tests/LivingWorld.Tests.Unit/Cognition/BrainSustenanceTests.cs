using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Cognition;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cognition;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Cognition;

/// <summary>AD-160b: a recompensa é a variação do valor que o NPC dá ao próprio estado (corpo, despensa, moeda, humor), com o
/// salário creditado à jornada; o filho já espera o que os pais esperam; o ganho anda em degraus sem viés.</summary>
public class BrainSustenanceTests
{
    private static (WorldState World, Npc Npc) Adult()
    {
        var (world, _) = ScenarioRunner.Create(7);
        world.UseBrain(BrainRules.Default);
        var npc = world.Npcs.Where(n => n.IsAlive && n.Household is not null).OrderBy(n => n.Id.Value).First();
        return (world, npc);
    }

    [Fact]
    public void The_value_is_the_sum_of_named_parcels()
    {
        var (world, npc) = Adult();
        var v = BrainMechanic.ValueOf(world, npc, world.CurrentDate.TotalHours);

        Assert.Equal(v.Body + v.Pantry + v.Money + v.Mood, v.Total, 9);
        Assert.InRange(v.Body, 0, 400);
    }

    [Fact]
    public void Food_in_the_pantry_is_worth_more_than_the_same_food_still_in_coins()
    {
        var (world, npc) = Adult();
        var rules = world.BrainRules!;
        var home = world.FindHousehold(npc.Household!.Value)!;
        var food = new ResourceType(world.EconomyRules.FoodResourceId);
        var water = new ResourceType(world.EconomyRules.WaterResourceId);
        foreach (var r in new[] { food, water }) home.Withdraw(r, home.Stock.GetValueOrDefault(r));
        long now = world.CurrentDate.TotalHours;
        var before = BrainMechanic.ValueOf(world, npc, now);
        home.Deposit(food, home.Members.Count);
        home.Deposit(water, home.Members.Count);
        var stocked = BrainMechanic.ValueOf(world, npc, now);

        Assert.Equal(rules.PantryDayValue, stocked.Pantry - before.Pantry, 6); // um dia por morador
        Assert.True(rules.PantryDayValue > rules.MoneyDayValue);
    }

    [Fact]
    public void A_working_day_is_credited_with_the_wage_it_earns()
    {
        var (world, clock) = ScenarioRunner.Create(7);
        world.UseBrain(BrainRules.Default);
        clock.Run(world, 3 * world.Calendar.HoursPerDay); // as vagas se preenchem nos primeiros dias
        var worker = world.Npcs.First(n => n.IsAlive && n.Employer is not null
            && world.EconomyRules.WageByProfession.GetValueOrDefault(n.Profession.Id) > 0);
        var idle = world.Npcs.First(n => n.IsAlive && n.Employer is null);

        Assert.True(BrainMechanic.WageCredit(world, worker, 8) > 0);
        Assert.Equal(0, BrainMechanic.WageCredit(world, idle, 8));
        Assert.True(BrainMechanic.WageCredit(world, worker, 8) > BrainMechanic.WageCredit(world, worker, 2));
    }

    [Fact]
    public void A_child_expects_from_each_action_what_the_parents_expect()
    {
        var mother = BrainState.Neutral();
        var father = BrainState.Neutral();
        mother.ActionMeans[(int)ActionType.Buy] = 80;
        father.ActionMeans[(int)ActionType.Buy] = 40;
        father.ActionMeans[(int)ActionType.Work] = -30;

        var child = BrainMechanic.Inherit(BrainRules.Default, mother, father, new WorldRng(1), heritability: 0.5);

        Assert.Equal(60, child.ActionMeans[(int)ActionType.Buy]);
        Assert.Equal(-30, child.ActionMeans[(int)ActionType.Work]); // só o pai já viveu
        Assert.Equal(BrainState.Unknown, child.ActionMeans[(int)ActionType.Socialize]);
    }

    [Fact]
    public void Gains_move_in_whole_steps_and_small_changes_average_out()
    {
        var rules = BrainRules.Default;
        ulong draw = 12345;
        double sum = 0;
        for (int i = 0; i < 20000; i++)
        {
            double d = BrainMechanic.Stepped(rules, 2.5, ref draw);
            Assert.True(d is 0 or 10);
            sum += d;
        }

        Assert.InRange(sum / 20000, 2.3, 2.7);
    }

    [Fact]
    public void Inherited_gains_land_on_steps_and_fit_the_compact_snapshot()
    {
        var child = BrainMechanic.Inherit(BrainRules.Default, null, null, new WorldRng(4), heritability: 0.5);

        Assert.All(child.Gains, g => Assert.Equal(0, g % BrainRules.Default.GainStep));
        string json = System.Text.Json.JsonSerializer.Serialize(child);
        var back = System.Text.Json.JsonSerializer.Deserialize<BrainState>(json)!;
        Assert.Equal(child.Gains, back.Gains);
        Assert.DoesNotContain("\"Gains\":[", json, StringComparison.Ordinal);
    }
}
