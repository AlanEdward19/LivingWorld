using LivingWorld.Domain;
using LivingWorld.Domain.Geography;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Economy;

/// <summary>População viva por região, recomputada uma vez por tick Daily (PERF-06).</summary>
public sealed class RegionPopulationIndex
{
    private readonly Dictionary<RegionId, long> _aliveByRegionId;

    private RegionPopulationIndex(Dictionary<RegionId, long> aliveByRegionId) =>
        _aliveByRegionId = aliveByRegionId;

    public long AliveInRegion(RegionId regionId) => _aliveByRegionId.GetValueOrDefault(regionId);

    public static RegionPopulationIndex BuildForTick(WorldState world)
    {
        var counts = new Dictionary<RegionId, long>();
        foreach (var npc in world.Npcs)
        {
            if (!npc.IsAlive) continue;
            var regionId = world.Map.RegionOf(npc.CurrentLocation);
            counts[regionId] = counts.GetValueOrDefault(regionId) + 1;
        }
        return new RegionPopulationIndex(counts);
    }
}
