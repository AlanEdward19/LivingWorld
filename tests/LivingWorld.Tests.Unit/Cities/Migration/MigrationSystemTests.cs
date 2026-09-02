using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Geography.Map;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Decision;
using LivingWorld.Simulation.Behavior.Needs;
using LivingWorld.Simulation.Cities.Migration;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Cities.Migration;

/// <summary>Fase 8, T12 (CITY-07): <see cref="MigrationSystem"/> — household materializado migra
/// pesando emprego/comida/segurança/laços familiares (CityRules), movendo todo mundo pro destino
/// no mesmo tick e preservando <see cref="HouseholdId"/>.</summary>
public class MigrationSystemTests
{
    private static readonly ResourceType Food = new(1);

    private static CityRules MakeRules(
        double employmentWeight = 0, double foodWeight = 1, double securityWeight = 0, double familyTiesWeight = 0) =>
        CityRules.Create(
            enabled: true, foodShortageThreshold: 20, housingShortageThreshold: 20, securityShortageThreshold: 20,
            emigrationRatePerDeficitUnit: 0.1, employmentWeight, foodWeight, securityWeight, familyTiesWeight,
            foundingConcentrationThreshold: 0.5, foundingResourceThreshold: 0.5, foundingRouteThreshold: 0.5,
            foundingDefensibilityThreshold: 0.5, foundingLeadershipThreshold: 0.5,
            organizationTicks: 10, materializationIdleTicksBeforeEligible: 5).Value!;

    private static WorldState MakeWorld(CityRules rules)
    {
        var economyRules = EconomyRules.Create(
            enabled: false, foodResourceId: 1, waterResourceId: 2,
            capacityByResourceLocation: new Dictionary<(int, int), long>(),
            spoilagePerDayByResource: new Dictionary<int, double>(),
            wageByProfession: new Dictionary<int, long>(),
            priceFloor: new Dictionary<int, long>(), priceCeiling: new Dictionary<int, long>(),
            priceSensitivity: 0.1, demandBaselinePerNpc: new Dictionary<int, double>()).Value!;

        return new WorldState(
            ScenarioRunner.DefaultCalendar, seed: 17, ScenarioRunner.DefaultMap(17),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog, ScenarioRunner.DefaultLifeStageRules,
            economyRules: economyRules, cityRules: rules);
    }

    private static TickContext MakeCtx(WorldState world) => new(world, world.Rng, world.Scheduler);

    // dynamic-city-growth, T5: mesma sonda de "casa" usada por MigrationSystem
    // (BuildingId(1)/buildingTypeId 1) — reproduzida aqui só pra dimensionar o mapa de teste
    // exatamente igual à bounding box do footprint, garantindo escassez real e determinística.
    private static readonly IReadOnlyList<CellCoord> ScarcityProbeShape =
        BuildingFootprintGenerator.Generate(new BuildingId(1), buildingTypeId: 1).Select(c => c.Cell).ToList();

    private static readonly GeographyCatalog TinyCatalog = new(
        TerrainIds: new HashSet<int> { 1 }, BiomeIds: new HashSet<int> { 1 }, ResourceIds: new HashSet<int>());

    private static readonly CostWeights TinyCostWeights = new(
        Base: 1.0, AltitudeWeight: 0.5, TerrainWeight: new Dictionary<int, double> { [1] = 1.0 });

    /// <summary>Mundo com mapa minúsculo (não o <see cref="ScenarioRunner.DefaultMap"/> 10x10,
    /// grande demais pra ficar escasso sem centenas de prédios) — <paramref name="width"/>/<paramref
    /// name="height"/> controlam se o mapa fica totalmente ocupado (== bounding box do footprint)
    /// ou com margem livre (maior que a bounding box).</summary>
    private static WorldState MakeWorldWithTinyMap(CityRules rules, int width, int height, ulong seed)
    {
        var economyRules = EconomyRules.Create(
            enabled: false, foodResourceId: 1, waterResourceId: 2,
            capacityByResourceLocation: new Dictionary<(int, int), long>(),
            spoilagePerDayByResource: new Dictionary<int, double>(),
            wageByProfession: new Dictionary<int, long>(),
            priceFloor: new Dictionary<int, long>(), priceCeiling: new Dictionary<int, long>(),
            priceSensitivity: 0.1, demandBaselinePerNpc: new Dictionary<int, double>()).Value!;
        var map = MapGenerator.Generate(seed, width, height, Math.Max(width, height), TinyCatalog, TinyCostWeights, [])
            .Value ?? throw new InvalidOperationException("mapa de teste inválido — bug no teste, não no gerador");

        return new WorldState(
            ScenarioRunner.DefaultCalendar, seed, map,
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog, ScenarioRunner.DefaultLifeStageRules,
            economyRules: economyRules, cityRules: rules);
    }

