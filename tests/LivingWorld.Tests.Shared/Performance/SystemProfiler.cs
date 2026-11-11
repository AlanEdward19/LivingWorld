using System.Diagnostics;
using System.Globalization;
using System.Text;
using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Needs;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Shared.Performance;

/// <summary>Perfil por sistema (#158): embrulha cada <see cref="ISimulationSystem"/> num cronômetro e diz onde o tick gasta
/// tempo e bytes. O embrulho só mede: nome, frequência, ordem e eventos passam direto, então o mundo é o mesmo com e sem ele.</summary>
public sealed class SystemProfiler
{
    private sealed class Timed(ISimulationSystem inner) : ISimulationSystem
    {
        public long Ticks, AllocatedBytes;
        public int Calls;

        public string Name => inner.Name;
        public TickFrequency Frequency => inner.Frequency;

        public void Tick(WorldState world, TickContext ctx)
        {
            long bytes = GC.GetAllocatedBytesForCurrentThread(), started = Stopwatch.GetTimestamp();
            inner.Tick(world, ctx);
            Ticks += Stopwatch.GetTimestamp() - started;
            AllocatedBytes += GC.GetAllocatedBytesForCurrentThread() - bytes;
            Calls++;
        }

        public void HandleEvent(WorldState world, TickContext ctx, ScheduledEvent evt)
        {
            long bytes = GC.GetAllocatedBytesForCurrentThread(), started = Stopwatch.GetTimestamp();
            inner.HandleEvent(world, ctx, evt);
            Ticks += Stopwatch.GetTimestamp() - started;
            AllocatedBytes += GC.GetAllocatedBytesForCurrentThread() - bytes;
        }
    }

    public sealed record SystemCost(string Name, TickFrequency Frequency, double Milliseconds, long AllocatedBytes, int Calls);

    public sealed record Report(
        int Ticks, long AliveNpcTicks, double TotalMilliseconds, long TotalAllocatedBytes,
        IReadOnlyList<SystemCost> Systems, long Wakes, long WakesWithoutEffect,
        IReadOnlyDictionary<WakeCause, long> ScheduledBy, int Relationships, int Alive,
        IReadOnlyDictionary<(WakeCause Cause, ActionType? During), long> ScheduledByAction)
    {
        public double MicrosPerAliveNpcTick => TotalMilliseconds * 1000.0 / Math.Max(1, AliveNpcTicks);
        public double BytesPerAliveNpcTick => (double)TotalAllocatedBytes / Math.Max(1, AliveNpcTicks);
        public double OutsideSystemsMilliseconds => TotalMilliseconds - Systems.Sum(s => s.Milliseconds);

        public string Format(int top = 12)
        {
            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.AppendLine(inv, $"ticks={Ticks} vivos~{AliveNpcTicks / Math.Max(1, Ticks)} total={TotalMilliseconds:F0} ms  us/npc-tick={MicrosPerAliveNpcTick:F3}  bytes/npc-tick={BytesPerAliveNpcTick:F0}");
            sb.AppendLine(inv, $"despertares/tick={(double)Wakes / Math.Max(1, Ticks):F1}  sem efeito={100.0 * WakesWithoutEffect / Math.Max(1, Wakes):F1}%  relações/vivo={(double)Relationships / Math.Max(1, Alive):F1}");
            sb.AppendLine("agendados por: " + string.Join("  ", ScheduledBy.OrderByDescending(k => k.Value).Select(k => string.Create(inv, $"{k.Key}={k.Value}"))));
            foreach (var group in ScheduledByAction.Where(k => k.Value > 0).GroupBy(k => k.Key.Cause).OrderByDescending(g => g.Sum(k => k.Value)))
                sb.Append("  ").Append(group.Key.ToString().PadRight(18)).AppendLine(string.Join(" ", group
                    .OrderByDescending(k => k.Value)
                    .Select(k => string.Create(inv, $"{k.Key.During?.ToString() ?? "-"}={k.Value}"))));
            foreach (var s in Systems.OrderByDescending(s => s.Milliseconds).Take(top))
                sb.AppendLine(inv, $"  {s.Name,-34} {s.Frequency,-8} {s.Milliseconds,9:F1} ms {100 * s.Milliseconds / TotalMilliseconds,5:F1}%  {s.AllocatedBytes / 1024,9} KB {100.0 * s.AllocatedBytes / Math.Max(1, TotalAllocatedBytes),5:F1}%  chamadas={s.Calls}");
            sb.AppendLine(inv, $"  {"(fora dos sistemas)",-34} {"",-8} {OutsideSystemsMilliseconds,9:F1} ms {100 * OutsideSystemsMilliseconds / TotalMilliseconds,5:F1}%");
            return sb.ToString();
        }
    }

    /// <summary>Relógio com os mesmos sistemas de <paramref name="clock"/>, na mesma ordem, cada um embrulhado.</summary>
    public static WorldClock Wrap(WorldClock clock) =>
        new(clock.Systems.Select(s => (ISimulationSystem)new Timed(s)).ToList(), clock.MaxIterationsPerTick);

    /// <summary>Aquece, zera os contadores e mede <paramref name="ticks"/> ticks.</summary>
    public static Report Measure(WorldState world, WorldClock clock, int warmupTicks, int ticks)
    {
        var profiled = Wrap(clock);
        profiled.Run(world, warmupTicks);
        foreach (Timed t in profiled.Systems.Cast<Timed>()) { t.Ticks = 0; t.AllocatedBytes = 0; t.Calls = 0; }
        world.WakeCounters.Reset();

        long alive = 0, bytes = GC.GetAllocatedBytesForCurrentThread();
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < ticks; i++)
        {
            alive += world.AliveNpcIndex.Alive.Count();
            profiled.Tick(world);
        }
        sw.Stop();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - bytes;

        var systems = profiled.Systems.Cast<Timed>()
            .Select(t => new SystemCost(t.Name, t.Frequency, t.Ticks * 1000.0 / Stopwatch.Frequency, t.AllocatedBytes, t.Calls))
            .ToList();
        var counters = world.WakeCounters;
        return new Report(
            ticks, alive, sw.Elapsed.TotalMilliseconds, allocated, systems, counters.Wakes, counters.WakesWithoutEffect,
            Enum.GetValues<WakeCause>().ToDictionary(c => c, counters.ScheduledBy),
            world.Relationships.Count, world.AliveNpcIndex.Alive.Count(),
            (from cause in Enum.GetValues<WakeCause>()
             from during in Enum.GetValues<ActionType>().Cast<ActionType?>().Append(null)
             select (cause, during)).ToDictionary(k => k, k => counters.ScheduledBy(k.cause, k.during)));
    }
}
