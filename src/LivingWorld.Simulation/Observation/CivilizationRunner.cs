using System.Text.Json.Nodes;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Simulation.Observation;

/// <summary>Roda um cenário headless e sem persistência por N anos, amostrando o mundo ao fim de cada um
/// (issue #112 fatia A). Cada chamada monta o próprio mundo, relógio e sink: nada é compartilhado entre
/// chamadas, então seeds podem rodar em paralelo.</summary>
public static class CivilizationRunner
{
    /// <summary><paramref name="afterYear"/> recebe o mundo depois de cada amostra (asserts de teste).</summary>
    public static SeedRun Run(
        string scenarioJson, ulong seed, int years,
        int ageBandYears = YearEventTally.DefaultAgeBandYears, Action<WorldState, WorldYearSample>? afterYear = null,
        Action<WorldState>? onStart = null)
    {
        var root = JsonNode.Parse(scenarioJson)!.AsObject();
        root["Seed"] = seed;
        var loaded = ScenarioLoaderV2.LoadWorld(root.ToJsonString());
        if (!loaded.IsSuccess) throw new InvalidOperationException(loaded.Error);

        var (world, baseClock) = loaded.Value;
        var tally = new YearEventTally(world, ageBandYears);
        var clock = new WorldClock(baseClock.Systems, baseClock.MaxIterationsPerTick, tally);
        var observer = new CivilizationObserver(world, tally);
        onStart?.Invoke(world);
        var samples = new List<WorldYearSample>(years);
        for (int year = 1; year <= years; year++)
        {
            clock.Run(world, world.Calendar.HoursPerYear);
            var sample = observer.Sample(world);
            samples.Add(sample);
            afterYear?.Invoke(world, sample);
        }

        return new SeedRun(seed, samples);
    }
}