    private static void CompletePendingMigrations(WorldState world, int maxHours = 300)
    {
        var clock = new WorldClock([new BehaviorDecisionSystem(), new RelocationArrivalSystem()]);
        var ctx = MakeCtx(world);
        for (int i = 0; i < maxHours; i++)
        {
            clock.Tick(world);
            foreach (var npc in world.Npcs.Where(n => n.IsAlive))
                NpcWakeScheduler.ScheduleWake(world, ctx, npc.Id.Value, world.CurrentDate.TotalHours + 1);
            if (world.Households.All(h => h.PendingRelocationCity is null))
                return;
        }
    }

    private static (City Origin, City Destination) MakeTwoCities(WorldState world)
    {
        var origin = new City(world.NextCityId(), new CellCoord(0, 0), 0, null, AggregatePopulationPool.Empty);
        var destination = new City(world.NextCityId(), new CellCoord(1, 1), 0, null, AggregatePopulationPool.Empty);
        world.AddCity(origin);
        world.AddCity(destination);
        return (origin, destination);
    }

    private static readonly Personality NeutralPersonality =
        Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    /// <summary>Npc "cru" (sem passar por PopulationSeeder — que casa adultos automaticamente em
    /// households próprios e poluiria a lista que <see cref="MigrationSystem"/> itera).</summary>
    private static Npc MakeNpc(WorldState world, long id, CityId city, CellCoord? location = null)
    {
        var resolved = location ?? world.FindCity(city)?.Location ?? ScenarioRunner.DefaultVillageLocation;
        return new(
            new NpcId(id), $"npc-{id}", Sex.Male, world.CurrentDate.AddYears(-30), ScenarioRunner.DefaultCulture,
            resolved, motherId: null, fatherId: null, household: null, health: 80,
            personality: NeutralPersonality, profession: ProfessionType.None, currentLocation: resolved,
            city: city);
    }

    /// <summary>Duas populações separadas, uma por cidade — a origem é quem o teste observa; a
    /// do destino só existe pra dar ao candidato um estoque/nível real (nunca o mesmo Npc nas
    /// duas listas de membros).</summary>
    private static (Npc OriginHead, Household OriginHousehold) SeedTwoOneNpcHouseholds(
        WorldState world, CityId originCityId, CityId destinationCityId, long originFood, long destinationFood)
    {
        var originHead = MakeNpc(world, 1, originCityId);
        var destinationHead = MakeNpc(world, 2, destinationCityId);
        world.AddNpc(originHead);
        world.AddNpc(destinationHead);

        var originLocation = world.FindCity(originCityId)!.Location;
        var originHousehold = new Household(new HouseholdId(1), originLocation, originHead.Id, [originHead.Id], city: originCityId);
        originHead.JoinHousehold(originHousehold.Id);
        originHousehold.Deposit(Food, originFood);
        world.AddHousehold(originHousehold);

        var destinationHousehold = new Household(new HouseholdId(2), world.FindCity(destinationCityId)!.Location, destinationHead.Id, [destinationHead.Id], city: destinationCityId);
        destinationHousehold.Deposit(Food, destinationFood);
        world.AddHousehold(destinationHousehold);

        return (originHead, originHousehold);
    }

    [Fact]
    public void Household_migrates_to_the_city_with_strictly_better_food_when_only_food_is_weighted()
    {
        var world = MakeWorld(MakeRules(foodWeight: 1));
        var (origin, destination) = MakeTwoCities(world);
        var (npc, household) = SeedTwoOneNpcHouseholds(world, origin.Id, destination.Id, originFood: 0, destinationFood: 1000);

        new MigrationSystem().Tick(world, MakeCtx(world));
        CompletePendingMigrations(world);

        Assert.Equal(destination.Id, npc.City);
        Assert.Equal(destination.Id, household.City);
    }

