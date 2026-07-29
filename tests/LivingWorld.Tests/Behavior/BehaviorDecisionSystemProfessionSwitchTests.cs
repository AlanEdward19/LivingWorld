using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Behavior;

/// <summary>Fase 6, T11: <see cref="BehaviorDecisionSystem"/> — escolha/troca de profissão
/// (SKILL-13/14). Score combina habilidade atual na candidata, personalidade (mesmo padrão de
/// <see cref="PersonalityWeighting"/>) e vagas abertas, todos como peso (nenhum trava). Sem
/// <see cref="SkillsRules"/> (default), a troca fica inteiramente desligada — mesmo
/// comportamento da Fase 4.</summary>
public class BehaviorDecisionSystemProfessionSwitchTests
{
    private static readonly WorldCalendar Calendar = new(24, 30, 12);
    private static readonly LifeStageRules Stages = LifeStageRules.Create(childMaxAge: 14, adultMaxAge: 64).Value!;

    private static readonly Personality Neutral =
        Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    private static readonly NeedsRules Rules = NeedsRules.Create(
        hungerDecayPerHour: 0, thirstDecayPerHour: 0, sleepDecayPerHour: 0, socialDecayPerHour: 0,
        urgencyThreshold: 100, maxActionSelectionSteps: 10, hysteresisEnabled: false,
        continuityBonus: 0, homelessSleepEfficiency: 0.5).Value!;

    private static readonly ActionCatalog Catalog = ActionCatalog.Create(
        maxDurationHours: new Dictionary<ActionType, int>
        {
            [ActionType.Eat] = 2,
            [ActionType.Sleep] = 8,
            [ActionType.Work] = 8,
            [ActionType.Socialize] = 3,
            [ActionType.Travel] = 4,
            [ActionType.Idle] = 2,
            [ActionType.Buy] = 2,
        },
        routineSlots: [new RoutineSlot(ProfessionId: null, Stage: LifeStage.Adult, HourStart: 0, HourEnd: 23, Action: ActionType.Work)],
        defaultAction: ActionType.Idle).Value!;

    private static readonly EconomyCatalog TwoProfessionCatalog = new(
        new Dictionary<int, ProductionRecipe>(), [], new Dictionary<int, int> { [1] = 1, [2] = 2 });

    private static SkillsRules MakeSkillsRules() => SkillsRules.Create(
        cap: 100, baseRateBySource: new Dictionary<SkillGainSource, double>(),
        skillByProfession: new Dictionary<int, SkillType> { [1] = SkillType.Agriculture, [2] = SkillType.Craft }).Value!;

    private static WorldState BuildWorld(EconomyCatalog? catalog = null)
    {
        var map = ScenarioRunner.DefaultMap(1);
        return new WorldState(
            Calendar, 1, map, ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            Rules, Catalog, Stages, economyCatalog: catalog ?? TwoProfessionCatalog);
    }

    private static Npc MakeAdult(WorldState world, ProfessionType profession, SkillSet? skills = null)
    {
        var location = new CellCoord(1, 1);
        var npc = new Npc(
            world.NextNpcIdAndAdvance(), "npc", Sex.Male, WorldDate.Epoch(Calendar).AddYears(-30), new CultureId(1),
            location, motherId: null, fatherId: null, household: null, health: 100,
            personality: Neutral, profession: profession, currentLocation: location, skills: skills);
        world.AddNpc(npc);
        SimulationWakeTestHelper.Wake(world, npc);
        return npc;
    }

    private static Workplace MakeWorkplace(WorldState world, int locationTypeId, int maxVacancies, int hiredCount)
    {
        var workplace = new Workplace(
            world.NextWorkplaceIdAndAdvance(), new LocationType(locationTypeId), new CellCoord(1, 1), maxVacancies,
            employees: [], stock: new Dictionary<ResourceType, long>(), treasury: Money.Zero,
            prices: new Dictionary<ResourceType, long>());
        world.AddWorkplace(workplace);
        for (int i = 0; i < hiredCount; i++)
        {
            var filler = MakeAdult(world, new ProfessionType(locationTypeId));
            workplace.Hire(filler.Id);
            filler.Hire(workplace.Id);
        }
        return workplace;
    }

