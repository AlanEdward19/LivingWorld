using System.Text.Json.Nodes;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Integration.Cities;

/// <summary>Diagnóstico da fundação e fusão de cidades (AD-173), sem asserção: <c>scenarios/default.json</c>, seeds 1, 2, 3 e 7,
/// 10 anos; quantas cidades-filhas nascem e quantas se fundem. <c>LW_CITY_LAYOUT=off</c> mede a colocação de antes;
/// <c>LW_CHAIN_REACH=n</c> troca o alcance da cadeia do limite. Rode com
/// <c>LW_CITY_FOUNDING_OUT=arquivo bash scripts/test.sh --project integration --filter CityFoundingReport</c>.</summary>
[Trait("Category", "Report")]
public class CityFoundingReport
{
    [Fact]
    public void CityFoundingReport_writes_daughters_founded_and_merged()
    {
        var path = Environment.GetEnvironmentVariable("LW_CITY_FOUNDING_OUT");
        if (string.IsNullOrEmpty(path)) return;
        string mode = Environment.GetEnvironmentVariable("LW_CITY_LAYOUT") ?? "";
        using var output = new StreamWriter(path);
        var root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "LivingWorld.sln"))) root = Directory.GetParent(root)!.FullName;
        foreach (ulong seed in new ulong[] { 1, 2, 3, 7 })
        {
            var node = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "scenarios", "default.json")))!.AsObject();
            node["Seed"] = seed;
            if (mode == "off") node["CityLayout"] = new JsonObject { ["Enabled"] = false };
            if (int.TryParse(Environment.GetEnvironmentVariable("LW_CHAIN_REACH"), out int reach)) node["CityLayout"] = new JsonObject { ["ChainedBoundsReachCells"] = reach };
            // AD-176: LW_STREETS=off mede sem a rua de verdade
            if (Environment.GetEnvironmentVariable("LW_STREETS") == "off")
                (node["CityLayout"] ??= new JsonObject())!.AsObject()["Streets"] = new JsonObject { ["Enabled"] = false };
            var (world, clock) = ScenarioLoaderV2.LoadWorld(node.ToJsonString()).Value;
            var founded = new HashSet<Guid>();
            var merged = new HashSet<Guid>();
            for (int month = 0; month < 120; month++)
            {
                clock.Run(world, world.Calendar.HoursPerYear / 12);
                foreach (var city in world.Cities.Where(c => c.FoundedFromCityId is not null))
                {
                    founded.Add(city.Id.Value);
                    if (city.MergedIntoCityId is not null) merged.Add(city.Id.Value);
                }
            }
            output.WriteLine($"modo={(mode == "" ? "on" : mode)} rua={Environment.GetEnvironmentVariable("LW_STREETS") ?? "on"} cadeia={Environment.GetEnvironmentVariable("LW_CHAIN_REACH") ?? "padrao"} seed={seed} filhas={founded.Count} fundidas={merged.Count} ativas={world.ActiveCities().Count()} predios={world.Buildings.Count} " +
                $"calcar={world.Technologies.Count(t => t.Category == LivingWorld.Simulation.Cities.Streets.PavingTechnique.Category)} {LivingWorld.Simulation.Cities.Streets.StreetDiagnostics.Line(world)}");
            output.Flush();
        }
    }
}
