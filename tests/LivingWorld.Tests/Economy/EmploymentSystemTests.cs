using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Economy;

/// <summary>Fase 5, T14: <see cref="EmploymentSystem"/> — contratação por vaga livre + profissão
/// compatível (ECON-18/19/20), demissão de órfão antes de nova contratação no mesmo tick.</summary>
public class EmploymentSystemTests
{
    private static readonly WorldCalendar Calendar = new(24, 30, 12);

    private static readonly Personality SomePersonality =
        Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    private static readonly EconomyRules EnabledRules = EconomyRules.Create(
        enabled: true, foodResourceId: 1, waterResourceId: 2,
        capacityByResourceLocation: new Dictionary<(int, int), long>(),
        spoilagePerDayByResource: new Dictionary<int, double>(),
        wageByProfession: new Dictionary<int, long> { [1] = 10 },
        priceFloor: new Dictionary<int, long>(),
        priceCeiling: new Dictionary<int, long>(),
        priceSensitivity: 0,
        demandBaselinePerNpc: new Dictionary<int, double>()).Value!;

    private static readonly EconomyCatalog CatalogWithMapping = new(
        new Dictionary<int, ProductionRecipe>(), [], new Dictionary<int, int> { [1] = 1 });

    private static WorldState BuildWorld(ulong seed = 1)
    {
        var map = ScenarioRunner.DefaultMap(seed);
        return new WorldState(
            Calendar, seed, map, ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog, ScenarioRunner.DefaultLifeStageRules,
            economyRules: EnabledRules, economyCatalog: CatalogWithMapping);
    }

    private static Npc MakeAdult(WorldState world, NpcId id, ProfessionType profession, CellCoord location, CityId city = default)
    {
        var npc = new Npc(
            id, $"npc-{id.Value}", Sex.Male, WorldDate.Epoch(Calendar).AddYears(-30), new CultureId(1), location,
            motherId: null, fatherId: null, household: null, health: 100,
            personality: SomePersonality, profession: profession, currentLocation: location, city: city);
        world.AddNpc(npc);
        return npc;
    }

    private static Workplace MakeWorkplace(WorldState world, int maxVacancies = 1, CityId city = default) =>
        new(world.NextWorkplaceIdAndAdvance(), new LocationType(1), new CellCoord(1, 1), maxVacancies,
            employees: [], stock: new Dictionary<ResourceType, long>(), treasury: Money.Zero,
            prices: new Dictionary<ResourceType, long>(), city: city);

    [Fact]
    public void Unemployed_adult_with_matching_profession_gets_hired_within_one_tick()
    {
        var world = BuildWorld();
        var npc = MakeAdult(world, new NpcId(1), new ProfessionType(1), new CellCoord(1, 1));
        var workplace = MakeWorkplace(world);
        world.AddWorkplace(workplace);
        var sink = new RecordingSink();
        var ctx = new TickContext(world, world.Rng, world.Scheduler, sink);

        new EmploymentSystem().Tick(world, ctx);

        Assert.Equal(workplace.Id, npc.Employer);
        Assert.Contains(npc.Id, workplace.Employees);
        Assert.Contains(sink.Events, e => e.Kind == WorldEventKind.Hired);
    }

    [Fact]
    public void Dead_employee_is_fired_and_orphan_reference_never_survives_a_tick()
    {
        var world = BuildWorld();
        var npc = MakeAdult(world, new NpcId(1), new ProfessionType(1), new CellCoord(1, 1));
        var workplace = MakeWorkplace(world);
        world.AddWorkplace(workplace);
        workplace.Hire(npc.Id);
        npc.Hire(workplace.Id);
        npc.Die(WorldDate.Epoch(Calendar).AddYears(1));
        var sink = new RecordingSink();
        var ctx = new TickContext(world, world.Rng, world.Scheduler, sink);

        new EmploymentSystem().Tick(world, ctx);

        Assert.Null(npc.Employer);
        Assert.DoesNotContain(npc.Id, workplace.Employees);
        Assert.Contains(sink.Events, e => e.Kind == WorldEventKind.Fired);
    }

    [Fact]
    public void Disabled_economy_never_hires_anyone()
    {
        var map = ScenarioRunner.DefaultMap(1);
        var world = new WorldState(
            Calendar, 1, map, ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog, ScenarioRunner.DefaultLifeStageRules);
        var npc = MakeAdult(world, new NpcId(1), new ProfessionType(1), new CellCoord(1, 1));
        world.AddWorkplace(MakeWorkplace(world));
        var ctx = new TickContext(world, world.Rng, world.Scheduler);

        new EmploymentSystem().Tick(world, ctx);

        Assert.Null(npc.Employer);
    }

