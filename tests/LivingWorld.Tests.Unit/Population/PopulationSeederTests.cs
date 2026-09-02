using LivingWorld.Api.Visual.Projection;
using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Cities.Spatial;
using LivingWorld.Domain.Geography;
using LivingWorld.Simulation.Cities;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Population;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Population;

public class PopulationSeederTests
{
    [Fact]
    public void SeedInitial_creates_exactly_one_matching_building_per_household()
    {
        var world = SeedWorld(count: 30);

        Assert.All(world.Households, household =>
            Assert.Single(world.Buildings, building =>
                building.City == household.City && AbsoluteFootprint(building).Contains(household.Location)));
    }

    [Fact]
    public void SeedInitial_never_reuses_a_house_position()
    {
        var world = SeedWorld(count: 30);

        Assert.Equal(world.Households.Count,
            world.Households.Select(household => household.Location).Distinct().Count());
    }

    [Fact]
    public void SeedInitial_house_building_footprints_do_not_overlap()
    {
        var world = SeedWorld(count: 30);
        var footprintCells = world.Buildings
            .OrderBy(building => building.Id.Value)
            .SelectMany(AbsoluteFootprint)
            .ToArray();

        Assert.Equal(
            (world.Households.Count, footprintCells.Length),
            (world.Buildings.Count, footprintCells.Distinct().Count()));
    }

    [Fact]
    public void SeedInitial_places_each_household_on_an_internal_floor_cell_of_its_house()
    {
        var world = SeedWorld(count: 30);

        Assert.All(world.Households, household =>
        {
            var house = Assert.Single(world.Buildings, building =>
                building.City == household.City && AbsoluteFootprint(building).Contains(household.Location));
            var origin = house.Position!.Value;
            var floorCells = BuildingFootprintGenerator
                .Generate(house.Id, house.BuildingTypeId, house.Orientation)
                .Where(cell => cell.Material == BuildingMaterial.Floor)
                .Select(cell => new CellCoord(origin.X + cell.Cell.X, origin.Y + cell.Cell.Y));

            Assert.Contains(household.Location, floorCells);
        });
    }

    [Fact]
    public void SeedInitial_keeps_every_rendered_house_footprint_inside_the_resolved_city_bounds()
    {
        const int count = 30;
        var world = SeedWorld(count);
        var city = Assert.Single(world.Cities);
        var bounds = CityOccupancy.ResolveGrownBounds(world, city, count).Bounds;

        var rendered = CityProjector.Build(world, city.Id).Value!.Buildings;
        Assert.NotEmpty(rendered);
        Assert.All(rendered, building =>
            Assert.All(
                BuildingFootprintGenerator.Generate(building.Id, building.BuildingTypeId, building.Orientation)
                    .Select(cell => new CellCoord(
                        building.Location.X + cell.Cell.X,
                        building.Location.Y + cell.Cell.Y)),
                cell => Assert.True(bounds.Contains(cell),
                    $"rendered building {building.Id.Value} cell {cell} is outside {bounds}")));
    }

    [Fact]
    public void Seeded_households_outside_grown_bounds_remain_owned_overflow_buildings()
    {
        // Raio fixo (2 células) descasava do footprint dinâmico assim que a população ficava
        // pequena o bastante pra produzir um lado menor que 5 (LIVE-POLISH: usuário via NPC "em
        // cima" da cidade no mapa-múndi, sem dar pra clicar — IsNpcInScope só mostra como externo
        // quem está fora dos bounds calculados pra mesma população).
        const int count = 36;
        var world = SeedWorld(count);
        var city = Assert.Single(world.Cities);

        var bounds = CityOccupancy.ResolveGrownBounds(world, city, count).Bounds;
        var ownedBuildings = CityOccupancy.OwnedBuildingFootprintBoxesWithOwners(world, city, bounds)
            .Select(item => item.Building)
            .ToList();
        Assert.All(world.Households, household => Assert.True(
            bounds.Contains(household.Location) || ownedBuildings.Any(building =>
                AbsoluteFootprint(building).Contains(household.Location) && building.City == household.City),
            $"household em {household.Location} não pertence aos bounds nem a overflow building da cidade"));
    }

