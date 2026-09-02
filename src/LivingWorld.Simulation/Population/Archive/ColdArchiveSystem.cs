using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Population.Archive;

/// <summary>Arquiva NPCs/fauna/flora mortos antigos no tier frio (PERF-10, REALISM-21).</summary>
public sealed class ColdArchiveSystem : ISimulationSystem
{
    public const string SystemName = "population-cold-archive";

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Yearly;

    public void Tick(WorldState world, TickContext ctx)
    {
        long now = ctx.CurrentTick;
        foreach (var npc in world.Npcs.Where(n => !n.IsAlive).OrderBy(n => n.Id.Value).ToList())
            world.ColdArchive.TryArchive(world, ctx, npc, now, world.PerfRules);

        foreach (var animal in world.Fauna.Where(a => !a.IsAlive).OrderBy(a => a.Id.Value).ToList())
            world.ColdArchive.TryArchiveAnimal(world, animal, now, world.PerfRules);
    }
}
