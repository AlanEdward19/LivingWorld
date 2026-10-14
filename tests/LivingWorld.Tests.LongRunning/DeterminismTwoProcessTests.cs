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
    public void Same_seed_writes_identical_positional_snapshot_bytes_across_two_separate_processes()
    {
        string a = RunCommand("snapshot-bytes 5 2160");
        string b = RunCommand("snapshot-bytes 5 2160");

        Assert.Equal(64, a.Length);
        Assert.Equal(a, b);
        Assert.NotEqual(a, RunCommand("snapshot-bytes 6 2160"));
    }

    private static string RunCommand(string arguments)
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
        Assert.True(process.ExitCode == 0, $"processo do cenário saiu com {process.ExitCode}: {error}");
        return output.Trim();
    }

    [Fact]
    public void Same_seed_writes_the_same_compacted_event_log_across_two_separate_processes()
    {
        string a = RunCommand("eventlog-digest 5 4320");
        string b = RunCommand("eventlog-digest 5 4320");

        Assert.Equal(64, a.Length);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Same_seed_writes_the_same_epidemic_across_two_separate_processes()
    {
        string a = RunCommand("disease-hash 5 8640");
        string b = RunCommand("disease-hash 5 8640");

        Assert.Equal(a, b);
        Assert.True(int.Parse(a.Split(';')[2]) > 0, "a vila não teve nenhuma morte: o teste não mediu nada");
    }

    [Fact]
    public void Same_seed_draws_the_same_climate_years_across_two_separate_processes()
    {
        string a = RunCommand("climate-hash 5 43200");
        string b = RunCommand("climate-hash 5 43200");

        Assert.Equal(a, b);
        Assert.True(int.Parse(a.Split(';')[1]) > 0, "nenhum ano de safra sorteado: o teste não mediu nada");
    }

    [Fact]
    public void Same_seed_writes_the_same_thefts_across_two_separate_processes()
    {
        string a = RunCommand("theft-hash 5 8640");
        string b = RunCommand("theft-hash 5 8640");

        Assert.Equal(a, b);
        Assert.True(int.Parse(a.Split(';')[1]) > 0, "nenhum furto descoberto: o teste não mediu nada");
    }

    [Fact]
    public void Same_seed_replays_an_incarnation_and_a_disappearance_across_two_separate_processes()
    {
        string a = RunCommand("player-hash 5 2000");
        string b = RunCommand("player-hash 5 2000");

        Assert.Equal(a, b);
        Assert.NotEqual("", a.Split(';')[1]);
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
