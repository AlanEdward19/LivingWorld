using System.Text.Json.Nodes;
using LivingWorld.Simulation.Periods;

namespace LivingWorld.Tests.Periods;

public class PeriodDynamicsLoaderTests
{
    private static JsonObject RootWithDynamics(JsonObject dynamics) => new() { ["Dynamics"] = dynamics };

    private static JsonObject ValidDynamics() => new()
    {
        ["ProfessionBiases"] = new JsonArray(new JsonObject { ["ProfessionId"] = 1, ["Weight"] = 2.0 }),
        ["SkillBiases"] = new JsonArray(new JsonObject { ["SkillId"] = 0, ["Weight"] = 1.5 }),
        ["TransformationRules"] = new JsonArray(
            new JsonObject
            {
                ["Kind"] = "Merge",
                ["SourceProfessionIds"] = new JsonArray(1, 2),
                ["TargetProfessionIds"] = new JsonArray(3),
                ["TriggerTick"] = 100,
            }),
    };

    [Fact]
    public void Missing_Dynamics_block_returns_empty_data()
    {
        var result = PeriodDynamicsLoader.Load(new JsonObject().ToJsonString());

        Assert.True(result.IsSuccess, result.Error);
        Assert.Empty(result.Value!.ProfessionBiases);
        Assert.Empty(result.Value!.SkillBiases);
        Assert.Empty(result.Value!.TransformationRules);
    }

    [Fact]
    public void Happy_path_parses_profession_biases_skill_biases_and_transformation_rules()
    {
        var result = PeriodDynamicsLoader.Load(RootWithDynamics(ValidDynamics()).ToJsonString());

        Assert.True(result.IsSuccess, result.Error);
        var data = result.Value!;

        Assert.Single(data.ProfessionBiases);
        Assert.Equal(1, data.ProfessionBiases[0].ProfessionId);
        Assert.Equal(2.0, data.ProfessionBiases[0].Weight);

        Assert.Single(data.SkillBiases);
        Assert.Equal(0, data.SkillBiases[0].SkillId);
        Assert.Equal(1.5, data.SkillBiases[0].Weight);

        Assert.Single(data.TransformationRules);
        var rule = data.TransformationRules[0];
        Assert.Equal(PeriodTransformationKind.Merge, rule.Kind);
        Assert.Equal([1, 2], rule.SourceProfessionIds);
        Assert.Equal([3], rule.TargetProfessionIds);
        Assert.Equal(100, rule.TriggerTick);
    }

    [Fact]
    public void Dynamics_not_an_object_fails_naming_the_field()
    {
        var root = new JsonObject { ["Dynamics"] = "not-an-object" };

        var result = PeriodDynamicsLoader.Load(root.ToJsonString());

        Assert.False(result.IsSuccess);
        Assert.Contains("Dynamics:", result.Error);
    }

    [Fact]
    public void Malformed_json_fails_naming_json()
    {
        var result = PeriodDynamicsLoader.Load("{not valid json");

        Assert.False(result.IsSuccess);
        Assert.StartsWith("json:", result.Error);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    public void Non_positive_profession_bias_weight_fails_naming_the_field(double weight)
    {
        var dynamics = new JsonObject
        {
            ["ProfessionBiases"] = new JsonArray(new JsonObject { ["ProfessionId"] = 1, ["Weight"] = weight }),
        };

        var result = PeriodDynamicsLoader.Load(RootWithDynamics(dynamics).ToJsonString());

        Assert.False(result.IsSuccess);
        Assert.Contains("Dynamics.ProfessionBiases[].Weight", result.Error);
    }

    [Fact]
    public void Missing_SkillId_in_skill_bias_fails_naming_the_field()
    {
        var dynamics = new JsonObject
        {
            ["SkillBiases"] = new JsonArray(new JsonObject { ["Weight"] = 1.0 }),
        };

        var result = PeriodDynamicsLoader.Load(RootWithDynamics(dynamics).ToJsonString());

        Assert.False(result.IsSuccess);
        Assert.Contains("Dynamics.SkillBiases[].SkillId", result.Error);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    public void Non_positive_skill_bias_weight_fails_naming_the_field(double weight)
    {
        var dynamics = new JsonObject
        {
            ["SkillBiases"] = new JsonArray(new JsonObject { ["SkillId"] = 0, ["Weight"] = weight }),
        };

        var result = PeriodDynamicsLoader.Load(RootWithDynamics(dynamics).ToJsonString());

        Assert.False(result.IsSuccess);
        Assert.Contains("Dynamics.SkillBiases[].Weight", result.Error);
    }

    [Fact]
    public void Unknown_transformation_kind_fails_naming_the_field()
    {
        var dynamics = new JsonObject
        {
            ["TransformationRules"] = new JsonArray(new JsonObject { ["Kind"] = "Teleport" }),
        };

        var result = PeriodDynamicsLoader.Load(RootWithDynamics(dynamics).ToJsonString());

        Assert.False(result.IsSuccess);
        Assert.Contains("Dynamics.TransformationRules[].Kind", result.Error);
    }

    [Fact]
    public void Negative_trigger_tick_fails_naming_the_field()
    {
        var dynamics = new JsonObject
        {
            ["TransformationRules"] = new JsonArray(new JsonObject
            {
                ["Kind"] = "Emerge",
                ["TargetProfessionIds"] = new JsonArray(5),
                ["TriggerTick"] = -1,
            }),
        };

        var result = PeriodDynamicsLoader.Load(RootWithDynamics(dynamics).ToJsonString());

        Assert.False(result.IsSuccess);
        Assert.Contains("Dynamics.TransformationRules[].TriggerTick", result.Error);
    }

    [Theory]
    [InlineData("Emerge", new int[] { 1 }, new int[] { 5 })] // Emerge não pode ter source
    [InlineData("Emerge", new int[] { }, new int[] { })] // Emerge exige exatamente 1 target
    [InlineData("Disappear", new int[] { }, new int[] { 5 })] // Disappear não pode ter target
    [InlineData("Disappear", new int[] { 1, 2 }, new int[] { })] // Disappear exige exatamente 1 source
    [InlineData("Merge", new int[] { 1 }, new int[] { 3 })] // Merge exige 2+ sources
    [InlineData("Merge", new int[] { 1, 2 }, new int[] { 3, 4 })] // Merge exige exatamente 1 target
    [InlineData("Split", new int[] { 1, 2 }, new int[] { 3, 4 })] // Split exige exatamente 1 source
    [InlineData("Split", new int[] { 1 }, new int[] { 3 })] // Split exige 2+ targets
    public void Transformation_rule_with_wrong_cardinality_is_rejected_naming_the_rule(
        string kind, int[] sources, int[] targets)
    {
        var dynamics = new JsonObject
        {
            ["TransformationRules"] = new JsonArray(new JsonObject
            {
                ["Kind"] = kind,
                ["SourceProfessionIds"] = new JsonArray(sources.Select(i => (JsonNode)i).ToArray()),
                ["TargetProfessionIds"] = new JsonArray(targets.Select(i => (JsonNode)i).ToArray()),
            }),
        };

        var result = PeriodDynamicsLoader.Load(RootWithDynamics(dynamics).ToJsonString());

        Assert.False(result.IsSuccess);
        Assert.Contains("Dynamics.TransformationRules[]:", result.Error);
        Assert.Contains(kind, result.Error);
    }
}
