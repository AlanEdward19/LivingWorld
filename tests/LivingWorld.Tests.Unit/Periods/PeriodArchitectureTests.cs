namespace LivingWorld.Tests.Unit.Periods;

/// <summary>Fase 13, T9 (PERIOD-04..06): nenhum id de período de referência vira literal de
/// decisão em <c>LivingWorld.Domain</c>/<c>LivingWorld.Simulation</c> — mesmo padrão de
/// <see cref="LivingWorld.Tests.Population.PopulationArchitectureTests"/>. Período é dado
/// (<c>scenarios/periods/*.json</c>), nunca um <c>switch(periodId)</c> ou <c>if</c> no
/// motor.</summary>
public class PeriodArchitectureTests
{
    private static readonly string[] BannedNames = ["prehistoric", "medieval", "modern", "futuristic", "creatures"];

    [Fact]
    public void No_cs_file_under_Domain_or_Simulation_contains_a_banned_period_id_literal()
    {
        var offenders = new List<string>();
        foreach (var file in SourceFiles())
        {
            string text = File.ReadAllText(file);
            foreach (var name in BannedNames)
                if (text.Contains($"\"{name}\"", StringComparison.OrdinalIgnoreCase))
                    offenders.Add($"{file}: \"{name}\"");
        }

        Assert.True(offenders.Count == 0, "literais de período banidos encontrados: " + string.Join(", ", offenders));
    }

    [Fact]
    public void Scanner_flags_a_banned_literal_when_one_is_injected_into_scratch_source()
    {
        const string mutatedSource = """
            namespace LivingWorld.Domain;
            public static class Mutant { public const string Bad = "medieval"; }
            """;

        Assert.Contains(BannedNames, name => mutatedSource.Contains($"\"{name}\"", StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> SourceFiles()
    {
        string repoRoot = FindRepoRoot();
        foreach (var project in new[] { "LivingWorld.Domain", "LivingWorld.Simulation" })
        {
            var dir = Path.Combine(repoRoot, "src", project);
            foreach (var file in Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories))
                if (!file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                    && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                    yield return file;
        }
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "LivingWorld.sln")))
            dir = Directory.GetParent(dir)?.FullName;
        return dir ?? throw new InvalidOperationException("LivingWorld.sln não encontrado a partir de " + AppContext.BaseDirectory);
    }
}