    [Fact]
    public void Migrating_npc_never_ends_up_with_no_city_it_moves_directly_to_the_destination()
    {
        var world = MakeWorld(MakeRules(foodWeight: 1));
        var (origin, destination) = MakeTwoCities(world);
        var (npc, _) = SeedTwoOneNpcHouseholds(world, origin.Id, destination.Id, originFood: 0, destinationFood: 1000);

        new MigrationSystem().Tick(world, MakeCtx(world));

        Assert.NotEqual(default, npc.City);
        Assert.Equal(origin.Id, npc.City);
        CompletePendingMigrations(world);
        Assert.Equal(destination.Id, npc.City);
    }

    [Fact]
    public void Household_id_is_preserved_across_migration()
    {
        var world = MakeWorld(MakeRules(foodWeight: 1));
        var (origin, destination) = MakeTwoCities(world);
        var (_, household) = SeedTwoOneNpcHouseholds(world, origin.Id, destination.Id, originFood: 0, destinationFood: 1000);

        new MigrationSystem().Tick(world, MakeCtx(world));

        Assert.Equal(new HouseholdId(1), household.Id);
        Assert.Same(household, world.FindHousehold(new HouseholdId(1)));
    }

    [Fact]
    public void Household_does_not_migrate_when_the_current_city_already_scores_highest()
    {
        var world = MakeWorld(MakeRules(foodWeight: 1));
        var (origin, destination) = MakeTwoCities(world);
        var (npc, household) = SeedTwoOneNpcHouseholds(world, origin.Id, destination.Id, originFood: 1000, destinationFood: 0);

        new MigrationSystem().Tick(world, MakeCtx(world));

        Assert.Equal(origin.Id, npc.City);
        Assert.Equal(origin.Id, household.City);
    }

    [Fact]
    public void Family_ties_can_keep_a_household_in_a_worse_city_when_weighted_heavily()
    {
        // Comida melhor no destino, mas o cônjuge mora na origem — laço familiar pesa mais.
        var world = MakeWorld(MakeRules(foodWeight: 1, familyTiesWeight: 100));
        var (origin, destination) = MakeTwoCities(world);
        var head = MakeNpc(world, 1, origin.Id);
        var spouseInOrigin = MakeNpc(world, 2, origin.Id);
        var unrelatedInDestination = MakeNpc(world, 3, destination.Id);
        head.Marry(spouseInOrigin.Id);
        world.AddNpc(head);
        world.AddNpc(spouseInOrigin);
        world.AddNpc(unrelatedInDestination);

        var household = new Household(new HouseholdId(1), origin.Location, head.Id, [head.Id], city: origin.Id);
        household.Deposit(Food, 0); // origem sem comida
        world.AddHousehold(household);

        var destinationHousehold = new Household(new HouseholdId(2), destination.Location, unrelatedInDestination.Id, [unrelatedInDestination.Id], city: destination.Id);
        destinationHousehold.Deposit(Food, 1000); // destino farto, mas sem ninguém da família
        world.AddHousehold(destinationHousehold);

        new MigrationSystem().Tick(world, MakeCtx(world));

        Assert.Equal(origin.Id, head.City); // ficou por causa do laço, apesar da comida pior
    }

    [Fact]
    public void Household_migration_moves_every_member_not_just_the_head()
    {
        // Fase 8, fix round 1, gap 3 (CITY-07 AC2): todo teste anterior usava household de 1
        // membro — nenhum provava que um membro não-chefe também migra junto. Household de 2
        // membros aqui: se só o chefe migrasse, este teste reprovaria.
        var world = MakeWorld(MakeRules(foodWeight: 1));
        var (origin, destination) = MakeTwoCities(world);
        var head = MakeNpc(world, 1, origin.Id);
        var otherMember = MakeNpc(world, 2, origin.Id);
        var destinationHead = MakeNpc(world, 3, destination.Id);
        world.AddNpc(head);
        world.AddNpc(otherMember);
        world.AddNpc(destinationHead);

        var household = new Household(new HouseholdId(1), origin.Location, head.Id, [head.Id, otherMember.Id], city: origin.Id);
        head.JoinHousehold(household.Id);
        otherMember.JoinHousehold(household.Id);
        household.Deposit(Food, 0); // origem sem comida
        world.AddHousehold(household);

        var destinationHousehold = new Household(new HouseholdId(2), destination.Location, destinationHead.Id, [destinationHead.Id], city: destination.Id);
        destinationHousehold.Deposit(Food, 1000); // destino farto
        world.AddHousehold(destinationHousehold);

        new MigrationSystem().Tick(world, MakeCtx(world));
        CompletePendingMigrations(world);

        Assert.Equal(destination.Id, head.City);
        Assert.Equal(destination.Id, otherMember.City); // membro não-chefe migrou junto
        Assert.Equal(destination.Id, household.City);
    }

