using LivingWorld.Domain;
using LivingWorld.Simulation;
using LivingWorld.Simulation.Narrative;

namespace LivingWorld.Tests.Narrative;

/// <summary>Fase 12, T2: <see cref="WindowedHistoryAggregator"/> (NARR-05..07).</summary>
public class WindowedHistoryAggregatorTests
{
    [Fact]
    public void TopFacts_orders_facts_by_significance_descending()
    {
        var (world, _) = ScenarioRunner.Create(1, historyRules: HistoryRules.Default);
        var city = new CityId(Guid.NewGuid());
        world.AddFact(new Fact(world.NextFactIdAndAdvance(), 10, WorldEventKind.CourtshipRejected, [], city, 0.2, "low"));
        world.AddFact(new Fact(world.NextFactIdAndAdvance(), 20, WorldEventKind.Death, [], city, 0.9, "high"));
        world.AddFact(new Fact(world.NextFactIdAndAdvance(), 30, WorldEventKind.Marriage, [], city, 0.5, "mid"));

        var top = WindowedHistoryAggregator.TopFacts(world, city, periodStartTick: 0, periodEndTick: 100, topK: 10);

        Assert.Equal(3, top.Count);
        Assert.Equal("high", top[0].Payload);
        Assert.Equal("mid", top[1].Payload);
        Assert.Equal("low", top[2].Payload);
    }

    [Fact]
    public void TopFacts_returns_only_the_K_most_significant_facts()
    {
        var (world, _) = ScenarioRunner.Create(1, historyRules: HistoryRules.Default);
        var city = new CityId(Guid.NewGuid());
        for (int i = 0; i < 5; i++)
        {
            world.AddFact(new Fact(
                world.NextFactIdAndAdvance(), 10 + i, WorldEventKind.Death, [], city, 0.1 * (i + 1), $"f{i}"));
        }

        var top = WindowedHistoryAggregator.TopFacts(world, city, periodStartTick: 0, periodEndTick: 100, topK: 2);

        Assert.Equal(2, top.Count);
        Assert.Equal("f4", top[0].Payload);
        Assert.Equal("f3", top[1].Payload);
    }

    [Fact]
    public void TopFacts_excludes_facts_outside_the_requested_period()
    {
        var (world, _) = ScenarioRunner.Create(1, historyRules: HistoryRules.Default);
        var city = new CityId(Guid.NewGuid());
        world.AddFact(new Fact(world.NextFactIdAndAdvance(), 5, WorldEventKind.Death, [], city, 0.9, "before"));
        world.AddFact(new Fact(world.NextFactIdAndAdvance(), 50, WorldEventKind.Death, [], city, 0.9, "inside"));
        world.AddFact(new Fact(world.NextFactIdAndAdvance(), 200, WorldEventKind.Death, [], city, 0.9, "after"));

        var top = WindowedHistoryAggregator.TopFacts(world, city, periodStartTick: 10, periodEndTick: 100, topK: 10);

        Assert.Single(top);
        Assert.Equal("inside", top[0].Payload);
    }

    [Fact]
    public void TopFacts_excludes_facts_from_other_locations()
    {
        var (world, _) = ScenarioRunner.Create(1, historyRules: HistoryRules.Default);
        var cityA = new CityId(Guid.NewGuid());
        var cityB = new CityId(Guid.NewGuid());
        world.AddFact(new Fact(world.NextFactIdAndAdvance(), 10, WorldEventKind.Death, [], cityA, 0.9, "in-a"));
        world.AddFact(new Fact(world.NextFactIdAndAdvance(), 20, WorldEventKind.Death, [], cityB, 0.9, "in-b"));

        var top = WindowedHistoryAggregator.TopFacts(world, cityA, periodStartTick: 0, periodEndTick: 100, topK: 10);

        Assert.Single(top);
        Assert.Equal("in-a", top[0].Payload);
    }

    [Fact]
    public void TopFacts_never_returns_empty_when_relevant_facts_exist_in_window()
    {
        // NARR-07: quando o agregador encontra fatos relevantes na janela, o resultado não pode
        // vir vazio (o que forçaria o renderer a produzir preenchimento genérico sem citação).
        var (world, _) = ScenarioRunner.Create(1, historyRules: HistoryRules.Default);
        var city = new CityId(Guid.NewGuid());
        world.AddFact(new Fact(world.NextFactIdAndAdvance(), 10, WorldEventKind.Death, [], city, 0.7, "relevant"));

        var top = WindowedHistoryAggregator.TopFacts(world, city, periodStartTick: 0, periodEndTick: 100, topK: 5);

        Assert.NotEmpty(top);
    }

    [Fact]
    public void TopFacts_returns_empty_when_topK_is_zero()
    {
        var (world, _) = ScenarioRunner.Create(1, historyRules: HistoryRules.Default);
        var city = new CityId(Guid.NewGuid());
        world.AddFact(new Fact(world.NextFactIdAndAdvance(), 10, WorldEventKind.Death, [], city, 0.9, "x"));

        var top = WindowedHistoryAggregator.TopFacts(world, city, periodStartTick: 0, periodEndTick: 100, topK: 0);

        Assert.Empty(top);
    }
}
