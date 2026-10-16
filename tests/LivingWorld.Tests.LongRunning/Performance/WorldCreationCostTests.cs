using System.Diagnostics;
using System.Globalization;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.LongRunning.Performance;

[Collection(ScalePerformanceCollection.Name)]
[Trait("Category", "Scenario")]
[Trait("Category", "Measure")]
public class WorldCreationCostTests
{
    [Fact]
    public void Creating_six_thousand_npcs_records_the_median_of_three_runs()
    {
        ScenarioRunner.Create(42, initialPopulation: 6000); // mesmo caminho aquecido fora da medição
        var samples = new List<double>();
        for (int i = 0; i < 3; i++)
        {
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            var watch = Stopwatch.StartNew();
            var (world, _) = ScenarioRunner.Create(42, initialPopulation: 6000);
            watch.Stop();
            Assert.Equal(6000, world.Npcs.Count);
            samples.Add(watch.Elapsed.TotalMilliseconds);
        }
        if (Environment.GetEnvironmentVariable("LW_CREATION_COST_OUT") is { } path)
            File.WriteAllText(path, string.Join(",", samples.Select(s => s.ToString("F3", CultureInfo.InvariantCulture))));
    }
}
