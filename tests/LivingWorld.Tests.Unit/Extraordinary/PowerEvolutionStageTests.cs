using LivingWorld.Domain;

namespace LivingWorld.Tests.Extraordinary;

public sealed class PowerEvolutionStageTests
{
    [Fact]
    public void Stage_accepts_age_threshold_only()
    {
        var stage = new PowerEvolutionStage(10, null, ["attribute.strength:1"]);

        Assert.Equal(10, stage.AgeThreshold);
        Assert.Null(stage.UseCountThreshold);
        Assert.Equal(["attribute.strength:1"], stage.EffectTokens);
    }

    [Fact]
    public void Stage_accepts_use_count_threshold_only()
    {
        var stage = new PowerEvolutionStage(null, 5, ["attribute.strength:2"]);

        Assert.Null(stage.AgeThreshold);
        Assert.Equal(5, stage.UseCountThreshold);
        Assert.Equal(["attribute.strength:2"], stage.EffectTokens);
    }

    [Fact]
    public void Stage_accepts_both_age_and_use_thresholds()
    {
        var stage = new PowerEvolutionStage(20, 10, ["attribute.strength:3"]);

        Assert.Equal(20, stage.AgeThreshold);
        Assert.Equal(10, stage.UseCountThreshold);
        Assert.Equal(["attribute.strength:3"], stage.EffectTokens);
    }

    [Fact]
    public void Descriptor_without_stages_is_null_safe_and_unchanged()
    {
        var descriptor = BaselineDescriptor();

        Assert.Null(descriptor.Stages);
        Assert.Equal(["attribute.strength:1"], descriptor.Effects);
    }

    [Fact]
    public void Descriptor_with_stages_preserves_baseline_fields()
    {
        var stages = new[]
        {
            new PowerEvolutionStage(10, null, ["attribute.strength:2"]),
            new PowerEvolutionStage(null, 5, ["attribute.strength:3"]),
            new PowerEvolutionStage(30, 15, ["attribute.strength:4"]),
        };
        var descriptor = BaselineDescriptor(stages);

        Assert.Equal(stages, descriptor.Stages);
        Assert.Equal("test-power", descriptor.Id);
        Assert.Equal(["attribute.strength:1"], descriptor.Effects);
    }

    private static PowerDescriptor BaselineDescriptor(IReadOnlyList<PowerEvolutionStage>? stages = null) =>
        new(
            "test-power",
            "test-source",
            ["attribute.strength:1"],
            "Active",
            [],
            "Guaranteed",
            [],
            [],
            [],
            [],
            Stages: stages);
}
