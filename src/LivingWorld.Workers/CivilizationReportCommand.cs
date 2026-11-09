using LivingWorld.Simulation.Observation;

namespace LivingWorld.Workers;

/// <summary>Modo CLI <c>civ-report &lt;scenario-path&gt; &lt;seed-inicial&gt; &lt;seeds&gt; &lt;anos&gt; &lt;saida.json&gt;</c> (issue #112
/// fatia B): roda seeds consecutivas em paralelo (cada mundo é independente: própria instância de mundo, relógio e sink),
/// amostra todo ano e grava o relatório. Sem persistência e sem duração no JSON.</summary>
public static class CivilizationReportCommand
{
    public static int Run(string scenarioPath, ulong firstSeed, int seeds, int years, string outputPath)
    {
        string json = BuildJson(scenarioPath, firstSeed, seeds, years);
        string? directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(outputPath, json);
        Console.WriteLine($"civ-report: {seeds} seeds x {years} anos -> {outputPath}");
        return 0;
    }

    public static string BuildJson(string scenarioPath, ulong firstSeed, int seeds, int years, int degreeOfParallelism = -1)
    {
        if (seeds <= 0) throw new ArgumentOutOfRangeException(nameof(seeds), "seeds deve ser > 0");
        if (years <= 0) throw new ArgumentOutOfRangeException(nameof(years), "anos deve ser > 0");

        string scenario = File.ReadAllText(scenarioPath);
        var runs = new SeedRun[seeds];
        Parallel.For(0, seeds, new ParallelOptions { MaxDegreeOfParallelism = degreeOfParallelism },
            i => runs[i] = CivilizationRunner.Run(scenario, firstSeed + (ulong)i, years));
        return CivilizationReport.ToJson(Path.GetFileName(scenarioPath), runs);
    }
}
