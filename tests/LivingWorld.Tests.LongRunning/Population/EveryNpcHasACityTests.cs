using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.LongRunning.Population;

/// <summary>Fase 8: todo NPC vivo pertence a uma cidade existente (amostrado por dia, 1 ano).</summary>
public class EveryNpcHasACityTests
{
    private const long OneYearInHours = 12 * 30 * 24;

    [Fact]
    public void Every_living_npc_belongs_to_an_existing_city_at_every_daily_sample()
    {
        var (world, clock) = ScenarioRunner.Create(seed: 42);
        Assert.NotEmpty(world.Cities);

        for (long tick = 1; tick <= OneYearInHours; tick++)
        {
            clock.Tick(world);
            if (tick % 24 != 0) continue;
            var cityIds = world.Cities.Select(c => c.Id).ToHashSet();
            foreach (var npc in world.Npcs.Where(n => n.IsAlive))
                Assert.True(cityIds.Contains(npc.City), $"NPC {npc.Id} sem cidade válida no tick {tick}");
        }
    }
}
