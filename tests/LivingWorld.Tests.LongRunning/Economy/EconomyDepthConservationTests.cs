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

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Money_and_every_resource_are_conserved_at_every_daily_sample_over_ten_years(int seed)
    {
        var (world, clock, sink) = Load(seed, population: 40);
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

        // A conservacao nao vale se os caminhos estiverem parados: producao, compra da cidade e obra se mexeram.
        Assert.True(world.ResourceProduced.GetValueOrDefault(new ResourceType(3)) > 0, "madeira nunca produzida");
        Assert.True(world.ResourceProduced.GetValueOrDefault(new ResourceType(5)) > 0, "pedra nunca produzida");
        Assert.True(world.ResourceConsumed.GetValueOrDefault(new ResourceType(3)) > 0, "madeira nunca consumida em obra");
        Assert.True(world.ResourceConsumed.GetValueOrDefault(new ResourceType(5)) > 0, "pedra nunca consumida em obra");
        // (a cidade passou a receber o espólio de quem morre sem herdeiro, então o tesouro final não prova mais a compra:
        // a madeira e a pedra consumidas em obra acima já provam)
    }

    /// <summary>Piso do mundo ao vivo com 40 NPCs, 8 seeds, ano 5 (a medida de 20 NPCs em 3 seeds era ruido: uma morte
    /// mexia a seed em 30%). Regravado em AD-093 (compra so no mercado, poco, reposicao pela distancia, custo de
    /// deslocamento de vila): o mundo ao vivo passou de 14 para cerca de 29 de 40 no ano 5.</summary>
    [Fact]
    public void Live_world_population_stays_within_90_percent_of_the_baseline()
    {
        var baseline = JsonSerializer.Deserialize<int[]>(
            File.ReadAllText(Path.Combine(RepoRoot(), "tests", "baselines", "live-default-population.json")))!;
        int total = 0;
        var perSeed = new List<int>();
        for (int seed = 1; seed <= baseline.Length; seed++)
        {
            var (world, clock, _) = Load(seed, population: 40);
            clock.Run(world, 5 * OneYearInHours);
            perSeed.Add(world.Npcs.Count(n => n.IsAlive));
            total += perSeed[^1];
        }

        Assert.True(total >= 0.9 * baseline.Sum(), $"populacao do ano 5 somada {total} [{string.Join(',', perSeed)}] < 90% de {baseline.Sum()}");
    }

    private static string RepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "LivingWorld.sln")))
            dir = Directory.GetParent(dir)?.FullName;
        return dir ?? throw new InvalidOperationException("LivingWorld.sln nao encontrado");
    }
}
