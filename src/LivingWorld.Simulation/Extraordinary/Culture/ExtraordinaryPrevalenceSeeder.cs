using LivingWorld.Domain;

namespace LivingWorld.Simulation;

/// <summary>Atribui portadores iniciais sem materializar pessoas do pool agregado.</summary>
public static class ExtraordinaryPrevalenceSeeder
{
    public static int Seed(WorldState world)
    {
        var scenario = world.Extraordinary;
        if (!scenario.Enabled || scenario.Prevalence <= 0 || scenario.Descriptors.Count == 0)
            return 0;

        int created = 0;
        foreach (var city in world.ActiveCities().OrderBy(item => item.Id.Value))
        {
            var rng = world.Rng.Stream($"extraordinary-prevalence-{city.Id}");
            var candidates = world.Npcs
                .Where(npc => npc.IsAlive && npc.City == city.Id)
                .Select(npc => npc.Id)
                .Concat(city.PoolNpcIds)
                .Distinct()
                .OrderBy(id => id.Value);
            foreach (var npcId in candidates)
            {
                if (rng.NextDouble() >= scenario.Prevalence) continue;
                if (world.ExtraordinaryCarriers.Any(carrier => carrier.CarrierId == npcId)) continue;
                int descriptorIndex = Math.Min(
                    scenario.Descriptors.Count - 1,
                    (int)(rng.NextDouble() * scenario.Descriptors.Count));
                var descriptor = scenario.Descriptors[descriptorIndex];
                var npc = world.FindNpc(npcId);
                var state = npc is null
                    ? DormantAggregate(npcId, descriptor)
                    : ExtraordinaryStateSystem.Resolve(world, npc, [descriptor.Id]);
                world.UpsertExtraordinaryCarrier(state);
                created++;
            }
        }
        return created;
    }

    private static ExtraordinaryCarrierState DormantAggregate(NpcId id, PowerDescriptor descriptor) => new(
        id, [descriptor.Id], false, "dormant",
        new ExtraordinaryAppearanceState(1, "", ""), null, 1);
}
