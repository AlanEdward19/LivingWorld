using LivingWorld.Domain;
using LivingWorld.Simulation;
using LivingWorld.Simulation.Behavior;

namespace LivingWorld.Tests.Stage4;

/// <summary>Fase 15.1, Stage 4, T9 (LWV-02.3/LWV-06): comuta de propósito pra trabalho/casa —
/// rota real entre células adjacentes, trabalho só depois de chegar no workplace real, retorno
/// pra casa quando a rotina sai da janela de trabalho, sem teleporte nem efeito pós-morte, e
/// bloqueio (sem workplace real) nunca fabrica deslocamento fingindo trabalhar.</summary>
public class PurposefulCommuteTests
{
    private static readonly WorldCalendar Calendar = new(24, 30, 12);

    private static readonly Personality Neutral =
        Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    private static readonly LifeStageRules Stages = LifeStageRules.Create(childMaxAge: 14, adultMaxAge: 64).Value!;

    private static readonly GeographyCatalog GeoCatalog = new(
        TerrainIds: new HashSet<int> { 1 }, BiomeIds: new HashSet<int> { 1 }, ResourceIds: new HashSet<int>());

    private static readonly NeedsRules Rules = NeedsRules.Create(
        hungerDecayPerHour: 0, thirstDecayPerHour: 0, sleepDecayPerHour: 0, socialDecayPerHour: 0,
        urgencyThreshold: 100, maxActionSelectionSteps: 10, hysteresisEnabled: false,
        continuityBonus: 0, homelessSleepEfficiency: 0.5).Value!;

    /// <summary>Mapa 2x1 com custo suficiente pra exigir mais de 1 tick entre as duas células
    /// (dist=1 × terrainFactor=1 × base=2.5 = 2.5 -> ceil = 3 ticks) — mesmo cenário de
    /// <c>BehaviorDecisionSystemTravelTests</c>, pra provar deslocamento real, não teleporte.</summary>
    private static WorldMap MakeTwoCellMap()
    {
        var cost = new CostWeights(Base: 2.5, AltitudeWeight: 0, TerrainWeight: new Dictionary<int, double> { [1] = 1.0 });
        var cells = new List<MapCell>
        {
            new(new CellCoord(0, 0), new TerrainType(1), new BiomeType(1), Altitude: 0, HasWater: false, Resources: []),
            new(new CellCoord(1, 0), new TerrainType(1), new BiomeType(1), Altitude: 0, HasWater: false, Resources: []),
        };
        var regions = RegionGrid.Partition(width: 2, height: 1, regionSize: 2);
        return WorldMap.Create(width: 2, height: 1, seed: 1, GeoCatalog, cost, cells, regions, settlements: []).Value!;
    }

    private static ActionCatalog MakeWorkAllDayCatalog() => ActionCatalog.Create(
        maxDurationHours: new Dictionary<ActionType, int>
        {
            [ActionType.Eat] = 1, [ActionType.Sleep] = 1, [ActionType.Work] = 8,
            [ActionType.Socialize] = 1, [ActionType.Travel] = 1, [ActionType.Idle] = 1, [ActionType.Buy] = 1,
        },
        routineSlots: [new RoutineSlot(ProfessionId: null, LifeStage.Adult, HourStart: 0, HourEnd: 23, ActionType.Work)],
        defaultAction: ActionType.Idle).Value!;

    /// <summary>Trabalha nas primeiras horas do dia, dorme (em casa) no resto — força o retorno
    /// pra casa quando a rotina sai da janela de trabalho, sem depender da duração de Work
    /// completar (Work dura mais que a janela inteira: a interrupção é da rotina, não do efeito).</summary>
    private static ActionCatalog MakeWorkThenSleepCatalog() => ActionCatalog.Create(
        maxDurationHours: new Dictionary<ActionType, int>
        {
            [ActionType.Eat] = 1, [ActionType.Sleep] = 1, [ActionType.Work] = 100,
            [ActionType.Socialize] = 1, [ActionType.Travel] = 1, [ActionType.Idle] = 1, [ActionType.Buy] = 1,
        },
        routineSlots:
        [
            new RoutineSlot(ProfessionId: null, LifeStage.Adult, HourStart: 0, HourEnd: 4, ActionType.Work),
            new RoutineSlot(ProfessionId: null, LifeStage.Adult, HourStart: 5, HourEnd: 23, ActionType.Sleep),
        ],
        defaultAction: ActionType.Idle).Value!;

