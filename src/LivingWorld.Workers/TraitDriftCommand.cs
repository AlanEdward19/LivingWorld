using System.Globalization;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Workers;

/// <summary>Experimento do traço neutro (AD-163, backlog 08 item 2): <c>trait-drift &lt;seed-inicial&gt; &lt;seeds&gt; &lt;anos&gt;</c>.
/// Por seed, três mundos: o real, o real sem a saúde na escolha de parceiro e o neutro (escolha ao acaso, sem seleção pela
/// vitalidade), como o teste FAM-32. Imprime a diferença do CV da abertura (traço que não entra em nada) real − neutro e
/// sem-saúde − neutro, com média, erro-padrão e o par.</summary>
public static class TraitDriftCommand
{
    public static int Run(ulong firstSeed, int seeds, int years)
    {
        var real = new double[seeds];
        var noHealth = new double[seeds];
        Parallel.For(0, seeds, i =>
        {
            ulong seed = firstSeed + (ulong)i;
            var neutralRules = ScenarioRunner.DefaultFamilyRules with { NeutralDriftEnabled = true, VitalityMortalitySelectionEnabled = false };
            double cvReal = Openness(seed, ScenarioRunner.DefaultFamilyRules, years);
            double cvNoHealth = Openness(seed, ExperimentSwitch.WithoutHealth(ScenarioRunner.DefaultFamilyRules), years);
            double cvNeutral = Openness(seed, neutralRules, years);
            real[i] = cvReal - cvNeutral;
            noHealth[i] = cvNoHealth - cvNeutral;
        });
        var inv = CultureInfo.InvariantCulture;
        for (int i = 0; i < seeds; i++)
            Console.WriteLine(string.Create(inv, $"deriva seed={firstSeed + (ulong)i} real-neutro={real[i]:F5} semsaude-neutro={noHealth[i]:F5}"));
        Console.WriteLine(string.Create(inv, $"deriva_resumo seeds={seeds} anos={years} real-neutro={real.Average():F5}±{Se(real):F5} semsaude-neutro={noHealth.Average():F5}±{Se(noHealth):F5} par(semsaude-real)={real.Zip(noHealth, (a, b) => b - a).Average():F5}±{Se(real.Zip(noHealth, (a, b) => b - a).ToArray()):F5}"));
        return 0;
    }

    private static double Openness(ulong seed, LivingWorld.Domain.Population.Family.FamilyRules rules, int years)
    {
        var (world, clock) = ScenarioRunner.Create(seed, familyRules: rules);
        for (int y = 0; y < years && world.AliveNpcIndex.Alive.Count > 0; y++) clock.Run(world, world.Calendar.HoursPerYear);
        var values = world.Npcs.Where(n => n.IsAlive).Select(n => (double)n.Personality.Openness).ToArray();
        if (values.Length == 0) return 0;
        double mean = values.Average();
        return mean == 0 ? 0 : Math.Sqrt(values.Sum(v => (v - mean) * (v - mean)) / values.Length) / mean;
    }

    private static double Se(double[] xs)
    {
        double m = xs.Average();
        return Math.Sqrt(xs.Sum(x => (x - m) * (x - m)) / Math.Max(1, xs.Length - 1) / xs.Length);
    }
}
