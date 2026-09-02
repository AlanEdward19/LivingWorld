using System.Text.Json.Nodes;
using LivingWorld.Api;
using LivingWorld.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace LivingWorld.Tests.Periods;

public class DefaultPeriodSeederTests
{
    [Fact]
    public void SeedIfEmpty_keeps_builtin_presets_safe_without_an_authored_food_economy()
    {
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<WorldDbContext>().UseSqlite(connection).Options;
        using var context = new WorldDbContext(options);
        context.Database.Migrate();
        var repository = new SqlitePeriodTemplateRepository(context);
        DefaultPeriodSeeder.SeedIfEmpty(repository);

        foreach (var template in repository.ListLatestPerPeriod())
        {
            Assert.Equal(1, template.Version);
            var payload = JsonNode.Parse(template.PayloadJson)!.AsObject();
            Assert.False(payload["EconomyEnabled"]!.GetValue<bool>());
            Assert.Empty(payload["Workplaces"]!.AsArray());
        }
    }
}
