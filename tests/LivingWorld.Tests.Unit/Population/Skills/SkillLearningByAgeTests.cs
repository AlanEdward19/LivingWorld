using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Population.Skills;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Population.Skills;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Population.Skills;

/// <summary>AD-132: aprendizado por idade, com quem se aprende, uso e esquecimento, e criação. Regras do cenário padrão
/// (<see cref="ScenarioRunner.DefaultSkillsRules"/>), um ano tem 360 dias; o sistema roda uma vez por dia (Midshift).</summary>
public class SkillLearningByAgeTests
{
    private static readonly WorldCalendar Calendar = new(24, 30, 12);
    private static readonly SkillsRules Rules = ScenarioRunner.DefaultSkillsRules;
    private static readonly SkillType Agriculture = Rules.SkillByProfession[1];
    private static readonly SkillType Craft = Rules.SkillByProfession[2];
    private static readonly CellCoord Home = new(1, 1);
    private static readonly long Year = Calendar.HoursPerYear;

    private static Personality Uniform(int value) =>
        Personality.Create(value, value, value, value, value, value, value, value, value, value).Value!;

    private static WorldState BuildWorld() => new(
        Calendar, 1, ScenarioRunner.DefaultMap(1), ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
        ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog, ScenarioRunner.DefaultLifeStageRules);

    private static Npc Make(
        WorldState world, int ageYears, int profession, HouseholdId? household = null, NpcId? mother = null, NpcId? father = null,
        ActionType? action = null, WorkplaceId? employer = null, Personality? personality = null, CellCoord? at = null)
    {
        var npc = new Npc(
            world.NextNpcIdAndAdvance(), "npc", Sex.Male, WorldDate.Epoch(Calendar).AddYears(-ageYears), new CultureId(1),
            at ?? Home, motherId: mother, fatherId: father, household: household, health: 100,
            personality: personality ?? Uniform(50), profession: new ProfessionType(profession), currentLocation: at ?? Home,
            currentAction: action, employer: employer, rateGene: new RateGene(1.0));
        world.AddNpc(npc);
        return npc;
    }

    private static void House(WorldState world, HouseholdId id, params Npc[] members) =>
        world.AddHousehold(new Household(id, Home, members[0].Id, members.Select(m => m.Id).ToList()));

    private static void Run(WorldState world, long hours, RearingRules? rearing = null) =>
        new WorldClock([new SkillTeachingSystem(Rules, ScenarioRunner.DefaultLifeStageRules, rearing)]).Run(world, hours);

    [Fact]
    public void Infant_living_and_watching_a_working_master_learns_no_trade()
    {
        var world = BuildWorld();
        var home = world.NextHouseholdIdAndAdvance();
        var smith = Make(world, 35, 2, home, action: ActionType.Work, employer: new WorkplaceId(1));
        smith.GainSkill(Craft, 90, Rules.Cap);
        var baby = Make(world, 0, 2, home, father: smith.Id);
        House(world, home, smith, baby);

        Run(world, Year);

        Assert.Equal(0, baby.Skills.Get(Craft));
    }

    [Fact]
    public void Orphan_raised_by_a_smith_uncle_learns_smithing_and_not_the_dead_fathers_farming()
    {
        var world = BuildWorld();
        var uncleHome = world.NextHouseholdIdAndAdvance();
        var father = Make(world, 40, 1);
        father.GainSkill(Agriculture, 90, Rules.Cap);
        father.Die(world.CurrentDate);
        var uncle = Make(world, 38, 2, uncleHome);
        uncle.GainSkill(Craft, 90, Rules.Cap);
        var child = Make(world, 9, 0, uncleHome, father: father.Id);
        House(world, uncleHome, uncle, child);

        Run(world, Year);

        Assert.True(child.Skills.Get(Craft) > 0 && child.Skills.Get(Agriculture) == 0,
            $"forja {child.Skills.Get(Craft)}, lavoura {child.Skills.Get(Agriculture)}");
    }

    [Fact]
    public void What_a_child_learns_at_home_stops_at_a_fraction_of_what_the_caregiver_knows()
    {
        var world = BuildWorld();
        var home = world.NextHouseholdIdAndAdvance();
        var mother = Make(world, 35, 1, home);
        mother.GainSkill(Agriculture, 40, Rules.Cap);
        var child = Make(world, 6, 0, home, mother: mother.Id);
        House(world, home, mother, child);

        Run(world, 8 * Year);

        double ceiling = 40 * Rules.Learning.HouseholdCeilingFraction;
        Assert.InRange(child.Skills.Get(Agriculture), double.Epsilon, ceiling);
    }

