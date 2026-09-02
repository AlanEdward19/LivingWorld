using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Population;

/// <summary>Fase 6, T9: <see cref="SkillTeachingSystem"/> — as 5 fontes sociais de ganho
/// (SKILL-04..08): treino deliberado, escola, parental, observação, tutoria mestre-&gt;aprendiz.
/// Edge Case da spec: mestre morto no meio da tutoria encerra o vínculo sem exceção.</summary>
public class SkillTeachingSystemTests
{
    private static readonly WorldCalendar Calendar = new(24, 30, 12);

    private static readonly Personality SomePersonality =
        Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    private static readonly SkillsRules Rules = SkillsRules.Create(
        cap: 100,
        baseRateBySource: new Dictionary<SkillGainSource, double>
        {
            [SkillGainSource.DeliberateTraining] = 3.0,
            [SkillGainSource.School] = 2.0,
            [SkillGainSource.Parental] = 1.5,
            [SkillGainSource.Observation] = 0.5,
            [SkillGainSource.Tutoring] = 4.0,
        },
        skillByProfession: new Dictionary<int, SkillType> { [1] = new SkillType(0) },
        teachingSkill: new SkillType(6)).Value!;

    private static WorldState BuildWorld()
    {
        var map = ScenarioRunner.DefaultMap(1);
        return new WorldState(
            Calendar, 1, map, ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog, ScenarioRunner.DefaultLifeStageRules);
    }

    private static Npc MakeNpc(
        WorldState world, int ageYears, ProfessionType profession, CellCoord location,
        ActionType? action = null, NpcId? motherId = null, NpcId? fatherId = null,
        HouseholdId? household = null, WorkplaceId? employer = null, Money wallet = default,
        RateGene? rateGene = null, NpcId? mentor = null)
    {
        var npc = new Npc(
            world.NextNpcIdAndAdvance(), "npc", Sex.Male, WorldDate.Epoch(Calendar).AddYears(-ageYears), new CultureId(1),
            location, motherId: motherId, fatherId: fatherId, household: household, health: 100,
            personality: SomePersonality, profession: profession, currentLocation: location,
            currentAction: action, wallet: wallet, employer: employer, rateGene: rateGene, mentor: mentor);
        world.AddNpc(npc);
        return npc;
    }

    private static TickContext Ctx(WorldState world) => new(world, world.Rng, world.Scheduler);

    // --- DeliberateTraining (SKILL-04) ---

    private static WorkplaceId AddWorkplace(WorldState world)
    {
        var id = world.NextWorkplaceIdAndAdvance();
        world.AddWorkplace(new Workplace(
            id, new LocationType(1), new CellCoord(1, 1), maxVacancies: 10,
            employees: [], stock: new Dictionary<ResourceType, long>(), treasury: Money.Zero,
            prices: new Dictionary<ResourceType, long>()));
        return id;
    }

    [Fact]
    public void DeliberateTraining_adult_idle_with_money_and_employer_gains_skill_and_pays_treasury()
    {
        var world = BuildWorld();
        var employer = AddWorkplace(world);
        var npc = MakeNpc(
            world, 30, new ProfessionType(1), new CellCoord(1, 1), ActionType.Idle, wallet: new Money(10), employer: employer);

        new SkillTeachingSystem(Rules, ScenarioRunner.DefaultLifeStageRules).Tick(world, Ctx(world));

        Assert.True(npc.Skills.Get(new SkillType(0)) > 0);
        Assert.Equal(9, npc.Wallet.Amount);
        Assert.Equal(1, world.FindWorkplace(employer)!.Treasury.Amount);
    }

    [Fact]
    public void DeliberateTraining_without_money_does_not_grant_skill()
    {
        var world = BuildWorld();
        var employer = AddWorkplace(world);
        var npc = MakeNpc(world, 30, new ProfessionType(1), new CellCoord(1, 1), ActionType.Idle, wallet: Money.Zero, employer: employer);

        new SkillTeachingSystem(Rules, ScenarioRunner.DefaultLifeStageRules).Tick(world, Ctx(world));

        Assert.Equal(0, npc.Skills.Get(new SkillType(0)));
    }

