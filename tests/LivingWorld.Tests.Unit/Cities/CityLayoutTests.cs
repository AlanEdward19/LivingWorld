using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Cities.Spatial;
using LivingWorld.Domain.Geography;
using LivingWorld.Simulation.Cities;
using LivingWorld.Simulation.Cities.Construction;
using LivingWorld.Simulation.Cities.Layout;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Tests.Shared.Cities;

namespace LivingWorld.Tests.Unit.Cities;

/// <summary>AD-173: a cidade cresce ao longo de ruas; cada prédio nasce num lote de frente para a rua, com recuo e quintal
/// escolhidos por quem constrói, e só aperta quando falta terra.</summary>
public class CityLayoutTests
{
    private static (WorldState World, City City, CityBounds Bounds) OpenCity(ulong seed, CityLayoutRules? layout = null)
    {
        var world = ScenarioRunner.Create(seed: seed, initialPopulation: 80, cityLayout: layout).World;
        var center = new CellCoord(world.Map.Width / 2 + 5, world.Map.Height / 2 + 5);
        var city = new City(world.NextCityId(), center, 0, null, AggregatePopulationPool.Empty);
        world.AddCity(city);
        return (world, city, new CityBounds(new CellCoord(center.X - 2, center.Y - 2), 5, 5));
    }

    /// <summary>Casas que o motor ergue sem família conhecida: o caminho das obras da cidade.</summary>
    private static List<(Building Building, int Croft)> Build(WorldState world, City city, CityBounds bounds, int count, int typeId = -1)
    {
        var placed = new List<(Building, int)>();
        for (int i = 0; i < count; i++)
        {
            var candidate = new Building(world.NextBuildingIdAndAdvance(), city.Id, typeId, 0);
            var lot = BuildingPlacementResolver.ResolveLot(candidate, city, world, bounds);
            Assert.NotNull(lot);
            var building = new Building(candidate.Id, city.Id, typeId, 0, lot!.Value.Position, lot.Value.Orientation, croftCells: lot.Value.Croft);
            world.AddBuilding(building);
            placed.Add((building, lot.Value.Croft));
        }
        return placed;
    }

    private static HashSet<CellCoord> Cells(Building b) =>
        BuildingFootprintGenerator.Generate(b).Select(c => new CellCoord(c.Cell.X + b.Position!.Value.X, c.Cell.Y + b.Position!.Value.Y)).ToHashSet();

    private static int Gap(Building a, Building b) =>
        Cells(a).SelectMany(p => Cells(b).Select(q => Math.Max(Math.Abs(p.X - q.X), Math.Abs(p.Y - q.Y)) - 1)).Min();

    [Fact]
    public void Placement_with_free_land_never_overlaps_and_keeps_the_side_setback()
    {
        var (world, city, bounds) = OpenCity(2024);
        var placed = Build(world, city, bounds, 20).Select(p => p.Building).ToList();

        for (int a = 0; a < placed.Count; a++)
            for (int b = a + 1; b < placed.Count; b++)
                Assert.True(Gap(placed[a], placed[b]) >= world.CityRules.EffectiveLayout().SideSetbackCells, $"prédios {a} e {b} encostados com terra sobrando");
    }

    [Fact]
    public void Every_door_opens_onto_a_street_while_land_lasts()
    {
        var (world, city, bounds) = OpenCity(2024);
        Build(world, city, bounds, 20);

        foreach (var (building, origin, croft) in CityStreets.Placed(world, city))
            Assert.NotNull(CityStreets.LotOf(world, city, building, origin, croft).Street); // de frente ou por caminho a pé
    }

    [Fact]
    public void A_street_cell_never_receives_a_building()
    {
        var (world, city, bounds) = OpenCity(2024);
        var network = CityStreets.Of(world, city);
        Assert.True(network.Count > 20); // sem rua o teste não provaria nada

        foreach (var (building, _) in Build(world, city, bounds, 20))
            Assert.DoesNotContain(Cells(building), network.Contains);
    }

    [Fact]
    public void A_house_outside_the_center_keeps_its_croft_while_land_lasts()
    {
        var (world, city, bounds) = OpenCity(2024);
        var rules = world.CityRules.EffectiveLayout();
        var network = CityStreets.Of(world, city);

        var placed = Build(world, city, bounds, 20);
        var outer = placed.Where(p => LotGeometry.FrontStreetCell(network, BuildingFootprintGenerator.Generate(p.Building), p.Building.Position!.Value, CityOccupancy.Reach(world)) is { } s
            && network.Depth[network.IndexOf(s)] > rules.CentralDepthCells).ToList();
        Assert.NotEmpty(outer);
        Assert.All(outer, p => Assert.Equal(rules.FarmerCroftCells, p.Croft)); // nenhum caiu de degrau
    }

