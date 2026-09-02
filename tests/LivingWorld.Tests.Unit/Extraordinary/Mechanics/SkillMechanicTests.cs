using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Extraordinary;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Population.Skills;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Extraordinary.Engine;
using LivingWorld.Simulation.Extraordinary.Systems;
using LivingWorld.Simulation.Population.Skills;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Extraordinary.Mechanics;

public sealed class SkillMechanicTests
{
    private static readonly SkillsRules PracticeRules = SkillsRules.Create(
        cap: 100,
        baseRateBySource: new Dictionary<SkillGainSource, double> { [SkillGainSource.Practice] = 2.0 },
        skillByProfession: new Dictionary<int, SkillType> { [1] = new SkillType(0) },
        teachingSkill: new SkillType(6)).Value!;

    private static readonly Personality Personality =
        Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    [Fact]
    public void Skill_copy_copies_the_targets_exact_value_and_leaves_the_target_unchanged()
    {
        var (world, carrier, target) = WorldWithPower(["skill.copy:0"], []);
        target.GainSkill(new SkillType(0), 40, 100);

        var result = ExtraordinaryInvocationEngine.Invoke(
            world, new TickContext(world, world.Rng, world.Scheduler),
            new ExtraordinaryInvocation(1, carrier.Id, "test-power", target.Id));

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal((40d, 40d), (carrier.Skills.Get(new SkillType(0)), target.Skills.Get(new SkillType(0))));
    }

    [Fact]
    public void Skill_copy_fails_when_the_target_does_not_have_that_skill_id()
    {
        var (world, carrier, target) = WorldWithPower(["skill.copy:7"], []);
        target.GainSkill(new SkillType(0), 40, 100);

        var result = ExtraordinaryInvocationEngine.Invoke(
            world, new TickContext(world, world.Rng, world.Scheduler),
            new ExtraordinaryInvocation(2, carrier.Id, "test-power", target.Id));

        Assert.False(result.IsSuccess);
        Assert.Equal(0, carrier.Skills.Get(new SkillType(7)));
        Assert.False(target.Skills.Values.ContainsKey(7));
    }

    [Fact]
    public void Skill_copy_remains_after_the_power_ceases()
    {
        var (world, carrier, target) = WorldWithPower(["skill.copy:0"], []);
        target.GainSkill(new SkillType(0), 40, 100);
        var ctx = new TickContext(world, world.Rng, world.Scheduler);

        ExtraordinaryInvocationEngine.Invoke(
            world, ctx, new ExtraordinaryInvocation(3, carrier.Id, "test-power", target.Id));
        var revoked = ExtraordinaryStateSystem.RevokeAuthored(world, ctx, carrier.Id, "test-power");

        Assert.True(revoked.IsSuccess, revoked.Error);
        Assert.Equal(40, carrier.Skills.Get(new SkillType(0)));
    }

    [Fact]
    public void Skill_learn_rate_scales_practice_gain_five_times_versus_control()
    {
        var treated = PracticeWorld(["skill.learn-rate:5"], manifested: true, new RateGene(1.0));
        var control = PracticeWorld(["skill.learn-rate:5"], manifested: false, new RateGene(1.0));
        var skill = new SkillType(0);

        new SkillPracticeSystem(PracticeRules).Tick(treated.World, treated.Ctx);
        new SkillPracticeSystem(PracticeRules).Tick(control.World, control.Ctx);

        Assert.Equal(control.Worker.Skills.Get(skill) * 5, treated.Worker.Skills.Get(skill));
    }

    [Fact]
    public void Skill_learn_rate_multiplies_rate_gene_instead_of_replacing_it()
    {
        var treated = PracticeWorld(["skill.learn-rate:5"], manifested: true, new RateGene(2.0));
        var control = PracticeWorld(["skill.learn-rate:5"], manifested: false, new RateGene(2.0));
        var skill = new SkillType(0);

        new SkillPracticeSystem(PracticeRules).Tick(treated.World, treated.Ctx);
        new SkillPracticeSystem(PracticeRules).Tick(control.World, control.Ctx);

        Assert.Equal(control.Worker.Skills.Get(skill) * 5, treated.Worker.Skills.Get(skill));
        Assert.Equal(
            PracticeRules.Gain(0, SkillGainSource.Practice, 2.0) * 5,
            treated.Worker.Skills.Get(skill));
    }