    [Fact]
    public void Tick_is_a_no_op_when_city_rules_are_disabled()
    {
        var world = MakeWorld(CityRules.Disabled);
        var (origin, destination) = MakeTwoCities(world);
        var (npc, _) = SeedTwoOneNpcHouseholds(world, origin.Id, destination.Id, originFood: 0, destinationFood: 1000);

        new MigrationSystem().Tick(world, MakeCtx(world));

        Assert.Equal(origin.Id, npc.City);
    }

    // --- T5 (CITYGROW, Edge Case "mapa sem célula livre em lugar nenhum") ---

    [Fact]
    public void Household_relocates_out_of_a_land_scarce_city_even_though_it_would_normally_score_best_on_food()
    {
        int w = ScarcityProbeShape.Max(c => c.X) + 1, h = ScarcityProbeShape.Max(c => c.Y) + 1;
        var world = MakeWorldWithTinyMap(MakeRules(foodWeight: 1), width: w, height: h, seed: 701);
        var (origin, destination) = MakeTwoCities(world);
        // Prédio idêntico à sonda de escassez (mesmo BuildingId/typeId), ocupando o mapa inteiro
        // (== bounding box do footprint): CityOccupancy.IsLandScarce vira true pro mundo inteiro.
        world.AddBuilding(new Building(
            new BuildingId(1), origin.Id, buildingTypeId: 1, completedAtTick: 0,
            position: new CellCoord(0, 0), orientation: 0));
        // Comida melhor na origem (destino sem comida): sem a escassez, a origem venceria o score.
        var (npc, household) = SeedTwoOneNpcHouseholds(world, origin.Id, destination.Id, originFood: 1000, destinationFood: 0);

        new MigrationSystem().Tick(world, MakeCtx(world));

        // O score de "ficar" na origem foi forçado ao mínimo teórico -> destino (score normal,
        // não escasso) vence mesmo com comida pior, provando que a escassez de terra decide.
        Assert.Equal(destination.Id, household.PendingRelocationCity);
        CompletePendingMigrations(world);
        Assert.Equal(destination.Id, npc.City);
    }

    [Fact]
    public void Land_scarce_single_city_world_does_not_crash_or_force_relocation_with_no_candidate()
    {
        int w = ScarcityProbeShape.Max(c => c.X) + 1, h = ScarcityProbeShape.Max(c => c.Y) + 1;
        var world = MakeWorldWithTinyMap(MakeRules(foodWeight: 1), width: w, height: h, seed: 702);
        var origin = new City(world.NextCityId(), new CellCoord(0, 0), 0, null, AggregatePopulationPool.Empty);
        world.AddCity(origin);
        world.AddBuilding(new Building(
            new BuildingId(1), origin.Id, buildingTypeId: 1, completedAtTick: 0,
            position: new CellCoord(0, 0), orientation: 0));
        var npc = MakeNpc(world, 1, origin.Id);
        world.AddNpc(npc);
        var household = new Household(new HouseholdId(1), origin.Location, npc.Id, [npc.Id], city: origin.Id);
        npc.JoinHousehold(household.Id);
        world.AddHousehold(household);

        new MigrationSystem().Tick(world, MakeCtx(world)); // world.Cities.Count < 2 -> guard existente no-opa

        Assert.Equal(origin.Id, npc.City);
        Assert.Null(household.PendingRelocationCity);
    }

