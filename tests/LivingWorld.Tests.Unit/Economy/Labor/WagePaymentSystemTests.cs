using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Labor;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Economy.Labor;

/// <summary>Fase 5, T17: <see cref="WagePaymentSystem"/> — caixa suficiente paga todo mundo,
/// caixa insuficiente emite <see cref="WorldEventKind.WageUnpaid"/> sem alterar nenhum saldo
/// (ECON-21/22).</summary>
public class WagePaymentSystemTests
{
    private static readonly WorldCalendar Calendar = new(24, 30, 12);

    private static readonly Personality SomePersonality =
        Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    private static readonly EconomyRules Rules = EconomyRules.Create(
        enabled: true, foodResourceId: 1, waterResourceId: 2,
        capacityByResourceLocation: new Dictionary<(int, int), long>(),
        spoilagePerDayByResource: new Dictionary<int, double>(),
        wageByProfession: new Dictionary<int, long> { [1] = 30 },
        priceFloor: new Dictionary<int, long>(),
        priceCeiling: new Dictionary<int, long>(),
        priceSensitivity: 0,
        demandBaselinePerNpc: new Dictionary<int, double>()).Value!;

    private static WorldState BuildWorld() =>
        new(
            Calendar, 1, ScenarioRunner.DefaultMap(1), ScenarioRunner.DefaultPopulationCatalog,
            ScenarioRunner.DefaultPopulationRules, ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules, economyRules: Rules,
            economyCatalog: new EconomyCatalog(new Dictionary<int, ProductionRecipe>(), [], new Dictionary<int, int>()));

    private static Npc MakeEmployee(WorldState world)
    {
        var npc = new Npc(
            world.NextNpcIdAndAdvance(), "worker", Sex.Male, WorldDate.Epoch(Calendar).AddYears(-30), new CultureId(1),
            new CellCoord(1, 1), motherId: null, fatherId: null, household: null, health: 100,
            personality: SomePersonality, profession: new ProfessionType(1), currentLocation: new CellCoord(1, 1));
        world.AddNpc(npc);
        return npc;
    }

    private static Workplace MakeWorkplace(WorldState world, Money treasury, params NpcId[] employees)
    {
        var workplace = new Workplace(
            world.NextWorkplaceIdAndAdvance(), new LocationType(1), new CellCoord(1, 1), maxVacancies: employees.Length,
            employees: employees, stock: new Dictionary<ResourceType, long>(), treasury: treasury,
            prices: new Dictionary<ResourceType, long>());
        world.AddWorkplace(workplace);
        return workplace;
    }

    [Fact]
    public void Workplace_with_sufficient_treasury_pays_every_employee_exact_sum()
    {
        var world = BuildWorld();
        var e1 = MakeEmployee(world);
        var e2 = MakeEmployee(world);
        var workplace = MakeWorkplace(world, new Money(100), e1.Id, e2.Id);
        e1.Hire(workplace.Id);
        e2.Hire(workplace.Id);
        var ctx = new TickContext(world, world.Rng, world.Scheduler);

        new WagePaymentSystem().Tick(world, ctx);

        Assert.Equal(new Money(30), e1.Wallet);
        Assert.Equal(new Money(30), e2.Wallet);
        Assert.Equal(new Money(40), workplace.Treasury);
    }

    /// <summary>Caixa menor que a folha: o empregador paga o que tem (nunca cunha moeda) e o restante fica em
    /// atraso, registrado em <see cref="WorldEventKind.WageUnpaid"/>. Sensor de mutação (R5): sem o débito do
    /// caixa no pagamento, a soma carteira + caixa passaria do total anterior (dinheiro criado do nada).</summary>
    [Fact]
    public void Workplace_with_insufficient_treasury_pays_what_it_has_and_emits_WageUnpaid_for_the_shortfall()
    {
        var world = BuildWorld();
        var e1 = MakeEmployee(world);
        var workplace = MakeWorkplace(world, new Money(10), e1.Id);
        e1.Hire(workplace.Id);
        var sink = new RecordingSink();
        var ctx = new TickContext(world, world.Rng, world.Scheduler, sink);

        new WagePaymentSystem().Tick(world, ctx);

        Assert.Equal(new Money(10), e1.Wallet);
        Assert.Equal(Money.Zero, workplace.Treasury);
        Assert.Equal(new Money(10), e1.Wallet + workplace.Treasury);   // nada foi criado
        Assert.Contains(sink.Events, e => e.Kind == WorldEventKind.WageUnpaid);
    }

    /// <summary>Dois empregados de mesmo salário e caixa para metade da folha: cada um recebe a mesma fração,
    /// e não o primeiro por id o salário inteiro e o segundo nada. Cada atraso gera seu evento.</summary>
    [Fact]
    public void Partial_treasury_pays_every_employee_the_same_fraction_and_reports_each_shortfall()
    {
        var world = BuildWorld();
        var e1 = MakeEmployee(world);
        var e2 = MakeEmployee(world);
        var workplace = MakeWorkplace(world, new Money(30), e1.Id, e2.Id);
        e1.Hire(workplace.Id);
        e2.Hire(workplace.Id);
        var sink = new RecordingSink();
        var ctx = new TickContext(world, world.Rng, world.Scheduler, sink);

        new WagePaymentSystem().Tick(world, ctx);

        Assert.Equal(new Money(15), e1.Wallet);
        Assert.Equal(new Money(15), e2.Wallet);
        Assert.Equal(Money.Zero, workplace.Treasury);
        var unpaid = sink.Events.Where(e => e.Kind == WorldEventKind.WageUnpaid).ToList();
        Assert.Equal(2, unpaid.Count);
        Assert.All(unpaid, e => Assert.Equal("WagePaymentSystem", e.SourceSystem));
    }

    private sealed class RecordingSink : IWorldEventSink
    {
        public List<WorldEvent> Events { get; } = [];
        public void Record(WorldEvent evt) => Events.Add(evt);
    }
}
