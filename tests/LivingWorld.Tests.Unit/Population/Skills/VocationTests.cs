using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Population.Skills;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Population.Skills;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Population.Skills;

/// <summary>AD-136: ninguém nasce com ofício; ele se escolhe na juventude pela inclinação, exposição, oportunidade e
/// pressão de quem cria, e combinar com ele pesa no rendimento, no aprendizado e no valor do emprego.</summary>
public class VocationTests
{
    private static readonly Personality Mid = Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;
    private static readonly Personality Obedient = Personality.Create(50, 85, 85, 50, 15, 15, 50, 50, 50, 50).Value!;
    private static readonly Personality Independent = Personality.Create(50, 15, 15, 50, 85, 85, 50, 50, 50, 50).Value!;

    private static WorldState World()
    {
        var calendar = ScenarioRunner.DefaultCalendar;
        var world = new WorldState(
            calendar, 3, ScenarioRunner.DefaultMap(3), ScenarioRunner.DefaultPopulationCatalog,
            ScenarioRunner.DefaultPopulationRules, ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules, familyRules: ScenarioRunner.DefaultFamilyRules);
        world.CurrentDate = WorldDate.Epoch(calendar).AddYears(60);
        return world;
    }

    private static TickContext Ctx(WorldState world) => new(world, world.Rng, world.Scheduler);

    /// <summary>Quantos de 200 jovens, cada um numa casa com um adulto ferreiro (ofício 2), escolhem ser ferreiros.</summary>
    private static int FollowersOfTheHouse(VocationRules vocation, Personality youth)
    {
        var world = World();
        var rules = ScenarioRunner.DefaultSkillsRules with { Vocation = vocation };
        int followed = 0;
        for (int i = 0; i < 200; i++)
        {
            var adult = new Npc(new NpcId(1000 + i), $"a{i}", Sex.Male, world.CurrentDate.AddYears(-40), new CultureId(1),
                new CellCoord(0, 0), null, null, null, 100, Mid, new ProfessionType(2), new CellCoord(0, 0));
            var kid = new Npc(new NpcId(5000 + i), $"k{i}", Sex.Male, world.CurrentDate.AddYears(-12), new CultureId(1),
                new CellCoord(0, 0), null, adult.Id, null, 100, youth, ProfessionType.None, new CellCoord(0, 0));
            world.AddNpc(adult);
            world.AddNpc(kid);
            var home = new Household(world.NextHouseholdIdAndAdvance(), new CellCoord(0, 0), adult.Id, [adult.Id, kid.Id]);
            world.AddHousehold(home);
            adult.JoinHousehold(home.Id);
            kid.JoinHousehold(home.Id);
            VocationMechanic.Choose(world, Ctx(world), kid, rules, ScenarioRunner.DefaultLifeStageRules);
            if (kid.Profession.Id == 2) followed++;
        }
        return followed;
    }

    [Fact]
    public void A_medieval_house_pushes_more_children_into_its_craft_than_a_modern_one()
    {
        Assert.True(FollowersOfTheHouse(VocationRules.Medieval, Mid) > FollowersOfTheHouse(VocationRules.Modern, Mid));
    }

    [Fact]
    public void The_obedient_child_gives_in_to_the_house_and_the_independent_one_does_not()
    {
        // sem perfil de ofício, para o temperamento não pesar também pela aptidão
        var rules = VocationRules.Medieval with { Profiles = new Dictionary<int, ProfessionProfile>() };
        Assert.True(FollowersOfTheHouse(rules, Obedient) > FollowersOfTheHouse(rules, Independent));
    }

    [Fact]
    public void Without_pressure_about_half_follow_the_house_by_inclination_alone()
    {
        int followed = FollowersOfTheHouse(VocationRules.Medieval with { ParentalPressure = 0 }, Mid);
        Assert.InRange(followed, 60, 140);
    }

    [Fact]
    public void Working_in_what_suits_you_yields_and_teaches_more_than_working_against_your_grain()
    {
        var rules = ScenarioRunner.DefaultSkillsRules;
        Npc With(double taste)
        {
            var npc = new Npc(new NpcId(1), "x", Sex.Male, WorldDate.Epoch(ScenarioRunner.DefaultCalendar), new CultureId(1),
                new CellCoord(0, 0), null, null, null, 100, Mid, new ProfessionType(1), new CellCoord(0, 0));
            npc.SetVocation(new VocationState(new Dictionary<int, double> { [1] = taste, [2] = 0 }, Chosen: true));
            return npc;
        }

        var suited = With(2.0);
        var misfit = With(-2.0);
        Assert.Equal((true, true, true),
            (VocationMechanic.OutputFit(rules, suited) > 1 && 1 > VocationMechanic.OutputFit(rules, misfit),
                VocationMechanic.LearningFit(rules, suited) > VocationMechanic.LearningFit(rules, misfit),
                VocationMechanic.JobValueFit(rules, suited, new ProfessionType(1)) > VocationMechanic.JobValueFit(rules, misfit, new ProfessionType(1))));
    }

    [Fact]
    public void Taste_for_a_craft_runs_in_the_family()
    {
        var world = World();
        var rules = VocationRules.Medieval;
        var love = new VocationState(new Dictionary<int, double> { [1] = 2.5, [2] = -2.5 }, Chosen: true);
        var mother = new Npc(new NpcId(1), "m", Sex.Female, world.CurrentDate.AddYears(-30), new CultureId(1), new CellCoord(0, 0),
            null, null, null, 100, Mid, new ProfessionType(1), new CellCoord(0, 0));
        var father = new Npc(new NpcId(2), "f", Sex.Male, world.CurrentDate.AddYears(-30), new CultureId(1), new CellCoord(0, 0),
            null, null, null, 100, Mid, new ProfessionType(1), new CellCoord(0, 0));
        mother.SetVocation(love);
        father.SetVocation(love);
        world.AddNpc(mother);
        world.AddNpc(father);
        double sum = 0;
        for (int i = 0; i < 200; i++)
        {
            var kid = new Npc(new NpcId(100 + i), $"k{i}", Sex.Male, world.CurrentDate, new CultureId(1), new CellCoord(0, 0),
                mother.Id, father.Id, null, 100, Mid, ProfessionType.None, new CellCoord(0, 0));
            world.AddNpc(kid);
            var taste = VocationMechanic.NewTaste(world, Ctx(world), kid, rules, chosen: false);
            sum += taste.TasteFor(1) - taste.TasteFor(2);
        }
        Assert.True(sum / 200 > 0.5);
    }
}