    [Fact]
    public void Household_stays_when_its_city_is_not_land_scarce_even_with_a_building_present()
    {
        // Mapa 10x10 default (bem maior que a bounding box de um único prédio) -> sempre há
        // célula livre em algum lugar do mapa -> CityOccupancy.IsLandScarce falso.
        var world = MakeWorld(MakeRules(foodWeight: 1));
        var (origin, destination) = MakeTwoCities(world);
        world.AddBuilding(new Building(
            new BuildingId(1), origin.Id, buildingTypeId: 1, completedAtTick: 0,
            position: new CellCoord(0, 0), orientation: 0));
        var (npc, household) = SeedTwoOneNpcHouseholds(world, origin.Id, destination.Id, originFood: 1000, destinationFood: 0);

        new MigrationSystem().Tick(world, MakeCtx(world));

        Assert.Equal(origin.Id, npc.City);
        Assert.Null(household.PendingRelocationCity);
    }

    // --- post-ship fix (user-reported, 2026-08-23): migration hysteresis ---

    /// <summary>Sobe a população materializada de uma cidade pra <paramref name="count"/>
    /// (dummies sem household, só pra CityPopulationQuery.Population contar) -- necessário pra
    /// dar granularidade fracionária ao FoodLevel (comida/população), já que com população 1 o
    /// nível satura em 0 ou 1 sem meio-termo.</summary>
    private static void AddFillerPopulation(WorldState world, CityId city, int count, ref long nextNpcId)
    {
        for (int i = 0; i < count; i++)
        {
            var filler = MakeNpc(world, nextNpcId++, city);
            world.AddNpc(filler);
        }
    }

    [Fact]
    public void Employed_housed_and_fed_household_does_not_migrate_even_when_another_city_scores_much_higher()
    {
        var world = MakeWorld(MakeRules(foodWeight: 1));
        var (origin, destination) = MakeTwoCities(world);
        var (head, household) = SeedTwoOneNpcHouseholds(
            world, origin.Id, destination.Id, originFood: 1, destinationFood: 1000);
        head.Hire(new WorkplaceId(1));
        long nextId = 100;
        AddFillerPopulation(world, origin.Id, 19, ref nextId);

        new MigrationSystem().Tick(world, MakeCtx(world));

        Assert.Equal(origin.Id, head.City);
        Assert.Null(household.PendingRelocationCity);
    }

    [Fact]
    public void Employed_housed_and_fed_household_does_not_migrate_while_temporarily_outside_its_city()
    {
        var world = MakeWorld(MakeRules(foodWeight: 1));
        var (origin, destination) = MakeTwoCities(world);
        var (head, household) = SeedTwoOneNpcHouseholds(
            world, origin.Id, destination.Id, originFood: 1, destinationFood: 1000);
        head.Hire(new WorkplaceId(1));
        head.MoveTo(new CellCoord(9, 9), world.CurrentDate.TotalHours);
        long nextId = 100;
        AddFillerPopulation(world, origin.Id, 19, ref nextId);

        new MigrationSystem().Tick(world, MakeCtx(world));

        Assert.Equal(origin.Id, head.City);
        Assert.Null(household.PendingRelocationCity);
    }

    [Fact]
    public void Household_already_relocating_is_not_retargeted_on_a_later_migration_tick()
    {
        var world = MakeWorld(MakeRules(foodWeight: 1));
        var (origin, destination) = MakeTwoCities(world);
        var third = new City(world.NextCityId(), new CellCoord(2, 2), 0, null, AggregatePopulationPool.Empty);
        world.AddCity(third);
        var (_, household) = SeedTwoOneNpcHouseholds(
            world, origin.Id, destination.Id, originFood: 0, destinationFood: 1);
        household.BeginRelocation(destination.Id);
        long nextId = 100;
        AddFillerPopulation(world, destination.Id, 9, ref nextId); // destino pendente: FoodLevel 1/10
        var thirdHead = MakeNpc(world, 3, third.Id);
        world.AddNpc(thirdHead);
        var thirdHousehold = new Household(new HouseholdId(3), third.Location, thirdHead.Id, [thirdHead.Id], city: third.Id);
        thirdHousehold.Deposit(Food, 1000);
        world.AddHousehold(thirdHousehold);

        new MigrationSystem().Tick(world, MakeCtx(world));

        Assert.Equal(destination.Id, household.PendingRelocationCity);
    }

