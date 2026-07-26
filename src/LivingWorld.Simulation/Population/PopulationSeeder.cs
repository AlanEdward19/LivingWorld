using LivingWorld.Domain;

namespace LivingWorld.Simulation;

/// <summary>Gera a população inicial (task 6) e registra em <see cref="WorldState"/>: chama o
/// gerador puro do Domain, adiciona NPCs/households e agenda a morte de cada um — nenhum deles
/// nasce sem um evento de óbito já na fila (task 4).</summary>
public static class PopulationSeeder
{
    public static void SeedInitial(WorldState world, int count, CultureId culture, CellCoord villageLocation)
    {
        var rng = world.Rng.Stream("population-init");
        var generated = PopulationGenerator.GenerateInitial(
            rng, world.CurrentDate, count, culture, villageLocation, world.PopulationRules.LifeTable,
            world.NextNpcId, world.NextHouseholdId);

        foreach (var npc in generated.Npcs)
            world.AddNpc(npc);
        foreach (var household in generated.Households)
            world.AddHousehold(household);

        world.AdvanceNpcIdTo(generated.NextNpcId);
        world.AdvanceHouseholdIdTo(generated.NextHouseholdId);

        var ctx = new TickContext(world, world.Rng, world.Scheduler);
        foreach (var npc in generated.Npcs)
            MortalitySystem.SchedulePlannedDeath(world, ctx, npc);
    }
}
