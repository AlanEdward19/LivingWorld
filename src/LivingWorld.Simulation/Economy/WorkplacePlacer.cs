using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cities;
using LivingWorld.Simulation.Cities.Construction;
using LivingWorld.Simulation.Cities.Queries;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Economy;

/// <summary>Põe um local de trabalho autorado no mundo: edifício na cidade mais próxima, na célula pedida se livre, senão onde
/// <see cref="BuildingPlacementResolver"/> achar espaço. Usado pelo cenário (criar mundo) e pela rota ao vivo (#46 fatia 4).</summary>
public static class WorkplacePlacer
{
    public static Result<Workplace> Place(WorldState world, InitialWorkplace workplace)
    {
        if (!world.ActiveCities().Any())
        {
            // SPEC_DEVIATION: um cenário autorado sem nenhuma cidade não oferece a entidade
            // proprietária exigida pelo placement. Mantém o workplace legado sem Building,
            // em vez de inventar uma cidade que o JSON não declarou.
            var legacy = new Workplace(
                world.NextWorkplaceIdAndAdvance(), workplace.LocationType, workplace.Location,
                workplace.MaxVacancies, employees: [], workplace.Stock, workplace.Treasury, workplace.Prices);
            world.AddWorkplace(legacy);
            return Result<Workplace>.Ok(legacy);
        }

        var city = NearestCity(world.ActiveCities(), workplace.Location);
        int buildingTypeId = workplace.LocationType.Id;
        var candidateId = new BuildingId(world.NextBuildingId);
        var candidateShape = BuildingFootprintGenerator.Generate(candidateId, buildingTypeId, orientation: 0)
            .Select(cell => cell.Cell)
            .ToList();
        long cityPopulation = CityPopulationQuery.Population(world, city.Id);
        var bounds = CityOccupancy.ResolveGrownBounds(world, city, cityPopulation).Bounds;
        var authoredFootprint = candidateShape
            .Select(cell => new CellCoord(workplace.Location.X + cell.X, workplace.Location.Y + cell.Y))
            .ToList();

        CellCoord position;
        int orientation;
        if (CityOccupancy.IsFree(world, city, bounds, authoredFootprint, candidateId))
        {
            position = workplace.Location;
            orientation = 0;
        }
        else
        {
            var candidate = new Building(candidateId, city.Id, buildingTypeId, completedAtTick: 0);
            var resolved = BuildingPlacementResolver.Resolve(candidate, city, world, bounds);
            if (resolved is null)
                return Result<Workplace>.Fail("Workplaces: nenhuma célula livre para posicionar o edifício autorado");
            position = resolved.Value.Position;
            orientation = resolved.Value.Orientation;
        }

        var building = new Building(
            world.NextBuildingIdAndAdvance(), city.Id, buildingTypeId, completedAtTick: 0, position, orientation);
        world.AddBuilding(building);
        var placed = new Workplace(
            world.NextWorkplaceIdAndAdvance(), workplace.LocationType, position, workplace.MaxVacancies,
            employees: [], workplace.Stock, workplace.Treasury, workplace.Prices, city.Id, building: building.Id);
        world.AddWorkplace(placed);
        return Result<Workplace>.Ok(placed);
    }

    private static City NearestCity(IEnumerable<City> cities, CellCoord location) =>
        cities
            .OrderBy(city => Math.Max(
                Math.Abs(city.Location.X - location.X),
                Math.Abs(city.Location.Y - location.Y)))
            .ThenBy(city => city.Id.Value)
            .First();
}
