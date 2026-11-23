using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Health;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cities;
using LivingWorld.Simulation.Cities.Migration;
using LivingWorld.Simulation.Cities.Streets;
using LivingWorld.Simulation.Cities.Works;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Crime;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Cities;

/// <summary>AD-192: consequências do bairro denso — a sujeira da casa (o que o motor mede), a higiene na doença, a parede-meia, a
/// aversão por pessoa e o que ela decide (onde morar, valor, migração), cada regra ligada e desligada.</summary>
public class DenseQuarterTests
{
    private const int House = LivingWorld.Simulation.Population.PopulationSeeder.InitialHouseBuildingTypeId;

    private static (WorldState World, WorldClock Clock) Village(StreetRules? streets = null, DiseaseRules? disease = null) =>
        ScenarioRunner.Create(4, cityLayout: streets is null ? null : CityLayoutRules.Default with { Streets = streets }, diseaseRules: disease);

    /// <summary>Duas casas coladas (parede-meia) num canto seco do mapa, cada uma com uma família da vila; já obstáculo da rota.</summary>
    private static (Building A, Building B) GluedPair(WorldState world) => GluedPair(world, out _);

    private static (Building A, Building B) GluedPair(WorldState world, out double aloneSqualor)
    {
        var city = world.ActiveCities().First();
        CellCoord? corner = null;
        var state = StreetRoutes.StateOf(world);
        for (int y = 1; y + 4 < world.Map.Height && corner is null; y++)
            for (int x = 1; x + 7 < world.Map.Width && corner is null; x++)
            {
                bool ok = true;
                for (int dy = -1; dy <= 3 && ok; dy++)
                    for (int dx = -1; dx <= 6 && ok; dx++)
                        ok = world.Map.TryGetCell(new CellCoord(x + dx, y + dy), out var c) && !c.HasWater
                             && state.Owner[(y + dy) * state.W + x + dx] == 0;
                if (ok) corner = new CellCoord(x, y);
            }
        Assert.NotNull(corner);
        var at = corner!.Value;
        long now = world.CurrentDate.TotalHours;
        aloneSqualor = Quarter.SiteSqualor(world, at, BuildingFootprintGenerator.Generate(new BuildingId(world.NextBuildingId), House, 0), 0);
        var a = new Building(world.NextBuildingIdAndAdvance(), city.Id, House, now, at, 0, croftCells: 0);
        var b = new Building(world.NextBuildingIdAndAdvance(), city.Id, House, now, new CellCoord(at.X + 3, at.Y), 0, croftCells: 0);
        world.AddBuilding(a);
        world.AddBuilding(b);
        var homes = world.Households.Where(h => !h.IsEmpty).OrderBy(h => h.Id.Value).Take(2).ToList();
        homes[0].MoveIn(a.Id, DwellingCapacity.InteriorCellOf(a, a.Position!.Value));
        homes[1].MoveIn(b.Id, DwellingCapacity.InteriorCellOf(b, b.Position!.Value));
        StreetGroundSystem.Occupied(world, now);
        return (a, b);
    }

    [Fact]
    public void Glued_houses_are_dirtier_than_the_same_lot_standing_alone()
    {
        var (world, _) = Village();
        var (a, _) = GluedPair(world, out double alone);
        Assert.True(Quarter.PlaceSqualor(world, a) > alone);
        var walls = new List<long>();
        Quarter.WallNeighbours(world, a, walls);
        Assert.NotEmpty(walls);
    }

    [Fact]
    public void Cramming_families_into_one_house_raises_its_squalor()
    {
        var (world, _) = Village();
        var homes = world.Households.Where(h => !h.IsEmpty && h.Dwelling is not null).OrderBy(h => h.Id.Value).ToList();
        var target = homes[0];
        double before = Quarter.HomeSqualor(world, target);
        var house = world.FindBuilding(target.Dwelling!.Value)!;
        foreach (var other in homes.Skip(1).Take(4)) other.MoveIn(house.Id, DwellingCapacity.InteriorCellOf(house, house.Position!.Value));
        Assert.True(Quarter.HomeSqualor(world, target) > before);
    }

