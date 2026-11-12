using LivingWorld.Simulation.Observation;

namespace LivingWorld.Workers;

/// <summary>Modo CLI <c>cognition-report &lt;seed-inicial&gt; &lt;seeds&gt; &lt;anos&gt;</c> (AD-138): luto (trabalho e convívio no ano
/// depois da morte de cônjuge ou filho, contra o ano antes), dispersão do dia no mesmo ofício e lembranças por adulto.</summary>
public static class CognitionReportCommand
{
    public static int Run(ulong firstSeed, int seeds, int years)
    {
        var runs = new CognitionReport.SeedResult[seeds];
        Parallel.For(0, seeds, i => runs[i] = CognitionReport.Run(firstSeed + (ulong)i, years));
        Console.Write(CognitionReport.Summarize(runs));
        return 0;
    }
}
