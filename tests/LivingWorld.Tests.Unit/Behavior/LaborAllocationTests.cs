using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Population.Skills;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Decision;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;
using LivingWorld.Tests.Shared;

namespace LivingWorld.Tests.Unit.Behavior;

/// <summary>labor-allocation: o adulto vai para a oficina onde o trabalho dele vale mais, com margem de 1,25,
/// carência de 30 dias, vaga real e troca atômica. Duas oficinas: tipo 1 produz o recurso 7 (preço <c>priceA</c>),
/// tipo 2 produz o recurso 8 (preço <c>priceB</c>), 1 unidade por trabalhador.</summary>
public class LaborAllocationTests
{
    private static readonly WorldCalendar Calendar = new(24, 30, 12);
    private static readonly LifeStageRules Stages = LifeStageRules.Create(childMaxAge: 14, adultMaxAge: 64).Value!;
    private static readonly Personality Neutral = Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;
    private static readonly CellCoord Spot = new(1, 1);

    private sealed class RecordingSink : IWorldEventSink
    {
        public List<WorldEvent> Events { get; } = [];
        public void Record(WorldEvent evt) => Events.Add(evt);
    }

    private static readonly NeedsRules Needs = NeedsRules.Create(
        hungerDecayPerHour: 0, thirstDecayPerHour: 0, sleepDecayPerHour: 0, socialDecayPerHour: 0,
        urgencyThreshold: 100, maxActionSelectionSteps: 10, hysteresisEnabled: false,
        continuityBonus: 0, homelessSleepEfficiency: 0.5).Value!;

    private static readonly ActionCatalog Actions = ActionCatalog.Create(
        maxDurationHours: Enum.GetValues<ActionType>().ToDictionary(a => a, _ => 8),
        routineSlots: [new RoutineSlot(ProfessionId: null, Stage: LifeStage.Adult, HourStart: 0, HourEnd: 23, Action: ActionType.Work)],
        defaultAction: ActionType.Idle).Value!;

    private static SkillsRules Skills() => SkillsRules.Create(
        cap: 100, baseRateBySource: new Dictionary<SkillGainSource, double>(),
        skillByProfession: new Dictionary<int, SkillType> { [1] = new SkillType(0), [2] = new SkillType(7) },
        teachingSkill: new SkillType(6)).Value!;

    private static EconomyRules Economy() => EconomyRules.Create(
        enabled: true, foodResourceId: 1, waterResourceId: 2,
        capacityByResourceLocation: new Dictionary<(int, int), long>(),
        spoilagePerDayByResource: new Dictionary<int, double>(),
        wageByProfession: new Dictionary<int, long>(),
        priceFloor: new Dictionary<int, long> { [7] = 1, [8] = 1 }, priceCeiling: new Dictionary<int, long> { [7] = 100, [8] = 100 },
        priceSensitivity: 0, demandBaselinePerNpc: new Dictionary<int, double>()).Value!;

    private static EconomyCatalog Catalog() => new(
        new Dictionary<int, ProductionRecipe>
        {
            [1] = ProductionRecipe.Create(new Dictionary<int, long>(), new Dictionary<int, long> { [7] = 1 }, null, 10).Value!,
            [2] = ProductionRecipe.Create(new Dictionary<int, long>(), new Dictionary<int, long> { [8] = 1 }, null, 10).Value!,
        },
        MarketLocationTypeIds: [1, 2], LocationTypeByProfession: new Dictionary<int, int> { [1] = 1, [2] = 2 });

    private static (WorldState World, Workplace A, Workplace B) Build(long priceA, long priceB, int vacanciesB = 5)
    {
        var world = new WorldState(
            Calendar, 1, ScenarioRunner.DefaultMap(1), ScenarioRunner.DefaultPopulationCatalog,
            ScenarioRunner.DefaultPopulationRules, Needs, Actions, Stages,
            economyRules: Economy(), economyCatalog: Catalog());
        var a = AddWorkplace(world, locationType: 1, resource: 7, price: priceA, vacancies: 5);
        var b = AddWorkplace(world, locationType: 2, resource: 8, price: priceB, vacancies: vacanciesB);
        return (world, a, b);
    }

    private static Workplace AddWorkplace(WorldState world, int locationType, int resource, long price, int vacancies)
    {
        var workplace = new Workplace(
            world.NextWorkplaceIdAndAdvance(), new LocationType(locationType), Spot, vacancies, employees: [],
            stock: new Dictionary<ResourceType, long>(), treasury: Money.Zero,
            prices: new Dictionary<ResourceType, long> { [new ResourceType(resource)] = price });
        world.AddWorkplace(workplace);
        return workplace;
    }

