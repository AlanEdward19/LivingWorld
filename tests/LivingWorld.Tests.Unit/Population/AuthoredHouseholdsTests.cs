using System.Text.Json.Nodes;
using LivingWorld.Domain.Population;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Population;

public class AuthoredHouseholdsTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "LivingWorld.sln"))) dir = dir.Parent;
        return dir!.FullName;
    }

    private static JsonObject Scenario(int population, JsonNode? authored)
    {
        var root = JsonNode.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "scenarios", "default.json")))!.AsObject();
        root["InitialPopulation"] = population;
        if (authored is not null) root["AuthoredHouseholds"] = authored;
        return root;
    }

    private static JsonArray Family() => JsonNode.Parse("""
        [{"Members":[
          {"Name":"Ana","Sex":"Female","AgeYears":34,"ProfessionId":1,"Spouse":1,
           "Personality":{"Extroversion":90,"Agreeableness":80,"Conscientiousness":70,"EmotionalStability":60,"Openness":50,"Ambition":40,"Loyalty":30,"Altruism":20,"Impulsivity":10,"RiskAversion":5}},
          {"Name":"Beto","Sex":"Male","AgeYears":36,"ProfessionId":2,"Spouse":0},
          {"Name":"Cai","Sex":"Male","AgeYears":6,"ProfessionId":1,"Mother":0,"Father":1}]}]
        """)!.AsArray();

    [Fact]
    public void Authored_family_is_created_with_exactly_the_given_people()
    {
        var result = ScenarioLoaderV2.LoadWorld(Scenario(0, Family()).ToJsonString());
        Assert.True(result.IsSuccess, result.Error);
        var world = result.Value.World;
        Assert.Equal(3, world.Npcs.Count);
        var ana = world.Npcs.Single(n => n.Name == "Ana");
        var beto = world.Npcs.Single(n => n.Name == "Beto");
        var cai = world.Npcs.Single(n => n.Name == "Cai");
        Assert.Equal(Sex.Female, ana.Sex);
        Assert.Equal(34, ana.AgeYears(world.CurrentDate));
        Assert.Equal(90, ana.Personality.Extroversion);
        Assert.Equal(2, beto.Profession.Id);
        Assert.Equal(beto.Id, ana.Spouse);
        Assert.Equal(ana.Id, beto.Spouse);
        Assert.Equal(ana.Id, cai.MotherId);
        Assert.Equal(beto.Id, cai.FatherId);
        Assert.Equal(ana.Household, cai.Household);
        Assert.Single(world.Households);
    }

    [Fact]
    public void Same_seed_and_block_give_the_same_world()
    {
        string json = Scenario(10, Family()).ToJsonString();
        var a = ScenarioLoaderV2.LoadWorld(json).Value.World;
        var b = ScenarioLoaderV2.LoadWorld(json).Value.World;
        Assert.Equal(a.Npcs.Select(n => (n.Id, n.Name, n.Personality)).ToList(), b.Npcs.Select(n => (n.Id, n.Name, n.Personality)).ToList());
    }

    [Fact]
    public void Without_the_block_the_world_is_the_same_as_before()
    {
        var plain = ScenarioLoaderV2.LoadWorld(Scenario(20, null).ToJsonString()).Value.World;
        var empty = ScenarioLoaderV2.LoadWorld(Scenario(20, new JsonArray()).ToJsonString()).Value.World;
        Assert.Equal(plain.Npcs.Select(n => (n.Id, n.Name, n.Personality)).ToList(), empty.Npcs.Select(n => (n.Id, n.Name, n.Personality)).ToList());
    }

    [Theory]
    [InlineData("ProfessionId", 999, "ProfessionId")]
    [InlineData("AgeYears", 500, "AgeYears")]
    [InlineData("Spouse", 7, "Spouse")]
    public void Invalid_field_fails_naming_the_field(string field, int value, string expected)
    {
        var family = Family();
        family[0]!["Members"]![1]![field] = value;
        var result = ScenarioLoaderV2.LoadWorld(Scenario(0, family).ToJsonString());
        Assert.False(result.IsSuccess);
        Assert.Contains(expected, result.Error);
    }

    [Fact]
    public void Family_without_an_adult_is_refused()
    {
        var family = JsonNode.Parse("""[{"Members":[{"Name":"Cai","Sex":"Male","AgeYears":6,"ProfessionId":1}]}]""")!.AsArray();
        var result = ScenarioLoaderV2.LoadWorld(Scenario(0, family).ToJsonString());
        Assert.False(result.IsSuccess);
        Assert.Contains("adulto", result.Error);
    }

    [Fact]
    public void Unknown_settlement_is_refused()
    {
        var family = Family();
        family[0]!["Settlement"] = "nao-existe";
        var result = ScenarioLoaderV2.LoadWorld(Scenario(0, family).ToJsonString());
        Assert.False(result.IsSuccess);
        Assert.Contains("Settlement", result.Error);
    }
}
