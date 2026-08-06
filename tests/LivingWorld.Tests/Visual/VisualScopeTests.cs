using LivingWorld.Api.Visual;

namespace LivingWorld.Tests.Visual;

/// <summary>Fase 15, T1 (VTT-01): <see cref="VisualScope"/> deve produzir uma chave estável
/// e endereçável por escopo, usada por subscribe/replay realtime (VTT-02, VTT-10).</summary>
public class VisualScopeTests
{
    [Fact]
    public void ScopeKey_for_world_scope_is_the_fixed_literal_world()
    {
        var scope = new VisualScope(VisualScopeKind.World, RefId: "");

        Assert.Equal("world", scope.ScopeKey);
    }

    [Fact]
    public void ScopeKey_for_city_scope_embeds_the_city_ref_id()
    {
        var scope = new VisualScope(VisualScopeKind.City, RefId: "42");

        Assert.Equal("city:42", scope.ScopeKey);
    }

    [Fact]
    public void ScopeKey_for_interior_scope_embeds_the_interior_ref_id()
    {
        var scope = new VisualScope(VisualScopeKind.Interior, RefId: "building-7");

        Assert.Equal("interior:building-7", scope.ScopeKey);
    }

    [Fact]
    public void Distinct_ref_ids_of_the_same_kind_produce_distinct_scope_keys()
    {
        var cityA = new VisualScope(VisualScopeKind.City, RefId: "1");
        var cityB = new VisualScope(VisualScopeKind.City, RefId: "2");

        Assert.NotEqual(cityA.ScopeKey, cityB.ScopeKey);
    }
}
