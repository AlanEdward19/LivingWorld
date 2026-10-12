using System.Diagnostics.Tracing;
using System.Text;
using LivingWorld.Tests.Shared.Performance;

namespace LivingWorld.Tests.LongRunning.Performance;

/// <summary>tick-cost-scaling: tipos que mais alocam por tick (GCAllocationTick, amostra a cada ~100 KB) em 10.000 NPCs.
/// Escreve <c>LW_ALLOC_OUT</c> (padrão: lw-alloc.txt no temp); não é gate.</summary>
[Collection(ScalePerformanceCollection.Name)]
[Trait("Category", "Heavy")]
[Trait("Category", "Measure")]
public class AllocProfileTests
{
    private sealed class Listener : EventListener
    {
        public readonly Dictionary<string, long> Bytes = [];
        public bool Enabled;

        protected override void OnEventSourceCreated(EventSource source)
        {
            if (source.Name == "Microsoft-Windows-DotNETRuntime")
                EnableEvents(source, EventLevel.Verbose, (EventKeywords)0x1);
        }

        protected override void OnEventWritten(EventWrittenEventArgs e)
        {
            if (!Enabled || e.EventName is null || !e.EventName.StartsWith("GCAllocationTick", StringComparison.Ordinal)) return;
            string type = e.Payload![e.PayloadNames!.IndexOf("TypeName")]?.ToString() ?? "?";
            long amount = Convert.ToInt64(e.Payload[e.PayloadNames.IndexOf("AllocationAmount64")]);
            lock (Bytes) Bytes[type] = Bytes.GetValueOrDefault(type) + amount;
        }
    }

    [Fact]
    public void Top_allocating_types_at_10k()
    {
        var (world, clock) = ScaleScenarioFixture.CreateWorld(42, 10_000);
        clock.Run(world, 48); // aquecimento
        using var listener = new Listener();
        listener.Enabled = true;
        clock.Run(world, 24 * 5);
        listener.Enabled = false;

        long total = listener.Bytes.Values.Sum();
        var sb = new StringBuilder($"amostra total {total / 1024} KB\n");
        foreach (var (type, bytes) in listener.Bytes.OrderByDescending(kv => kv.Value).Take(40))
            sb.AppendLine($"{100.0 * bytes / total,5:F1}% {type}");
        File.WriteAllText(Environment.GetEnvironmentVariable("LW_ALLOC_OUT") ?? Path.Combine(Path.GetTempPath(), "lw-alloc.txt"), sb.ToString());
    }
}
