using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Extraordinary;

/// <summary>T7 / EVO-10, EVO-15, EVO-16: rolls de ocorrência e caminho.</summary>
public sealed class PowerInheritanceResolverTests
{
    [Fact]
    public void Decide_without_both_parents_carriers_skips_all_rolls()
    {
        var onlyA = PowerInheritanceResolver.Decide(
            worldSeed: 7, childId: new NpcId(99),
            parentAIsCarrier: true, parentBIsCarrier: false);
        var onlyB = PowerInheritanceResolver.Decide(
            worldSeed: 7, childId: new NpcId(99),
            parentAIsCarrier: false, parentBIsCarrier: true);
        var neither = PowerInheritanceResolver.Decide(
            worldSeed: 7, childId: new NpcId(99),
            parentAIsCarrier: false, parentBIsCarrier: false);

        Assert.False(onlyA.Occurred);
        Assert.Null(onlyA.Outcome);
        Assert.False(onlyB.Occurred);
        Assert.Null(onlyB.Outcome);
        Assert.False(neither.Occurred);
        Assert.Null(neither.Outcome);
    }

    [Fact]
    public void Decide_with_inheritance_chance_zero_never_occurs()
    {
        var rules = PowerInheritanceRules.Create(0, 1, 1, 1).Value!;

        var decision = PowerInheritanceResolver.Decide(
            worldSeed: 42, childId: new NpcId(3),
            parentAIsCarrier: true, parentBIsCarrier: true, rules);

        Assert.False(decision.Occurred);
        Assert.Null(decision.Outcome);
    }

    [Fact]
    public void Decide_with_only_both_weight_always_selects_both_when_occurs()
    {
        var rules = PowerInheritanceRules.Create(1.0, bothWeight: 1, oneOfWeight: 0, mixedWeight: 0).Value!;

        for (long id = 1; id <= 50; id++)
        {
            var decision = PowerInheritanceResolver.Decide(
                worldSeed: 100, childId: new NpcId(id),
                parentAIsCarrier: true, parentBIsCarrier: true, rules);

            Assert.True(decision.Occurred);
            Assert.Equal(PowerInheritanceOutcome.Both, decision.Outcome);
        }
    }

    [Fact]
    public void Decide_with_only_one_of_weight_always_selects_one_of_when_occurs()
    {
        var rules = PowerInheritanceRules.Create(1.0, 0, 1, 0).Value!;

        var decision = PowerInheritanceResolver.Decide(
            worldSeed: 11, childId: new NpcId(8),
            parentAIsCarrier: true, parentBIsCarrier: true, rules);

        Assert.True(decision.Occurred);
        Assert.Equal(PowerInheritanceOutcome.OneOf, decision.Outcome);
    }

    [Fact]
    public void Decide_with_only_mixed_weight_always_selects_mixed_when_occurs()
    {
        var rules = PowerInheritanceRules.Create(1.0, 0, 0, 1).Value!;

        var decision = PowerInheritanceResolver.Decide(
            worldSeed: 11, childId: new NpcId(8),
            parentAIsCarrier: true, parentBIsCarrier: true, rules);

        Assert.True(decision.Occurred);
        Assert.Equal(PowerInheritanceOutcome.Mixed, decision.Outcome);
    }

    [Fact]
    public void Same_seed_and_child_produce_same_outcome()
    {
        var rules = PowerInheritanceRules.Default;

        var a = PowerInheritanceResolver.Decide(
            999, new NpcId(44), true, true, rules);
        var b = PowerInheritanceResolver.Decide(
            999, new NpcId(44), true, true, rules);

        Assert.Equal(a.Occurred, b.Occurred);
        Assert.Equal(a.Outcome, b.Outcome);
    }

    [Fact]
    public void World_overload_uses_carrier_power_ids_for_parent_check()
    {
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, 5, ScenarioRunner.DefaultMap(5),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules,
            extraordinary: new ExtraordinaryScenarioData(true, []),
            extraordinaryCarriers:
            [
                Carrier(new NpcId(1), ["power-a"]),
                Carrier(new NpcId(2), ["power-b"]),
            ]);

        var both = PowerInheritanceResolver.Decide(
            world, childId: new NpcId(10), parentAId: new NpcId(1), parentBId: new NpcId(2),
            rules: PowerInheritanceRules.Create(1, 1, 0, 0).Value!);
        var missingParent = PowerInheritanceResolver.Decide(
            world, childId: new NpcId(10), parentAId: new NpcId(1), parentBId: new NpcId(99),
            rules: PowerInheritanceRules.Create(1, 1, 0, 0).Value!);

        Assert.True(both.Occurred);
        Assert.Equal(PowerInheritanceOutcome.Both, both.Outcome);
        Assert.False(missingParent.Occurred);
    }

    private static ExtraordinaryCarrierState Carrier(NpcId id, IReadOnlyList<string> powerIds) =>
        new(id, powerIds, true, "active", new ExtraordinaryAppearanceState(1, "", ""), null, 1);
}
