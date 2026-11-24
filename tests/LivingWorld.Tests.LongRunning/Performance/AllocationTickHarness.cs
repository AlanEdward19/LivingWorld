using System.Diagnostics.Tracing;
using System.Text;
using LivingWorld.Tests.Shared.Performance;

namespace LivingWorld.Tests.LongRunning.Performance;

/// <summary>AD-197i: de que tipos vêm os bytes do tick (amostras <c>AllocationTick</c> do runtime, uma a cada ~100 KB alocados, no próprio
/// processo, sem ferramenta externa), no fixture de escala de 1.000 NPCs. Rode com <c>LW_MEASURE_OUT=arquivo bash scripts/test.sh
/// --project longrunning --filter "FullyQualifiedName~AllocationTickHarness&amp;Category=Report"</c>.</summary>
[Trait("Category", "Measure")]
[Trait("Category", "Report")]
public class AllocationTickHarness
{
    private sealed class Sampler : EventListener
    {
        public readonly Dictionary<string, long> BytesByType = [];
        public bool On;

        protected override void OnEventSourceCreated(EventSource source)
        {
            if (source.Name == "Microsoft-Windows-DotNETRuntime")
                EnableEvents(source, EventLevel.Verbose, (EventKeywords)0x1); // GC
        }

        protected override void OnEventWritten(EventWrittenEventArgs e)
        {
            if (!On || e.EventName is not "GCAllocationTick_V4" and not "GCAllocationTick_V3" || e.Payload is null) return;
            int nameAt = e.PayloadNames!.IndexOf("TypeName");
            int amountAt = e.PayloadNames.IndexOf("AllocationAmount64");
            string type = nameAt >= 0 ? e.Payload[nameAt] as string ?? "?" : "?";
            long amount = amountAt >= 0 ? Convert.ToInt64(e.Payload[amountAt]) : 100_000;
            lock (BytesByType) BytesByType[type] = BytesByType.GetValueOrDefault(type) + amount;
        }
    }

    [Fact]
    public void Measure()
    {
        var (world, clock) = ScaleScenarioFixture.CreateWorld(42, 1000);
        clock.Run(world, 240);
        using var sampler = new Sampler();
        Thread.Sleep(500);
        sampler.On = true;
        clock.Run(world, 720);
        sampler.On = false;
        var sb = new StringBuilder();
        long total;
        lock (sampler.BytesByType)
        {
            total = sampler.BytesByType.Values.Sum();
            foreach (var (type, bytes) in sampler.BytesByType.OrderByDescending(kv => kv.Value).Take(40))
                sb.AppendLine($"{bytes / 1024,10} KB  {100.0 * bytes / Math.Max(1, total),5:F1}%  {type}");
        }
        sb.Insert(0, $"amostrado={total / 1024} KB em 720 ticks\n");
        File.WriteAllText(Environment.GetEnvironmentVariable("LW_MEASURE_OUT") ?? Path.Combine(Path.GetTempPath(), "lw-alloc.txt"), sb.ToString());
    }
}
