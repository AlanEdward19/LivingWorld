using LivingWorld.Domain.History;
using LivingWorld.Simulation.History.Causality;

namespace LivingWorld.Tests.Unit.History.Causality;

/// <summary>Issue #113 fatia A (R5): todo kind do enum tem papel causal declarado no código.</summary>
public class CausalPolicyTests
{
    [Fact]
    public void Every_world_event_kind_is_classified_as_root_or_transformation()
    {
        var missing = Enum.GetValues<WorldEventKind>().Where(k => CausalPolicy.RoleOf(k) is null).ToList();

        Assert.True(missing.Count == 0,
            $"Kinds sem classificação em CausalPolicy (transformação ou raiz): {string.Join(", ", missing)}");
    }

    [Fact]
    public void Policy_lists_contain_only_defined_kinds_without_overlap()
    {
        var all = CausalPolicy.TransformationKinds.Concat(CausalPolicy.RootKinds).ToList();

        Assert.All(all, k => Assert.True(Enum.IsDefined(k), $"{k} não existe no enum"));
        Assert.Equal(all.Count, all.Distinct().Count());
        Assert.Equal(Enum.GetValues<WorldEventKind>().Length, all.Count);
    }

    [Theory]
    [InlineData(WorldEventKind.Death, CausalRole.Transformation)]
    [InlineData(WorldEventKind.Evicted, CausalRole.Transformation)]
    [InlineData(WorldEventKind.SettlementFounded, CausalRole.Transformation)]
    [InlineData(WorldEventKind.Birth, CausalRole.Root)]
    [InlineData(WorldEventKind.AuthoredImmigration, CausalRole.Root)]
    public void Boundary_kinds_have_the_expected_role(WorldEventKind kind, CausalRole expected) =>
        Assert.Equal(expected, CausalPolicy.RoleOf(kind));

    [Fact]
    public void IsTransformation_is_true_for_every_transformation_kind() =>
        Assert.All(CausalPolicy.TransformationKinds, k => Assert.True(CausalPolicy.IsTransformation(k), $"{k}"));

    [Fact]
    public void IsTransformation_is_false_for_every_root_kind() =>
        Assert.All(CausalPolicy.RootKinds, k => Assert.False(CausalPolicy.IsTransformation(k), $"{k}"));

    [Fact]
    public void IsTransformation_is_false_for_an_unclassified_kind() =>
        Assert.False(CausalPolicy.IsTransformation((WorldEventKind)9999));

    [Fact]
    public void Unclassified_value_is_reported_as_null_not_defaulted_to_root() =>
        Assert.Null(CausalPolicy.RoleOf((WorldEventKind)9999));
}
