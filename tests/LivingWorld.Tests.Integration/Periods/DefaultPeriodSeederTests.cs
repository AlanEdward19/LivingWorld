using System.Text.Json.Nodes;
using LivingWorld.Api.Periods;
using LivingWorld.Infrastructure.EfCore;
using LivingWorld.Infrastructure.Repositories;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Periods;
using LivingWorld.Simulation.Scenarios;
using Microsoft.EntityFrameworkCore;

namespace LivingWorld.Tests.Integration.Periods;

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

    [Fact]
    public void SeedIfEmpty_names_every_profession_and_skill_of_every_builtin_preset()
    {
        foreach (var json in SeededPayloads())
        {
            var definition = PeriodDefinitionValidator.Validate(json).Value!;
            var catalog = PeriodCatalog.From(definition);
            Assert.NotEmpty(catalog.ProfessionIds);
            foreach (int id in catalog.ProfessionIds)
                Assert.True(catalog.ProfessionNames.ContainsKey(id), $"profissão {id} sem nome");
            foreach (int id in catalog.SkillIds)
                Assert.True(catalog.SkillNames.ContainsKey(id), $"habilidade {id} sem nome");
        }
    }

    [Fact]
    public void SeedIfEmpty_gives_every_builtin_preset_inherited_looks()
    {
        foreach (var json in SeededPayloads())
        {
            var world = ScenarioLoaderV2.LoadWorld(json).Value.World;
            Assert.NotNull(world.AppearanceRules);
            Assert.All(world.Npcs, n => Assert.NotNull(n.Appearance));
        }
    }

    [Fact]
    public void SeedIfEmpty_gives_every_builtin_preset_the_object_catalog_with_the_migrated_furniture()
    {
        foreach (var json in SeededPayloads())
        {
            var catalog = ScenarioLoaderV2.LoadWorld(json).Value.World.ObjectCatalog;
            Assert.NotNull(catalog);
            Assert.Equal(9, catalog.Objects.Count(o => o.ResourceId is null));
            Assert.Equal(["bed", "bed", "table", "chest"], catalog.FurnishingFor(10));
            Assert.Equal(["table", "bench", "crate"], catalog.FurnishingFor(999));
        }
    }

    [Fact]
    public void An_invalid_Objects_block_makes_the_whole_period_invalid()
    {
        foreach (var json in SeededPayloads())
        {
            var broken = JsonNode.Parse(json)!.AsObject();
            broken["objectsFormat"] = 2;

            var result = PeriodDefinitionValidator.Validate(broken.ToJsonString());

            Assert.False(result.IsSuccess);
            Assert.Contains("objectsFormat", result.Error);
        }
    }

    [Fact]
    public void Profession_names_do_not_change_the_world_hash()
    {
        foreach (var json in SeededPayloads())
        {
            var bare = JsonNode.Parse(json)!.AsObject();
            bare.Remove("Dynamics");
            var named = LoadHash(json);
            Assert.Equal(LoadHash(bare.ToJsonString()), named);
        }
    }

    private static string LoadHash(string json)
    {
        var world = ScenarioLoaderV2.LoadWorld(json).Value.World;
        return WorldSnapshot.CanonicalHash(world);
    }

    private static List<string> SeededPayloads()
    {
        var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<WorldDbContext>().UseSqlite(connection).Options;
        var context = new WorldDbContext(options);
        context.Database.Migrate();
        var repository = new SqlitePeriodTemplateRepository(context);
        DefaultPeriodSeeder.SeedIfEmpty(repository);
        return repository.ListLatestPerPeriod().Select(t => t.PayloadJson).ToList();
    }
}