    [Fact]
    public void Skill_learn_rate_leaves_no_residue_after_the_power_ceases()
    {
        var treated = PracticeWorld(["skill.learn-rate:5"], manifested: true, new RateGene(1.0));
        var skill = new SkillType(0);
        var system = new SkillPracticeSystem(PracticeRules);

        system.Tick(treated.World, treated.Ctx);
        double afterBoost = treated.Worker.Skills.Get(skill);
        var revoked = ExtraordinaryStateSystem.RevokeAuthored(
            treated.World, treated.Ctx, treated.Worker.Id, "test-power");
        Assert.True(revoked.IsSuccess, revoked.Error);

        system.Tick(treated.World, treated.Ctx);

        Assert.Equal(
            afterBoost + PracticeRules.Gain(afterBoost, SkillGainSource.Practice, 1.0),
            treated.Worker.Skills.Get(skill));
    }

    private static (WorldState World, Npc Carrier, Npc Target) WorldWithPower(
        IReadOnlyList<string> effects, IReadOnlyList<string> costs)
    {
        var descriptor = Descriptor(effects, costs);
        var state = CarrierState(new NpcId(1), descriptor.Id, manifested: true);
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, 42, ScenarioRunner.DefaultMap(42),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules,
            extraordinary: new ExtraordinaryScenarioData(true, [descriptor]), extraordinaryCarriers: [state]);
        var carrier = MakeNpc(new NpcId(1), "carrier");
        var target = MakeNpc(new NpcId(2), "target");
        world.AddNpc(carrier);
        world.AddNpc(target);
        return (world, carrier, target);
    }

    private static (WorldState World, Npc Worker, TickContext Ctx) PracticeWorld(
        IReadOnlyList<string> effects, bool manifested, RateGene rateGene)
    {
        var descriptor = Descriptor(effects, []);
        var location = new CellCoord(1, 1);
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, 1, ScenarioRunner.DefaultMap(1),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules,
            extraordinary: new ExtraordinaryScenarioData(true, [descriptor]),
            extraordinaryCarriers: [CarrierState(new NpcId(1), descriptor.Id, manifested)]);
        var workplace = new Workplace(
            world.NextWorkplaceIdAndAdvance(), new LocationType(1), location, maxVacancies: 5,
            employees: [], stock: new Dictionary<ResourceType, long>(), treasury: Money.Zero,
            prices: new Dictionary<ResourceType, long>());
        world.AddWorkplace(workplace);
        var worker = new Npc(
            new NpcId(1), "worker", Sex.Male, WorldDate.Epoch(ScenarioRunner.DefaultCalendar).AddYears(-30),
            ScenarioRunner.DefaultCulture, location, motherId: null, fatherId: null, household: null, health: 100,
            personality: Personality, profession: new ProfessionType(1), currentLocation: location,
            currentAction: ActionType.Work, rateGene: rateGene);
        world.AddNpc(worker);
        workplace.Hire(worker.Id);
        worker.Hire(workplace.Id);
        return (world, worker, new TickContext(world, world.Rng, world.Scheduler));
    }

    private static PowerDescriptor Descriptor(IReadOnlyList<string> effects, IReadOnlyList<string> costs) =>
        new("test-power", "test-source", effects, "Active", costs, "Guaranteed",
            [], [], [], []);

    private static ExtraordinaryCarrierState CarrierState(NpcId id, string powerId, bool manifested) =>
        new(id, [powerId], manifested, manifested ? "active" : "dormant",
            new ExtraordinaryAppearanceState(1, "", ""), null, 1);

    private static Npc MakeNpc(NpcId id, string name) => new(
        id, name, Sex.Male, WorldDate.Epoch(ScenarioRunner.DefaultCalendar),
        ScenarioRunner.DefaultCulture, new CellCoord(0, 0), motherId: null, fatherId: null,
        household: null, health: 100,
        personality: Personality, profession: ProfessionType.None, currentLocation: new CellCoord(0, 0));
}
