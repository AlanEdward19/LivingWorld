using System.Text.Json.Nodes;
using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Resolvers;
using LivingWorld.Simulation.Cities;
using LivingWorld.Simulation.Cities.Migration;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Cities;

/// <summary>lodging-and-rent: prédio cujo tipo declara <c>Lodging</c> aluga vagas; o locador é a oficina do prédio.</summary>
public class LodgingSystemTests
{
    private const int InnTypeId = 12;
    private const long Rent = 100;

    private static JsonNode DefaultRoot() =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "scenarios", "default.json")))!;

    /// <summary>Cenário padrão com uma estalagem: receita 12 com 3 vagas, aluguel 100 e carência de 1 mês, e uma oficina
    /// do tipo 12 (o locador) que o carregador erige com o prédio.</summary>
    private static WorldState WorldWithInn(JsonNode? root = null, bool declareLodging = true, Action<JsonObject>? tune = null)
    {
        root ??= DefaultRoot();
        var recipe = new JsonObject
        {
            ["Inputs"] = new JsonObject { ["3"] = 1 },
            ["TicksToBuild"] = 1,
            ["HousingCapacityProvided"] = 3,
        };
        if (declareLodging)
            recipe["Lodging"] = new JsonObject { ["RentPerMonth"] = Rent, ["GraceMonths"] = 1 };
        tune?.Invoke(recipe);
        root["BuildingRecipes"]![InnTypeId.ToString()] = recipe;
        ((JsonArray)root["Workplaces"]!).Add(new JsonObject
        {
            ["LocationTypeId"] = InnTypeId,
            ["X"] = 9,
            ["Y"] = 9,
            ["MaxVacancies"] = 2,
            ["Treasury"] = 0,
            ["Stock"] = new JsonObject(),
            ["Prices"] = new JsonObject(),
        });

        var result = ScenarioLoaderV2.LoadWorld(root.ToJsonString());
        Assert.True(result.IsSuccess, result.Error);
        return result.Value.Item1;
    }

    private static Building Inn(WorldState world) => world.Buildings.Single(b => b.BuildingTypeId == InnTypeId);

    private static Workplace Landlord(WorldState world) => world.Workplaces.Single(w => w.Building == Inn(world).Id);

    private static void Run(WorldState world) =>
        new LodgingSystem().Tick(world, new TickContext(world, world.Rng, world.Scheduler));

    private static Household Tenant(WorldState world, int index, long wallet)
    {
        var household = world.Households.OrderBy(h => h.Id.Value).ElementAt(index);
        var head = world.FindNpc(household.Head)!;
        long difference = wallet - head.Wallet.Amount; // fixa a carteira em `wallet`
        if (difference > 0) head.CreditWallet(new Money(difference));
        else if (difference < 0) head.TryDebitWallet(new Money(-difference));
        return household;
    }

    private static long TotalMoney(WorldState world) =>
        world.Npcs.Sum(n => n.Wallet.Amount) + world.Workplaces.Sum(w => w.Treasury.Amount) + world.Cities.Sum(c => c.Treasury.Amount);

    private static string RepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "LivingWorld.sln")))
            dir = Directory.GetParent(dir)?.FullName;
        return dir ?? throw new InvalidOperationException("LivingWorld.sln não encontrado");
    }

    [Fact]
    public void Loader_reads_lodging_terms_and_rejects_invalid_ones()
    {
        var world = WorldWithInn();
        var terms = world.CityCatalog.BuildingRecipes[InnTypeId].Lodging;
        Assert.Equal(new LodgingTerms(Rent, 1), terms);

        var broken = DefaultRoot();
        ((JsonArray)broken["Workplaces"]!).Add(new JsonObject());
        broken["BuildingRecipes"]!["13"] = new JsonObject
        {
            ["Inputs"] = new JsonObject(),
            ["TicksToBuild"] = 1,
            ["HousingCapacityProvided"] = 2,
            ["Lodging"] = new JsonObject { ["RentPerMonth"] = 0, ["GraceMonths"] = 0 },
        };
        Assert.False(ScenarioLoaderV2.LoadWorld(broken.ToJsonString()).IsSuccess);
    }

    [Fact]
    public void Rent_moves_money_from_the_head_to_the_landlord_without_creating_or_losing_any()
    {
        var world = WorldWithInn();
        var household = Tenant(world, 0, wallet: 500);
        household.LiveIn(Inn(world).Id);
        long before = TotalMoney(world);
        long treasuryBefore = Landlord(world).Treasury.Amount;

        Run(world);

        Assert.Equal(400, world.FindNpc(household.Head)!.Wallet.Amount);
        Assert.Equal(treasuryBefore + Rent, Landlord(world).Treasury.Amount);
        Assert.Equal(before, TotalMoney(world));
        Assert.Equal(0, household.RentArrearsMonths);
    }

    [Fact]
    public void Unpaid_rent_builds_arrears_and_eviction_comes_only_after_the_grace_months()
    {
        var world = WorldWithInn();
        var household = Tenant(world, 0, wallet: 10); // não paga
        household.LiveIn(Inn(world).Id);

        Run(world);
        Assert.Equal(1, household.RentArrearsMonths);
        Assert.Equal(Inn(world).Id, household.Dwelling); // carência de 1 mês: ainda mora
        Assert.False(household.IsHomeless);

        Run(world);
        Assert.Null(household.Dwelling);
        Assert.True(household.IsHomeless);
        Assert.Equal(10, world.FindNpc(household.Head)!.Wallet.Amount); // nada foi cobrado a menos nem a mais
    }

    [Fact]
    public void An_evicted_household_sleeps_on_the_ground_and_is_pushed_to_migrate()
    {
        var world = WorldWithInn();
        var household = Tenant(world, 0, wallet: 0);
        household.LiveIn(Inn(world).Id);
        Run(world);
        Run(world);
        var head = world.FindNpc(household.Head)!;

        var rest = RestPlaceResolver.Resolve(world, head);

        Assert.Equal(RestPlaceKind.Ground, rest.Kind);
        Assert.True(household.IsHomeless);
    }

    [Fact]
    public void A_homeless_household_is_treated_as_needing_to_migrate_and_a_housed_one_is_not()
    {
        var root = DefaultRoot();
        // Só o emprego pesa e a cidade de origem não emprega ninguém no tick 0: a outra sempre pontua melhor, então
        // o que decide se há mudança é só a necessidade (o despejo), nunca a pontuação.
        root["MigrationEmploymentWeight"] = 10.0;
        root["MigrationFoodWeight"] = 0.0;
        root["MigrationSecurityWeight"] = 0.0;
        root["MigrationFamilyTiesWeight"] = 0.0;
        var world = WorldWithInn(root);
        var home = world.ActiveCities().Single();
        var elsewhere = new City(world.NextCityId(), new LivingWorld.Domain.Geography.CellCoord(17, 17), 0, null, AggregatePopulationPool.Empty);
        world.AddCity(elsewhere);
        var evicted = Tenant(world, 0, wallet: 0);
        evicted.LiveIn(Inn(world).Id);
        Run(world);
        Run(world);
        Assert.True(evicted.IsHomeless);
        // Controle: um domicílio com casa dentro dos bounds da cidade (fora deles a regra antiga já manda migrar).
        var bounds = LivingWorld.Simulation.Cities.CityOccupancy.ResolveGrownBounds(
            world, home, LivingWorld.Simulation.Cities.Queries.CityPopulationQuery.Population(world, home.Id)).Bounds;
        var housedIndex = world.Households.OrderBy(h => h.Id.Value).Select((h, i) => (h, i))
            .First(x => x.i > 0 && x.h.Dwelling != Inn(world).Id && bounds.Contains(x.h.Location)).i;
        var housed = Tenant(world, housedIndex, wallet: 500);

        new MigrationSystem().Tick(world, new TickContext(world, world.Rng, world.Scheduler));

        Assert.Equal(elsewhere.Id, evicted.PendingRelocationCity);
        Assert.Equal(home.Id, evicted.City); // sai só na chegada
        Assert.Null(housed.PendingRelocationCity);
    }

    [Fact]
    public void A_household_without_a_roof_rents_the_cheapest_free_unit_up_to_the_capacity()
    {
        var world = WorldWithInn();
        var inn = Inn(world);
        var households = Enumerable.Range(0, 5).Select(i => Tenant(world, i, wallet: 500)).ToList();
        foreach (var household in households) household.Evict(tick: 1); // todos sem teto

        Run(world);

        var lodged = households.Where(h => h.Dwelling == inn.Id).ToList();
        Assert.Equal(3, lodged.Count); // 3 vagas
        Assert.Equal(new[] { 0, 1, 2 }.Select(i => households[i].Id), lodged.Select(h => h.Id)); // por id
        Assert.All(lodged, h => Assert.False(h.IsHomeless));
        Assert.All(lodged, h => Assert.Equal(DwellingCapacity.InteriorCellOf(inn, inn.Position!.Value), h.Location));
        Assert.Equal(2, households.Count(h => h.IsHomeless));
    }

    [Fact]
    public void A_household_that_cannot_afford_the_rent_stays_without_a_roof()
    {
        var world = WorldWithInn();
        var household = Tenant(world, 0, wallet: Rent - 1);
        household.Evict(tick: 1);

        Run(world);

        Assert.Null(household.Dwelling);
    }

    [Fact]
    public void A_scenario_without_lodging_terms_changes_nothing()
    {
        var world = WorldWithInn(declareLodging: false);
        var household = Tenant(world, 0, wallet: 500);
        household.LiveIn(Inn(world).Id);
        long money = TotalMoney(world);

        Run(world);

        Assert.Equal(money, TotalMoney(world));
        Assert.Equal(Inn(world).Id, household.Dwelling);
        Assert.Equal(0, household.RentArrearsMonths);
    }

    [Fact]
    public void Same_seed_gives_the_same_outcome()
    {
        string Outcome()
        {
            var world = WorldWithInn();
            for (int i = 0; i < 6; i++) Tenant(world, i, wallet: i * 60);
            foreach (var household in world.Households.OrderBy(h => h.Id.Value).Take(6)) household.Evict(tick: 1);
            for (int month = 0; month < 3; month++) Run(world);
            return string.Join(",", world.Households.OrderBy(h => h.Id.Value).Select(h => $"{h.Id.Value}:{h.Dwelling?.Value}:{h.RentArrearsMonths}:{h.EvictedAtTick}"))
                + "|" + Landlord(world).Treasury.Amount;
        }

        Assert.Equal(Outcome(), Outcome());
    }

    [Fact]
    public void The_seeder_uses_the_recipe_capacity_for_shared_dwellings()
    {
        var world = WorldWithInn();
        var inn = Inn(world);

        Assert.Equal(3, DwellingCapacity.Of(world, inn));
        var house = world.Buildings.First(b => b.BuildingTypeId == -1);
        Assert.Equal(BuildingLayoutGenerator.FloorsOf(house.Id, house.BuildingTypeId), DwellingCapacity.Of(world, house));
    }

    // --- preço dinâmico ---

    private static void WithPricing(JsonObject recipe, double centrality = 0, double amenity = 0, double demand = 0, double min = 0.5, double max = 3.0)
    {
        recipe["Pricing"] = new JsonObject
        {
            ["BaseValue"] = 10_000,
            ["RentYieldPerMonth"] = 0.01,
            ["CentralityWeight"] = centrality,
            ["AmenityWeight"] = amenity,
            ["AmenityRadius"] = 30,
            ["WaterfrontWeight"] = 0.0,
            ["DemandSensitivity"] = demand,
            ["MinMultiplier"] = min,
            ["MaxMultiplier"] = max,
        };
    }

    private static (ValuationContext Context, BuildingRecipe Recipe) Valuation(WorldState world) =>
        (new ValuationContext(world, world.FindCity(Inn(world).City)!), world.CityCatalog.BuildingRecipes[InnTypeId]);

    [Fact]
    public void Rent_follows_the_property_value_when_the_type_declares_pricing()
    {
        var world = WorldWithInn(tune: recipe => WithPricing(recipe, min: 1.0, max: 1.0)); // multiplicador travado em 1
        var (context, recipe) = Valuation(world);

        Assert.Equal(10_000, BuildingValuation.ValueOf(context, Inn(world), recipe));
        Assert.Equal(100, BuildingValuation.RentOf(context, Inn(world), recipe)); // 10000 x 0,01
    }

    [Fact]
    public void Rent_rises_with_demand_and_stays_inside_the_scenario_limits()
    {
        var world = WorldWithInn(tune: recipe => WithPricing(recipe, demand: 1.0, min: 0.5, max: 1.5));
        var households = world.Households.OrderBy(h => h.Id.Value).Take(12).ToList();
        foreach (var household in households) household.Evict(tick: 1); // todos precisam de teto
        var (context, recipe) = Valuation(world);

        long crowded = BuildingValuation.RentOf(context, Inn(world), recipe);
        foreach (var household in households.Take(3)) household.MoveIn(Inn(world).Id, Inn(world).Position!.Value); // 3 de 3 vagas, 9 ainda sem teto
        long still = BuildingValuation.RentOf(new ValuationContext(world, world.FindCity(Inn(world).City)!), Inn(world), recipe);

        Assert.Equal(150, crowded); // pressão alta: teto do cenário (1,5 x 100)
        Assert.Equal(150, still);

        var calm = WorldWithInn(tune: r => WithPricing(r, demand: 1.0, min: 0.5, max: 1.5));
        foreach (var household in calm.Households.Where(h => h.Dwelling is null).ToList()) household.LiveIn(Inn(calm).Id);
        var (calmContext, calmRecipe) = Valuation(calm);
        Assert.Equal(50, BuildingValuation.RentOf(calmContext, Inn(calm), calmRecipe)); // ninguém precisa: piso do cenário (0,5 x 100)
    }

    [Fact]
    public void A_central_building_costs_more_than_one_at_the_edge_and_comerce_nearby_adds_value()
    {
        var plain = WorldWithInn(tune: recipe => WithPricing(recipe, min: 0.1, max: 5.0));
        var central = WorldWithInn(tune: recipe => WithPricing(recipe, centrality: 1.0, min: 0.1, max: 5.0));
        var busy = WorldWithInn(tune: recipe => WithPricing(recipe, amenity: 1.0, min: 0.1, max: 5.0));

        long ValueIn(WorldState world)
        {
            var (context, recipe) = Valuation(world);
            return BuildingValuation.ValueOf(context, Inn(world), recipe);
        }

        Assert.True(ValueIn(central) >= ValueIn(plain));
        Assert.True(ValueIn(busy) > ValueIn(plain)); // as outras oficinas do cenário ficam dentro do raio
    }

    [Fact]
    public void Establishments_have_a_dynamic_value_too_but_no_rent()
    {
        var root = DefaultRoot();
        root["BuildingRecipes"]!["13"] = new JsonObject
        {
            ["Inputs"] = new JsonObject(),
            ["TicksToBuild"] = 1,
            ["HousingCapacityProvided"] = 0,
            ["Pricing"] = new JsonObject { ["BaseValue"] = 5_000, ["DemandSensitivity"] = 1.0, ["MinMultiplier"] = 0.5, ["MaxMultiplier"] = 2.0 },
        };
        ((JsonArray)root["Workplaces"]!).Add(new JsonObject
        {
            ["LocationTypeId"] = 13,
            ["X"] = 12,
            ["Y"] = 12,
            ["MaxVacancies"] = 4,
            ["Treasury"] = 0,
            ["Stock"] = new JsonObject(),
            ["Prices"] = new JsonObject(),
        });
        var world = ScenarioLoaderV2.LoadWorld(root.ToJsonString()).Value.Item1;
        var shop = world.Buildings.Single(b => b.BuildingTypeId == 13);
        var recipe = world.CityCatalog.BuildingRecipes[13];
        var context = new ValuationContext(world, world.FindCity(shop.City)!);

        long value = BuildingValuation.ValueOf(context, shop, recipe);

        Assert.InRange(value, 2_500, 10_000);
        Assert.Equal(0, BuildingValuation.RentOf(context, shop, recipe));
        Assert.Equal(0, BuildingValuation.ValueOf(context, world.Buildings.First(b => b.BuildingTypeId == -1), world.CityCatalog.BuildingRecipes.GetValueOrDefault(-1) ?? recipe with { Pricing = null }));
    }

    [Fact]
    public void Dynamic_rent_is_what_the_tenant_actually_pays()
    {
        var world = WorldWithInn(tune: recipe => WithPricing(recipe, min: 2.0, max: 2.0)); // x2 fixo: aluguel 200
        var household = Tenant(world, 0, wallet: 1_000);
        household.LiveIn(Inn(world).Id);
        long treasury = Landlord(world).Treasury.Amount;

        Run(world);

        Assert.Equal(800, world.FindNpc(household.Head)!.Wallet.Amount);
        Assert.Equal(treasury + 200, Landlord(world).Treasury.Amount);
    }

    [Fact]
    public void Loader_rejects_inconsistent_pricing()
    {
        var root = DefaultRoot();
        root["BuildingRecipes"]!["13"] = new JsonObject
        {
            ["Inputs"] = new JsonObject(),
            ["TicksToBuild"] = 1,
            ["HousingCapacityProvided"] = 0,
            ["Pricing"] = new JsonObject { ["BaseValue"] = 100, ["MinMultiplier"] = 2.0, ["MaxMultiplier"] = 1.0 },
        };
        Assert.False(ScenarioLoaderV2.LoadWorld(root.ToJsonString()).IsSuccess);
    }

    // --- hóspede de diária ---

    private static void WithNightly(JsonObject recipe, long rate = 40, int minDistance = 3) =>
        recipe["Lodging"]!.AsObject().Also(l => { l["NightlyRate"] = rate; l["GuestMinDistance"] = minDistance; });

    private static Npc Traveller(WorldState world, long wallet)
    {
        var household = Tenant(world, 0, wallet);
        var npc = world.FindNpc(household.Head)!;
        var inn = Inn(world);
        npc.MoveTo(new CellCoord(inn.Position!.Value.X, inn.Position.Value.Y + 1), tick: 0); // colado na hospedagem, longe de casa
        return npc;
    }

    [Fact]
    public void A_traveller_far_from_home_sleeps_in_the_inn_and_pays_the_night_to_the_landlord()
    {
        var world = WorldWithInn(tune: recipe => WithNightly(recipe));
        var npc = Traveller(world, wallet: 500);
        var home = world.FindHousehold(npc.Household!.Value)!.Location;
        Assert.True(Math.Max(Math.Abs(npc.CurrentLocation.X - home.X), Math.Abs(npc.CurrentLocation.Y - home.Y)) > 3);

        var rest = RestPlaceResolver.Resolve(world, npc);

        Assert.Equal(RestPlaceKind.Lodging, rest.Kind);
        Assert.Equal(Inn(world).Id.Value, rest.TargetId);
        var room = GuestLodging.FindRoom(world, npc, home)!.Value;
        long before = TotalMoney(world);
        long treasury = Landlord(world).Treasury.Amount;
        Assert.True(GuestLodging.TryCharge(npc, room));
        Assert.Equal(40, Landlord(world).Treasury.Amount - treasury);
        Assert.Equal(before, TotalMoney(world));
    }

    [Fact]
    public void A_traveller_near_home_or_too_poor_or_with_no_free_room_does_not_take_a_room()
    {
        var world = WorldWithInn(tune: recipe => WithNightly(recipe));
        var poor = Traveller(world, wallet: 39);
        Assert.NotEqual(RestPlaceKind.Lodging, RestPlaceResolver.Resolve(world, poor).Kind);

        var rich = world.FindNpc(world.Households.OrderBy(h => h.Id.Value).ElementAt(1).Head)!;
        rich.CreditWallet(new Money(500));
        rich.MoveTo(world.FindHousehold(rich.Household!.Value)!.Location, tick: 0); // em casa
        Assert.NotEqual(RestPlaceKind.Lodging, RestPlaceResolver.Resolve(world, rich).Kind);

        var full = WorldWithInn(tune: recipe => WithNightly(recipe));
        foreach (var household in full.Households.OrderBy(h => h.Id.Value).Skip(1).Take(3)) household.LiveIn(Inn(full).Id); // 3 vagas tomadas
        var late = Traveller(full, wallet: 500);
        Assert.NotEqual(RestPlaceKind.Lodging, RestPlaceResolver.Resolve(full, late).Kind);
    }

    [Fact]
    public void A_scenario_without_nightly_rate_never_offers_a_room()
    {
        var world = WorldWithInn();
        var npc = Traveller(world, wallet: 500);

        Assert.NotEqual(RestPlaceKind.Lodging, RestPlaceResolver.Resolve(world, npc).Kind);
    }
}

internal static class JsonObjectExtensions
{
    public static T Also<T>(this T value, Action<T> action)
    {
        action(value);
        return value;
    }
}