    private static Npc AddAdult(WorldState world, Workplace? job, int profession)
    {
        var npc = new Npc(
            world.NextNpcIdAndAdvance(), "npc", Sex.Male, WorldDate.Epoch(Calendar).AddYears(-30), new CultureId(1),
            Spot, motherId: null, fatherId: null, household: null, health: 100,
            personality: Neutral, profession: new ProfessionType(profession), currentLocation: Spot);
        world.AddNpc(npc);
        if (job is not null)
        {
            job.Hire(npc.Id);
            npc.Hire(job.Id);
        }

        SimulationWakeTestHelper.Wake(world, npc);
        return npc;
    }

    /// <summary>Avança a data até a hora em que o empregado é reavaliado (a cada 6 h, escalonado por id).</summary>
    private static void AlignToReviewHour(WorldState world, Npc npc)
    {
        while ((world.CurrentDate.TotalHours + npc.Id.Value) % 6 != 0)
            world.CurrentDate = world.CurrentDate.AddHours(1);
    }

    private static List<WorldEvent> Evaluate(WorldState world)
    {
        var sink = new RecordingSink();
        new BehaviorDecisionSystem(Skills()).Tick(world, new TickContext(world, world.Rng, world.Scheduler, sink));
        return sink.Events;
    }

    [Fact]
    public void Equal_value_keeps_the_current_job_and_trade()
    {
        var (world, a, b) = Build(priceA: 5, priceB: 5);
        var npc = AddAdult(world, a, profession: 1);
        AlignToReviewHour(world, npc);

        var events = Evaluate(world);

        Assert.Equal(a.Id, npc.Employer);
        Assert.Equal(new ProfessionType(1), npc.Profession);
        Assert.DoesNotContain(events, e => e.SourceSystem == "LaborAllocation");
    }

    [Fact]
    public void Margin_of_1_25_decides_the_move()
    {
        var (world, a, b) = Build(priceA: 10, priceB: 12); // 1,2x: abaixo da margem
        var npc = AddAdult(world, a, profession: 1);
        AlignToReviewHour(world, npc);
        Evaluate(world);
        Assert.Equal(a.Id, npc.Employer);

        b.SetPrice(new ResourceType(8), 13); // 1,3x: acima
        world.CurrentDate = world.CurrentDate.AddDays(1); // o valor do trabalho vale o dia inteiro
        AlignToReviewHour(world, npc);
        var events = Evaluate(world);

        Assert.Equal(b.Id, npc.Employer);
        Assert.Equal(new ProfessionType(2), npc.Profession);
        Assert.Equal(world.CurrentDate.TotalHours, npc.LastJobChangeTick);
        Assert.DoesNotContain(npc.Id, a.Employees);
        Assert.Contains(npc.Id, b.Employees);
    }

    [Fact]
    public void Exactly_the_margin_stays_and_one_unit_above_it_moves()
    {
        var (world, a, b) = Build(priceA: 100, priceB: 125); // exatamente 1,25x
        var npc = AddAdult(world, a, profession: 1);
        AlignToReviewHour(world, npc);
        Evaluate(world);
        Assert.Equal(a.Id, npc.Employer);

        b.SetPrice(new ResourceType(8), 126);
        world.CurrentDate = world.CurrentDate.AddDays(1);
        AlignToReviewHour(world, npc);
        Evaluate(world);
        Assert.Equal(b.Id, npc.Employer);
    }

    [Fact]
    public void Cooldown_blocks_until_the_thirtieth_day_and_releases_on_it()
    {
        var (world, a, b) = Build(priceA: 5, priceB: 50);
        var npc = AddAdult(world, a, profession: 1);
        world.CurrentDate = world.CurrentDate.AddDays(60);
        AlignToReviewHour(world, npc);
        npc.MarkJobChange(world.CurrentDate.TotalHours - 30 * 24 + 1); // faltam 1 hora
        Evaluate(world);
        Assert.Equal(a.Id, npc.Employer);

        npc.MarkJobChange(world.CurrentDate.TotalHours - 30 * 24); // exatamente 30 dias
        Evaluate(world);
        Assert.Equal(b.Id, npc.Employer);
    }

    [Fact]
    public void A_market_holding_leftover_stock_with_no_price_is_not_a_seller()
    {
        var (world, farm, forge) = Build(priceA: 5, priceB: 5);
        farm.SetPrice(new ResourceType(1), 5);
        farm.Deposit(new ResourceType(1), 10, world.EconomyRules);
        forge.Deposit(new ResourceType(1), 10, world.EconomyRules); // sobra de trigo na forja, sem preço de trigo
        var index = LivingWorld.Simulation.Economy.Market.MarketIndex.BuildForTick(world);

        var nearest = index.NearestSelling(Spot, new ResourceType(1), new ResourceType(2));

        Assert.Equal(farm.Id, nearest!.Id);
    }