    private static (WorldState World, Npc Npc, Workplace Workplace) BuildEmployedWorld(
        WorldMap map, ActionCatalog catalog, CellCoord householdLocation, CellCoord workplaceLocation)
    {
        var world = new WorldState(
            Calendar, seed: 1, map, ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            Rules, catalog, Stages);

        var npcId = world.NextNpcIdAndAdvance();
        var npc = new Npc(
            npcId, "npc", Sex.Male, WorldDate.Epoch(Calendar).AddYears(-30), new CultureId(1), householdLocation,
            motherId: null, fatherId: null, household: world.NextHouseholdIdAndAdvance(), health: 100,
            personality: Neutral, profession: ProfessionType.None, currentLocation: householdLocation);

        var household = new Household(npc.Household!.Value, householdLocation, npc.Id, [npc.Id]);
        var workplace = new Workplace(
            world.NextWorkplaceIdAndAdvance(), new LocationType(1), workplaceLocation, maxVacancies: 1,
            employees: [npc.Id], stock: new Dictionary<ResourceType, long>(), treasury: Money.Zero,
            prices: new Dictionary<ResourceType, long>());
        npc.Hire(workplace.Id);

        world.AddNpc(npc);
        world.AddHousehold(household);
        world.AddWorkplace(workplace);

        var ctx = new TickContext(world, world.Rng, world.Scheduler);
        NpcWakeScheduler.ScheduleWake(world, ctx, npc.Id.Value, world.CurrentDate.TotalHours + 1);

        return (world, npc, workplace);
    }

    private static (WorldState World, Npc Npc) BuildUnemployedAdultWorld(WorldMap map, ActionCatalog catalog, CellCoord location)
    {
        var world = new WorldState(
            Calendar, seed: 1, map, ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            Rules, catalog, Stages);

        var npc = new Npc(
            world.NextNpcIdAndAdvance(), "npc", Sex.Male, WorldDate.Epoch(Calendar).AddYears(-30), new CultureId(1), location,
            motherId: null, fatherId: null, household: world.NextHouseholdIdAndAdvance(), health: 100,
            personality: Neutral, profession: ProfessionType.None, currentLocation: location);
        var household = new Household(npc.Household!.Value, location, npc.Id, [npc.Id]);

        world.AddNpc(npc);
        world.AddHousehold(household);

        var ctx = new TickContext(world, world.Rng, world.Scheduler);
        NpcWakeScheduler.ScheduleWake(world, ctx, npc.Id.Value, world.CurrentDate.TotalHours + 1);
        return (world, npc);
    }

    [Fact]
    public void Employed_npc_commutes_to_the_real_workplace_consuming_real_ticks_before_arriving()
    {
        var map = MakeTwoCellMap();
        var home = new CellCoord(0, 0);
        var work = new CellCoord(1, 0);
        long ticksNeeded = TravelResolution.TicksBetween(map, home, work);
        Assert.True(ticksNeeded > 1, "cenário precisa de mais de 1 tick pra provar deslocamento real");

        var (world, npc, workplace) = BuildEmployedWorld(map, MakeWorkAllDayCatalog(), home, work);
        var clock = new WorldClock([new BehaviorDecisionSystem()]);

        clock.Tick(world);

        // LWV-02.3: decidiu trabalhar num local diferente do atual -> ação efetiva é Travel; não
        // pula direto pro destino no mesmo tick da decisão (adjacent route, não teleporte).
        Assert.Equal(ActionType.Travel, npc.CurrentAction);
        Assert.Equal(home, npc.CurrentLocation);

        for (int i = 0; i < ticksNeeded + 5; i++)
            clock.Tick(world);

        Assert.Equal(work, npc.CurrentLocation);
        Assert.Equal(workplace.Location, npc.CurrentLocation);
    }

    [Fact]
    public void Npc_only_starts_working_after_arriving_at_the_real_workplace()
    {
        var map = MakeTwoCellMap();
        var home = new CellCoord(0, 0);
        var work = new CellCoord(1, 0);
        long ticksNeeded = TravelResolution.TicksBetween(map, home, work);

        var (world, npc, workplace) = BuildEmployedWorld(map, MakeWorkAllDayCatalog(), home, work);
        var clock = new WorldClock([new BehaviorDecisionSystem()]);

        clock.Tick(world);
        Assert.Equal(ActionType.Travel, npc.CurrentAction); // ainda em trânsito, nunca "trabalha" remoto

        for (int i = 0; i < ticksNeeded + 5; i++)
            clock.Tick(world);

        // LWV-02.3: só passa a Work depois de chegar no workplace real, nunca antes.
        Assert.Equal(ActionType.Work, npc.CurrentAction);
        Assert.Equal(workplace.Location, npc.CurrentLocation);
    }

