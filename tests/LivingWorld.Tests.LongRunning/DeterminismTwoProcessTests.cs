using System.Diagnostics;

namespace LivingWorld.Tests.LongRunning;

/// <summary>Task 8: roda o mesmo cenário em dois processos separados de verdade — .NET
/// randomiza o hash de string por processo, então uma run no mesmo processo não pegaria bug
/// de ordenação de <see cref="Dictionary{TKey,TValue}"/>. Usa o LivingWorld.Workers já
/// referenciado pelo projeto de testes como o processo-cenário (`dotnet &lt;dll&gt; hash seed ticks`).</summary>
public class DeterminismTwoProcessTests
{
    [Fact]
    public void Same_seed_produces_identical_hashes_across_two_separate_processes()
    {
        var (canonicalA, volatileA) = RunInSeparateProcess(seed: 42, ticks: 400);
        var (canonicalB, volatileB) = RunInSeparateProcess(seed: 42, ticks: 400);

        Assert.Equal(canonicalA, canonicalB);
        Assert.Equal(volatileA, volatileB);
    }

    [Fact]
    public void Different_seeds_produce_different_canonical_hashes_across_processes()
    {
        var (canonicalA, _) = RunInSeparateProcess(seed: 42, ticks: 400);
        var (canonicalB, _) = RunInSeparateProcess(seed: 43, ticks: 400);

        Assert.NotEqual(canonicalA, canonicalB);
    }

    private static (string Canonical, string Volatile) RunInSeparateProcess(ulong seed, long ticks)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{WorkersDllPath}\" hash {seed} {ticks}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("falha ao iniciar processo do cenário");
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        Assert.True(process.ExitCode == 0, $"processo do cenário saiu com {process.ExitCode}: {error}");

        var parts = output.Trim().Split(';');
        Assert.Equal(2, parts.Length);
        return (parts[0], parts[1]);
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
