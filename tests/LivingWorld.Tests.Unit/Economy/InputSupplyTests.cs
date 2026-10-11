using System.Text.Json.Nodes;
using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Production;
using LivingWorld.Simulation.Economy.Supply;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Economy;

/// <summary>economy-depth: receita com insumo (C1, C2), compra de insumo por oficina e cidade (C7, C8) e o
/// tesouro da cidade no snapshot (C10).</summary>
public class InputSupplyTests
{
    private static readonly WorldCalendar Calendar = new(24, 30, 12);
    private static readonly ResourceType Wood = new(3);
    private static readonly ResourceType Iron = new(4);
    private static readonly CellCoord Spot = new(1, 1);

    private static readonly Personality SomePersonality =
        Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    private static EconomyRules Rules() => EconomyRules.Create(
        enabled: true, foodResourceId: 1, waterResourceId: 2,
        capacityByResourceLocation: new Dictionary<(int, int), long>(),
        spoilagePerDayByResource: new Dictionary<int, double>(),
        wageByProfession: new Dictionary<int, long>(),
        priceFloor: new Dictionary<int, long> { [3] = 1 }, priceCeiling: new Dictionary<int, long> { [3] = 50 },
        priceSensitivity: 0, demandBaselinePerNpc: new Dictionary<int, double>()).Value!;

    private static CityRules CityRulesOn() => CityRules.Create(
        enabled: true, foodShortageThreshold: 100, housingShortageThreshold: 100, securityShortageThreshold: 100,
        emigrationRatePerDeficitUnit: 0, migrationEmploymentWeight: 1, migrationFoodWeight: 1,
        migrationSecurityWeight: 1, migrationFamilyTiesWeight: 1, foundingConcentrationThreshold: 1,
        foundingResourceThreshold: 1, foundingRouteThreshold: 1, foundingDefensibilityThreshold: 1,
        foundingLeadershipThreshold: 1, organizationTicks: 10, materializationIdleTicksBeforeEligible: 5).Value!;

    /// <summary>Receita 2 (forja): 2 de madeira por trabalhador rendem 5 de ferro por trabalhador.</summary>
    private static EconomyCatalog ForgeCatalog() => new(
        new Dictionary<int, ProductionRecipe>
        {
            [2] = ProductionRecipe.Create(
                new Dictionary<int, long> { [3] = 2 }, new Dictionary<int, long> { [4] = 5 },
                requiresCellResource: null, maxWorkersPerCycle: 5).Value!,
        },
        MarketLocationTypeIds: [3], LocationTypeByProfession: new Dictionary<int, int>());

    private static CityCatalog HouseCatalog(long wood) => new(
        new Dictionary<int, BuildingRecipe>
        {
            [10] = BuildingRecipe.Create(new Dictionary<ResourceType, long> { [Wood] = wood }, 5, housingCapacityProvided: 4).Value!,
        });

    private static (WorldState World, City City) BuildWorld(CityCatalog? cityCatalog = null, long cityTreasury = 0)
    {
        var world = new WorldState(
            Calendar, 1, ScenarioRunner.DefaultMap(1), ScenarioRunner.DefaultPopulationCatalog,
            ScenarioRunner.DefaultPopulationRules, ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules, economyRules: Rules(), economyCatalog: ForgeCatalog(),
            cityRules: CityRulesOn(), cityCatalog: cityCatalog ?? CityCatalog.Empty);
        var city = new City(
            world.NextCityId(), Spot, 0, null, AggregatePopulationPool.Empty, treasury: new Money(cityTreasury));
        world.AddCity(city);
        return (world, city);
    }

    private static Workplace AddWorkplace(
        WorldState world, City city, int locationType, long wood = 0, long price = 0, long treasury = 0, int workers = 0)
    {
        var stock = new Dictionary<ResourceType, long>();
        if (wood > 0) stock[Wood] = wood;
        var prices = new Dictionary<ResourceType, long>();
        if (price > 0) prices[Wood] = price;
        var workplace = new Workplace(
            world.NextWorkplaceIdAndAdvance(), new LocationType(locationType), Spot, maxVacancies: 10,
            employees: [], stock, new Money(treasury), prices, city.Id);
        world.AddWorkplace(workplace);
        for (int i = 0; i < workers; i++)
        {
            var npc = new Npc(
                world.NextNpcIdAndAdvance(), "worker", Sex.Male, WorldDate.Epoch(Calendar).AddYears(-30), new CultureId(1),
                Spot, motherId: null, fatherId: null, household: null, health: 100,
                personality: SomePersonality, profession: new ProfessionType(1), currentLocation: Spot);
            world.AddNpc(npc);
            workplace.Hire(npc.Id);
            npc.Hire(workplace.Id);
        }
        return workplace;
    }

