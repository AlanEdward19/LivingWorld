using LivingWorld.Tests.Shared.Performance;

namespace LivingWorld.Tests.LongRunning.Performance;

/// <summary>Perfil por sistema sob demanda (não é gate): <c>bash scripts/profile.sh [--pop N] [--ticks N]</c>. Escreve em
/// <c>LW_PROFILE_OUT</c> a tabela de tempo e bytes por sistema e os contadores de despertar no fixture de escala.</summary>
[Collection(ScalePerformanceCollection.Name)]
[Trait("Category", "Heavy")]
[Trait("Category", "Measure")]
public class SystemProfileHarness
{
    [Fact]
    public void Profile()
    {
        int population = ReadInt("LW_PROFILE_POP", 1000);
        int ticks = ReadInt("LW_PROFILE_TICKS", 720);
        var (world, clock) = ScaleScenarioFixture.CreateWorld(42, population);

        var report = SystemProfiler.Measure(world, clock, warmupTicks: 240, ticks);

        string text = $"pop={population}\n{report.Format()}";
        File.WriteAllText(Environment.GetEnvironmentVariable("LW_PROFILE_OUT") ?? Path.Combine(Path.GetTempPath(), "lw-profile.txt"), text);
        Assert.True(report.Systems.Count > 0);
    }

    private static int ReadInt(string name, int fallback) =>
        int.TryParse(Environment.GetEnvironmentVariable(name), out int value) && value > 0 ? value : fallback;
}