    [Fact]
    public void Household_that_arrives_in_a_fed_city_does_not_return_to_the_now_empty_origin()
    {
        var world = MakeWorld(MakeRules(foodWeight: 1));
        var (origin, destination) = MakeTwoCities(world);
        var (head, household) = SeedTwoOneNpcHouseholds(
            world, origin.Id, destination.Id, originFood: 0, destinationFood: 1000);

        new MigrationSystem().Tick(world, MakeCtx(world));
        CompletePendingMigrations(world);
        Assert.Equal(destination.Id, head.City);
        Assert.Equal(destination.Location, household.Location);

        for (int day = 0; day < 10; day++)
        {
            new MigrationSystem().Tick(world, MakeCtx(world));
            CompletePendingMigrations(world);
        }

        Assert.Equal(destination.Id, head.City);
        Assert.Equal(destination.Id, household.City);
        Assert.Null(household.PendingRelocationCity);
    }

    [Fact]
    public void Arrival_updates_residence_and_preserves_employment_in_another_city()
    {
        var world = MakeWorld(MakeRules(foodWeight: 1));
        var origin = new City(world.NextCityId(), new CellCoord(0, 0), 0, null, AggregatePopulationPool.Empty);
        var destination = new City(world.NextCityId(), new CellCoord(8, 8), 0, null, AggregatePopulationPool.Empty);
        world.AddCity(origin);
        world.AddCity(destination);
        var (head, household) = SeedTwoOneNpcHouseholds(
            world, origin.Id, destination.Id, originFood: 0, destinationFood: 1000);
        var workplace = new Workplace(
            new WorkplaceId(1), new LocationType(1), origin.Location, 1, [head.Id],
            new Dictionary<ResourceType, long>(), Money.Zero, new Dictionary<ResourceType, long>(), origin.Id);
        world.AddWorkplace(workplace);
        head.Hire(workplace.Id);

        new MigrationSystem().Tick(world, MakeCtx(world));
        CompletePendingMigrations(world);

        Assert.Equal(destination.Id, head.City);
        Assert.Equal(destination.Id, household.City);
        Assert.Equal(destination.Location, household.Location);
        Assert.Equal(workplace.Id, head.Employer);
        Assert.Contains(head.Id, workplace.Employees);

        // O trabalho intermunicipal pode levá-lo fisicamente à origem, mas avaliações diárias
        // não confundem esse commute com uma nova mudança de residência.
        var clock = new WorldClock([new BehaviorDecisionSystem(), new RelocationArrivalSystem()]);
        var ctx = MakeCtx(world);
        for (int hour = 0; hour < 5 * 24; hour++)
        {
            NpcWakeScheduler.ScheduleWake(world, ctx, head.Id.Value, world.CurrentDate.TotalHours + 1);
            clock.Tick(world);
            if ((hour + 1) % 24 == 0)
                new MigrationSystem().Tick(world, MakeCtx(world));
        }

        Assert.Equal(destination.Id, head.City);
        Assert.Null(household.PendingRelocationCity);
        Assert.Equal(destination.Location, household.Location);
        Assert.Equal(workplace.Id, head.Employer);
    }

    [Fact]
    public void Arrival_preserves_an_unscoped_authored_job_in_another_city()
    {
        var world = MakeWorld(MakeRules());
        var (origin, destination) = MakeTwoCities(world);
        var head = MakeNpc(world, 1, origin.Id, destination.Location);
        world.AddNpc(head);
        var household = new Household(new HouseholdId(1), origin.Location, head.Id, [head.Id], city: origin.Id);
        household.BeginRelocation(destination.Id);
        world.AddHousehold(household);
        var authoredWorkplace = new Workplace(
            new WorkplaceId(1), new LocationType(1), origin.Location, 1, [head.Id],
            new Dictionary<ResourceType, long>(), Money.Zero, new Dictionary<ResourceType, long>());
        world.AddWorkplace(authoredWorkplace);
        head.Hire(authoredWorkplace.Id);

        new RelocationArrivalSystem().Tick(world, MakeCtx(world));

        Assert.Equal(destination.Id, head.City);
        Assert.Equal(destination.Location, household.Location);
        Assert.Equal(authoredWorkplace.Id, head.Employer);
        Assert.Contains(head.Id, authoredWorkplace.Employees);
    }

