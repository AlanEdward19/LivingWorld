using System.Diagnostics;
using System.Text;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;
using LivingWorld.Tests.Shared.Performance;

namespace LivingWorld.Tests.LongRunning.Performance;

/// <summary>tick-cost-scaling: perfil por sistema em 1.000, 5.000 e 10.000 NPCs. Cada sistema é embrulhado num decorador
/// cronometrado (mesma ordem, mesma frequência), então o relógio executa exatamente o que executaria em produção. Escreve
/// <c>LW_PROFILE_OUT</c> (padrão: lw-profile.txt no temp) com µs por NPC-tick de cada sistema e o total; não é gate.</summary>
[Collection(ScalePerformanceCollection.Name)]
[Trait("Category", "Heavy")]
[Trait("Category", "Measure")]
public class ScaleProfileTests
{
    private sealed class TimedSystem(ISimulationSystem inner, Dictionary<string, long> ticks) : ISimulationSystem
    {
        public string Name => inner.Name;
        public TickFrequency Frequency => inner.Frequency;

        public void Tick(WorldState world, TickContext ctx)
        {
            long start = Stopwatch.GetTimestamp();
            inner.Tick(world, ctx);
            ticks[Name] = ticks.GetValueOrDefault(Name) + (Stopwatch.GetTimestamp() - start);
        }

        public void HandleEvent(WorldState world, TickContext ctx, ScheduledEvent evt) => inner.HandleEvent(world, ctx, evt);
    }

    private static (double TotalMicros, Dictionary<string, double> BySystem, double AllocPerNpcTick) Profile(int population, int days)
    {
        var (world, clock) = ScaleScenarioFixture.CreateWorld(42, population);
        var ticks = new Dictionary<string, long>();
        var timed = new WorldClock([.. clock.Systems.Select(s => (ISimulationSystem)new TimedSystem(s, ticks))], clock.MaxIterationsPerTick);

        GC.Collect();
        long allocBefore = GC.GetTotalAllocatedBytes(precise: true);
        long aliveNpcTicks = 0;
        var total = Stopwatch.StartNew();
        for (int t = 0; t < days * 24; t++)
        {
            aliveNpcTicks += world.AliveNpcIndex.Alive.Count;
            timed.Tick(world);
        }

        total.Stop();
        long alloc = GC.GetTotalAllocatedBytes(precise: true) - allocBefore;
        double perNpcTick = 1.0 / Math.Max(1, aliveNpcTicks);
        var bySystem = ticks.ToDictionary(kv => kv.Key, kv => kv.Value * 1e6 / Stopwatch.Frequency * perNpcTick);
        return (total.Elapsed.TotalMicroseconds * perNpcTick, bySystem, alloc * perNpcTick);
    }

    [Fact]
    public void Profile_per_system_at_1k_5k_and_10k()
    {
        var sb = new StringBuilder();
        var results = new Dictionary<int, (double Total, Dictionary<string, double> BySystem, double Alloc)>();
        for (int warm = 0; warm < 3; warm++) _ = Profile(1_000, days: 10); // aquecimento do JIT: sem ele o primeiro tamanho medido paga a compilação
        foreach (int population in new[] { 1_000, 2_000, 3_000, 5_000, 10_000 })
        {
            var r = Profile(population, days: population >= 10_000 ? 7 : 14);
            results[population] = r;
            sb.AppendLine($"== {population}: total {r.TotalMicros:F3} us/NPC-tick, alloc {r.AllocPerNpcTick:F0} B/NPC-tick");
        }

        var sizes = results.Keys.OrderBy(k => k).ToArray();
        sb.AppendLine("system | " + string.Join(" | ", sizes.Select(k => $"{k / 1000}k")) + " | last/first");
        foreach (var name in results[sizes[^1]].BySystem.Keys.OrderByDescending(k => results[sizes[^1]].BySystem[k]))
        {
            var values = sizes.Select(k => results[k].BySystem.GetValueOrDefault(name)).ToArray();
            sb.AppendLine($"{name} | {string.Join(" | ", values.Select(v => v.ToString("F3")))} | {(values[0] > 0 ? values[^1] / values[0] : 0):F2}x");
        }

        File.WriteAllText(Environment.GetEnvironmentVariable("LW_PROFILE_OUT") ?? Path.Combine(Path.GetTempPath(), "lw-profile.txt"), sb.ToString());
    }

    /// <summary>tick-cost-scaling AC 1-2 (como medido, AD-096): a alocação por NPC-tick a 10.000 fica em no máximo 110% da
    /// de 1.000, e o custo por NPC-tick tem um joelho de cache entre 1.000 e 3.000 NPCs (a 10.000, cerca de 1,8x o de 1.000)
    /// mas é plano depois dele: 10.000 em no máximo 125% do de 5.000. Só gate de pesados (<c>--heavy</c>).</summary>
    [Fact]
    public void Allocation_is_flat_across_scale_and_time_is_flat_beyond_the_cache_knee()
    {
        for (int warm = 0; warm < 3; warm++) _ = Profile(1_000, days: 10);
        var small = Profile(1_000, days: 14);
        var mid = Profile(5_000, days: 14);
        var large = Profile(10_000, days: 7);

        Assert.True(large.AllocPerNpcTick <= 1.10 * small.AllocPerNpcTick,
            $"alocação a 10.000 ({large.AllocPerNpcTick:F0} B/NPC-tick) passa de 110% da de 1.000 ({small.AllocPerNpcTick:F0})");
        Assert.True(large.TotalMicros <= 1.25 * mid.TotalMicros,
            $"custo a 10.000 ({large.TotalMicros:F3} us/NPC-tick) passa de 125% do de 5.000 ({mid.TotalMicros:F3})");
    }
}
