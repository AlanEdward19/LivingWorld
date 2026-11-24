using System.Diagnostics;
using System.Text;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Tests.Shared.Performance;

namespace LivingWorld.Tests.LongRunning.Performance;

/// <summary>AD-197c: custo do tempo real com jogador. Uma hora de mundo em ticks finos (Δ = 50 ms, escala 1:1: 72 mil ticks = uma hora
/// real a 20 ticks por segundo) com 1.000 NPCs, a decisão fina ligada e desligada (<c>LW_AD197C=off</c>, só o relógio, como na AD-197b),
/// contra a mesma hora num tick só (o ritmo rápido). Escreve CPU por segundo real e a fração de um núcleo. Rode com
/// <c>LW_MEASURE_OUT=arquivo bash scripts/test.sh --project longrunning --filter FineTimeCostHarness</c>.</summary>
[Trait("Category", "Scenario")]
[Trait("Category", "Measure")]
[Trait("Category", "Report")] // sem asserção: fora do gate
public class FineTimeCostHarness
{
    private const int Delta = 50;
    private static int Population => int.TryParse(Environment.GetEnvironmentVariable("LW_MEASURE_POP"), out int pop) && pop > 0 ? pop : 1000;

    [Fact]
    public void Measure()
    {
        var sb = new StringBuilder();
        foreach (bool fine in new[] { true, false })
        {
            double best = double.MaxValue, hourlyBest = double.MaxValue;
            long wakesInside = 0;
            for (int run = 0; run < 3; run++)
            {
                var (world, clock) = ScaleScenarioFixture.CreateWorld(42, Population);
                world.UsePlayers(fine ? null : PlayerRules.Default with { Time = TimeRules.Default with { FineDecisions = false } });
                clock.Run(world, 24 * 7); // uma semana de hora em hora: contratações e rotina assentadas
                var copy = LivingWorld.Simulation.Core.WorldSnapshot.Deserialize(LivingWorld.Simulation.Core.WorldSnapshot.Serialize(world));
                GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                var cpu = Process.GetCurrentProcess().TotalProcessorTime;
                long started = world.WakeCounters.Wakes;
                clock.Run(world, WorldMillis.PerHour / Delta, Delta);
                best = Math.Min(best, (Process.GetCurrentProcess().TotalProcessorTime - cpu).TotalMicroseconds);
                wakesInside = world.WakeCounters.Wakes - started;
                GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                cpu = Process.GetCurrentProcess().TotalProcessorTime;
                clock.Tick(copy);
                hourlyBest = Math.Min(hourlyBest, (Process.GetCurrentProcess().TotalProcessorTime - cpu).TotalMicroseconds);
            }
            double perRealSecond = best / 3600.0; // escala 1:1: a hora de mundo é uma hora real
            sb.AppendLine($"fine{Population} decisions={(fine ? "on" : "off")} deltaMs={Delta} cpuMicrosPerWorldHour={best:F0} "
                + $"cpuMicrosPerRealSecond={perRealSecond:F1} coreFraction={perRealSecond / 1e6:P3} wakes={wakesInside} "
                + $"hourlyTickCpuMicros={hourlyBest:F0} ratioToOneHourTick={best / Math.Max(1, hourlyBest):F2}");
        }
        File.WriteAllText(Environment.GetEnvironmentVariable("LW_MEASURE_OUT") ?? Path.Combine(Path.GetTempPath(), "lw-fine-time.txt"), sb.ToString());
    }
}
