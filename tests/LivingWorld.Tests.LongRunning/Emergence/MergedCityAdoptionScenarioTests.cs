using System.Text.Json.Nodes;
using LivingWorld.Domain.Emergence;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.LongRunning.Emergence;

/// <summary>#114: depois de qualquer fusão de cidade (ADR-0019), nenhuma tecnologia lista como adotante uma cidade fundida
/// (lápide). Cenário vivo (<c>scenarios/default.json</c>, emergência e fundação ligadas), 10 anos, conferido a cada dia.
/// Nestes seeds nenhuma tecnologia emerge sozinha em 10 anos, então cada filha, assim que surge, recebe uma tecnologia
/// adotada só por ela; o teste exige que alguma dessas filhas tenha sido fundida (caso não vácuo).</summary>
public class MergedCityAdoptionScenarioTests
{
    private const long TenYearsInHours = 10L * 12 * 30 * 24;

    private static string RepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "LivingWorld.sln")))
            dir = Directory.GetParent(dir)?.FullName;
        return dir ?? throw new InvalidOperationException("LivingWorld.sln nao encontrado");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void No_technology_is_adopted_by_a_merged_city_at_any_daily_sample_over_ten_years(int seed)
    {
        var node = JsonNode.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "scenarios", "default.json")))!.AsObject();
        node["Seed"] = seed;
        var loaded = ScenarioLoaderV2.LoadWorld(node.ToJsonString());
        Assert.True(loaded.IsSuccess, loaded.Error);
        var world = loaded.Value.World;
        var clock = loaded.Value.Clock;
        var seeded = new HashSet<CityId>();
        int axisCount = world.EmergenceRules.AxisCount;

        for (long hour = 0; hour < TenYearsInHours; hour += 24)
        {
            clock.Run(world, 24);
            foreach (var daughter in world.Cities.Where(c => c.FoundedFromCityId is not null).OrderBy(c => c.Id.Value))
            {
                if (daughter.MergedIntoCityId is null && seeded.Add(daughter.Id))
                {
                    var axes = new int[axisCount];
                    axes[0] = 1 + seeded.Count; // vetor distinto por filha
                    world.AddTechnology(new Technology(
                        ScenarioRunner.DefaultCulture, EmergenceRules.MobilityCategory, new TechnologyVector(axes),
                        world.CurrentDate.TotalHours, daughter.Id, null, [daughter.Id]));
                }
            }
            var tombstones = world.Cities.Where(c => c.MergedIntoCityId is not null).Select(c => c.Id).ToHashSet();
            foreach (var tech in world.Technologies)
                Assert.DoesNotContain(tech.AdoptedBy, tombstones.Contains);
        }

        int mergedSeeded = world.Cities.Count(c => seeded.Contains(c.Id) && c.MergedIntoCityId is not null);
        Assert.True(mergedSeeded > 0, "nenhuma filha com tecnologia adotada foi fundida — a asserção seria vácua");
    }
}
