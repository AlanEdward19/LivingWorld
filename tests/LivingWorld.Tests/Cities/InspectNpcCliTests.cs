using System.Diagnostics;

namespace LivingWorld.Tests.Cities;

/// <summary>Fase 8, T16 (CITY-06): <c>LivingWorld.Workers inspect-npc &lt;id&gt;</c> — mesmo
/// molde de processo real de <see cref="DeterminismTwoProcessTests"/>. NPC vivo imprime saída
/// (exit 0, AC #2); id inválido sai com código não-zero, sem stack trace não tratada (AC #3).</summary>
public class InspectNpcCliTests
{
    [Fact]
    public void InspectNpc_prints_output_and_exits_zero_for_a_living_npc_id()
    {
        var (exitCode, stdout, _) = RunInSeparateProcess("0");

        Assert.Equal(0, exitCode);
        Assert.False(string.IsNullOrWhiteSpace(stdout));
    }

    [Fact]
    public void InspectNpc_exits_nonzero_without_an_unhandled_stack_trace_for_an_id_that_does_not_exist()
    {
        var (exitCode, _, stderr) = RunInSeparateProcess("999999");

        Assert.NotEqual(0, exitCode);
        Assert.DoesNotContain("Unhandled exception", stderr);
    }

    private static (int ExitCode, string Stdout, string Stderr) RunInSeparateProcess(string id)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{WorkersDllPath}\" inspect-npc {id}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("falha ao iniciar processo do cenário");
        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return (process.ExitCode, stdout, stderr);
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
