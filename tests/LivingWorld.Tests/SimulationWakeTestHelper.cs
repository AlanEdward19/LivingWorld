using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests;

/// <summary>Acorda NPCs no lote Hourly quando testes chamam sistemas sem <see cref="WorldClock"/>.</summary>
public static class SimulationWakeTestHelper
{
    public static void WakeAllAlive(WorldState world)
    {
        foreach (var npc in world.Npcs.Where(n => n.IsAlive))
            world.AddNpcWake(npc);
    }

    public static void Wake(WorldState world, Npc npc) => world.AddNpcWake(npc);
}
