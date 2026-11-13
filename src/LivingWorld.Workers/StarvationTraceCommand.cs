using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Observation;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Workers;

/// <summary>Modo CLI <c>starvation-trace &lt;seed-inicial&gt; &lt;seeds&gt; &lt;anos&gt;</c> (AD-140): uma linha por morte de fome do cenário
/// padrão com a situação do morto e da casa no instante da morte (idade, mês, casa, despensa, carteiras, emprego, mãe, mercado,
/// tesouro, doença). Só lê o mundo.</summary>
public static class StarvationTraceCommand
{
    public static int Run(ulong firstSeed, int seeds, int years)
    {
        var lines = new List<string>();
        Parallel.For(0, seeds, i =>
        {
            ulong seed = firstSeed + (ulong)i;
            var (world, baseClock) = ScenarioRunner.Create(seed, familyRules: PressureSwitch.FamilyRules());
            var clock = new WorldClock(baseClock.Systems, baseClock.MaxIterationsPerTick, new StarvationTrace(world, seed, lines));
            for (int y = 0; y < years && world.AliveNpcIndex.Alive.Count > 0; y++)
            {
                clock.Run(world, world.Calendar.HoursPerYear);
                lock (lines) lines.Add($"{seed} {y + 1:D3} money {MoneyTrace.Line(world)}");
            }
        });
        lines.Sort(StringComparer.Ordinal);
        foreach (var l in lines) Console.WriteLine(l);
        return 0;
    }
}
