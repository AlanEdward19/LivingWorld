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
        // LW_PROFILE_VILLAGE_YEARS=N (AD-167): a vila padrão de hoje (ScenarioRunner.Create, seed 42) aquecida N anos, no lugar do
        // fixture de escala; é o mundo que os testes de cenário simulam (o caminho crítico do gate)
        int villageYears = ReadInt("LW_PROFILE_VILLAGE_YEARS", 0);
        var (world, clock) = villageYears > 0
            ? LivingWorld.Simulation.Scenarios.ScenarioRunner.Create(42)
            : ScaleScenarioFixture.CreateWorld(42, population,
                extraordinary: Environment.GetEnvironmentVariable("LW_PROFILE_POWERS") == "1"
                    ? LivingWorld.Simulation.Observation.PowerGeneticsReport.ReferenceScenario() : null);
        // LW_MEASURE_BRAIN=0 desliga o cérebro (ou uma variante dele), como no MeasureHarness: custo do cérebro na mesma árvore (AD-167)
        if (Environment.GetEnvironmentVariable("LW_MEASURE_BRAIN") is { Length: > 0 } brain)
            world.UseBrain(LivingWorld.Domain.Cognition.BrainRules.Variant(brain));
        if (villageYears > 0) clock.Run(world, (villageYears - 1) * world.Calendar.HoursPerYear);
        // LW_MEASURE_NO_INVENTION=1 desliga a invenção (AD-143), para comparar o perfil com e sem ela na mesma árvore
        if (Environment.GetEnvironmentVariable("LW_MEASURE_NO_INVENTION") == "1") world.UseInvention(null);
        // LW_MEASURE_CARRY=0/1 desliga/liga a carga pessoal (AD-151), para medir o custo dela na mesma árvore
        else if (Environment.GetEnvironmentVariable("LW_MEASURE_CARRY") is "0" or "1" && world.InventionRules is { } inv)
            world.UseInvention(inv with { PantryNeedsPresence = Environment.GetEnvironmentVariable("LW_MEASURE_CARRY") == "1" });

        // AD-187: rotas calculadas e idas de água no período medido (o trabalho real do mundo novo, ao lado do custo)
        var tally0 = (world.InventionTally.WaterTrips, world.InventionTally.WaterTripsByThirst);
        long computed0 = world.RouteState?.Computed ?? 0;
        var report = SystemProfiler.Measure(world, clock, warmupTicks: ReadInt("LW_PROFILE_WARMUP", 240) is var w && Environment.GetEnvironmentVariable("LW_PROFILE_WARMUP") == "0" ? 0 : w, ticks); // AD-185: LW_PROFILE_WARMUP=0 mede o começo do mundo (o cache de rotas frio)

        string text = $"pop={(villageYears > 0 ? $"vila-padrao-{villageYears}a" : population)}\n{report.Format()}"
            + $"rotas calculadas={(world.RouteState?.Computed ?? 0) - computed0} idas de água={world.InventionTally.WaterTrips - tally0.Item1} por sede={world.InventionTally.WaterTripsByThirst - tally0.Item2}\n";
        File.WriteAllText(Environment.GetEnvironmentVariable("LW_PROFILE_OUT") ?? Path.Combine(Path.GetTempPath(), "lw-profile.txt"), text);
        Assert.True(report.Systems.Count > 0);
    }

    private static int ReadInt(string name, int fallback) =>
        int.TryParse(Environment.GetEnvironmentVariable(name), out int value) && value > 0 ? value : fallback;
}
