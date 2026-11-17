using System.Diagnostics;
using System.Text;
using System.Text.Json;
using LivingWorld.Api.Visual.Scope;
using LivingWorld.Tests.Shared.Performance;

namespace LivingWorld.Tests.LongRunning.Performance;

/// <summary>AD-170: custo da projeção do observável com assinante (não é gate). Uma cidade de 500 NPCs (<c>LW_MEASURE_POP</c>), 240
/// ticks: µs do tick sozinho por NPC-tick, µs da projeção + diff + serialização do escopo Cidade e do Mundo por tick, e bytes do
/// delta (média, p95, máximo) e do snapshot. Escreve em <c>LW_MEASURE_OUT</c>. Sem assinante o laço não projeta nada
/// (<c>TickLoopService.PublishDeltas</c> sai antes), então o custo por NPC-tick do motor é o mesmo. AD-172: projeta como o laço (uma
/// leitura do tick para os dois escopos, com a do tick anterior), mede KB alocados por tick e confere a cada tick que snapshot +
/// deltas reconstroem a projeção nova (<c>replayMismatches</c>). <c>LW_MEASURE_SAMPLE=prefixo</c> grava cada delta em JSON.</summary>
[Trait("Category", "Scenario")]
[Trait("Category", "Measure")]
[Trait("Category", "Report")]
public class ScopeProjectionCostHarness
{
    /// <summary>O redutor guarda processos e espólios por id; a projeção, na ordem de montagem. Compara o conjunto.</summary>
    private static LivingScopeState Normalized(LivingScopeState state) => state with
    {
        Processes = state.Processes.OrderBy(p => p.Id).ToList(),
        Leavings = state.Leavings.OrderBy(l => l.Id).ToList(),
    };

    private static int Population => int.TryParse(Environment.GetEnvironmentVariable("LW_MEASURE_POP"), out int pop) && pop > 0 ? pop : 500;

    [Fact]
    public void Measure()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var (world, clock) = ScaleScenarioFixture.CreateWorld(42, Population);
        var city = new VisualScope(VisualScopeKind.City, world.ActiveCities().First().Id.ToString());
        var global = new VisualScope(VisualScopeKind.World, "");
        for (int t = 0; t < 48; t++) clock.Tick(world); // aquece JIT e caches

        var cityState = LivingScopeProjector.Build(world, city);
        var worldState = LivingScopeProjector.Build(world, global);
        long snapshotBytes = Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(cityState, options));
        var tickWatch = new Stopwatch();
        var projectWatch = new Stopwatch();
        var cityBytes = new List<long>();
        var worldBytes = new List<long>();
        long alive = 0;
        long allocated = 0;
        int replayMismatches = 0;
        ScopeProjectionFrame? lastFrame = null;
        var (cityReplay, worldReplay) = (cityState, worldState);
        var cityFields = new Dictionary<string, long>();
        var worldFields = new Dictionary<string, long>();
        static void Tally(Dictionary<string, long> into, string json)
        {
            foreach (var (key, value) in System.Text.Json.Nodes.JsonNode.Parse(json)!.AsObject())
                into[key] = into.GetValueOrDefault(key) + Encoding.UTF8.GetByteCount(value?.ToJsonString() ?? "null");
        }
        static string Top(Dictionary<string, long> fields) =>
            string.Join(" ", fields.OrderByDescending(f => f.Value).Take(8).Select(f => $"{f.Key}={f.Value / 240}"));
        for (int t = 0; t < 240; t++)
        {
            alive += world.Npcs.Count(n => n.IsAlive);
            tickWatch.Start();
            clock.Tick(world);
            tickWatch.Stop();
            long tick = world.CurrentDate.TotalHours;
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            projectWatch.Start();
            // AD-172: uma leitura do tick para os dois escopos, com a do tick anterior, como o laço faz
            var frame = lastFrame = new ScopeProjectionFrame(world, previous: lastFrame);
            var nextCity = LivingScopeProjector.Build(frame, city);
            var nextWorld = LivingScopeProjector.Build(frame, global);
            var cityDiff = ScopeDeltaBuilder.Diff(tick, cityState, nextCity);
            var worldDiff = ScopeDeltaBuilder.Diff(tick, worldState, nextWorld);
            string cityDelta = JsonSerializer.Serialize(cityDiff, options);
            string worldDelta = JsonSerializer.Serialize(worldDiff, options);
            projectWatch.Stop();
            allocated += GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            // AD-172: snapshot + deltas reconstroem a projeção nova a cada tick (avanço de processo, campos omitidos)
            (cityReplay, worldReplay) = (LivingDeltaReducer.Apply(cityReplay, cityDiff), LivingDeltaReducer.Apply(worldReplay, worldDiff));
            if (!Normalized(cityReplay).Equals(Normalized(nextCity)) || !Normalized(worldReplay).Equals(Normalized(nextWorld))) replayMismatches++;
            Tally(cityFields, cityDelta);
            Tally(worldFields, worldDelta);
            if (Environment.GetEnvironmentVariable("LW_MEASURE_SAMPLE") is { Length: > 0 } sample)
            {
                File.WriteAllText($"{sample}.{t:D3}.city.json", cityDelta);
                File.WriteAllText($"{sample}.{t:D3}.world.json", worldDelta);
            }
            cityBytes.Add(Encoding.UTF8.GetByteCount(cityDelta));
            worldBytes.Add(Encoding.UTF8.GetByteCount(worldDelta));
            (cityState, worldState) = (nextCity, nextWorld);
        }

        static string Stats(List<long> bytes)
        {
            var sorted = bytes.OrderBy(b => b).ToList();
            return $"avg={sorted.Average():F0} p95={sorted[(int)(sorted.Count * 0.95)]} max={sorted[^1]}";
        }

        int residents = cityState.Npcs.Count;
        string report =
            $"pop={Population} cityNpcs={residents} tickMicrosPerNpcTick={tickWatch.Elapsed.TotalMicroseconds / Math.Max(1, alive):F3} "
            + $"projectionMicrosPerTick={projectWatch.Elapsed.TotalMicroseconds / 240:F0} "
            + $"projectionMicrosPerNpcTick={projectWatch.Elapsed.TotalMicroseconds / Math.Max(1, alive):F3} "
            + $"projectionAllocKbPerTick={allocated / 240 / 1024} replayMismatches={replayMismatches}\n"
            + $"cityDeltaBytes {Stats(cityBytes)} worldDeltaBytes {Stats(worldBytes)} citySnapshotBytes={snapshotBytes}\n"
            + $"cityPerTickByField {Top(cityFields)}\nworldPerTickByField {Top(worldFields)} worldNpcs={worldState.Npcs.Count}\n";
        File.WriteAllText(Environment.GetEnvironmentVariable("LW_MEASURE_OUT") ?? Path.Combine(Path.GetTempPath(), "lw-scope-cost.txt"), report);
        Assert.True(residents > 0);
        Assert.Equal(0, replayMismatches);
    }
}
