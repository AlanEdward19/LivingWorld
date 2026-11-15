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
        // LW_PROFILE_POWERS=1 liga os poderes do cenário de referência (AD-156), para medir o custo da herança genética
        var (world, clock) = ScaleScenarioFixture.CreateWorld(42, population,
            extraordinary: Environment.GetEnvironmentVariable("LW_PROFILE_POWERS") == "1"
                ? LivingWorld.Simulation.Observation.PowerGeneticsReport.ReferenceScenario() : null);
        // LW_MEASURE_NO_INVENTION=1 desliga a invenção (AD-143), para comparar o perfil com e sem ela na mesma árvore
        if (Environment.GetEnvironmentVariable("LW_MEASURE_NO_INVENTION") == "1") world.UseInvention(null);
        // LW_MEASURE_CARRY=0/1 desliga/liga a carga pessoal (AD-151), para medir o custo dela na mesma árvore
        else if (Environment.GetEnvironmentVariable("LW_MEASURE_CARRY") is "0" or "1" && world.InventionRules is { } inv)
            world.UseInvention(inv with { PantryNeedsPresence = Environment.GetEnvironmentVariable("LW_MEASURE_CARRY") == "1" });

        var report = SystemProfiler.Measure(world, clock, warmupTicks: 240, ticks);

        string text = $"pop={population}\n{report.Format()}";
        File.WriteAllText(Environment.GetEnvironmentVariable("LW_PROFILE_OUT") ?? Path.Combine(Path.GetTempPath(), "lw-profile.txt"), text);
        Assert.True(report.Systems.Count > 0);
    }

    private static int ReadInt(string name, int fallback) =>
        int.TryParse(Environment.GetEnvironmentVariable(name), out int value) && value > 0 ? value : fallback;
}
