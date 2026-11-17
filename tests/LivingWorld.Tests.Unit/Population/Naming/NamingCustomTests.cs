using System.Text.Json.Nodes;
using LivingWorld.Domain.Population;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Population.Naming;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Population.Naming;

/// <summary>AD-183 (item 185 do 08d): o cenário declara o costume de nome por cultura; ausente, vale o sorteio da cultura.</summary>
public class NamingCustomTests
{
    private static JsonObject DefaultJson()
    {
        var dir = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(dir, "LivingWorld.sln"))) dir = Directory.GetParent(dir)!.FullName;
        var node = JsonNode.Parse(File.ReadAllText(Path.Combine(dir, "scenarios", "default.json")))!.AsObject();
        node["Seed"] = 3;
        return node;
    }

    private static CultureId CultureOf(WorldState world) => world.Npcs.First().Culture;

    [Fact]
    public void Declared_custom_overrides_what_the_culture_draws()
    {
        var plain = ScenarioLoaderV2.LoadWorld(DefaultJson().ToJsonString()).Value.World;
        var culture = CultureOf(plain);
        var drawn = CultureNaming.Of(plain, culture);
        var node = DefaultJson();
        node["Naming"] = JsonNode.Parse($$"""
            {"Cultures": [{"Culture": {{culture.Id}}, "Lineage": "{{(drawn.Lineage == Lineage.Patrilineal ? "Matrilineal" : "Patrilineal")}}",
              "SpouseTakesName": {{(!drawn.SpouseTakesName).ToString().ToLowerInvariant()}}, "AncestorNameChance": 1.0, "DaughterCity": "Founder"}]}
            """);

        var loaded = ScenarioLoaderV2.LoadWorld(node.ToJsonString());
        Assert.True(loaded.IsSuccess, loaded.Error);
        var declared = CultureNaming.Of(loaded.Value.World, culture);

        Assert.NotEqual(drawn.Lineage, declared.Lineage);
        Assert.NotEqual(drawn.SpouseTakesName, declared.SpouseTakesName);
        Assert.Equal((1.0, true, false), (declared.AncestorNameChance, declared.DaughterHonorsFounder, declared.DaughterTakesMotherName));
        Assert.Equal(drawn.Syllables, declared.Syllables);
    }

    [Fact]
    public void Custom_survives_the_snapshot()
    {
        var node = DefaultJson();
        node["Naming"] = JsonNode.Parse("""{"Cultures": [{"Culture": 1, "AncestorNameChance": 0.9}]}""");
        var world = ScenarioLoaderV2.LoadWorld(node.ToJsonString()).Value.World;

        var reopened = WorldSnapshot.Deserialize(WorldSnapshot.Serialize(world));

        Assert.Equal(0.9, reopened.FamilyRules.Names.CustomOf(1)!.AncestorNameChance);
    }

    [Theory]
    [InlineData("""{"Cultures": [{"Culture": 1, "Lineage": "Avuncular"}]}""", "Lineage")]
    [InlineData("""{"Cultures": [{"Culture": 1, "AncestorNameChance": 1.5}]}""", "AncestorNameChance")]
    [InlineData("""{"Cultures": [{"Culture": 1, "DaughterCity": "Rio"}]}""", "DaughterCity")]
    [InlineData("""{"Cultures": [{"Culture": 1}, {"Culture": 1}]}""", "repetida")]
    [InlineData("""{"Lineage": "Matrilineal"}""", "Cultures")]
    public void Invalid_custom_refuses_the_scenario(string block, string field)
    {
        var node = DefaultJson();
        node["Naming"] = JsonNode.Parse(block);

        var loaded = ScenarioLoaderV2.LoadWorld(node.ToJsonString());

        Assert.False(loaded.IsSuccess);
        Assert.Contains(field, loaded.Error);
    }

    [Fact]
    public void Without_the_block_the_world_keeps_the_drawn_custom()
    {
        var world = ScenarioLoaderV2.LoadWorld(DefaultJson().ToJsonString()).Value.World;

        Assert.Null(world.FamilyRules.Names.Customs);
    }
}
