using System.Reflection;
using LivingWorld.Api.Visual.Scope;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Tests.Unit.Visual.Scope;

/// <summary>Fase 15.1, T2 (VTT2-11): <see cref="ScopeDeltaBuilder.Diff"/> diffa o estado
/// projetado de um escopo entre dois ticks, publicando só o que mudou.</summary>
public class ScopeDeltaBuilderTests
{
    private static readonly NpcId Npc1 = new(1);
    private static readonly NpcId Npc2 = new(2);
    private static readonly NpcId Npc3 = new(3);

    [Fact]
    public void Diff_returns_only_npcs_that_changed_cell()
    {
        var before = new Dictionary<NpcId, CellCoord> { [Npc1] = new(0, 0), [Npc2] = new(5, 5) };
        var after = new Dictionary<NpcId, CellCoord> { [Npc1] = new(1, 0), [Npc2] = new(5, 5) };

        var delta = ScopeDeltaBuilder.Diff(tick: 10, before, after);

        Assert.Equal([new NpcPositionDelta(Npc1, new CellCoord(1, 0))], delta.Moved);
    }

    [Fact]
    public void Diff_includes_ids_removed_from_the_scope()
    {
        var before = new Dictionary<NpcId, CellCoord> { [Npc1] = new(0, 0), [Npc2] = new(5, 5) };
        var after = new Dictionary<NpcId, CellCoord> { [Npc1] = new(0, 0) };

        var delta = ScopeDeltaBuilder.Diff(tick: 10, before, after);

        Assert.Equal([Npc2], delta.Removed);
        Assert.Empty(delta.Moved);
    }

    [Fact]
    public void Identical_state_produces_an_empty_delta()
    {
        var state = new Dictionary<NpcId, CellCoord> { [Npc1] = new(0, 0), [Npc2] = new(5, 5) };

        var delta = ScopeDeltaBuilder.Diff(tick: 10, state, new Dictionary<NpcId, CellCoord>(state));

        Assert.Empty(delta.Moved);
        Assert.Empty(delta.Removed);
    }

    [Fact]
    public void Npc_appearing_for_the_first_time_is_reported_as_moved()
    {
        var before = new Dictionary<NpcId, CellCoord> { [Npc1] = new(0, 0) };
        var after = new Dictionary<NpcId, CellCoord> { [Npc1] = new(0, 0), [Npc3] = new(2, 2) };

        var delta = ScopeDeltaBuilder.Diff(tick: 10, before, after);

        Assert.Equal([new NpcPositionDelta(Npc3, new CellCoord(2, 2))], delta.Moved);
        Assert.Empty(delta.Removed);
    }

    [Fact]
    public void Diff_carries_the_tick_it_was_computed_for()
    {
        var delta = ScopeDeltaBuilder.Diff(tick: 42, new Dictionary<NpcId, CellCoord>(), new Dictionary<NpcId, CellCoord>());

        Assert.Equal(42, delta.Tick);
    }

    [Fact]
    public void Diff_never_receives_WorldState_or_any_layer_builder_type()
    {
        // Estrutural: garante que o caminho de delta não pode recomputar camadas porque NENHUM
        // overload de Diff aceita WorldState (única fonte de dado que os *LayerBuilder
        // consomem) — GetMethod (singular) passou a lançar AmbiguousMatchException assim que um
        // segundo overload apareceu (LivingScopeState, T3); GetMethods cobre todos de uma vez.
        var methods = typeof(ScopeDeltaBuilder).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.Name == nameof(ScopeDeltaBuilder.Diff));

        Assert.NotEmpty(methods);
        Assert.All(methods, method =>
            Assert.DoesNotContain(method.GetParameters(), p => p.ParameterType == typeof(WorldState)));
    }
}
