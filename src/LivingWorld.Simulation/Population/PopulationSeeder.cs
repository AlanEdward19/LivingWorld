using LivingWorld.Domain;
using LivingWorld.Simulation.Behavior;

namespace LivingWorld.Simulation;

/// <summary>Gera a população inicial (task 6) e registra em <see cref="WorldState"/>: chama o
/// gerador puro do Domain, adiciona NPCs/households e agenda a morte de cada um — nenhum deles
/// nasce sem um evento de óbito já na fila (task 4).</summary>
public static class PopulationSeeder
{
    public static void SeedInitial(WorldState world, int count, CultureId culture, CellCoord villageLocation, CityId city = default)
    {
        // Mesmo lado que CityBoundsResolver vai calcular pra essa população (LIVE-POLISH: raio
        // fixo de 2 células espalhava família pra fora do footprint real assim que a cidade
        // ficava menor que 5x5 — família nascia "em cima" da cidade no mapa-múndi e não dava
        // pra clicar nela, porque IsNpcInScope só mostra externo quem está fora dos bounds).
        int radius = CityBoundsResolver.SideFor(count, world.Map.Width, world.Map.Height) / 2;

        var rng = world.Rng.Stream("population-init");
        var generated = PopulationGenerator.GenerateInitial(
            rng, world.CurrentDate, count, culture, villageLocation, world.PopulationRules.LifeTable,
            world.PopulationCatalog, world.NextNpcId, world.NextHouseholdId, city,
            HouseholdSpawnCells(world.Map, villageLocation, radius));

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

    private static IReadOnlyList<CellCoord> HouseholdSpawnCells(WorldMap map, CellCoord villageLocation, int radius) =>
        map.Cells
            .Where(cell => Math.Max(
                Math.Abs(cell.Coord.X - villageLocation.X),
                Math.Abs(cell.Coord.Y - villageLocation.Y)) <= radius)
            .OrderBy(cell => Math.Max(
                Math.Abs(cell.Coord.X - villageLocation.X),
                Math.Abs(cell.Coord.Y - villageLocation.Y)))
            .ThenBy(cell => cell.Coord.Y)
            .ThenBy(cell => cell.Coord.X)
            .Select(cell => cell.Coord)
            .ToList();
}
