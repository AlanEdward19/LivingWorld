using System.Diagnostics;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.History;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.History;

/// <summary>Fase 10, T5: <see cref="LivingMemoryWindow"/> (HIST-01 AC3).</summary>
public class LivingMemoryWindowTests
{
    private static readonly WorldCalendar Calendar = new(24, 30, 12);

    [Fact]
    public void HasLivingWitness_returns_true_while_a_participant_is_alive()
    {
        var (world, _) = ScenarioRunner.Create(1);
        var npc = world.Npcs[0];
        var fact = new Fact(new FactId(1), 1, WorldEventKind.Death, [npc.Id], npc.City, 0.9, npc.Id.Value.ToString());

        Assert.True(LivingMemoryWindow.HasLivingWitness(fact, world));
    }

    [Fact]
    public void HasLivingWitness_returns_false_after_last_participant_dies()
    {
        var (world, _) = ScenarioRunner.Create(1);
        var npc = world.Npcs[0];
        var fact = new Fact(new FactId(1), 1, WorldEventKind.Death, [npc.Id], npc.City, 0.9, npc.Id.Value.ToString());
        npc.Die(WorldDate.Epoch(Calendar).AddYears(30));

        Assert.False(LivingMemoryWindow.HasLivingWitness(fact, world));
    }

    [Fact]
    public void Recall_applies_witness_bias_without_distortion_operators()
    {
        var (world, _) = ScenarioRunner.Create(1);
        var npc = world.Npcs[0];
        var fact = new Fact(new FactId(1), 1, WorldEventKind.Marriage, [npc.Id], npc.City, 0.8, "1|2");
        var recall = LivingMemoryWindow.Recall(fact, world);

        Assert.Equal(fact.Participants, recall.Participants);
        Assert.Equal(fact.Payload, recall.Payload);
        Assert.Equal(fact.Kind, recall.Kind);
        Assert.True(recall.PerceivedSignificance > fact.Significance);
        Assert.Equal(fact.Significance + 0.05, recall.PerceivedSignificance, precision: 6);
    }

    [Fact]
    public void Living_memory_digest_is_identical_across_two_separate_processes()
    {
        var a = RunDigestInSeparateProcess(42, 300);
        var b = RunDigestInSeparateProcess(42, 300);
        Assert.Equal(a, b);
    }

    private static string RunDigestInSeparateProcess(ulong seed, long ticks)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{WorkersDllPath}\" history-living-memory-digest {seed} {ticks}",
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

    private static readonly string WorkersDllPath = FindWorkersDll();

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
