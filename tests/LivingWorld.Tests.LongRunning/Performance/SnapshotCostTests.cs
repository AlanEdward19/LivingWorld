using System.Diagnostics;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Snapshot;
using LivingWorld.Tests.Shared.Performance;
using Xunit.Abstractions;

namespace LivingWorld.Tests.LongRunning.Performance;

/// <summary>snapshot-positional-binary, S5: carregar um mundo grande pelo bloco binário custa no máximo o que custava
/// pelo JSON, e gravar não fica mais caro que serializar JSON (a gravação roda fora do tick, nas fronteiras de snapshot).</summary>
public class SnapshotCostTests(ITestOutputHelper output)
{
    private static double BestOf(int runs, Action action)
    {
        action(); // aquecimento
        double best = double.MaxValue;
        for (int i = 0; i < runs; i++)
        {
            var sw = Stopwatch.StartNew();
            action();
            sw.Stop();
            best = Math.Min(best, sw.Elapsed.TotalMilliseconds);
        }

        return best;
    }

    [Fact]
    [Trait("Category", "Heavy")]
    public void Loading_a_ten_thousand_npc_world_from_binary_is_not_slower_than_from_json_and_saving_is_not_dearer()
    {
        var (world, clock) = ScaleScenarioFixture.CreateWorld(seed: 42, initialPopulation: 10_000);
        clock.Tick(world);

        PositionalSnapshot.EncodeFull(world, out _); // aquece
        GC.Collect();
        long heapBefore = GC.GetTotalMemory(forceFullCollection: true);
        PositionalSnapshot.EncodeFull(world, out var heldBase);
        GC.Collect();
        long baseHeapMb = (GC.GetTotalMemory(forceFullCollection: true) - heapBefore) / (1024 * 1024);
        GC.KeepAlive(heldBase);
        output.WriteLine($"base em memoria (10.000 NPCs): {baseHeapMb} MB");

        string json = WorldSnapshot.Serialize(world);
        byte[] binary = PositionalSnapshot.EncodeFull(world, out var snapshotBase);

        double jsonLoad = BestOf(3, () => WorldSnapshot.Deserialize(json));
        double binaryLoad = BestOf(3, () => PositionalSnapshot.DecodeFull(binary));
        double jsonSave = BestOf(3, () => WorldSnapshot.Serialize(world));
        double binarySave = BestOf(3, () => PositionalSnapshot.EncodeFull(world, out _));
        double deltaSave = BestOf(3, () => PositionalSnapshot.EncodeDelta(world, snapshotBase));

        output.WriteLine($"bytes json={json.Length} binary={binary.Length} ({100.0 * binary.Length / json.Length:F1}%)");
        output.WriteLine($"load ms json={jsonLoad:F0} binary={binaryLoad:F0}");
        output.WriteLine($"save ms json={jsonSave:F0} binaryFull={binarySave:F0} binaryDelta={deltaSave:F0}");

        Assert.True(baseHeapMb <= 50, $"a base em memoria ocupa {baseHeapMb} MB");
        Assert.True(binaryLoad <= jsonLoad, $"carga binária {binaryLoad:F0} ms contra JSON {jsonLoad:F0} ms");
        Assert.True(binarySave <= jsonSave * 1.2, $"gravação binária {binarySave:F0} ms contra JSON {jsonSave:F0} ms");
    }
}
