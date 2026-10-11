using System.Text.Json;
using System.Text.Json.Nodes;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.LongRunning.Economy;

/// <summary>economy-depth sobre o cenário ao vivo (<c>scenarios/default.json</c>): dinheiro e cada recurso se conservam
/// a cada amostra diária por 10 anos (C13) e a população do ano 10 não cai abaixo de 90% do medido antes da feature
/// (C14, <c>tests/baselines/live-default-population.json</c>).</summary>
public class EconomyDepthConservationTests
{
    private const long OneYearInHours = 12 * 30 * 24;

    private sealed class LossSink : IWorldEventSink
    {
        public Dictionary<int, long> Lost { get; } = [];

        public void Record(WorldEvent evt)
        {
            if (evt.Kind != WorldEventKind.ResourceLost) return;
            var parts = evt.Payload.Split('|');
            Lost[int.Parse(parts[1])] = Lost.GetValueOrDefault(int.Parse(parts[1])) + long.Parse(parts[2]);
        }
    }

    private static (WorldState World, WorldClock Clock, LossSink Sink) Load(int seed, int population)
    {
        var node = JsonNode.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "scenarios", "default.json")))!.AsObject();
        node["Seed"] = seed;
        node["InitialPopulation"] = population;
        var loaded = ScenarioLoaderV2.LoadWorld(node.ToJsonString());
        Assert.True(loaded.IsSuccess, loaded.Error);
        var sink = new LossSink();
        return (loaded.Value.World, new WorldClock(loaded.Value.Clock.Systems, sink: sink), sink);
    }

    private static long TotalMoney(WorldState world) =>
        world.Npcs.Sum(n => n.Wallet.Amount)
        + world.Workplaces.Sum(w => w.Treasury.Amount)
        + world.Cities.Sum(c => c.Treasury.Amount);

    private static long Stocked(WorldState world, ResourceType resource) =>
        world.Workplaces.Sum(w => w.Stock.GetValueOrDefault(resource))
        + world.Households.Sum(h => h.Stock.GetValueOrDefault(resource))
        + world.Cities.Sum(c => c.Stock.GetValueOrDefault(resource));

    [Fact]
    public void Money_and_every_resource_are_conserved_at_every_daily_sample_over_ten_years()
    {
        var (world, clock, sink) = Load(seed: 2, population: 40);
        long initialMoney = TotalMoney(world);
        var resources = new[] { 1, 2, 3, 5 }.Select(id => new ResourceType(id)).ToList();
        var initialStock = resources.ToDictionary(r => r, r => Stocked(world, r));

        for (long tick = 1; tick <= 10 * OneYearInHours; tick++)
        {
            clock.Tick(world);
            if (tick % 24 != 0) continue;

            long expectedMoney = initialMoney + world.MoneyMinted.Amount - world.MoneyDestroyed.Amount;
            Assert.True(expectedMoney == TotalMoney(world),
                $"tick {tick}: dinheiro {TotalMoney(world)} != esperado {expectedMoney}");

            foreach (var resource in resources)
            {
                long left = initialStock[resource] + world.ResourceProduced.GetValueOrDefault(resource);
                long right = world.ResourceConsumed.GetValueOrDefault(resource) + Stocked(world, resource)
                    + sink.Lost.GetValueOrDefault(resource.Id);
                Assert.True(left == right, $"tick {tick}, recurso {resource.Id}: {left} != {right}");
            }
        }
    }

    [Fact]
    public void Live_world_population_stays_within_90_percent_of_the_baseline()
    {
        var baseline = JsonSerializer.Deserialize<int[]>(
            File.ReadAllText(Path.Combine(RepoRoot(), "tests", "baselines", "live-default-population.json")))!;
        int total = 0;
        for (int seed = 1; seed <= baseline.Length; seed++)
        {
            var (world, clock, _) = Load(seed, population: 20);
            clock.Run(world, 10 * OneYearInHours);
            total += world.Npcs.Count(n => n.IsAlive);
        }

        Assert.True(total >= 0.9 * baseline.Sum(), $"populacao do ano 10 somada {total} < 90% de {baseline.Sum()}");
    }

    private static string RepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "LivingWorld.sln")))
            dir = Directory.GetParent(dir)?.FullName;
        return dir ?? throw new InvalidOperationException("LivingWorld.sln nao encontrado");
    }
}
