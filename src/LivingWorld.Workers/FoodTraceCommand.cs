using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Observation;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Workers;

/// <summary>Modo CLI <c>food-trace &lt;seed&gt; &lt;anos&gt;</c> (AD-135): uma linha por mês do cenário padrão com vivos, lavradores,
/// estado do campo, celeiro, semente, venda, despensas, produção, consumo e mortes de fome.</summary>
public static class FoodTraceCommand
{
    public static int Run(ulong seed, int years, string? scenarioPath = null)
    {
        var (world, baseClock) = scenarioPath is null ? ScenarioRunner.Create(seed) : Load(scenarioPath, seed);
        var tally = new FoodTally(world);
        var clock = new WorldClock(baseClock.Systems, baseClock.MaxIterationsPerTick, tally);
        var food = new ResourceType(world.EconomyRules.FoodResourceId);
        Console.WriteLine("ano mes vivos lavr status desenv celeiro semente pe venda casas prod cons fome clima coelhos lobos plantas preco_trigo/agua dinheiro");
        for (int m = 0; m < years * 12; m++)
        {
            clock.Run(world, world.Calendar.HoursPerMonth);
            tally.SampleMonth();
            var farm = world.Workplaces.First(w => w.LocationType.Id == 1);
            int farms = world.Workplaces.Count(w => w.LocationType.Id == 1);
            var field = world.CropBatches.FirstOrDefault(c => c.Status != CropStatus.Harvested);
            long homes = world.Households.Sum(h => h.Stock.GetValueOrDefault(food));
            int i = tally.ProducedByMonth.Count - 1;
            Console.WriteLine(
                $"{m / 12} {m % 12} {world.AliveNpcIndex.Alive.Count}/{farms} {farm.Employees.Count} {field?.Status} {field?.Development:F0} {field?.Barn} {field?.SeedReserve} {field?.Standing} " +
                $"{farm.Stock.GetValueOrDefault(food)} {homes} {tally.ProducedByMonth[i]} {tally.ConsumedByMonth[i]} {tally.StarvationByMonth[i]} {world.ClimateFactorAt(farm.Location, world.CurrentDate.TotalHours - 1)} " +
                $"{world.Fauna.Count(a => a.IsAlive && a.Species == "rabbit")} {world.Fauna.Count(a => a.IsAlive && a.Species == "wolf")} {world.Flora.Count} " +
                $"preco={farm.Prices.GetValueOrDefault(food)}/{farm.Prices.GetValueOrDefault(new ResourceType(world.EconomyRules.WaterResourceId))} {MoneyTrace.Line(world)}");
            if (world.AliveNpcIndex.Alive.Count == 0) break;
        }

        return 0;
    }

    private static (WorldState, WorldClock) Load(string path, ulong seed)
    {
        var root = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!;
        root["Seed"] = seed;
        var loaded = ScenarioLoaderV2.LoadWorld(root.ToJsonString());
        if (!loaded.IsSuccess) throw new InvalidOperationException(loaded.Error);
        return loaded.Value;
    }
}
