using LivingWorld.Domain;

namespace LivingWorld.Simulation.History;

/// <summary>Reconstrói linhagem a partir do esqueleto (Fase 10, HIST-22/23) — nunca tabela
/// paralela.</summary>
public static class LineageQuery
{
    public static Result<Lineage> ReconstructFrom(NpcId descendant, WorldState world)
    {
        var npc = world.FindNpc(descendant);
        if (npc is null)
            return Result<Lineage>.Fail("npc_not_found");

        var generations = new List<LineageGeneration>();
        var visited = new HashSet<long>();
        var current = npc;

        while (true)
        {
            if (!visited.Add(current.Id.Value))
                return Result<Lineage>.Fail("cycle_detected");

            long? birthTick = FindFactTick(world, current.Id, WorldEventKind.Birth);
            long? deathTick = FindFactTick(world, current.Id, WorldEventKind.Death)
                ?? FindFactTick(world, current.Id, WorldEventKind.Starvation)
                ?? FindFactTick(world, current.Id, WorldEventKind.MaternalDeath);

            if (deathTick is long deadAt && HasPostDeathSkeletonEvent(world, current.Id, deadAt))
                return Result<Lineage>.Fail("post_death_event");

            if (deathTick is long death && birthTick is not long birth)
                return Result<Lineage>.Fail("death_without_birth");
            if (deathTick is long d && birthTick is long b && b >= d)
                return Result<Lineage>.Fail("birth_not_before_death");

            generations.Add(new LineageGeneration(current.Id, current.MotherId, current.FatherId, birthTick, deathTick));

            var nextId = current.MotherId ?? current.FatherId;
            if (nextId is null)
                break;

            var parent = world.FindNpc(nextId.Value);
            if (parent is null)
                return Result<Lineage>.Fail("hole_detected");

            current = parent;
        }

        return Result<Lineage>.Ok(new Lineage(descendant, generations));
    }

    internal static bool HasPostDeathSkeletonEvent(WorldState world, NpcId npcId, long deathTick) =>
        world.Facts.Any(f =>
            f.Tick > deathTick
            && f.Kind != WorldEventKind.CompensatingCorrection
            && f.Participants.Any(p => p == npcId));

    private static long? FindFactTick(WorldState world, NpcId npcId, WorldEventKind kind)
    {
        long? tick = null;
        foreach (var fact in world.Facts)
        {
            if (fact.Kind != kind || !fact.Participants.Contains(npcId))
                continue;
            tick = tick is null ? fact.Tick : Math.Min(tick.Value, fact.Tick);
        }
        return tick;
    }
}
