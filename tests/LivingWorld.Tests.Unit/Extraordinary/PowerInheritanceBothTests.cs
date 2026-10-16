using LivingWorld.Domain.Extraordinary;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Extraordinary.Inheritance;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Extraordinary;

/// <summary>T8 / EVO-11: caminho "ambos" — descritores completos e independentes.</summary>
public sealed class PowerInheritanceBothTests
{
    [Fact]
    public void ApplyBoth_returns_both_parent_descriptors_unchanged_and_independent()
    {
        var powerA = Descriptor("power-a", "source.a", ["effect.a:1"], ["cost.a:1"]);
        var powerB = Descriptor("power-b", "source.b", ["effect.b:2"], ["cost.b:2"]);
        var parentA = new List<PowerDescriptor> { powerA };
        var parentB = new List<PowerDescriptor> { powerB };

        var child = PowerInheritanceResolver.ApplyBoth(parentA, parentB);

        Assert.Equal(2, child.Count);
        Assert.Equal(powerA, child[0]);
        Assert.Equal(powerB, child[1]);
        Assert.Same(powerA, child[0]);
        Assert.Same(powerB, child[1]);

        // Lista do filho é independente — mutar a lista dos pais não altera o resultado.
        parentA.Clear();
        parentB.Clear();
        Assert.Equal(2, child.Count);
        Assert.Equal("power-a", child[0].Id);
        Assert.Equal("power-b", child[1].Id);
        Assert.Equal(["effect.a:1"], child[0].Effects);
        Assert.Equal(["effect.b:2"], child[1].Effects);
    }

    [Fact]
    public void ApplyBoth_preserves_both_when_parents_share_same_mechanic_axis()
    {
        // Edge: ambos têm attribute.strength — Both NÃO funde (só Mixed funde).
        var powerA = Descriptor("str-a", "src", ["attribute.strength:3"], []);
        var powerB = Descriptor("str-b", "src", ["attribute.strength:7"], []);

        var child = PowerInheritanceResolver.ApplyBoth([powerA], [powerB]);

        Assert.Equal(2, child.Count);
        Assert.Equal(powerA, child[0]);
        Assert.Equal(powerB, child[1]);
        Assert.NotEqual(child[0].Id, child[1].Id);
    }

    [Fact]
    public void ResolveDescriptors_with_both_weight_only_is_deterministic()
    {
        var rules = PowerInheritanceRules.Create(1.0, bothWeight: 1, oneOfWeight: 0, mixedWeight: 0).Value!;
        var powerA = Descriptor("power-a", "source.a", ["effect.a"], []);
        var powerB = Descriptor("power-b", "source.b", ["effect.b"], []);
        const ulong seed = 42;
        var childId = new NpcId(7);

        var first = PowerInheritanceResolver.ResolveDescriptors(
            seed, childId, true, true, [powerA], [powerB], rules);
        var second = PowerInheritanceResolver.ResolveDescriptors(
            seed, childId, true, true, [powerA], [powerB], rules);
        var decision = PowerInheritanceResolver.Decide(seed, childId, true, true, rules);

        Assert.True(decision.Occurred);
        Assert.Equal(PowerInheritanceOutcome.Both, decision.Outcome);
        Assert.Equal(first, second);
        Assert.Equal(2, first.Count);
        Assert.Equal(powerA, first[0]);
        Assert.Equal(powerB, first[1]);
    }

    [Fact]
    public void ResolveDescriptors_when_inheritance_does_not_occur_returns_empty()
    {
        var rules = PowerInheritanceRules.Create(0, 1, 0, 0).Value!;
        var powerA = Descriptor("power-a", "source.a", ["effect.a"], []);
        var powerB = Descriptor("power-b", "source.b", ["effect.b"], []);

        var result = PowerInheritanceResolver.ResolveDescriptors(
            1, new NpcId(1), true, true, [powerA], [powerB], rules);

        Assert.Empty(result);
    }

    [Fact]
    public void ResolveDescriptors_world_overload_applies_both_from_carrier_power_ids()
    {
        var powerA = Descriptor("power-a", "source.a", ["effect.a"], []);
        var powerB = Descriptor("power-b", "source.b", ["effect.b"], []);
        var rules = PowerInheritanceRules.Create(1.0, 1, 0, 0).Value!;
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, 5, ScenarioRunner.DefaultMap(5),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules,
            extraordinary: new ExtraordinaryScenarioData(true, [powerA, powerB]),
            extraordinaryCarriers:
            [
                Carrier(new NpcId(1), ["power-a"]),
                Carrier(new NpcId(2), ["power-b"]),
            ]);

        var child = PowerInheritanceResolver.ResolveDescriptors(
            world, childId: new NpcId(10), parentAId: new NpcId(1), parentBId: new NpcId(2),
            rules);

        Assert.Equal(2, child.Count);
        Assert.Equal(powerA, child[0]);
        Assert.Equal(powerB, child[1]);
    }

    private static PowerDescriptor Descriptor(
        string id, string source, IReadOnlyList<string> effects, IReadOnlyList<string> costs) =>
        new(id, source, effects, "Active", costs, "Guaranteed", [], [], [], []);

    private static ExtraordinaryCarrierState Carrier(NpcId id, IReadOnlyList<string> powerIds) =>
        new(id, powerIds, true, "active", new ExtraordinaryAppearanceState(1, "", ""), null, 1);
}
