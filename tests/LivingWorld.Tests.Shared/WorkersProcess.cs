using System.Diagnostics;

namespace LivingWorld.Tests.Shared;

/// <summary>Roda <c>LivingWorld.Workers</c> num processo separado — prova de determinismo entre
/// processos (ordem de <c>Dictionary</c>, hash seed). Um único lugar acha o binário e dispara o
/// processo, em vez de uma cópia por classe de teste.</summary>
public static class WorkersProcess
{
    private static readonly Lazy<string> WorkersDll = new(FindWorkersDll);

    /// <summary>Executa <c>dotnet LivingWorld.Workers.dll &lt;arguments&gt;</c> e devolve o stdout aparado.</summary>
    public static string RunDigest(string arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{WorkersDll.Value}\" {arguments}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("falha ao iniciar processo");
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, $"processo saiu com {process.ExitCode}: {error}");
        return output.Trim();
    }

    private static string FindWorkersDll()
    {
        var configuration = AppContext.BaseDirectory.Contains($"{Path.DirectorySeparatorChar}Release{Path.DirectorySeparatorChar}")
            ? "Release"
            : "Debug";
        var path = Path.Combine(FindRepoRoot(), "src", "LivingWorld.Workers", "bin", configuration, "net10.0", "LivingWorld.Workers.dll");
        if (!File.Exists(path))
            throw new FileNotFoundException($"LivingWorld.Workers.dll não encontrado em {path}", path);
        return path;
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "LivingWorld.sln")))
            dir = Directory.GetParent(dir)?.FullName;
        return dir ?? throw new InvalidOperationException("LivingWorld.sln não encontrado");
    }
}
