using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Needs;
using LivingWorld.Simulation.Cities;
using LivingWorld.Simulation.Cities.Construction;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Population.Lifecycle;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Population;

/// <summary>Gera a população inicial (task 6) e registra em <see cref="WorldState"/>: chama o
/// gerador puro do Domain, adiciona NPCs/households e agenda a morte de cada um — nenhum deles
/// nasce sem um evento de óbito já na fila (task 4).</summary>
public static class PopulationSeeder
{
    internal const int InitialHouseBuildingTypeId = -1;

    public static void SeedInitial(WorldState world, int count, CultureId culture, CellCoord villageLocation, CityId city = default)
    {
        var placementCity = ResolvePlacementCity(world, city, villageLocation);

        var rng = world.Rng.Stream("population-init");
        var generated = PopulationGenerator.GenerateInitial(
            rng, world.CurrentDate, count, culture, villageLocation, world.PopulationRules.LifeTable,
            world.PopulationCatalog, world.NextNpcId, world.NextHouseholdId, placementCity.Id,
            householdLocationsFactory: householdCount => PlaceHouseholdBuildings(
                world, placementCity, count, householdCount),
            bodyRules: world.BodyRules);

        foreach (var npc in generated.Npcs)
        {
            npc.ConfigureNeedDecay(world.NeedsRules, world.CurrentDate.TotalHours);
            world.AddNpc(npc);
        }
        foreach (var household in generated.Households)
            world.AddHousehold(household);

        world.AdvanceNpcIdTo(generated.NextNpcId);
        world.AdvanceHouseholdIdTo(generated.NextHouseholdId);

        var ctx = new TickContext(world, world.Rng, world.Scheduler);
        foreach (var npc in generated.Npcs)
        {
            MortalitySystem.SchedulePlannedDeath(world, ctx, npc);
            NpcWakeScheduler.ScheduleWake(world, ctx, npc.Id.Value, world.CurrentDate.TotalHours + 1);
        }
    }

    private static City ResolvePlacementCity(WorldState world, CityId requestedCity, CellCoord villageLocation)
    {
        if (requestedCity != default)
            return world.FindActiveCity(requestedCity)
                ?? throw new InvalidOperationException("Population city must exist before household placement.");

        var existing = world.ActiveCities().FirstOrDefault(candidate => candidate.Location == villageLocation);
        if (existing is not null) return existing;

        var created = new City(
            world.NextCityId(), villageLocation, world.CurrentDate.TotalHours,
            foundedFromCityId: null, AggregatePopulationPool.Empty);
        world.AddCity(created);
        return created;
    }

    // Post-ship fix (bug real "casas gigantes, ultrapassam o limite da cidade"): usava um scan
    // dedicado (ResolveInitialBatch) que caía num fallback varrendo o MAPA INTEIRO sempre que uma
    // casa não cabia nos bounds ainda pequenos (population-derived) de uma cidade recém-fundada —
    // espalhando casas arbitrariamente longe da cidade, fora do alcance de
    // CityOccupancy.ResolveGrownBounds's AbsorptionRingCells (que só absorve overflow PRÓXIMO da
    // borda). Reusa exatamente o mesmo caminho que todo outro prédio já usa
    // (BuildingPlacementResolver.Resolve: célula livre nos bounds, senão anel de overflow a partir
    // da borda) — mesmo padrão de peek-id/adicionar-incremental de ConstructionSystem.CompleteProject,
    // pra que Resolve veja as casas já colocadas neste lote como ocupadas.
    private static IReadOnlyList<CellCoord> PlaceHouseholdBuildings(
        WorldState world, City city, int population, int householdCount)
    {
        var locations = new List<CellCoord>(householdCount);
        for (int i = 0; i < householdCount; i++)
        {
            var bounds = CityOccupancy.ResolveGrownBounds(world, city, population).Bounds;
            var candidate = new Building(new BuildingId(world.NextBuildingId), city.Id, InitialHouseBuildingTypeId, world.CurrentDate.TotalHours);
            // Post-ship fix (real-household-workplace-buildings, map-auto-resize removal): a
            // small authored map can run out of free rectangular room for every household before
            // this loop is done -- now common, since the map is never silently regenerated bigger
            // to make room anymore (that was the actual bug: same seed, different world). Same
            // last-resort ring/hash position BuildingPlacementResolver.ResolveQueuedSite already
            // uses for a building with nowhere else to go (never fails, may overlap): a household
            // still needs SOME location to exist (unlike a workplace, it can't just be skipped),
            // and land scarcity should degrade its exact position, never crash world loading.
            var (position, orientation) = BuildingPlacementResolver.Resolve(candidate, city, world, bounds)
                is { } resolved
                ? (resolved.Position, resolved.Orientation)
                : (CityOccupancy.LegacyRingFallback(candidate.Id, city.Location), 0);

            world.AddBuilding(new Building(
                world.NextBuildingIdAndAdvance(), city.Id, InitialHouseBuildingTypeId,
                world.CurrentDate.TotalHours, position, orientation));
            // Household.Location é o destino real de sono/retorno. A origem do footprint é uma
            // célula de parede; guardar essa origem fazia o NPC parar "na casa", mas nunca entrar.
            // Casas 3x3 sempre têm exatamente uma célula Floor, rotacionada junto com a planta.
            var interior = BuildingFootprintGenerator
                .Generate(candidate.Id, InitialHouseBuildingTypeId, orientation)
                .Where(cell => cell.Material == BuildingMaterial.Floor)
                .OrderBy(cell => cell.Cell.Y)
                .ThenBy(cell => cell.Cell.X)
                .Select(cell => new CellCoord(position.X + cell.Cell.X, position.Y + cell.Cell.Y))
                .First();
            locations.Add(interior);
        }
        return locations;
    }
}