    private static TickContext Ctx(WorldState world) => new(world, world.Rng, world.Scheduler);

    [Fact]
    public void Cycle_consumes_exact_inputs_and_deposits_exact_outputs()
    {
        var (world, city) = BuildWorld();
        var forge = AddWorkplace(world, city, locationType: 2, wood: 10, workers: 2);

        new ProductionSystem().Tick(world, Ctx(world));

        Assert.Equal(10 - 2 * 2, forge.Stock[Wood]);
        Assert.Equal(5 * 2, forge.Stock[Iron]);
    }

    [Fact]
    public void Missing_input_produces_nothing_and_changes_nothing()
    {
        var (world, city) = BuildWorld();
        var forge = AddWorkplace(world, city, locationType: 2, wood: 0, treasury: 100, workers: 2);
        string before = WorldSnapshot.CanonicalHash(world);

        new ProductionSystem().Tick(world, Ctx(world));

        Assert.False(forge.Stock.ContainsKey(Iron) && forge.Stock[Iron] > 0);
        Assert.Equal(before, WorldSnapshot.CanonicalHash(world));
    }

    [Fact]
    public void Workplace_buys_three_days_of_inputs_from_a_neighbour_at_the_neighbour_price()
    {
        var (world, city) = BuildWorld();
        var forge = AddWorkplace(world, city, locationType: 2, treasury: 1000, workers: 2);
        var sawmill = AddWorkplace(world, city, locationType: 3, wood: 100, price: 2, treasury: 50);

        new InputSupplySystem().Tick(world, Ctx(world));

        long wanted = 2 * 2 * InputSupplySystem.TargetDays; // 2 por trabalhador x 2 trabalhadores x 3 dias
        Assert.Equal(wanted, forge.Stock[Wood]);
        Assert.Equal(100 - wanted, sawmill.Stock[Wood]);
        Assert.Equal(1000 - wanted * 2, forge.Treasury.Amount);
        Assert.Equal(50 + wanted * 2, sawmill.Treasury.Amount);
    }

    [Fact]
    public void City_buys_the_smaller_of_shortfall_and_seller_stock_at_seller_price()
    {
        var (world, city) = BuildWorld(HouseCatalog(wood: 20), cityTreasury: 1000);
        var sawmill = AddWorkplace(world, city, locationType: 3, wood: 15, price: 4, treasury: 0);

        new InputSupplySystem().Tick(world, Ctx(world));

        Assert.Equal(15, city.Stock[Wood]);              // faltavam 20, a serraria so tinha 15
        Assert.Equal(0, sawmill.Stock[Wood]);
        Assert.Equal(60, sawmill.Treasury.Amount);        // 15 x 4
        Assert.Equal(940, city.Treasury.Amount);

        string hash = WorldSnapshot.CanonicalHash(world);
        new InputSupplySystem().Tick(world, Ctx(world));  // sem estoque do outro lado: a segunda rodada nao muda nada
        Assert.Equal(hash, WorldSnapshot.CanonicalHash(world));
    }

    [Fact]
    public void Insufficient_treasury_transfers_nothing_and_keeps_the_hash()
    {
        var (world, city) = BuildWorld(HouseCatalog(wood: 20), cityTreasury: 3);
        AddWorkplace(world, city, locationType: 3, wood: 15, price: 4);
        string before = WorldSnapshot.CanonicalHash(world);

        new InputSupplySystem().Tick(world, Ctx(world));

        Assert.Equal(before, WorldSnapshot.CanonicalHash(world));
        Assert.False(city.Stock.ContainsKey(Wood));
    }

    [Fact]
    public void City_treasury_round_trips_and_old_snapshots_load_as_zero()
    {
        var (world, _) = BuildWorld(cityTreasury: 1234);
        var json = WorldSnapshot.Serialize(world);

        var restored = WorldSnapshot.Deserialize(json);
        Assert.Equal(1234, restored.Cities[0].Treasury.Amount);
        Assert.Equal(WorldSnapshot.CanonicalHash(world), WorldSnapshot.CanonicalHash(restored));

        var node = JsonNode.Parse(json)!.AsObject();
        ((JsonObject)node["Cities"]!.AsArray()[0]!).Remove("Treasury");
        var old = WorldSnapshot.Deserialize(node.ToJsonString());
        Assert.Equal(0, old.Cities[0].Treasury.Amount);
    }
}