    [Fact]
    public void Move_is_atomic_one_fired_and_one_hired_in_the_same_tick()
    {
        var (world, a, b) = Build(priceA: 5, priceB: 50);
        var npc = AddAdult(world, a, profession: 1);
        AlignToReviewHour(world, npc);

        var events = Evaluate(world).Where(e => e.SourceSystem == "LaborAllocation").ToList();

        Assert.Equal([WorldEventKind.Fired, WorldEventKind.Hired], events.Select(e => e.Kind));
        Assert.Equal($"{npc.Id.Value}|{a.Id.Value}", events[0].Payload);
        Assert.Equal($"{npc.Id.Value}|{b.Id.Value}", events[1].Payload);
        Assert.Equal(events[0].Tick, events[1].Tick);
    }

    [Fact]
    public void A_recent_change_blocks_a_new_move_for_thirty_days()
    {
        var (world, a, b) = Build(priceA: 5, priceB: 50);
        var npc = AddAdult(world, a, profession: 1);
        world.CurrentDate = world.CurrentDate.AddDays(60);
        npc.MarkJobChange(world.CurrentDate.TotalHours - 29 * 24);
        AlignToReviewHour(world, npc);
        Evaluate(world);
        Assert.Equal(a.Id, npc.Employer);

        npc.MarkJobChange(world.CurrentDate.TotalHours - 31 * 24);
        Evaluate(world);
        Assert.Equal(b.Id, npc.Employer);
    }

    [Fact]
    public void Full_workplace_keeps_the_worker_where_he_is_without_firing_him()
    {
        var (world, a, b) = Build(priceA: 5, priceB: 50, vacanciesB: 1);
        var other = AddAdult(world, b, profession: 2); // a vaga da melhor oficina já está ocupada
        var npc = AddAdult(world, a, profession: 1);
        AlignToReviewHour(world, npc);

        var events = Evaluate(world);

        Assert.Equal(a.Id, npc.Employer);
        Assert.Equal(b.Id, other.Employer);
        Assert.DoesNotContain(events, e => e.Kind == WorldEventKind.Fired);
    }

    [Fact]
    public void Unemployed_adult_takes_the_workplace_where_work_is_worth_most()
    {
        var (world, a, b) = Build(priceA: 5, priceB: 9);
        var npc = AddAdult(world, job: null, profession: 1);

        Evaluate(world);

        Assert.Equal(b.Id, npc.Employer);
        Assert.Equal(new ProfessionType(2), npc.Profession);
    }

    [Fact]
    public void Full_stock_makes_the_output_worthless_so_nobody_is_pulled_to_a_glut()
    {
        var rules = EconomyRules.Create(
            enabled: true, foodResourceId: 1, waterResourceId: 2,
            capacityByResourceLocation: new Dictionary<(int, int), long> { [(8, 2)] = 100 },
            spoilagePerDayByResource: new Dictionary<int, double>(), wageByProfession: new Dictionary<int, long>(),
            priceFloor: new Dictionary<int, long> { [7] = 1, [8] = 1 }, priceCeiling: new Dictionary<int, long> { [7] = 100, [8] = 100 },
            priceSensitivity: 0, demandBaselinePerNpc: new Dictionary<int, double>()).Value!;
        var world = new WorldState(
            Calendar, 1, ScenarioRunner.DefaultMap(1), ScenarioRunner.DefaultPopulationCatalog,
            ScenarioRunner.DefaultPopulationRules, Needs, Actions, Stages, economyRules: rules, economyCatalog: Catalog());
        var a = AddWorkplace(world, 1, 7, price: 5, vacancies: 5);
        var b = new Workplace(
            world.NextWorkplaceIdAndAdvance(), new LocationType(2), Spot, 5, employees: [],
            stock: new Dictionary<ResourceType, long> { [new ResourceType(8)] = 100 }, treasury: Money.Zero,
            prices: new Dictionary<ResourceType, long> { [new ResourceType(8)] = 50 }); // preço alto, mas o estoque está cheio
        world.AddWorkplace(b);
        var npc = AddAdult(world, a, profession: 1);
        AlignToReviewHour(world, npc);

        Evaluate(world);

        Assert.Equal(a.Id, npc.Employer);
    }

    [Fact]
    public void Last_job_change_survives_a_snapshot_round_trip()
    {
        var (world, a, _) = Build(priceA: 5, priceB: 5);
        var npc = AddAdult(world, a, profession: 1);
        npc.MarkJobChange(777);

        var restored = WorldSnapshot.Deserialize(WorldSnapshot.Serialize(world));

        Assert.Equal(777, restored.Npcs.Single().LastJobChangeTick);
    }
}