    [Fact]
    public void Youth_at_apprentice_age_gets_the_best_master_of_the_trade_brought_from_home()
    {
        var world = BuildWorld();
        var youth = Make(world, (int)Rules.Learning.ApprenticeAgeYears + 1, 0);
        youth.GainSkill(Craft, 10, Rules.Cap);
        var weakSmith = Make(world, 30, 2, employer: new WorkplaceId(1));
        weakSmith.GainSkill(Craft, 50, Rules.Cap);
        var bestSmith = Make(world, 45, 2, employer: new WorkplaceId(1));
        bestSmith.GainSkill(Craft, 90, Rules.Cap);
        var farmer = Make(world, 40, 1, employer: new WorkplaceId(2));
        farmer.GainSkill(Agriculture, 95, Rules.Cap);

        Run(world, 24);

        Assert.Equal(bestSmith.Id, youth.Mentor);
    }

    [Fact]
    public void Unused_skill_loses_the_disuse_fraction_in_five_years_out_of_the_trade()
    {
        var world = BuildWorld();
        var idle = Make(world, 30, 2); // sem emprego: não exerce o ofício
        idle.GainSkill(Craft, 80, Rules.Cap);

        Run(world, 5 * Year);

        var learning = Rules.Learning;
        double perPeriod = learning.LossFraction(32, practiced: false, physical: true, learning.MaintenanceEveryDays, 360);
        double expected = 80;
        for (int i = 0; i < 5 * 360 / learning.MaintenanceEveryDays; i++) expected -= expected * perPeriod;
        Assert.True(expected < 80 && Math.Abs(expected - idle.Skills.Get(Craft)) < 1e-6, $"esperado {expected}, medido {idle.Skills.Get(Craft)}");
    }

    [Fact]
    public void Practiced_trade_does_not_decay_but_the_unused_one_does()
    {
        var world = BuildWorld();
        var smith = Make(world, 30, 2, employer: new WorkplaceId(1));
        smith.GainSkill(Craft, 80, Rules.Cap);
        smith.GainSkill(Agriculture, 80, Rules.Cap);

        Run(world, 2 * Year);

        Assert.True(smith.Skills.Get(Craft) == 80 && smith.Skills.Get(Agriculture) < 80,
            $"forja {smith.Skills.Get(Craft)}, lavoura {smith.Skills.Get(Agriculture)}");
    }

    [Fact]
    public void Manual_skill_declines_in_old_age_even_for_who_still_works_in_the_trade()
    {
        var world = BuildWorld();
        var young = Make(world, 40, 2, employer: new WorkplaceId(1));
        var old = Make(world, 70, 2, employer: new WorkplaceId(1));
        young.GainSkill(Craft, 80, Rules.Cap);
        old.GainSkill(Craft, 80, Rules.Cap);

        Run(world, Year);

        Assert.True(young.Skills.Get(Craft) == 80 && old.Skills.Get(Craft) < 80,
            $"aos 40: {young.Skills.Get(Craft)}, aos 70: {old.Skills.Get(Craft)}");
    }

    [Fact]
    public void Deliberate_training_yields_more_than_only_watching_someone_work()
    {
        double Gain(SkillGainSource source) => Rules.Gain(20, source, 1.0);

        Assert.True(Gain(SkillGainSource.DeliberateTraining) > Gain(SkillGainSource.Practice)
            && Gain(SkillGainSource.Practice) > Gain(SkillGainSource.Observation));
    }

    [Fact]
    public void Adult_personality_carries_the_values_of_who_raised_the_child()
    {
        RearingRecord? record = null;
        Personality Raise(RearingRules rearing)
        {
            var world = BuildWorld();
            var home = world.NextHouseholdIdAndAdvance();
            var aunt = Make(world, 40, 1, home, personality: Uniform(90));
            var child = Make(world, 12, 0, home, personality: Uniform(50));
            House(world, home, aunt, child);
            Run(world, 2 * Year, rearing);
            record ??= child.Rearing;
            Run(world, Year + 24, rearing); // fez 15: virou adulto
            Assert.Null(child.Rearing);
            return child.Personality;
        }

        var raised = Raise(RearingRules.Default);
        var control = Raise(RearingRules.Disabled);

        Assert.True(record is { Samples: > 0 } && raised.Conscientiousness > control.Conscientiousness && control.Conscientiousness == 50,
            $"criado pela tia: {raised.Conscientiousness}, sem criação: {control.Conscientiousness}");
    }

    [Fact]
    public void Snapshot_without_the_rearing_field_still_opens()
    {
        var (world, clock) = ScenarioRunner.Create(7);
        clock.Run(world, 60 * 24);
        var node = System.Text.Json.Nodes.JsonNode.Parse(WorldSnapshot.Serialize(world))!.AsObject();
        int stripped = 0;
        foreach (var npc in node["Npcs"]!.AsArray())
            if (npc!.AsObject().Remove("Rearing")) stripped++;

        var reopened = WorldSnapshot.Deserialize(node.ToJsonString());

        Assert.True(stripped > 0 && reopened.Npcs.All(n => n.Rearing is null));
    }
}