    [Fact]
    public void Household_does_not_migrate_for_a_marginal_score_improvement_within_the_hysteresis_margin()
    {
        // foodWeight=1 -> score == FoodLevel == min(1, food/population). População 20 em cada
        // cidade dá granularidade de 0.05 por unidade de comida. Origem: 10/20 = 0.5. Destino:
        // 11/20 = 0.55 -> 10% de melhora, dentro da margem de 15% (HysteresisMargin) -> não migra.
        var world = MakeWorld(MakeRules(foodWeight: 1));
        var (origin, destination) = MakeTwoCities(world);
        var (npc, household) = SeedTwoOneNpcHouseholds(world, origin.Id, destination.Id, originFood: 10, destinationFood: 11);
        long nextId = 100;
        AddFillerPopulation(world, origin.Id, 19, ref nextId); // +1 do head da própria household = 20
        AddFillerPopulation(world, destination.Id, 19, ref nextId);

        new MigrationSystem().Tick(world, MakeCtx(world));

        Assert.Equal(origin.Id, npc.City);
        Assert.Null(household.PendingRelocationCity);
    }

    [Fact]
    public void Household_still_migrates_when_the_score_gap_is_substantially_beyond_the_hysteresis_margin()
    {
        // Destino com comida farta: 20/20 = 1.0 contra 0/20 na origem. Há necessidade real e a
        // diferença está muito além dos 15% de margem, então a migração continua permitida.
        var world = MakeWorld(MakeRules(foodWeight: 1));
        var (origin, destination) = MakeTwoCities(world);
        var (npc, household) = SeedTwoOneNpcHouseholds(world, origin.Id, destination.Id, originFood: 0, destinationFood: 20);
        long nextId = 100;
        AddFillerPopulation(world, origin.Id, 19, ref nextId);
        AddFillerPopulation(world, destination.Id, 19, ref nextId);

        new MigrationSystem().Tick(world, MakeCtx(world));
        CompletePendingMigrations(world);

        Assert.Equal(destination.Id, npc.City);
        Assert.Equal(destination.Id, household.City);
    }

    [Fact]
    public void Population_settles_instead_of_oscillating_across_several_ticks_between_two_close_scoring_cities()
    {
        // Repro do bug relatado: sem a margem, um household cuja cidade atual perde por ~11% no
        // score (abaixo dos 15% de margem, mas seria suficiente pra vencer sob o `>` estrito
        // antigo) migraria -- e, ao chegar, a mesma conta se inverteria (a cidade que ele deixou
        // fica com 1 npc a menos -> comida por capita sobe -> ele voltaria no dia seguinte).
        // Filler households fixos garantem a mesma comida total (9) nas duas cidades; o household
        // "móvel" nunca deposita comida própria (0), então só a contagem de população (que MUDA
        // se ele migrar) altera o nível de comida de cada cidade -- exatamente o feedback loop
        // relatado. Origem, com o household: 9/(9+1)=0.9. Destino, sem ele: 9/9=1.0 (~11% melhor,
        // abaixo da margem de 15%) -> não deveria migrar em nenhum dos "dias" simulados.
        var world = MakeWorld(MakeRules(foodWeight: 1));
        var (origin, destination) = MakeTwoCities(world);
        long nextId = 100;
        AddFillerPopulation(world, origin.Id, 9, ref nextId);
        AddFillerPopulation(world, destination.Id, 9, ref nextId);

        var head = MakeNpc(world, 1, origin.Id);
        world.AddNpc(head);
        var household = new Household(new HouseholdId(1), origin.Location, head.Id, [head.Id], city: origin.Id);
        head.JoinHousehold(household.Id);
        household.Deposit(Food, 0); // household móvel nunca contribui com comida própria
        world.AddHousehold(household);

        var fillerHouseholdOrigin = new Household(new HouseholdId(2), origin.Location, new NpcId(998), [new NpcId(998)], city: origin.Id);
        fillerHouseholdOrigin.Deposit(Food, 9);
        world.AddHousehold(fillerHouseholdOrigin);
        var fillerHouseholdDestination = new Household(new HouseholdId(3), destination.Location, new NpcId(999), [new NpcId(999)], city: destination.Id);
        fillerHouseholdDestination.Deposit(Food, 9);
        world.AddHousehold(fillerHouseholdDestination);

        for (int day = 0; day < 5; day++)
        {
            new MigrationSystem().Tick(world, MakeCtx(world));
            CompletePendingMigrations(world);
        }

        Assert.Equal(origin.Id, head.City); // estabilizou -- nunca migrou, então nunca oscilou de volta
        Assert.Equal(origin.Id, household.City);
    }
}