    [Fact]
    public void SeedInitial_places_houses_that_do_not_fit_the_initial_bounds_near_the_city_edge_not_scattered_map_wide()
    {
        // Bug real ("casas gigantes, ultrapassam o limite da cidade"): uma cidade recém-fundada
        // tem bounds population-derived pequenos (MinSize 3x3) que não cabem nem UMA casa (4-6
        // células de lado) -- todo household caía num fallback de scan MAPA INTEIRO em vez do
        // anel de overflow a partir da borda, espalhando casas arbitrariamente longe. O fix faz
        // toda casa passar por BuildingPlacementResolver.Resolve (mesmo caminho de qualquer outro
        // prédio: célula livre nos bounds, senão anel de overflow A PARTIR DA BORDA, nearest-first)
        // em vez do scan mapa-inteiro antigo.
        //
        // Nota de escopo: NÃO afirma que os bounds RESOLVIDOS AO VIVO (CityOccupancy.ResolveGrownBounds)
        // acabam contendo toda casa -- CityBoundsResolver.Resolve's absorção de overflow compara
        // cada footprint contra a caixa SÓ-POPULAÇÃO original (nunca contra a caixa já crescida
        // por absorções anteriores), então uma casa a 1 célula da borda final pode legitimamente
        // ficar de fora da união se estiver longe o bastante da caixa ORIGINAL -- limitação
        // pré-existente de CityBoundsResolver.Resolve (dynamic-city-growth), fora do escopo deste
        // fix. O que este fix garante (e este teste verifica) é a DISTÂNCIA da casa em si: nunca
        // mais que AbsorptionRingCells de folga fora dos bounds crescidos -- exatamente "perto da
        // borda", nunca "em qualquer canto do mapa" como o bug original produzia.
        const int count = 20;
        var world = SeedWorld(count);
        var city = Assert.Single(world.Cities);

        var grownBounds = CityOccupancy.ResolveGrownBounds(world, city, count).Bounds;
        int absorptionRingCells = world.CityRules.AbsorptionRingCells;

        Assert.All(world.Households, household =>
            Assert.True(GapToBounds(household.Location, grownBounds) <= absorptionRingCells,
                $"household em {household.Location} ficou a {GapToBounds(household.Location, grownBounds)} células dos bounds {grownBounds} (folga máxima aceitável: {absorptionRingCells}) -- casa espalhada longe da cidade em vez de perto da borda"));
    }

    /// <summary>Distância de Chebyshev de uma célula até a borda mais próxima de <paramref
    /// name="bounds"/> — 0 quando a célula já está dentro. Mesma métrica de
    /// <c>CityBoundsResolver.ChebyshevGap</c> (privada), aplicada a um ponto em vez de dois
    /// retângulos.</summary>
    private static int GapToBounds(CellCoord cell, CityBounds bounds)
    {
        int right = bounds.Origin.X + bounds.Width - 1;
        int bottom = bounds.Origin.Y + bounds.Height - 1;
        int dx = Math.Max(0, Math.Max(bounds.Origin.X - cell.X, cell.X - right));
        int dy = Math.Max(0, Math.Max(bounds.Origin.Y - cell.Y, cell.Y - bottom));
        return Math.Max(dx, dy);
    }

    private static WorldState SeedWorld(int count)
    {
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, seed: 73, ScenarioRunner.InitialMap(73, count),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules);
        var city = new City(
            world.NextCityId(), ScenarioRunner.DefaultVillageLocation, 0, null,
            AggregatePopulationPool.Empty);
        world.AddCity(city);

        PopulationSeeder.SeedInitial(
            world, count, ScenarioRunner.DefaultCulture, city.Location, city.Id);
        return world;
    }

    private static IEnumerable<CellCoord> AbsoluteFootprint(Building building)
    {
        var origin = building.Position!.Value;
        return BuildingFootprintGenerator.Generate(building.Id, building.BuildingTypeId, building.Orientation)
            .Select(cell => new CellCoord(origin.X + cell.Cell.X, origin.Y + cell.Cell.Y));
    }
}
