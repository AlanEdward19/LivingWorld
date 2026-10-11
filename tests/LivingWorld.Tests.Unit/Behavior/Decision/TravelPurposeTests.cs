using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Decision;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;
using LivingWorld.Tests.Shared;

namespace LivingWorld.Tests.Unit.Behavior.Decision;

/// <summary>O destino de uma viagem segue o motivo dela. Antes seguia uma ordem fixa (cama, trabalho, mercado):
/// quem saía do trabalho para comprar comida era levado para casa, e de casa ao trabalho, sem nunca chegar ao
/// mercado — gente com dinheiro morria de fome com a despensa vazia.</summary>
public class TravelPurposeTests
{
    private static readonly WorldCalendar Calendar = new(24, 30, 12);
    private static readonly CellCoord HomeCell = new(1, 1);
    private static readonly CellCoord WorkCell = new(3, 3);
    private static readonly CellCoord MarketCell = new(8, 8);

    private static readonly EconomyRules Rules = EconomyRules.Create(
        enabled: true, foodResourceId: 1, waterResourceId: 2,
        capacityByResourceLocation: new Dictionary<(int, int), long>(),
        spoilagePerDayByResource: new Dictionary<int, double>(),
        wageByProfession: new Dictionary<int, long> { [1] = 30 },
        priceFloor: new Dictionary<int, long>(),
        priceCeiling: new Dictionary<int, long>(),
        priceSensitivity: 0,
        demandBaselinePerNpc: new Dictionary<int, double>()).Value!;

    private static (WorldState World, Npc Npc) Build(ActionType? purpose)
    {
        var catalog = new EconomyCatalog(
            new Dictionary<int, ProductionRecipe>(), [2], new Dictionary<int, int> { [1] = 1 });
        var world = new WorldState(
            Calendar, 1, ScenarioRunner.DefaultMap(1), ScenarioRunner.DefaultPopulationCatalog,
            ScenarioRunner.DefaultPopulationRules, ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules, economyRules: Rules, economyCatalog: catalog);

        var npc = new Npc(
            world.NextNpcIdAndAdvance(), "worker", Sex.Male, WorldDate.Epoch(Calendar).AddYears(-30), new CultureId(1),
            HomeCell, null, null, null, 100,
            Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!, new ProfessionType(1), WorkCell,
            wallet: new Money(100));
        world.AddNpc(npc);
        var household = new Household(new HouseholdId(1), HomeCell, npc.Id, [npc.Id]);
        npc.JoinHousehold(household.Id);
        world.AddHousehold(household);

        var farm = new Workplace(
            world.NextWorkplaceIdAndAdvance(), new LocationType(1), WorkCell, maxVacancies: 1, employees: [npc.Id],
            stock: new Dictionary<ResourceType, long>(), treasury: Money.Zero, prices: new Dictionary<ResourceType, long>());
        world.AddWorkplace(farm);
        npc.Hire(farm.Id);
        world.AddWorkplace(new Workplace(
            world.NextWorkplaceIdAndAdvance(), new LocationType(2), MarketCell, maxVacancies: 0, employees: [],
            stock: new Dictionary<ResourceType, long> { [new ResourceType(1)] = 100, [new ResourceType(2)] = 100 },
            treasury: Money.Zero,
            prices: new Dictionary<ResourceType, long> { [new ResourceType(1)] = 1, [new ResourceType(2)] = 1 }));

        npc.SetCurrentAction(ActionType.Travel, tick: -1000);   // viaja há muito tempo: chega no próximo despertar
        npc.SetTravelPurpose(purpose);
        return (world, npc);
    }

    private static CellCoord ArrivalOf(WorldState world, Npc npc)
    {
        SimulationWakeTestHelper.Wake(world, npc);
        new BehaviorDecisionSystem().Tick(world, new TickContext(world, world.Rng, world.Scheduler));
        return npc.CurrentLocation;
    }

    [Fact]
    public void Someone_travelling_to_buy_arrives_at_the_market_not_at_home()
    {
        var (world, npc) = Build(ActionType.Buy);

        Assert.Equal(MarketCell, ArrivalOf(world, npc));
    }

    [Fact]
    public void Someone_travelling_to_sleep_goes_home_and_to_work_goes_to_work()
    {
        var (sleepWorld, sleeper) = Build(ActionType.Sleep);
        Assert.Equal(HomeCell, ArrivalOf(sleepWorld, sleeper));

        var (workWorld, worker) = Build(ActionType.Work);
        // já está no trabalho: o destino do motivo é a própria célula, então vale a ordem de antes (casa)
        Assert.NotEqual(MarketCell, ArrivalOf(workWorld, worker));
    }

    [Fact]
    public void The_purpose_is_part_of_the_canonical_state_and_clears_when_the_trip_ends()
    {
        var (world, npc) = Build(ActionType.Buy);
        var withPurpose = WorldSnapshot.CanonicalHash(world);

        npc.SetTravelPurpose(null);
        Assert.NotEqual(withPurpose, WorldSnapshot.CanonicalHash(world));

        npc.SetTravelPurpose(ActionType.Buy);
        npc.SetCurrentAction(ActionType.Work, tick: 5);   // qualquer ação que não seja viagem limpa o motivo
        Assert.Null(npc.TravelPurpose);
    }
}