    /// <summary>Fase 6, T12 (fix de conservação de dinheiro, ECON-26/27): sem vínculo
    /// empregatício, o custo do treino não tem Treasury pra ir — sem-op, mesmo padrão de "sem
    /// trabalhador presente, produção 0" do ProductionSystem, nunca dinheiro sumindo em
    /// silêncio.</summary>
    [Fact]
    public void DeliberateTraining_without_employer_does_not_grant_skill_or_charge_money()
    {
        var world = BuildWorld();
        var npc = MakeNpc(world, 30, new ProfessionType(1), new CellCoord(1, 1), ActionType.Idle, wallet: new Money(10));

        new SkillTeachingSystem(Rules, ScenarioRunner.DefaultLifeStageRules).Tick(world, Ctx(world));

        Assert.Equal(0, npc.Skills.Get(new SkillType(0)));
        Assert.Equal(10, npc.Wallet.Amount);
    }

    // --- School (SKILL-05) ---

    [Fact]
    public void School_child_with_mapped_profession_gains_skill()
    {
        var world = BuildWorld();
        var npc = MakeNpc(world, 10, new ProfessionType(1), new CellCoord(1, 1), ActionType.Idle);

        new SkillTeachingSystem(Rules, ScenarioRunner.DefaultLifeStageRules).Tick(world, Ctx(world));

        Assert.True(npc.Skills.Get(new SkillType(0)) > 0);
    }

    [Fact]
    public void School_does_not_apply_to_adult()
    {
        var world = BuildWorld();
        var npc = MakeNpc(world, 30, new ProfessionType(1), new CellCoord(1, 1), ActionType.Work, wallet: Money.Zero);

        new SkillTeachingSystem(Rules, ScenarioRunner.DefaultLifeStageRules).Tick(world, Ctx(world));

        Assert.Equal(0, npc.Skills.Get(new SkillType(0)));
    }

    // --- Parental (SKILL-06) ---

    [Fact]
    public void Parental_child_living_with_employed_parent_gains_skill()
    {
        var world = BuildWorld();
        var household = new HouseholdId(1);
        var location = new CellCoord(1, 1);
        var parent = MakeNpc(
            world, 30, new ProfessionType(1), location, ActionType.Work,
            household: household, employer: new WorkplaceId(1));
        var child = MakeNpc(world, 5, ProfessionType.None, location, ActionType.Idle, motherId: parent.Id, household: household);

        new SkillTeachingSystem(Rules, ScenarioRunner.DefaultLifeStageRules).Tick(world, Ctx(world));

        Assert.True(child.Skills.Get(new SkillType(0)) > 0);
    }

    [Fact]
    public void Parental_child_not_living_with_parent_does_not_gain()
    {
        var world = BuildWorld();
        // Locais diferentes: sem isso o pai trabalhando perto contaria como Observação (SKILL-07,
        // fonte distinta) e mascararia a ausência de ganho Parental que este teste verifica.
        var parent = MakeNpc(
            world, 30, new ProfessionType(1), new CellCoord(1, 1), ActionType.Work,
            household: new HouseholdId(1), employer: new WorkplaceId(1));
        var child = MakeNpc(
            world, 5, ProfessionType.None, new CellCoord(9, 9), ActionType.Idle, motherId: parent.Id, household: new HouseholdId(2));

        new SkillTeachingSystem(Rules, ScenarioRunner.DefaultLifeStageRules).Tick(world, Ctx(world));

        Assert.Equal(0, child.Skills.Get(new SkillType(0)));
    }

    // --- Observation (SKILL-07) ---

    [Fact]
    public void Observation_npc_near_working_npc_gains_less_than_direct_tutoring_for_same_inputs()
    {
        var location = new CellCoord(1, 1);

        var observationWorld = BuildWorld();
        var worker = MakeNpc(observationWorld, 30, new ProfessionType(1), location, ActionType.Work);
        var observer = MakeNpc(observationWorld, 30, ProfessionType.None, location, ActionType.Idle);
        new SkillTeachingSystem(Rules, ScenarioRunner.DefaultLifeStageRules).Tick(observationWorld, Ctx(observationWorld));
        double observationGain = observer.Skills.Get(new SkillType(0));

        var tutoringWorld = BuildWorld();
        var master = MakeNpc(tutoringWorld, 30, new ProfessionType(1), location, ActionType.Work);
        master.GainSkill(new SkillType(0), 50, Rules.Cap);
        var apprentice = MakeNpc(tutoringWorld, 30, ProfessionType.None, location, ActionType.Idle, mentor: master.Id);
        new SkillTeachingSystem(Rules, ScenarioRunner.DefaultLifeStageRules).Tick(tutoringWorld, Ctx(tutoringWorld));
        double tutoringGain = apprentice.Skills.Get(new SkillType(0));

        Assert.True(observationGain > 0);
        Assert.True(tutoringGain > observationGain);
    }

