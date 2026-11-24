using System.Diagnostics;
using System.Text;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.LongRunning.Performance;

/// <summary>AD-197c (aviso da junção da AD-197b): custo de agendar, cancelar e vencer k eventos dentro da MESMA hora (instantes fora da
/// hora cheia, o caso do passo fino), em ordem embaralhada determinística, para k = 1.000, 10.000 e 100.000. Escreve µs por operação.
/// Rode com <c>LW_MEASURE_OUT=arquivo bash scripts/test.sh --project longrunning --filter "FullyQualifiedName~EventSchedulerInsideHourHarness&amp;Category=Report"</c>.</summary>
[Trait("Category", "Measure")]
[Trait("Category", "Report")]
public class EventSchedulerInsideHourHarness
{
    [Fact]
    public void Measure()
    {
        var sb = new StringBuilder();
        foreach (int k in new[] { 1_000, 10_000, 100_000 })
        {
            var ids = Enumerable.Range(1, k).Select(i => (long)i).OrderBy(i => (i * 2_654_435_761L) % 1_000_003).ToArray();
            var scheduler = new EventScheduler();
            var sw = Stopwatch.StartNew();
            foreach (long id in ids) scheduler.Schedule(new ScheduledEvent(id, 3_600_000 + 1 + (id * 37 % 3_599_998), "sys"));
            double schedule = sw.Elapsed.TotalMicroseconds / k;
            sw.Restart();
            for (int i = 0; i < k; i += 2) scheduler.Cancel(ids[i]);
            double cancel = sw.Elapsed.TotalMicroseconds / (k / 2);
            sw.Restart();
            int popped = 0;
            for (long now = 3_600_050; now <= 7_200_000; now += 50) // uma hora de ticks de 50 ms
                while (scheduler.HasDue(now)) { var due = scheduler.PopDue(now); popped += due.Count; scheduler.Release(due); }
            double pop = sw.Elapsed.TotalMicroseconds / Math.Max(1, popped);
            sb.AppendLine($"insideHour k={k} scheduleMicros={schedule:F3} cancelMicros={cancel:F3} popMicrosPerEvent={pop:F3} popped={popped}");
        }
        File.WriteAllText(Environment.GetEnvironmentVariable("LW_MEASURE_OUT") ?? Path.Combine(Path.GetTempPath(), "lw-scheduler.txt"), sb.ToString());
    }
}
