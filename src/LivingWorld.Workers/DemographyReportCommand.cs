using LivingWorld.Simulation.Observation;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Workers;

/// <summary>Modo CLI <c>demography-report &lt;seed-inicial&gt; &lt;seeds&gt; &lt;anos&gt; &lt;aquecimento&gt;</c> (AD-130): roda o cenário
/// padrão de código (<see cref="ScenarioRunner.Create"/>) em seeds consecutivas, em paralelo, e imprime natalidade,
/// mortalidade, 1q0, e0, intervalo entre partos, razão de sexo e calendário dos nascimentos.</summary>
public static class DemographyReportCommand
{
    public static int Run(ulong firstSeed, int seeds, int years, int warmupYears)
    {
        var runs = new DemographyReport.SeedResult[seeds];
        Parallel.For(0, seeds, i => runs[i] = DemographyReport.Run(firstSeed + (ulong)i, years, warmupYears));
        Console.Write(DemographyReport.Summarize(runs, years, warmupYears, ScenarioRunner.DefaultCalendar.HoursPerYear));
        return 0;
    }
}
