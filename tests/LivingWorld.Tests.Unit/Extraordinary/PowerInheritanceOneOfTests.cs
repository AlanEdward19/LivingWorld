using LivingWorld.Domain.Extraordinary;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Extraordinary.Inheritance;

namespace LivingWorld.Tests.Unit.Extraordinary;

/// <summary>T9 / EVO-12: caminho "um só" — exatamente um pai, cópia fiel.</summary>
public sealed class PowerInheritanceOneOfTests
{
    [Fact]
    public void ApplyOneOf_returns_exactly_one_parents_descriptors_faithfully()
    {
        var powerA = Descriptor("power-a", "source.a", ["effect.a:1"], ["cost.a:1"]);
        var powerB = Descriptor("power-b", "source.b", ["effect.b:2"], ["cost.b:2"]);
        var parentA = new List<PowerDescriptor> { powerA };
        var parentB = new List<PowerDescriptor> { powerB };
        const ulong seed = 42;
        var childId = new NpcId(7);

        var child = PowerInheritanceResolver.ApplyOneOf(parentA, parentB, seed, childId);

        Assert.Single(child);
        Assert.True(
            child[0].Equals(powerA) || child[0].Equals(powerB),
            "Child must be a faithful copy of exactly one parent.");
        Assert.False(
            child[0].Equals(powerA) && child[0].Equals(powerB),
            "Child must not blend both parents.");

        // Lista do filho é independente — mutar as listas dos pais não altera o resultado.
        var chosenId = child[0].Id;
        parentA.Clear();
        parentB.Clear();
        Assert.Single(child);
        Assert.Equal(chosenId, child[0].Id);
    }

    [Fact]
    public void ApplyOneOf_same_seed_and_child_chooses_same_parent()
    {
        var powerA = Descriptor("power-a", "source.a", ["effect.a"], []);
        var powerB = Descriptor("power-b", "source.b", ["effect.b"], []);
        const ulong seed = 99;
        var childId = new NpcId(3);

        var first = PowerInheritanceResolver.ApplyOneOf([powerA], [powerB], seed, childId);
        var second = PowerInheritanceResolver.ApplyOneOf([powerA], [powerB], seed, childId);

        Assert.Equal(first, second);
        Assert.Single(first);
        Assert.Equal(first[0].Id, second[0].Id);
    }

    [Fact]
    public void ApplyOneOf_different_seeds_or_childIds_can_flip_parent()
    {
        var powerA = Descriptor("power-a", "source.a", ["effect.a"], []);
        var powerB = Descriptor("power-b", "source.b", ["effect.b"], []);

        var seenA = false;
        var seenB = false;
        for (ulong seed = 1; seed <= 64 && !(seenA && seenB); seed++)
        {
            for (int child = 1; child <= 32 && !(seenA && seenB); child++)
            {
                var result = PowerInheritanceResolver.ApplyOneOf(
                    [powerA], [powerB], seed, new NpcId(child));
                Assert.Single(result);
                if (result[0].Equals(powerA))
                    seenA = true;
                if (result[0].Equals(powerB))
                    seenB = true;
            }
        }

        Assert.True(seenA && seenB, "Different seeds/childIds should be able to select either parent.");
    }

    [Fact]
    public void ResolveDescriptors_with_one_of_weight_only_is_deterministic()
    {
        var rules = PowerInheritanceRules.Create(1.0, bothWeight: 0, oneOfWeight: 1, mixedWeight: 0).Value!;
        var powerA = Descriptor("power-a", "source.a", ["effect.a"], []);
        var powerB = Descriptor("power-b", "source.b", ["effect.b"], []);
        const ulong seed = 42;
        var childId = new NpcId(7);

        var first = PowerInheritanceResolver.ResolveDescriptors(
            seed, childId, true, true, [powerA], [powerB], rules);
        var second = PowerInheritanceResolver.ResolveDescriptors(
            seed, childId, true, true, [powerA], [powerB], rules);
        var decision = PowerInheritanceResolver.Decide(seed, childId, true, true, rules);
        var expected = PowerInheritanceResolver.ApplyOneOf([powerA], [powerB], seed, childId);

        Assert.True(decision.Occurred);
        Assert.Equal(PowerInheritanceOutcome.OneOf, decision.Outcome);
        Assert.Equal(first, second);
        Assert.Equal(expected, first);
        Assert.Single(first);
    }

    private static PowerDescriptor Descriptor(
        string id, string source, IReadOnlyList<string> effects, IReadOnlyList<string> costs) =>
        new(id, source, effects, "Active", costs, "Guaranteed", [], [], [], []);
}
