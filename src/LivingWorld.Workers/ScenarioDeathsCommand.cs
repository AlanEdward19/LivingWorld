using System.Text.Json.Nodes;
using LivingWorld.Domain.History;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Observation;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Workers;

/// <summary>Modo CLI <c>scenario-deaths &lt;cenario.json&gt; &lt;seed-inicial&gt; &lt;seeds&gt; &lt;anos&gt;</c> (AD-135): roda um cenário JSON
/// em seeds consecutivas e imprime, por seed, mortes de fome e de sede e a população final.</summary>
public static class ScenarioDeathsCommand
{
    private sealed class Counter : IWorldEventSink
    {
        public long Starvation, Dehydration;

        public void Record(WorldEvent evt)
        {
            if (evt.SourceSystem != YearEventTally.DeathSource) return;
            if (evt.Kind == WorldEventKind.Starvation) Starvation++;
            else if (evt.Kind == WorldEventKind.Dehydration) Dehydration++;
        }
    }

    public static int Run(string path, ulong firstSeed, int seeds, int years)
    {
        string json = File.ReadAllText(path);
        var rows = new (long Starv, long Dehyd, int Alive)[seeds];
        Parallel.For(0, seeds, i =>
        {
            var root = JsonNode.Parse(json)!;
            root["Seed"] = firstSeed + (ulong)i;
            var loaded = ScenarioLoaderV2.LoadWorld(root.ToJsonString());
            if (!loaded.IsSuccess) throw new InvalidOperationException(loaded.Error);
            var (world, baseClock) = loaded.Value;
            var counter = new Counter();
            var clock = new WorldClock(baseClock.Systems, baseClock.MaxIterationsPerTick, counter);
            clock.Run(world, (long)years * world.Calendar.HoursPerYear);
            rows[i] = (counter.Starvation, counter.Dehydration, world.AliveNpcIndex.Alive.Count);
        });
        for (int i = 0; i < seeds; i++)
            Console.WriteLine($"seed={firstSeed + (ulong)i} fome={rows[i].Starv} sede={rows[i].Dehyd} vivos={rows[i].Alive}");
        Console.WriteLine($"media fome={rows.Average(r => r.Starv):F2} sede={rows.Average(r => r.Dehyd):F2} vivos={rows.Average(r => r.Alive):F1} extintas={rows.Count(r => r.Alive == 0)}");
        return 0;
    }
}