    /// <summary>ECON-20, checado a cada tick por 10 anos — vaga nunca excede o teto, todo
    /// Employer resolve pra um Workplace existente (mesmo idioma de
    /// BehaviorDecisionSystemHysteresisTests: <c>clock.Tick</c> por hora, não <c>clock.Run</c> em
    /// lote).</summary>
    [Fact]
    public void No_workplace_exceeds_max_vacancies_and_every_employer_resolves_over_10_years()
    {
        var world = new WorldState(
            Calendar, 7, ScenarioRunner.InitialMap(7, 20), ScenarioRunner.DefaultPopulationCatalog,
            ScenarioRunner.DefaultPopulationRules, ScenarioRunner.DefaultNeedsRules,
            ScenarioRunner.DefaultActionCatalog, ScenarioRunner.DefaultLifeStageRules);
        PopulationSeeder.SeedInitial(world, 20, ScenarioRunner.DefaultCulture, ScenarioRunner.DefaultVillageLocation);
        world.AddWorkplace(MakeWorkplace(world, maxVacancies: 5));
        // ScenarioRunner.DefaultSystems() já inclui EmploymentSystem (T20) — não duplicar.
        var clock = new WorldClock(ScenarioRunner.DefaultSystems());
        const long tenYears = 10 * 12 * 30 * 24;

        for (long tick = 0; tick < tenYears; tick++)
        {
            clock.Tick(world);

            foreach (var workplace in world.Workplaces)
                Assert.True(workplace.Employees.Count <= workplace.MaxVacancies,
                    $"workplace {workplace.Id.Value} excedeu MaxVacancies no tick {world.CurrentDate.TotalHours}");

            foreach (var npc in world.Npcs.Where(n => n.Employer is not null))
                Assert.NotNull(world.FindWorkplace(npc.Employer!.Value));
        }
    }

    /// <summary>Ghost-town fix: um workplace com vaga que pertence a outra cidade não pode
    /// contratar um NPC desempregado de fora dela — sem isso, NPCs "trabalhavam" numa cidade
    /// vizinha e nunca ficavam de fato na cidade recém-fundada.</summary>
    [Fact]
    public void Unemployed_adult_in_city_A_is_not_hired_by_vacancy_in_city_B()
    {
        var world = BuildWorld();
        var cityA = new CityId(Guid.NewGuid());
        var cityB = new CityId(Guid.NewGuid());
        var npc = MakeAdult(world, new NpcId(1), new ProfessionType(1), new CellCoord(1, 1), city: cityA);
        var workplaceInCityB = MakeWorkplace(world, city: cityB);
        world.AddWorkplace(workplaceInCityB);
        var sink = new RecordingSink();
        var ctx = new TickContext(world, world.Rng, world.Scheduler, sink);

        new EmploymentSystem().Tick(world, ctx);

        Assert.Null(npc.Employer);
        Assert.Empty(workplaceInCityB.Employees);
        Assert.DoesNotContain(sink.Events, e => e.Kind == WorldEventKind.Hired);
    }

    /// <summary>Regression companion to the same-city guard above: a vacancy in the NPC's own
    /// city still hires normally.</summary>
    [Fact]
    public void Unemployed_adult_in_city_A_is_hired_by_vacancy_in_city_A()
    {
        var world = BuildWorld();
        var cityA = new CityId(Guid.NewGuid());
        var npc = MakeAdult(world, new NpcId(1), new ProfessionType(1), new CellCoord(1, 1), city: cityA);
        var workplaceInCityA = MakeWorkplace(world, city: cityA);
        world.AddWorkplace(workplaceInCityA);
        var sink = new RecordingSink();
        var ctx = new TickContext(world, world.Rng, world.Scheduler, sink);

        new EmploymentSystem().Tick(world, ctx);

        Assert.Equal(workplaceInCityA.Id, npc.Employer);
        Assert.Contains(npc.Id, workplaceInCityA.Employees);
        Assert.Contains(sink.Events, e => e.Kind == WorldEventKind.Hired);
    }

    private sealed class RecordingSink : IWorldEventSink
    {
        public List<WorldEvent> Events { get; } = [];
        public void Record(WorldEvent evt) => Events.Add(evt);
    }
}
