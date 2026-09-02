using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Geography.Map;
using LivingWorld.Domain.Population;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Behavior.Resolvers;

/// <summary>Resolve o lugar de descanso do NPC (Fase 15.1, Stage 4, T12, LWV-03.1): cama do
/// household se existir, senão a moradia, senão o chão no local atual. Sem caminho válido o
/// chamador bloqueia — nunca teleporta nem aplica sono remoto.</summary>
public static class RestPlaceResolver
{
    public static RestPlaceRef Resolve(WorldState world, Npc npc)
    {
        var catalog = world.RestPlaceCatalog;
        if (npc.Household is { } householdId && world.FindHousehold(householdId) is { } household)
        {
            var bed = world.RestPlaces
                .Where(place => place.Kind == RestPlaceKind.Bed && place.OwnerHousehold == householdId)
                .OrderBy(place => place.Id.Value)
                .FirstOrDefault();
            if (bed is not null)
                return new RestPlaceRef(RestPlaceKind.Bed, bed.Id.Value, bed.Location, catalog.BedEfficiency);

            return new RestPlaceRef(RestPlaceKind.Dwelling, householdId.Value, household.Location, catalog.DwellingEfficiency);
        }

        return new RestPlaceRef(RestPlaceKind.Ground, 0, npc.CurrentLocation, catalog.GroundEfficiency);
    }

    public static bool IsReachable(WorldMap map, CellCoord origin, CellCoord destination) =>
        origin == destination || MapPathfinder.ShortestCost(map, origin, destination).IsSuccess;
}
