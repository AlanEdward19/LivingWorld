using System.Globalization;
using LivingWorld.Domain.Behavior;
using LivingWorld.Simulation.Observation;

namespace LivingWorld.Workers;

/// <summary>Sonda do cérebro (AD-160): <c>brain-trace &lt;seed-inicial&gt; &lt;seeds&gt; &lt;anos&gt;</c>. Roda o cenário padrão com o cérebro
/// ligado (<c>LW_BRAIN_VARIANT</c> escolhe a variante, ver <see cref="BrainVariant"/>) e, em cada decisão urgente, compara a escolha com
/// os ganhos do NPC à da utilidade pura no mesmo estado (<see cref="BrainDecisionTally"/>). Imprime, por década, quantas decisões o
/// cérebro trocou, a matriz utilidade → cérebro e alguns casos. Só lê o mundo.</summary>
public static class BrainTraceCommand
{
    public static int Run(ulong firstSeed, int seeds, int years)
    {
        var inv = CultureInfo.InvariantCulture;
        BrainDecisionTally.Diagnose = true;
        var rules = BrainVariant.FromEnvironment() ?? LivingWorld.Domain.Cognition.BrainRules.Default;
        var outputs = new string[seeds];
        Parallel.For(0, seeds, i =>
        {
            ulong seed = firstSeed + (ulong)i;
            var w = new StringWriter(inv);
            var (world, clock) = BrainDecisionTally.CreateWorld(seed, rules);
            for (int decade = 0; decade * 10 < years && world.AliveNpcIndex.Alive.Count > 0; decade++)
            {
                BrainDecisionTally.Reset();
                var tally = BrainDecisionTally.Current;
                tally.MaxSamples = decade == 0 || decade * 10 + 10 >= years ? 6 : 0;
                clock.Run(world, Math.Min(10, years - decade * 10) * world.Calendar.HoursPerYear);
                w.WriteLine($"seed={seed} anos={decade * 10}-{Math.Min(years, decade * 10 + 10)} vivos={world.AliveNpcIndex.Alive.Count} "
                    + $"decisoes={tally.Decisions} trocadas={Pct(tally.Changed, tally.Decisions)} margem_media={tally.MarginSum / Math.Max(1, tally.Decisions):F2} "
                    + $"criticas={tally.Critical} criticas_trocadas={Pct(tally.CriticalChanged, tally.Critical)} "
                    + $"criticas_fora_da_comida={tally.CriticalAwayFromSurvival}");
                var pairs = new List<(long N, string Pair)>();
                for (int a = 0; a < tally.Matrix.GetLength(0); a++)
                    for (int b = 0; b < tally.Matrix.GetLength(1); b++)
                        if (a != b && tally.Matrix[a, b] > 0) pairs.Add((tally.Matrix[a, b], $"{(ActionType)a}->{(ActionType)b}"));
                w.WriteLine("  trocas: " + string.Join(" ", pairs.OrderByDescending(p => p.N).Take(8).Select(p => $"{p.Pair}={p.N}")));
                foreach (var s in tally.Samples) w.WriteLine("  caso " + s);
            }

            outputs[i] = w.ToString();
        });
        foreach (var o in outputs) Console.Write(o);
        return 0;
    }

    private static string Pct(long a, long b) => b == 0 ? "-" : (100.0 * a / b).ToString("F2", CultureInfo.InvariantCulture) + "%";
}

/// <summary>Variantes do cérebro para os relatórios (AD-160): <c>LW_BRAIN_VARIANT</c> vazio = <see cref="LivingWorld.Domain.Cognition.BrainRules.Default"/>.</summary>
public static class BrainVariant
{
    public static LivingWorld.Domain.Cognition.BrainRules? FromEnvironment() =>
        Environment.GetEnvironmentVariable("LW_BRAIN_VARIANT") switch
        {
            "frozen" => LivingWorld.Domain.Cognition.BrainRules.Default with { LearningPerPoint = 0, InheritedNoiseMilli = 0, RearingPullPerSample = 0 },
            "ad142" => LivingWorld.Domain.Cognition.BrainRules.Default with { CriticalNeed = 0, FirstExperienceSetsExpectation = false },
            "body" => LivingWorld.Domain.Cognition.BrainRules.Default with { FirstExperienceSetsExpectation = false }, // só o corpo decide
            "body40" => LivingWorld.Domain.Cognition.BrainRules.Default with { CriticalNeed = 40 }, // AD-160, 3ª tentativa: o corpo decide mais cedo // o experimento como foi juntado
            _ => null,
        };
}