    private static TickContext Ctx(WorldState world) => new(world, world.Rng, world.Scheduler);

    [Fact]
    public void Without_skills_rules_profession_never_switches()
    {
        var world = BuildWorld();
        MakeWorkplace(world, locationTypeId: 1, maxVacancies: 5, hiredCount: 0);
        MakeWorkplace(world, locationTypeId: 2, maxVacancies: 5, hiredCount: 0);
        var npc = MakeAdult(world, new ProfessionType(1), SkillSet.Initial(0).WithGain(SkillType.Craft, 90, cap: 100));

        new BehaviorDecisionSystem(skillsRules: null).Tick(world, Ctx(world));

        Assert.Equal(new ProfessionType(1), npc.Profession);
    }

    [Fact]
    public void Higher_skill_in_candidate_profession_wins_the_switch()
    {
        var world = BuildWorld();
        MakeWorkplace(world, locationTypeId: 1, maxVacancies: 5, hiredCount: 0);
        MakeWorkplace(world, locationTypeId: 2, maxVacancies: 5, hiredCount: 0);
        // Craft (profissão 2) muito mais alto que Agriculture (profissão 1, corrente) — vagas
        // iguais nos dois lados, então só a habilidade decide.
        var npc = MakeAdult(world, new ProfessionType(1), SkillSet.Initial(0).WithGain(SkillType.Craft, 90, cap: 100));

        new BehaviorDecisionSystem(MakeSkillsRules()).Tick(world, Ctx(world));

        Assert.Equal(new ProfessionType(2), npc.Profession);
    }

    [Fact]
    public void Switching_profession_preserves_old_skill_value()
    {
        var world = BuildWorld();
        MakeWorkplace(world, locationTypeId: 1, maxVacancies: 5, hiredCount: 0);
        MakeWorkplace(world, locationTypeId: 2, maxVacancies: 5, hiredCount: 0);
        var skills = SkillSet.Initial(0).WithGain(SkillType.Agriculture, 30, cap: 100).WithGain(SkillType.Craft, 90, cap: 100);
        var npc = MakeAdult(world, new ProfessionType(1), skills);

        new BehaviorDecisionSystem(MakeSkillsRules()).Tick(world, Ctx(world));

        Assert.Equal(new ProfessionType(2), npc.Profession);
        Assert.Equal(30, npc.Skills.Get(SkillType.Agriculture)); // estagnação, não reset (T7)
    }

    [Fact]
    public void Child_never_switches_profession()
    {
        var world = BuildWorld();
        MakeWorkplace(world, locationTypeId: 1, maxVacancies: 5, hiredCount: 0);
        MakeWorkplace(world, locationTypeId: 2, maxVacancies: 5, hiredCount: 0);
        var location = new CellCoord(1, 1);
        var child = new Npc(
            world.NextNpcIdAndAdvance(), "child", Sex.Male, WorldDate.Epoch(Calendar).AddYears(-5), new CultureId(1),
            location, motherId: null, fatherId: null, household: null, health: 100,
            personality: Neutral, profession: new ProfessionType(1), currentLocation: location,
            skills: SkillSet.Initial(0).WithGain(SkillType.Craft, 90, cap: 100));
        world.AddNpc(child);

        new BehaviorDecisionSystem(MakeSkillsRules()).Tick(world, Ctx(world));

        Assert.Equal(new ProfessionType(1), child.Profession);
    }

    [Fact]
    public void Open_vacancies_tilt_the_switch_when_skills_are_otherwise_equal()
    {
        var world = BuildWorld();
        MakeWorkplace(world, locationTypeId: 1, maxVacancies: 5, hiredCount: 5); // lotado
        MakeWorkplace(world, locationTypeId: 2, maxVacancies: 5, hiredCount: 0); // vazio
        // Mesma habilidade (0) nas duas — só a vaga aberta desempata a favor da profissão 2.
        var npc = MakeAdult(world, new ProfessionType(1));

        new BehaviorDecisionSystem(MakeSkillsRules()).Tick(world, Ctx(world));

        Assert.Equal(new ProfessionType(2), npc.Profession);
    }
}
