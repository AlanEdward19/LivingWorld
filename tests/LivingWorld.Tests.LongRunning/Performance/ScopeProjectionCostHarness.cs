using System.Diagnostics;
using System.Text;
using System.Text.Json;
using LivingWorld.Api.Visual.Scope;
using LivingWorld.Tests.Shared.Performance;

namespace LivingWorld.Tests.LongRunning.Performance;

/// <summary>AD-170: custo da projeção do observável com assinante (não é gate). Uma cidade de 500 NPCs (<c>LW_MEASURE_POP</c>), 240
/// ticks: µs do tick sozinho por NPC-tick, µs da projeção + diff + serialização do escopo Cidade e do Mundo por tick, e bytes do
/// delta (média, p95, máximo) e do snapshot. Escreve em <c>LW_MEASURE_OUT</c>. Sem assinante o laço não projeta nada
/// (<c>TickLoopService.PublishDeltas</c> sai antes), então o custo por NPC-tick do motor é o mesmo.</summary>
[Trait("Category", "Scenario")]
[Trait("Category", "Measure")]
[Trait("Category", "Report")]
public class ScopeProjectionCostHarness
{
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
            projectWatch.Start();
            var nextCity = LivingScopeProjector.Build(world, city);
            var nextWorld = LivingScopeProjector.Build(world, global);
            string cityDelta = JsonSerializer.Serialize(ScopeDeltaBuilder.Diff(tick, cityState, nextCity), options);
            string worldDelta = JsonSerializer.Serialize(ScopeDeltaBuilder.Diff(tick, worldState, nextWorld), options);
            projectWatch.Stop();
            Tally(cityFields, cityDelta);
            Tally(worldFields, worldDelta);
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
            + $"projectionMicrosPerNpcTick={projectWatch.Elapsed.TotalMicroseconds / Math.Max(1, alive):F3}\n"
            + $"cityDeltaBytes {Stats(cityBytes)} worldDeltaBytes {Stats(worldBytes)} citySnapshotBytes={snapshotBytes}\n"
            + $"cityPerTickByField {Top(cityFields)}\nworldPerTickByField {Top(worldFields)} worldNpcs={worldState.Npcs.Count}\n";
        File.WriteAllText(Environment.GetEnvironmentVariable("LW_MEASURE_OUT") ?? Path.Combine(Path.GetTempPath(), "lw-scope-cost.txt"), report);
        Assert.True(residents > 0);
    }
}
