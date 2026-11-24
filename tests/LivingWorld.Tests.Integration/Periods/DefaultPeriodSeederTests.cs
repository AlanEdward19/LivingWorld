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
    /// <summary>AD-179 (reescrito): antes o teste exigia economia desligada e nenhuma oficina, porque o preset sem cadeia produtiva
    /// matava de fome. Agora o preset é um mundo completo (a economia da vila de código: fazenda com poço, forja, mercado), e o
    /// teste de cenário <c>MenuWorldsLiveTests</c> confere que ninguém morre de fome nem de sede no primeiro ano.</summary>
    [Fact]
    public void SeedIfEmpty_gives_every_builtin_preset_a_living_economy_with_work_market_and_technology()
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
            Assert.True(payload["EconomyEnabled"]!.GetValue<bool>());
            var types = payload["Workplaces"]!.AsArray().Select(w => w!["LocationTypeId"]!.GetValue<int>()).ToHashSet();
            Assert.Contains(1, types); // fazenda: trigo e poço
            Assert.Contains(2, types); // forja: ferramenta
            Assert.True(payload["Emergence"]!["Enabled"]!.GetValue<bool>());
            var world = ScenarioLoaderV2.LoadWorld(template.PayloadJson).Value.World;
            Assert.True(world.EconomyRules.PresenceNeedsWork);
            Assert.NotNull(world.EconomyRules.Venture);
            Assert.NotNull(world.EconomyRules.Credit);
            Assert.True(world.Map.Width * world.Map.Height >= 30 * world.AliveNpcIndex.Alive.Count, "lote para cada casa e oficina");
        }
    }

    [Fact]
    public void SeedIfEmpty_replaces_a_preset_seeded_as_a_fixture_without_economy_by_a_new_version()
    {
        var (repository, _) = EmptyRepository();
        var old = JsonNode.Parse(SeededPayloads()[0])!.AsObject();
        old["EconomyEnabled"] = false;
        string periodId = FirstPresetId();
        repository.Save(new LivingWorld.Infrastructure.Records.PeriodTemplateRecord
        {
            PeriodId = periodId,
            Version = 1,
            PayloadJson = old.ToJsonString(),
            CreatedAtUtc = DateTime.UnixEpoch,
            Source = FirstPresetSource(),
        });

        DefaultPeriodSeeder.SeedIfEmpty(repository);
        DefaultPeriodSeeder.SeedIfEmpty(repository); // idempotente

        var latest = repository.FindLatestVersion(periodId)!;
        Assert.Equal(2, latest.Version);
        Assert.True(JsonNode.Parse(latest.PayloadJson)!["EconomyEnabled"]!.GetValue<bool>());
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

    [Fact]
    public void Presentation_block_does_not_change_the_world_hash()
    {
        foreach (var json in SeededPayloads())
        {
            var bare = JsonNode.Parse(json)!.AsObject();
            Assert.True(bare.Remove("Presentation"), "preset sem bloco Presentation");
            Assert.Equal(LoadHash(bare.ToJsonString()), LoadHash(json));
        }
    }

    [Fact]
    public void SeedIfEmpty_adds_the_presentation_as_a_new_version_of_a_preset_seeded_before_it()
    {
        var (repository, _) = EmptyRepository();
        var old = JsonNode.Parse(SeededPayloads()[0])!.AsObject();
        old.Remove("Presentation");
        string periodId = FirstPresetId();
        repository.Save(new LivingWorld.Infrastructure.Records.PeriodTemplateRecord
        {
            PeriodId = periodId,
            Version = 1,
            PayloadJson = old.ToJsonString(),
            CreatedAtUtc = DateTime.UnixEpoch,
            Source = FirstPresetSource(),
        });

        DefaultPeriodSeeder.SeedIfEmpty(repository);
        DefaultPeriodSeeder.SeedIfEmpty(repository); // idempotente: não cria uma terceira versão

        var latest = repository.FindLatestVersion(periodId)!;
        Assert.Equal(2, latest.Version);
        var upgraded = JsonNode.Parse(latest.PayloadJson)!.AsObject();
        Assert.NotNull(upgraded["Presentation"]?["Era"]);
        upgraded.Remove("Presentation");
        Assert.True(JsonNode.DeepEquals(old, upgraded), "o resto do payload gravado tem de ficar intacto");
    }

    [Fact]
    public void SeedIfEmpty_never_touches_a_period_registered_by_an_operator_with_a_preset_id()
    {
        var (repository, _) = EmptyRepository();
        var payload = JsonNode.Parse(SeededPayloads()[0])!.AsObject();
        payload.Remove("Presentation");
        repository.Save(new LivingWorld.Infrastructure.Records.PeriodTemplateRecord
        {
            PeriodId = FirstPresetId(),
            Version = 1,
            PayloadJson = payload.ToJsonString(),
            CreatedAtUtc = DateTime.UnixEpoch,
            Source = "operador",
        });

        DefaultPeriodSeeder.SeedIfEmpty(repository);

        Assert.Equal(1, repository.FindLatestVersion(FirstPresetId())!.Version);
    }

    private static string FirstPresetId() => "vila-pequena";
    private static string FirstPresetSource() => "Vila pequena";

    private static (SqlitePeriodTemplateRepository Repository, WorldDbContext Context) EmptyRepository()
    {
        var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        connection.Open();
        var context = new WorldDbContext(new DbContextOptionsBuilder<WorldDbContext>().UseSqlite(connection).Options);
        context.Database.Migrate();
        return (new SqlitePeriodTemplateRepository(context), context);
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
