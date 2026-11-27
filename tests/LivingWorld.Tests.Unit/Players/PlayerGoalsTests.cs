using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Players;
using LivingWorld.Simulation.Scenarios;
using static LivingWorld.Tests.Unit.Players.PlayerAbsenceTests;
using static LivingWorld.Tests.Unit.Players.PlayerControlTests;

namespace LivingWorld.Tests.Unit.Players;

/// <summary>#235: as metas nascem do estado do motor (nada escrito à mão), cada uma com um próximo passo que os comandos de hoje
/// executam; a habilidade sobe pelo mesmo caminho do NPC; o diário reaproveita a biografia e o registro de sessão.</summary>
public class PlayerGoalsTests
{
    private static void Satisfy(WorldState w, Npc npc)
    {
        long now = w.CurrentDate.TotalHours;
        npc.SetHunger(100, now);
        npc.SetThirst(100, now);
        npc.SetSleep(100, now);
        npc.SetSocial(100, now);
    }

    private static PlayerGoal Goal(WorldState w, Npc npc, string kind) => PlayerGoals.Of(w, npc).First(g => g.Kind == kind);

    [Fact]
    public void A_body_with_every_need_met_has_no_need_goal()
    {
        var (arm, npc, _) = Body(3);
        Satisfy(arm.World, npc);
        Assert.DoesNotContain(PlayerGoals.Of(arm.World, npc), g => g.Kind.StartsWith("need.", StringComparison.Ordinal));
    }

    [Fact]
    public void Hunger_becomes_the_most_urgent_goal_and_scales_with_how_empty_the_body_is()
    {
        var (arm, npc, _) = Body(3);
        var w = arm.World;
        Satisfy(w, npc);
        npc.SetHunger(30, w.CurrentDate.TotalHours);
        var mild = Goal(w, npc, "need.hunger");
        npc.SetHunger(0, w.CurrentDate.TotalHours);
        var dire = Goal(w, npc, "need.hunger");
        Assert.True(dire.Urgency > mild.Urgency);
        Assert.Equal("need.hunger", PlayerGoals.Of(w, npc)[0].Kind);
    }

    [Fact]
    public void The_hunger_step_is_a_real_action_the_commands_can_run()
    {
        var (arm, npc, service) = Body(3);
        var w = arm.World;
        Satisfy(w, npc);
        var home = w.FindHousehold(npc.Household!.Value)!;
        home.Deposit(new ResourceType(w.EconomyRules.FoodResourceId), 5);
        Place(w, npc, home.Location);
        npc.SetHunger(10, w.CurrentDate.TotalHours);

        var step = Goal(w, npc, "need.hunger").Step!;
        Assert.Equal("interact", step.Command);
        Assert.True(step.Here);
        Assert.Equal(("stock", "take"), (step.Kind, step.Verb));
        Assert.True(service.Interact(w, "alan", step.Kind, step.TargetId, step.Verb, step.ResourceId).IsSuccess); // o comando existente aceita o passo
    }

    [Fact]
    public void Far_from_food_the_step_walks_to_a_real_place()
    {
        var (arm, npc, _) = Body(3);
        var w = arm.World;
        Satisfy(w, npc);
        var home = w.FindHousehold(npc.Household!.Value)!;
        home.Deposit(new ResourceType(w.EconomyRules.FoodResourceId), 5);
        int dx = 4;
        for (; dx < 40; dx++) // um ponto da rua longe de despensa e banca: onde nenhuma oferta de comida alcança o corpo
        {
            Place(w, npc, new CellCoord(home.Location.X + dx, home.Location.Y));
            if (w.Map.TryGetCell(npc.CurrentLocation, out _) && !PlayerInteractions.Offers(w, npc).Any(o => o.Kind is "stock" or "stall")) break;
        }
        npc.SetHunger(10, w.CurrentDate.TotalHours);

        var step = Goal(w, npc, "need.hunger").Step!;
        Assert.Equal(("go", false), (step.Command, step.Here));
        Assert.Equal((home.Location.X, home.Location.Y), (step.X, step.Y));
        Assert.Equal(dx, step.Distance);
    }

    [Fact]
    public void A_hungry_dependent_child_is_a_goal_of_the_parent()
    {
        var arm = PlayerOrdersTests.Family(6);
        var w = arm.World;
        var parent = PlayerOrdersTests.Parent(w);
        var child = PlayerOrdersTests.ChildOf(w, parent);
        Satisfy(w, parent);
        child.SetHunger(5, w.CurrentDate.TotalHours);
        var goal = Goal(w, parent, "family.child-hungry");
        Assert.Contains(child.Name, goal.Reason);
        Assert.NotNull(goal.Step);
        child.SetHunger(100, w.CurrentDate.TotalHours);
        Assert.DoesNotContain(PlayerGoals.Of(w, parent), g => g.Kind == "family.child-hungry");
    }

    [Fact]
    public void A_sick_housemate_is_a_goal_and_a_healthy_one_is_not()
    {
        var arm = PlayerOrdersTests.Family(6);
        var w = arm.World;
        var parent = PlayerOrdersTests.Parent(w);
        var child = PlayerOrdersTests.ChildOf(w, parent);
        child.SetHealth(100);
        Assert.DoesNotContain(PlayerGoals.Of(w, parent), g => g.Kind.StartsWith("family.", StringComparison.Ordinal) && g.Kind != "family.child-hungry");
        child.SetHealth(20);
        var goal = Goal(w, parent, "family.member-sick");
        Assert.Contains(child.Name, goal.Reason);
        Assert.True(goal.Urgency >= 80);
        Assert.Contains(goal.Step!.Command, new[] { "go", "interact" });
    }