    [Fact]
    public void Placement_is_deterministic_for_the_same_seed()
    {
        var a = OpenCity(77);
        var b = OpenCity(77);

        var first = Build(a.World, a.City, a.Bounds, 12).Select(p => (p.Building.Position, p.Croft)).ToList();
        var second = Build(b.World, b.City, b.Bounds, 12).Select(p => (p.Building.Position, p.Croft)).ToList();

        Assert.Equal(first, second);
    }

    [Fact]
    public void Turning_the_layout_off_brings_back_the_placement_without_streets()
    {
        var (world, city, bounds) = OpenCity(2024, CityLayoutRules.Off);

        Assert.Equal(0, CityStreets.Of(world, city).Count);
        Assert.All(Build(world, city, bounds, 5), p => Assert.Equal(0, p.Croft));
    }

    [Fact]
    public void The_seeded_village_has_streets_and_no_door_without_a_way_out()
    {
        var world = ScenarioRunner.Create(seed: 1).World;

        var stats = CityLayoutStats.Measure(world);

        Assert.Equal(0, stats.Overlapping);
        Assert.Equal(0, stats.DoorsBlocked);
        Assert.Equal(0, stats.OnStreet);
    }

    [Fact]
    public void A_poor_household_builds_a_hovel_against_the_neighbour_with_its_door_on_the_street()
    {
        var terms = LotTerms.Choose(CityLayoutRules.Default, new LotProfile(true, false, 4, MeansPerHead: 1, MedianMeansPerHead: 10), central: false);

        Assert.Equal(new LotTerms(0, 0, 0), terms);
    }

    [Fact]
    public void An_artisan_in_the_center_shares_walls_and_opens_onto_the_street()
    {
        var terms = LotTerms.Choose(CityLayoutRules.Default, new LotProfile(true, true, 3, 10, 10), central: true);

        Assert.Equal(new LotTerms(0, 0, 0), terms);
    }

    [Fact]
    public void A_large_farming_family_at_the_edge_asks_for_a_deeper_croft()
    {
        var rules = CityLayoutRules.Default;
        var small = LotTerms.Choose(rules, new LotProfile(true, false, 2, 10, 10), central: false);
        var large = LotTerms.Choose(rules, new LotProfile(true, false, rules.LargeFamilyMembers, 10, 10), central: false);

        Assert.Equal(small.Croft + 1, large.Croft);
    }

    [Fact]
    public void Only_a_household_with_means_pays_for_a_croft_in_the_center()
    {
        var rules = CityLayoutRules.Default;
        var middling = LotTerms.Choose(rules, new LotProfile(true, false, 3, 10, 10), central: true);
        var wealthy = LotTerms.Choose(rules, new LotProfile(true, false, 3, 100, 10), central: true);

        Assert.Equal((0, rules.FarmerCroftCells), (middling.Croft, wealthy.Croft));
    }

    [Fact]
    public void A_street_used_by_enough_people_is_trodden_into_dirt()
    {
        // AD-176: a superfície vem do pisoteio das viagens de verdade (não mais de quantos moram na rua): meio ano de vila
        var (world, clock) = ScenarioRunner.Create(seed: 1);
        clock.Run(world, world.Calendar.HoursPerMonth * 7);
        var city = world.ActiveCities().First();

        var roads = CityStreets.RoadsOf(world, city);

        Assert.Contains(roads, r => r.Surface == StreetSurface.Dirt && r.Users > 0 && r.Wear >= world.StreetRules.DirtWear / 2);
    }

    [Fact]
    public void The_chosen_croft_survives_a_snapshot_round_trip()
    {
        var world = ScenarioRunner.Create(seed: 1).World;

        var reopened = WorldSnapshot.Deserialize(WorldSnapshot.Serialize(world));

        Assert.Equal(
            world.Buildings.Select(b => (b.Id, b.CroftCells)).ToList(),
            reopened.Buildings.Select(b => (b.Id, b.CroftCells)).ToList());
        Assert.Contains(world.Buildings, b => b.CroftCells > 0);
    }

    [Fact]
    public void The_layout_switch_survives_a_snapshot_round_trip()
    {
        var world = ScenarioRunner.Create(seed: 1, cityLayout: CityLayoutRules.Off).World;

        var reopened = WorldSnapshot.Deserialize(WorldSnapshot.Serialize(world));

        Assert.False(reopened.CityRules.EffectiveLayout().Enabled);
    }
}
