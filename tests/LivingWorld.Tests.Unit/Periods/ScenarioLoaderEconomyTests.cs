using System.Text.Json.Nodes;
using LivingWorld.Domain.Geography;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Periods;

/// <summary>economy-depth: o cenário ao vivo (<c>scenarios/default.json</c>) declara a economia de madeira e pedra
/// e o loader a monta inteira — tesouro da cidade (C11), reserva inicial (C11) e consumidores de cada recurso (C3).</summary>
public class ScenarioLoaderEconomyTests
{
    private static JsonObject DefaultJson(int seed = 1)
    {
        var node = JsonNode.Parse(File.ReadAllText(Path.Combine(FindRepoRoot(), "scenarios", "default.json")))!.AsObject();
        node["Seed"] = seed;
        return node;
    }

    [Fact]
    public void Loader_reads_the_city_treasury_declared_in_the_scenario()
    {
        var loaded = ScenarioLoaderV2.LoadWorld(DefaultJson().ToJsonString());

        Assert.True(loaded.IsSuccess, loaded.Error);
        Assert.Equal(20000, loaded.Value.World.ActiveCities().Single().Treasury.Amount);
    }

    [Fact]
    public void Negative_city_treasury_is_rejected_at_load()
    {
        var node = DefaultJson();
        node["Cities"]!.AsArray()[0]!["Treasury"] = -1;

        var loaded = ScenarioLoaderV2.LoadWorld(node.ToJsonString());

        Assert.False(loaded.IsSuccess);
        Assert.Contains("Treasury", loaded.Error);
    }

    [Fact]
    public void Loaded_world_seeds_the_initial_buffer_and_has_living_npcs_after_year_one_in_three_seeds()
    {
        foreach (int seed in new[] { 1, 2, 3 })
        {
            var (world, clock) = ScenarioLoaderV2.LoadWorld(DefaultJson(seed).ToJsonString()).Value;
            var food = new ResourceType(world.EconomyRules.FoodResourceId);
            Assert.All(world.Households, h => Assert.Equal(50L * h.Members.Count, h.Stock[food]));
            Assert.All(world.Npcs, n => Assert.Equal(50, n.Wallet.Amount));

            clock.Run(world, 360 * 24);

            Assert.True(world.Npcs.Any(n => n.IsAlive), $"seed {seed}: ninguem vivo depois do ano 1");
        }
    }

    [Fact]
    public void Every_resource_has_a_producer_and_a_consumer()
    {
        var world = ScenarioLoaderV2.LoadWorld(DefaultJson().ToJsonString()).Value.World;
        var rules = world.EconomyRules;
        var recipes = world.EconomyCatalog.Recipes.Values.ToList();

        var resources = rules.PriceFloor.Keys
            .Concat([rules.FoodResourceId, rules.WaterResourceId])
            .Distinct().ToList();
        Assert.Contains(3, resources);
        Assert.Contains(5, resources);

        var produced = recipes.SelectMany(r => r.Outputs.Keys).ToHashSet();
        var consumed = recipes.SelectMany(r => r.Inputs.Keys)
            .Concat(world.CityCatalog.BuildingRecipes.Values.SelectMany(r => r.Inputs.Keys.Select(k => k.Id)))
            .Concat([rules.FoodResourceId, rules.WaterResourceId]) // comer e beber consomem
            .ToHashSet();

        foreach (int resource in resources)
        {
            Assert.True(produced.Contains(resource), $"recurso {resource} sem produtor");
            Assert.True(consumed.Contains(resource), $"recurso {resource} sem consumidor");
        }
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "LivingWorld.sln")))
            dir = Directory.GetParent(dir)?.FullName;
        return dir ?? throw new InvalidOperationException("LivingWorld.sln nao encontrado");
    }
}