    [Fact]
    public void Overdue_rent_is_a_goal_that_points_at_work()
    {
        var (arm, npc, _) = Body(3, Pick);
        var w = arm.World;
        Satisfy(w, npc);
        var home = w.FindHousehold(npc.Household!.Value)!;
        home.RecordUnpaidRent();
        home.RecordUnpaidRent();
        var goal = Goal(w, npc, "money.rent-arrears");
        Assert.Equal(80, goal.Urgency);
        Assert.NotNull(goal.Step);
        Assert.Equal(w.FindWorkplace(npc.Employer!.Value)!.Location, new CellCoord(goal.Step.X, goal.Step.Y));
    }

    [Fact]
    public void Goals_are_deterministic_ordered_and_capped()
    {
        var (arm, npc, _) = Body(3);
        var w = arm.World;
        npc.SetHunger(0, w.CurrentDate.TotalHours);
        npc.SetThirst(10, w.CurrentDate.TotalHours);
        npc.SetSleep(20, w.CurrentDate.TotalHours);
        var a = PlayerGoals.Of(w, npc);
        var b = PlayerGoals.Of(w, npc);
        Assert.Equal(a.Select(g => (g.Kind, g.Urgency, g.Reason)), b.Select(g => (g.Kind, g.Urgency, g.Reason)));
        Assert.Equal(a.OrderByDescending(g => g.Urgency).Select(g => g.Kind), a.Select(g => g.Kind));
        Assert.True(a.Count <= PlayerGoals.MaxGoals);
    }

    [Fact]
    public void Asking_for_goals_does_not_change_the_world()
    {
        var (arm, npc, _) = Body(3);
        var w = arm.World;
        npc.SetHunger(0, w.CurrentDate.TotalHours);
        string before = WorldSnapshot.CanonicalHash(w);
        PlayerGoals.Of(w, npc);
        PlayerSkills.Of(w, npc);
        PlayerDiary.Of(w, npc, "alan", null, null);
        Assert.Equal(before, WorldSnapshot.CanonicalHash(w));
    }

    [Fact]
    public void Skill_levels_are_equal_bands_and_report_what_is_missing()
    {
        var (arm, npc, _) = Body(3, Pick);
        var w = arm.World;
        var rules = ScenarioRunner.DefaultSkillsRules;
        var skill = rules.SkillByProfession[npc.Profession.Id];
        npc.GainSkill(skill, 25 - npc.Skills.Get(skill), rules.Cap); // 25 de 100: nível 2, metade do caminho para o 3
        var progress = PlayerSkills.Of(w, npc, rules).First(s => s.SkillId == skill.Id);
        Assert.Equal((2, 5.0, 0.5), (progress.Level, progress.ToNext, progress.Fraction));
        npc.GainSkill(skill, 500, rules.Cap);
        var top = PlayerSkills.Of(w, npc, rules).First(s => s.SkillId == skill.Id);
        Assert.Equal((PlayerSkills.MaxLevel, 0.0), (top.Level, top.ToNext));
    }

    [Fact]
    public void Working_as_the_player_raises_the_skill_through_the_same_system_as_any_npc()
    {
        var (arm, npc, service) = Body(3, Pick);
        var w = arm.World;
        var rules = ScenarioRunner.DefaultSkillsRules;
        var skill = rules.SkillByProfession[npc.Profession.Id];
        var shop = w.FindWorkplace(npc.Employer!.Value)!;
        Satisfy(w, npc);
        Place(w, npc, shop.Location);
        double before = npc.Skills.Get(skill);
        Assert.True(service.Act(w, "alan", ActionType.Work).IsSuccess);
        arm.Clock.Run(w, 6);
        Assert.True(PlayerSkills.Of(w, npc, rules).First(s => s.SkillId == skill.Id).Practicing,
            "o corpo no posto, em Work, conta como praticando");
        arm.Clock.Run(w, 48);
        Assert.True(npc.Skills.Get(skill) > before);
    }

    [Fact]
    public void The_diary_holds_what_the_player_did_while_controlling_and_milestones_of_the_life()
    {
        var (arm, npc, service) = Body(3);
        var w = arm.World;
        Assert.True(service.Act(w, "alan", ActionType.Eat).IsSuccess);
        arm.Clock.Run(w, 2);
        Assert.True(service.Act(w, "alan", ActionType.Sleep).IsSuccess);
        long since = PlayerDiary.ControlledSince(service.Log, "alan", npc.Id.Value)!.Value;
        var diary = PlayerDiary.Of(w, npc, "alan", service.Log, since);
        var own = diary.Where(e => e.Source == "own").ToList();
        Assert.Equal(["Sleep", "Eat"], own.Select(e => e.Verb));
        Assert.All(own, e => Assert.True(e.WhileControlled));
        Assert.Equal(diary.OrderByDescending(e => e.AtMs).Select(e => e.AtMs), diary.Select(e => e.AtMs));
        Assert.True(PlayerDiary.Milestones.Contains(LivingWorld.Domain.History.WorldEventKind.Marriage));
    }

    [Fact]
    public void The_diary_marks_what_happened_before_control_as_not_controlled()
    {
        var (arm, npc, service) = Body(3);
        var w = arm.World;
        var diary = PlayerDiary.Of(w, npc, "alan", service.Log, w.CurrentDate.TotalMilliseconds + 1);
        Assert.All(diary.Where(e => e.Source == "fact"), e => Assert.False(e.WhileControlled));
    }
}