    [Fact]
    public void Observation_excludes_own_mentor_leaving_gain_to_tutoring_formula_only()
    {
        var world = BuildWorld();
        var location = new CellCoord(1, 1);
        var master = MakeNpc(world, 30, new ProfessionType(1), location, ActionType.Work);
        master.GainSkill(new SkillType(0), 40, Rules.Cap);
        var apprentice = MakeNpc(world, 30, ProfessionType.None, location, ActionType.Idle, mentor: master.Id);

        new SkillTeachingSystem(Rules, ScenarioRunner.DefaultLifeStageRules).Tick(world, Ctx(world));

        // Formula esperada da tutoria isolada (sem contribuição de observação empilhada): se
        // Observation contasse o mestre (mesmo sendo o Mentor), o ganho final excederia esta
        // fórmula — a igualdade exata prova a exclusão.
        // masterFactor = (min(masterSkill,cap)/cap) * (1 + masterTeaching/cap); baseGain via curva.
        double masterSkill = Math.Min(master.Skills.Get(new SkillType(0)), Rules.Cap);
        double masterTeaching = master.Skills.Get(new SkillType(6));
        double masterFactor = (masterSkill / Rules.Cap) * (1.0 + masterTeaching / Rules.Cap);
        double baseGain = Rules.Gain(0, SkillGainSource.Tutoring, apprentice.RateGene.Value);
        double expected = baseGain * masterFactor;

        Assert.Equal(expected, apprentice.Skills.Get(new SkillType(0)), precision: 10);
    }

    // --- Tutoring (SKILL-08) ---

    [Fact]
    public void Tutoring_gain_is_higher_with_higher_master_skill_and_teaching()
    {
        var location = new CellCoord(1, 1);

        var lowWorld = BuildWorld();
        var lowMaster = MakeNpc(lowWorld, 30, new ProfessionType(1), location, ActionType.Work);
        lowMaster.GainSkill(new SkillType(0), 5, Rules.Cap);
        var lowApprentice = MakeNpc(lowWorld, 10, ProfessionType.None, location, ActionType.Idle, mentor: lowMaster.Id);
        new SkillTeachingSystem(Rules, ScenarioRunner.DefaultLifeStageRules).Tick(lowWorld, Ctx(lowWorld));

        var highWorld = BuildWorld();
        var highMaster = MakeNpc(highWorld, 30, new ProfessionType(1), location, ActionType.Work);
        highMaster.GainSkill(new SkillType(0), 90, Rules.Cap);
        highMaster.GainSkill(new SkillType(6), 90, Rules.Cap);
        var highApprentice = MakeNpc(highWorld, 10, ProfessionType.None, location, ActionType.Idle, mentor: highMaster.Id);
        new SkillTeachingSystem(Rules, ScenarioRunner.DefaultLifeStageRules).Tick(highWorld, Ctx(highWorld));

        Assert.True(highApprentice.Skills.Get(new SkillType(0)) > lowApprentice.Skills.Get(new SkillType(0)));
    }

    [Fact]
    public void Tutoring_dead_mentor_clears_mentor_reference_without_exception_and_grants_no_gain()
    {
        var world = BuildWorld();
        var location = new CellCoord(1, 1);
        var master = MakeNpc(world, 30, new ProfessionType(1), location, ActionType.Work);
        var apprentice = MakeNpc(world, 10, ProfessionType.None, location, ActionType.Idle, mentor: master.Id);
        master.Die(world.CurrentDate);

        var exception = Record.Exception(() => new SkillTeachingSystem(Rules, ScenarioRunner.DefaultLifeStageRules).Tick(world, Ctx(world)));

        Assert.Null(exception);
        Assert.Null(apprentice.Mentor);
        Assert.Equal(0, apprentice.Skills.Get(new SkillType(0)));
    }
}
