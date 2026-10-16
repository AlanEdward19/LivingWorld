using System.Diagnostics;
using LivingWorld.Domain.History;
using LivingWorld.Tests.Shared.Performance;
using Xunit.Abstractions;

namespace LivingWorld.Tests.LongRunning.Performance;

[Collection(ScalePerformanceCollection.Name)]
public class RuntimeHistoryCostTests(ITestOutputHelper output)
{
    [Fact]
    [Trait("Category", "Measure")]
    public void Enabled_history_costs_at_most_three_percent_more_per_npc_tick()
    {
        const int rounds = 5, ticks = 720;
        var disabled = new List<double>();
        var enabled = new List<double>();
        // Alternate arms to avoid always charging JIT and thermal drift to the same arm.
        for (int round = -1; round < rounds; round++)
        {
            foreach (bool on in round % 2 == 0 ? new[] { false, true } : new[] { true, false })
            {
                var rules = on ? HistoryRules.Default with { SkeletonSignificanceThreshold = 0.4 } : HistoryRules.Disabled;
                var (world, clock) = ScaleScenarioFixture.CreateWorld(42, 1000, rules);
                GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                long npcTicks = 0;
                var sw = Stopwatch.StartNew();
                for (int tick = 0; tick < ticks; tick++)
                {
                    npcTicks += world.AliveNpcIndex.Alive.Count;
                    clock.Tick(world);
                }
                sw.Stop();
                double micros = sw.Elapsed.TotalMicroseconds / npcTicks;
                if (round >= 0) (on ? enabled : disabled).Add(micros);
                if (on) Assert.NotEmpty(world.Facts);
            }
        }
        double before = disabled.Order().ElementAt(rounds / 2);
        double after = enabled.Order().ElementAt(rounds / 2);
        string measurement = $"disabled={before:F6} enabled={after:F6} ratio={after / before:F6}; samples off=[{string.Join(',', disabled)}] on=[{string.Join(',', enabled)}]";
        output.WriteLine(measurement);
        if (Environment.GetEnvironmentVariable("LW_HISTORY_COST_OUT") is { } path) File.WriteAllText(path, measurement);
        Assert.True(after / before <= 1.03, $"history CPU ratio {after / before:F4} exceeds 1.03");
    }
}