    [Fact]
    public void The_hygiene_factor_is_one_at_the_reference_and_grows_with_squalor()
    {
        var rules = StreetRules.Default;
        Assert.Equal(1.0, Quarter.HygieneFactor(rules, rules.HygieneReference), 9);
        Assert.True(Quarter.HygieneFactor(rules, 0.6) > Quarter.HygieneFactor(rules, 0.3));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Squalor_weighs_on_the_cases_from_the_environment_only_with_the_rule(bool hygiene)
    {
        // a mesma vila: com a regra, a casa suja pega mais diarreia e febre do ambiente (o mundo diverge); sem ela, o hash bate com o de
        // uma vila em que a sujeira nem é medida
        var (on, clockOn) = Village(StreetRules.Default with { QuarterHygiene = hygiene, WallContagion = false });
        var (reference, clockRef) = Village(StreetRules.Default with { QuarterHygiene = false, WallContagion = false });
        clockOn.Run(on, on.Calendar.HoursPerYear / 2);
        clockRef.Run(reference, reference.Calendar.HoursPerYear / 2);
        Assert.Equal(hygiene, on.Infections.Count != reference.Infections.Count || on.Immunities.Count != reference.Immunities.Count
            || !on.Npcs.Select(n => n.Health).SequenceEqual(reference.Npcs.Select(n => n.Health)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_cold_passes_through_a_shared_wall_only_with_the_rule(bool walls)
    {
        var cold = new DiseaseProfile(9, 0.0, 0.0, 30, 3650)
        {
            Name = "teste",
            Route = DiseaseRoute.Respiratory,
            HouseholdContagionPerDay = 0,
            CrewContagionPerDay = 0,
            CommunityContagionPerContact = 0.6,
        };
        var rules = new DiseaseRules(true, [cold], int.MaxValue, 0) { CommunityContactsPerDay = 0 };
        var (world, clock) = Village(StreetRules.Default with { WallContagion = walls }, rules);
        var (a, b) = GluedPair(world);
        var sickHome = world.Households.First(h => h.Dwelling == a.Id);
        var otherHome = world.Households.First(h => h.Dwelling == b.Id);
        long now = world.CurrentDate.TotalHours;
        world.AddInfection(new Infection(sickHome.Members[0], cold.Id, now + 24 * 30) { InfectedTick = now, OnsetTick = now, Symptomatic = true });
        clock.Run(world, 24 * 10);
        bool spread = otherHome.Members.Any(m => world.IsInfected(m, cold.Id) || world.IsImmune(m, cold.Id));
        Assert.Equal(walls, spread);
    }

    [Fact]
    public void Knowing_a_crime_by_someone_from_a_crammed_house_raises_the_aversion()
    {
        var (world, _) = Village();
        var city = world.ActiveCities().First();
        var bounds = CityOccupancy.ResolveGrownBounds(world, city, 100).Bounds;
        var homes = world.Households.Where(h => !h.IsEmpty && h.Dwelling is not null).OrderBy(h => h.Id.Value).ToList();
        var crammed = homes[0];
        var house = world.FindBuilding(crammed.Dwelling!.Value)!;
        foreach (var other in homes.Skip(1).Take(8)) other.MoveIn(house.Id, DwellingCapacity.InteriorCellOf(house, house.Position!.Value));
        Assert.True(Quarter.HomeSqualor(world, crammed) >= world.StreetRules.DenseSqualor);
        var victim = homes.Skip(9).Select(h => world.FindNpc(h.Head)!).OrderBy(n => Quarter.Aversion(world, n, city, bounds)).ThenBy(n => n.Id.Value).First();
        double before = Quarter.Aversion(world, victim, city, bounds);
        world.Crime!.Cases.Add(new CrimeCase
        {
            Id = 1,
            Kind = CrimeKind.Theft,
            Offender = crammed.Head.Value,
            Accused = crammed.Head.Value,
            Victim = victim.Id.Value,
            City = city.Id.Value,
            Tick = world.CurrentDate.TotalHours
        });
        Assert.True(Quarter.Aversion(world, victim, city, bounds) > before);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Where_to_live_weighs_the_squalor_by_the_family_aversion_only_with_the_rule(bool weigh)
    {
        var (world, _) = Village(StreetRules.Default with { QuarterInLotChoice = weigh });
        var city = world.ActiveCities().First();
        var bounds = CityOccupancy.ResolveGrownBounds(world, city, 100).Bounds;
        var home = world.Households.First(h => !h.IsEmpty);
        var members = home.Members.Select(m => world.FindNpc(m)!).ToList();
        Assert.Equal(weigh, BuildWorks.QuarterYearly(world, city, bounds, members) > 0);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_glued_house_is_worth_less_only_with_the_rule(bool value)
    {
        var (world, _) = Village(StreetRules.Default with { QuarterInValue = value });
        var (a, _) = GluedPair(world);
        var recipe = new BuildingRecipe(new Dictionary<ResourceType, long>(), 1, 1,
            Pricing: new PricingTerms(100, MinMultiplier: 0, MaxMultiplier: 10));
        double withRule = BuildingValuation.MultiplierOf(new ValuationContext(world, world.ActiveCities().First()), a, recipe);
        var (plain, _) = Village(StreetRules.Default with { QuarterInValue = false });
        var (pa, _) = GluedPair(plain);
        double without = BuildingValuation.MultiplierOf(new ValuationContext(plain, plain.ActiveCities().First()), pa, recipe);
        Assert.Equal(value, withRule < without);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_known_crime_makes_the_city_less_safe_for_migration_only_with_the_rule(bool migrate)
    {
        var (world, _) = Village(StreetRules.Default with { QuarterInMigration = migrate });
        var city = world.ActiveCities().First();
        var home = world.Households.First(h => !h.IsEmpty);
        world.Crime!.Cases.Add(new CrimeCase
        {
            Id = 1,
            Kind = CrimeKind.Theft,
            Offender = 1,
            Accused = 1,
            Victim = home.Head.Value,
            City = city.Id.Value,
            Tick = world.CurrentDate.TotalHours
        });
        double security = MigrationSystem.SecurityLevel(world, home, city.Id, new MigrationSystem.SafetyCache());
        Assert.Equal(migrate, security < 0.5);
    }
}
