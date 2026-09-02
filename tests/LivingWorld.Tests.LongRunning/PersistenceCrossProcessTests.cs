using System.Diagnostics;
using LivingWorld.Simulation;

namespace LivingWorld.Tests;

/// <summary>Task 10: salvar no tick T, reabrir em outro processo e rodar até T+extra dá o mesmo
/// hash de rodar direto — mesmo padrão de <see cref="DeterminismTwoProcessTests"/> (AD-020),
/// via os modos CLI <c>persist-save</c>/<c>persist-resume</c> do LivingWorld.Workers.</summary>
public class PersistenceCrossProcessTests
{
    private const ulong Seed = 42;
    private const long TicksBeforeSave = 300;
    private const long ExtraTicks = 500;

    [Fact]
    public void Saving_at_tick_T_and_resuming_in_another_process_matches_a_continuous_run_to_T_plus_extra()
    {
        string dbPath = Path.Combine(Path.GetTempPath(), $"livingworld-persist-{Guid.NewGuid():N}.db");
        try
        {
            RunProcess($"persist-save {Seed} \"{dbPath}\" {TicksBeforeSave}");
            string resumedHash = RunProcess($"persist-resume \"{dbPath}\" {ExtraTicks}").Trim();

            var (continuousWorld, continuousClock) = ScenarioRunner.Create(Seed);
            continuousClock.Run(continuousWorld, TicksBeforeSave + ExtraTicks);
            string continuousHash = WorldSnapshot.CanonicalHash(continuousWorld);

            Assert.Equal(continuousHash, resumedHash);
        }
        finally
        {
            File.Delete(dbPath);
        }
    }

    private static string RunProcess(string arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{WorkersDllPath}\" {arguments}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("falha ao iniciar processo do cenário");
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        Assert.True(process.ExitCode == 0, $"processo saiu com {process.ExitCode}: {error}");
        return output;
    }

    private static readonly string WorkersDllPath = FindWorkersDll();

    private static string FindWorkersDll()
    {
        var repoRoot = FindRepoRoot();
        var configuration = AppContext.BaseDirectory.Contains($"{Path.DirectorySeparatorChar}Release{Path.DirectorySeparatorChar}")
            ? "Release"
            : "Debug";
        var path = Path.Combine(repoRoot, "src", "LivingWorld.Workers", "bin", configuration, "net10.0", "LivingWorld.Workers.dll");
        if (!File.Exists(path))
            throw new FileNotFoundException($"LivingWorld.Workers.dll não encontrado em {path} — rode bash scripts/build.sh primeiro.", path);
        return path;
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "LivingWorld.sln")))
            dir = Directory.GetParent(dir)?.FullName;
        return dir ?? throw new InvalidOperationException("LivingWorld.sln não encontrado a partir de " + AppContext.BaseDirectory);
    }
}
