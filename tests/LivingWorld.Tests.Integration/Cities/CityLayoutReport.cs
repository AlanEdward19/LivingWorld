using LivingWorld.Api.Periods;
using LivingWorld.Infrastructure.EfCore;
using LivingWorld.Infrastructure.Repositories;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Tests.Shared.Cities;
using Microsoft.EntityFrameworkCore;

namespace LivingWorld.Tests.Integration.Cities;

/// <summary>Diagnóstico do traçado da cidade (AD-173), sem asserção: mundo padrão e período <c>cidade-media</c>, seeds 1, 2 e 7,
/// no nascimento e nos anos 1, 10 e <c>LW_CITY_LAYOUT_YEARS</c> (padrão 20). Rode com
/// <c>LW_CITY_LAYOUT_OUT=arquivo bash scripts/test.sh --project integration --filter CityLayoutReport</c>.</summary>
[Trait("Category", "Report")]
public class CityLayoutReport
{
    /// <summary><c>LW_CITY_LAYOUT=off</c>: a colocação de antes, para o lado a lado.</summary>
    private static bool Off => Environment.GetEnvironmentVariable("LW_CITY_LAYOUT") == "off";

    [Fact]
    public void CityLayoutReport_writes_the_layout_numbers()
    {
        var path = Environment.GetEnvironmentVariable("LW_CITY_LAYOUT_OUT");
        if (string.IsNullOrEmpty(path)) return;
        int years = int.TryParse(Environment.GetEnvironmentVariable("LW_CITY_LAYOUT_YEARS"), out int y) ? y : 20;
        using var output = new StreamWriter(path);
        string cidadeMedia = PeriodPayload("cidade-media");
        foreach (ulong seed in new ulong[] { 1, 2, 7 })
        {
            var (world, clock) = ScenarioRunner.Create(seed, cityLayout: Off ? LivingWorld.Domain.Cities.CityLayoutRules.Off : null);
            Report(output, $"padrao seed={seed}", world, clock, years, seed == 1);
            var root = System.Text.Json.Nodes.JsonNode.Parse(cidadeMedia)!;
            root["Seed"] = seed;
            if (Off) root["CityLayout"] = new System.Text.Json.Nodes.JsonObject { ["Enabled"] = false };
            var loaded = ScenarioLoaderV2.LoadWorld(root.ToJsonString()).Value;
            Report(output, $"cidade-media seed={seed}", loaded.Item1, loaded.Item2, years, seed == 2);
        }
    }

    private static void Report(StreamWriter output, string label, WorldState world, WorldClock clock, int years, bool ascii)
    {
        output.WriteLine(CityLayoutStats.Measure(world).Line($"{label} ano=0"));
        if (ascii) output.WriteLine(CityLayoutStats.Ascii(world));
        int done = 0;
        foreach (int mark in new[] { 1, 10, years }.Where(m => m <= years).Distinct())
        {
            clock.Run(world, (long)(mark - done) * world.Calendar.HoursPerYear);
            done = mark;
            output.WriteLine(CityLayoutStats.Measure(world).Line($"{label} ano={mark}"));
            output.Flush();
        }
        if (ascii) output.WriteLine(CityLayoutStats.Ascii(world));
        output.Flush();
    }

    private static string PeriodPayload(string periodId)
    {
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<WorldDbContext>().UseSqlite(connection).Options;
        using var context = new WorldDbContext(options);
        context.Database.Migrate();
        var repository = new SqlitePeriodTemplateRepository(context);
        DefaultPeriodSeeder.SeedIfEmpty(repository);
        return repository.FindLatestVersion(periodId)!.PayloadJson;
    }
}