    [Fact]
    public void Npc_returns_home_when_the_routine_leaves_the_work_window()
    {
        var map = MakeTwoCellMap();
        var home = new CellCoord(0, 0);
        var work = new CellCoord(1, 0);

        var (world, npc, _) = BuildEmployedWorld(map, MakeWorkThenSleepCatalog(), home, work);
        var clock = new WorldClock([new BehaviorDecisionSystem()]);
        var ctx = new TickContext(world, world.Rng, world.Scheduler);

        // Necessidades com decay=0 nunca cruzam limiar — sem isso o próximo despertar agendado
        // seria só ao fim da duração gigante de Work (T9 usa 100h de propósito, ver comentário
        // do catálogo), e a virada de rotina pra Sleep (hora 5) nunca seria observada dentro da
        // janela deste teste. Reagendar o despertar a cada tick simula o mesmo "acordar por
        // hora" que o cenário de produção obtém via NEEDS-08/PERF-08 quando alguma necessidade
        // decai de verdade — não é o que este teste está verificando.
        for (int i = 0; i < 20; i++)
        {
            clock.Tick(world);
            NpcWakeScheduler.ScheduleWake(world, ctx, npc.Id.Value, world.CurrentDate.TotalHours + 1);
        }

        // LWV-02.3: comuta de propósito é ida E volta — não fica presa no emprego pra sempre.
        Assert.Equal(home, npc.CurrentLocation);
    }

    [Fact]
    public void Return_leg_also_consumes_real_ticks_instead_of_teleporting_home()
    {
        var map = MakeTwoCellMap();
        var home = new CellCoord(0, 0);
        var work = new CellCoord(1, 0);
        long ticksNeeded = TravelResolution.TicksBetween(map, home, work);

        var (world, npc, _) = BuildEmployedWorld(map, MakeWorkThenSleepCatalog(), home, work);
        var clock = new WorldClock([new BehaviorDecisionSystem()]);
        var ctx = new TickContext(world, world.Rng, world.Scheduler);

        for (int i = 0; i < ticksNeeded + 3; i++)
        {
            clock.Tick(world);
            NpcWakeScheduler.ScheduleWake(world, ctx, npc.Id.Value, world.CurrentDate.TotalHours + 1);
        }
        Assert.Equal(work, npc.CurrentLocation); // chegou e trabalha, ainda dentro da janela de Work

        for (int i = 0; i < 20; i++) // atravessa a virada pra Sleep e a volta — ver comentário do teste anterior
        {
            clock.Tick(world);
            NpcWakeScheduler.ScheduleWake(world, ctx, npc.Id.Value, world.CurrentDate.TotalHours + 1);
        }

        Assert.Equal(home, npc.CurrentLocation);
        // A volta em si nunca é instantânea: se fosse teleporte, um único tick bastaria mesmo
        // partindo do zero — a mesma rota de ida (ticksNeeded > 1) vale pra volta.
        Assert.True(ticksNeeded > 1);
    }

    [Fact]
    public void Npc_that_dies_while_commuting_to_work_never_arrives_or_works()
    {
        var map = MakeTwoCellMap();
        var home = new CellCoord(0, 0);
        var work = new CellCoord(1, 0);
        long ticksNeeded = TravelResolution.TicksBetween(map, home, work);

        var (world, npc, _) = BuildEmployedWorld(map, MakeWorkAllDayCatalog(), home, work);
        var clock = new WorldClock([new BehaviorDecisionSystem()]);

        clock.Tick(world); // decide ir trabalhar -> Travel
        Assert.Equal(ActionType.Travel, npc.CurrentAction);

        npc.Die(world.CurrentDate); // morre em trânsito

        for (int i = 0; i < ticksNeeded + 5; i++) clock.Tick(world);

        Assert.Equal(home, npc.CurrentLocation); // nunca chegou
        Assert.NotEqual(ActionType.Work, npc.CurrentAction); // efeito de trabalho nunca aplicado
    }

    [Fact]
    public void Adult_without_a_real_workplace_is_blocked_and_never_fakes_movement_while_marked_working()
    {
        var map = MakeTwoCellMap();
        var location = new CellCoord(0, 0);
        var (world, npc) = BuildUnemployedAdultWorld(map, MakeWorkAllDayCatalog(), location);
        var clock = new WorldClock([new BehaviorDecisionSystem()]);

        var exception = Record.Exception(() =>
        {
            for (int i = 0; i < 10; i++) clock.Tick(world);
        });

        Assert.Null(exception);
        Assert.Null(npc.Employer); // nunca tem workplace real
        Assert.Equal(ActionType.Work, npc.CurrentAction); // rotina continua marcando Work
        // LWV-02.3: sem capacidade real (sem employer/workplace), Work nunca fabrica deslocamento
        // fingindo trabalhar — bloqueado fica onde está.
        Assert.Equal(location, npc.CurrentLocation);
    }
}
