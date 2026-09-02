using LivingWorld.Domain.Extraordinary;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Extraordinary.Mechanics;
using LivingWorld.Simulation.Extraordinary.Systems;

namespace LivingWorld.Tests.Unit.Extraordinary;

/// <summary>T12 / EVO-20..22: uma amostra por categoria de mecânica da 16.1 —
/// estágio + pelo menos uma mistura cruzada cada. Falha se alguma categoria exigir
/// tratamento especial no motor.</summary>
public sealed class PowerEvolutionCoverageTests
{
    /// <summary>Categorias exigidas pela tasks.md / EVO-22.</summary>
    public static TheoryData<string, string> CategorySamples { get; } = new()
    {
        { "attribute", "attribute.strength:2" },
        { "gravity", "gravity.self:0.5" },
        { "mind", "mind.alter-trait:agreeableness:+10" },
        { "luck", "luck.capacity-bonus:1" },
        { "combat", "combat.strike:10" },
        { "transfer", "transfer.health:5" },
        { "instantiation", "npc.clone:1" },
        { "control", "control.possess:Sleep" },
        { "bond", "bond.oath:npc.health:-5" },
        { "dimensional", "dimension.pocket:1" },
        { "environmental", "environment.temperature:1" },
        { "fauna", "fauna.summon:1" },
        { "flora", "flora.grow:1" },
    };

    [Theory]
    [MemberData(nameof(CategorySamples))]
    public void Stage_resolution_works_for_every_mechanic_category(string category, string effectToken)
    {
        var baseline = Descriptor($"{category}-base", [effectToken], stages: null);
        var staged = Descriptor(
            $"{category}-staged",
            [effectToken],
            stages: [new PowerEvolutionStage(AgeThreshold: null, UseCountThreshold: 3, EffectTokens: [effectToken])]);

        Assert.Equal(0, ExtraordinaryPowerStageSystem.ResolveStageIndex(baseline, ageYears: 99, useCount: 99));
        Assert.Equal(0, ExtraordinaryPowerStageSystem.ResolveStageIndex(staged, ageYears: 10, useCount: 2));
        Assert.Equal(1, ExtraordinaryPowerStageSystem.ResolveStageIndex(staged, ageYears: 10, useCount: 3));

        var effective = ExtraordinaryPowerStageSystem.EffectiveEffects(staged, ageYears: 10, useCount: 3);
        Assert.Equal([effectToken], effective);
    }

    [Theory]
    [MemberData(nameof(CategorySamples))]
    public void Mix_with_attribute_never_throws_and_stays_contract_safe(string category, string effectToken)
    {
        if (category == "attribute")
            return; // cruzado abaixo cobre attribute↔gravity

        var left = Descriptor($"mix-{category}", [effectToken]);
        var right = Descriptor("mix-attribute", ["attribute.strength:1"]);

        PowerDescriptor? mixed = null;
        var ex = Record.Exception(() =>
            mixed = MixDescriptorBuilder.Build(left, right, seed: 42, childId: new NpcId(7)));

        Assert.Null(ex);
        // null = falha segura de contrato (EVO-14); não-null deve passar Prepare
        if (mixed is not null)
            Assert.True(MixDescriptorBuilder.PassesPrepareContract(mixed));
    }

    [Fact]
    public void Attribute_and_gravity_cross_mix_produces_valid_descriptor()
    {
        var a = Descriptor("cov-attr", ["attribute.strength:2"]);
        var b = Descriptor("cov-grav", ["gravity.self:0.5"]);

        var mixed = MixDescriptorBuilder.Build(a, b, seed: 9, childId: new NpcId(2));

        Assert.NotNull(mixed);
        Assert.Contains("attribute.strength:2", mixed!.Effects);
        Assert.Contains("gravity.self:0.5", mixed.Effects);
        Assert.True(MixDescriptorBuilder.PassesPrepareContract(mixed));
    }

    [Fact]
    public void Every_required_category_is_represented_in_the_matrix()
    {
        string[] required =
        [
            "attribute", "gravity", "mind", "luck", "combat", "transfer",
            "instantiation", "control", "bond", "dimensional", "environmental", "fauna", "flora",
        ];

        var present = CategorySamples.Select(row => (string)row[0]).ToHashSet(StringComparer.Ordinal);
        foreach (var category in required)
            Assert.True(present.Contains(category), $"categoria ausente na matriz: {category}");
    }

    private static PowerDescriptor Descriptor(
        string id,
        IReadOnlyList<string> effects,
        IReadOnlyList<PowerEvolutionStage>? stages = null) =>
        new(
            id, "coverage-source", effects, "Active", [], "Guaranteed",
            [], [], [], [], Stages: stages);
}
