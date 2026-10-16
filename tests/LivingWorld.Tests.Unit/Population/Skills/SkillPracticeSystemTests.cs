using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Population.Skills;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Population.Skills;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Population.Skills;

/// <summary>Fase 6, T8: <see cref="SkillPracticeSystem"/> — ganho por prática no trabalho
/// (SKILL-03). NPC empregado, em Work, presente no próprio Workplace ganha a habilidade mapeada;
/// sem mapeamento/ausente/ação diferente — sem-op, sem exceção.</summary>
public class SkillPracticeSystemTests
{
    private static readonly WorldCalendar Calendar = new(24, 30, 12);

    private static readonly Personality SomePersonality =
        Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    private static readonly SkillsRules Rules = SkillsRules.Create(
        cap: 100,
        baseRateBySource: new Dictionary<SkillGainSource, double> { [SkillGainSource.Practice] = 2.0 },
        skillByProfession: new Dictionary<int, SkillType> { [1] = new SkillType(0) },
        teachingSkill: new SkillType(6)).Value!;

    private static WorldState BuildWorld()
    {
        var map = ScenarioRunner.DefaultMap(1);
        return new WorldState(
            Calendar, 1, map, ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog, ScenarioRunner.DefaultLifeStageRules);
    }

    private static Workplace MakeWorkplace(WorldState world, CellCoord location) =>
        new(world.NextWorkplaceIdAndAdvance(), new LocationType(1), location, maxVacancies: 5,
            employees: [], stock: new Dictionary<ResourceType, long>(), treasury: Money.Zero,
            prices: new Dictionary<ResourceType, long>());

    private static Npc MakeWorker(
        WorldState world, ProfessionType profession, CellCoord location, ActionType? action, RateGene? rateGene = null)
    {
        var npc = new Npc(
            world.NextNpcIdAndAdvance(), "worker", Sex.Male, WorldDate.Epoch(Calendar).AddYears(-30), new CultureId(1),
            location, motherId: null, fatherId: null, household: null, health: 100,
            personality: SomePersonality, profession: profession, currentLocation: location,
            currentAction: action, rateGene: rateGene);
        world.AddNpc(npc);
        return npc;
    }

    [Fact]
    public void Employed_worker_working_present_at_own_workplace_gains_mapped_skill()
    {
        var world = BuildWorld();
        var location = new CellCoord(1, 1);
        var workplace = MakeWorkplace(world, location);
        world.AddWorkplace(workplace);
        var npc = MakeWorker(world, new ProfessionType(1), location, ActionType.Work);
        workplace.Hire(npc.Id);
        npc.Hire(workplace.Id);
        var ctx = new TickContext(world, world.Rng, world.Scheduler);

        new SkillPracticeSystem(Rules).Tick(world, ctx);

        Assert.True(npc.Skills.Get(new SkillType(0)) > 0);
    }

    [Fact]
    public void Worker_with_unmapped_profession_does_not_gain_and_does_not_throw()
    {
        var world = BuildWorld();
        var location = new CellCoord(1, 1);
        var workplace = MakeWorkplace(world, location);
        world.AddWorkplace(workplace);
        var npc = MakeWorker(world, new ProfessionType(999), location, ActionType.Work);
        workplace.Hire(npc.Id);
        npc.Hire(workplace.Id);
        var ctx = new TickContext(world, world.Rng, world.Scheduler);

        var exception = Record.Exception(() => new SkillPracticeSystem(Rules).Tick(world, ctx));

        Assert.Null(exception);
        Assert.Equal(0, npc.Skills.Get(new SkillType(0)));
    }

    [Fact]
    public void Worker_not_present_at_workplace_location_does_not_gain()
    {
        var world = BuildWorld();
        var workplace = MakeWorkplace(world, new CellCoord(1, 1));
        world.AddWorkplace(workplace);
        var npc = MakeWorker(world, new ProfessionType(1), new CellCoord(9, 9), ActionType.Work);
        workplace.Hire(npc.Id);
        npc.Hire(workplace.Id);
        var ctx = new TickContext(world, world.Rng, world.Scheduler);

        new SkillPracticeSystem(Rules).Tick(world, ctx);

        Assert.Equal(0, npc.Skills.Get(new SkillType(0)));
    }

    [Fact]
    public void Worker_not_currently_working_does_not_gain()
    {
        var world = BuildWorld();
        var location = new CellCoord(1, 1);
        var workplace = MakeWorkplace(world, location);
        world.AddWorkplace(workplace);
        var npc = MakeWorker(world, new ProfessionType(1), location, ActionType.Idle);
        workplace.Hire(npc.Id);
        npc.Hire(workplace.Id);
        var ctx = new TickContext(world, world.Rng, world.Scheduler);

        new SkillPracticeSystem(Rules).Tick(world, ctx);

        Assert.Equal(0, npc.Skills.Get(new SkillType(0)));
    }

    [Fact]
    public void Same_setup_produces_byte_identical_gain_across_two_independent_worlds()
    {
        double GainOnce()
        {
            var world = BuildWorld();
            var location = new CellCoord(1, 1);
            var workplace = MakeWorkplace(world, location);
            world.AddWorkplace(workplace);
            var npc = MakeWorker(world, new ProfessionType(1), location, ActionType.Work, new RateGene(1.3));
            workplace.Hire(npc.Id);
            npc.Hire(workplace.Id);
            var ctx = new TickContext(world, world.Rng, world.Scheduler);

            new SkillPracticeSystem(Rules).Tick(world, ctx);
            return npc.Skills.Get(new SkillType(0));
        }

        Assert.Equal(GainOnce(), GainOnce());
    }
}
