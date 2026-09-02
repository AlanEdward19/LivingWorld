using LivingWorld.Domain.History;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Economy;

/// <summary>Fase 5, T9: cunhagem/destruição explícitas e raras (ECON-26/27) — nunca chamadas
/// implicitamente por transação/salário, só por evento nomeado (AD-042).</summary>
public class MoneySupplyTests
{
    private static readonly WorldCalendar Calendar = new(24, 30, 12);

    private sealed class RecordingSink : IWorldEventSink
    {
        public List<WorldEvent> Events { get; } = [];
        public void Record(WorldEvent evt) => Events.Add(evt);
    }

    private static (WorldState World, TickContext Ctx, RecordingSink Sink) BuildWorld(ulong seed = 1)
    {
        var map = ScenarioRunner.DefaultMap(seed);
        var world = new WorldState(
            Calendar, seed, map, ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog, ScenarioRunner.DefaultLifeStageRules);
        var sink = new RecordingSink();
        var ctx = new TickContext(world, world.Rng, world.Scheduler, sink);
        return (world, ctx, sink);
    }

    [Fact]
    public void Mint_increases_MoneyMinted_and_logs_the_named_event()
    {
        var (world, ctx, sink) = BuildWorld();

        world.Mint(ctx, new Money(500), "tesouro-inicial");

        Assert.Equal(new Money(500), world.MoneyMinted);
        var evt = Assert.Single(sink.Events);
        Assert.Equal(WorldEventKind.Minted, evt.Kind);
        Assert.Equal("500|tesouro-inicial", evt.Payload);
    }

    [Fact]
    public void Destroy_requires_sufficient_net_supply_and_logs_the_named_event()
    {
        var (world, ctx, sink) = BuildWorld();
        world.Mint(ctx, new Money(500), "tesouro-inicial");

        var result = world.Destroy(ctx, new Money(200), "imposto");

        Assert.True(result.IsSuccess);
        Assert.Equal(new Money(200), world.MoneyDestroyed);
        Assert.Equal(WorldEventKind.Destroyed, sink.Events[^1].Kind);
    }

    [Fact]
    public void Destroy_fails_without_side_effect_when_net_supply_insufficient()
    {
        var (world, ctx, sink) = BuildWorld();
        world.Mint(ctx, new Money(100), "tesouro-inicial");
        sink.Events.Clear();

        var result = world.Destroy(ctx, new Money(200), "imposto");

        Assert.False(result.IsSuccess);
        Assert.Equal(new Money(0), world.MoneyDestroyed);
        Assert.Empty(sink.Events);
    }
}
