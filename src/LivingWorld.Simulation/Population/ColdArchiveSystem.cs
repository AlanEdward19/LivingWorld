using LivingWorld.Domain;

namespace LivingWorld.Simulation.Population;

/// <summary>Arquiva NPCs mortos antigos no tier frio (PERF-10).</summary>
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
    }
}
