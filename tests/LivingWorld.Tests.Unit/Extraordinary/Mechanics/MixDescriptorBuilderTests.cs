using LivingWorld.Domain.Extraordinary;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Extraordinary.Mechanics;

namespace LivingWorld.Tests.Unit.Extraordinary.Mechanics;

/// <summary>T10 / EVO-13, EVO-14, EVO-21: MixDescriptorBuilder — recombinação por eixo.</summary>
public sealed class MixDescriptorBuilderTests
{
    [Fact]
    public void Same_mechanic_key_aggregates_magnitude_without_cap()
    {
        var a = Descriptor("power-a", "src-a", ["attribute.strength:2"], []);
        var b = Descriptor("power-b", "src-b", ["attribute.strength:3"], []);

        var mixed = MixDescriptorBuilder.Build(a, b, seed: 42, childId: new NpcId(7));

        Assert.NotNull(mixed);
        Assert.Equal(["attribute.strength:5"], mixed!.Effects);
        Assert.True(MixDescriptorBuilder.PassesPrepareContract(mixed));
    }

    [Fact]
    public void Key_only_in_one_parent_is_included_alongside_other_keys()
    {
        var a = Descriptor("power-a", "src-a", ["attribute.strength:2"], []);
        var b = Descriptor("power-b", "src-b", ["luck.capacity-bonus:1"], []);

        var mixed = MixDescriptorBuilder.Build(a, b, seed: 11, childId: new NpcId(3));

        Assert.NotNull(mixed);
        Assert.Contains("attribute.strength:2", mixed!.Effects);
        Assert.Contains("luck.capacity-bonus:1", mixed.Effects);
        Assert.Equal(2, mixed.Effects.Count);
    }

    [Fact]
    public void Different_mechanics_never_throw_incompatibility()
    {
        var a = Descriptor("gravity-a", "src", ["gravity.self:0"], []);
        var b = Descriptor("luck-b", "src", ["luck.capacity-bonus:2"], []);

        var mixed = MixDescriptorBuilder.Build(a, b, seed: 99, childId: new NpcId(1));

        Assert.NotNull(mixed);
        Assert.Contains("gravity.self:0", mixed!.Effects);
        Assert.Contains("luck.capacity-bonus:2", mixed.Effects);
    }

    [Fact]
    public void Incompatible_args_on_same_key_discard_mix()
    {
        var a = Descriptor("power-a", "src", ["attribute.strength:2"], []);
        var b = Descriptor("power-b", "src", ["attribute.strength:abc"], []);

        var mixed = MixDescriptorBuilder.Build(a, b, seed: 1, childId: new NpcId(1));

        Assert.Null(mixed);
    }

    [Fact]
    public void Unknown_mechanic_token_after_mix_discards_result()
    {
        var a = Descriptor("power-a", "src", ["unknown.token:1"], []);
        var b = Descriptor("power-b", "src", ["unknown.token:2"], []);

        var mixed = MixDescriptorBuilder.Build(a, b, seed: 5, childId: new NpcId(2));

        Assert.Null(mixed);
    }

    [Fact]
    public void Source_conflict_is_resolved_deterministically_by_hash()
    {
        var a = Descriptor("power-a", "source-alpha", ["attribute.strength:1"], []);
        var b = Descriptor("power-b", "source-beta", ["attribute.strength:1"], []);
        const ulong seed = 77;
        var child = new NpcId(19);

        var first = MixDescriptorBuilder.Build(a, b, seed, child);
        var second = MixDescriptorBuilder.Build(a, b, seed, child);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(first!.Id, second!.Id);
        Assert.Equal(first.Source, second.Source);
        Assert.Equal(first.Effects, second.Effects);
        bool preferA = DeterministicChoice.InUnitInterval(seed, child, MixDescriptorBuilder.SourceSalt) < 0.5;
        Assert.Equal(preferA ? "source-alpha" : "source-beta", first.Source);
    }

    [Fact]
    public void Costs_and_acquisition_rules_recombine_by_key()
    {
        var a = new PowerDescriptor(
            "power-a", "src", ["attribute.strength:1"], "Active",
            ["carrier.health:2"], "Guaranteed", [], [], [],
            ["rate:1:event:exposure"]);
        var b = new PowerDescriptor(
            "power-b", "src", ["attribute.strength:1"], "Active",
            ["carrier.health:3"], "Guaranteed", [], [], [],
            ["birth"]);

        var mixed = MixDescriptorBuilder.Build(a, b, seed: 3, childId: new NpcId(4));

        Assert.NotNull(mixed);
        Assert.Equal(["carrier.health:5"], mixed!.Costs);
        Assert.Contains("rate:1:event:exposure", mixed.AcquisitionRules);
        Assert.Contains("birth", mixed.AcquisitionRules);
        Assert.True(MixDescriptorBuilder.PassesPrepareContract(mixed));
    }

    [Fact]
    public void Mixed_id_is_new_and_deterministic()
    {
        var a = Descriptor("power-a", "src", ["attribute.strength:1"], []);
        var b = Descriptor("power-b", "src", ["luck.capacity-bonus:1"], []);
        const ulong seed = 100;
        var child = new NpcId(8);

        var first = MixDescriptorBuilder.Build(a, b, seed, child);
        var second = MixDescriptorBuilder.Build(a, b, seed, child);
        var otherChild = MixDescriptorBuilder.Build(a, b, seed, new NpcId(9));

        Assert.NotNull(first);
        Assert.Equal(first!.Id, second!.Id);
        Assert.NotEqual(first.Id, a.Id);
        Assert.NotEqual(first.Id, b.Id);
        Assert.StartsWith("mixed-power-a-power-b-", first.Id);
        Assert.NotEqual(first.Id, otherChild!.Id);
    }

    [Fact]
    public void Manifestation_condition_picks_non_null_or_hash_on_conflict()
    {
        var with = Descriptor("power-a", "src", ["attribute.strength:1"], [], condition: "hour:0-12");
        var without = Descriptor("power-b", "src", ["attribute.strength:1"], []);

        var mixed = MixDescriptorBuilder.Build(with, without, seed: 2, childId: new NpcId(5));

        Assert.NotNull(mixed);
        Assert.Equal("hour:0-12", mixed!.ManifestationCondition);
    }

    private static PowerDescriptor Descriptor(
        string id,
        string source,
        IReadOnlyList<string> effects,
        IReadOnlyList<string> costs,
        string? condition = null) =>
        new(
            id, source, effects, "Active", costs, "Guaranteed",
            [], [], [], [], ManifestationCondition: condition);
}
