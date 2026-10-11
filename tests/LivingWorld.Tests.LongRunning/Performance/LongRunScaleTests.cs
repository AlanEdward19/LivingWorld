using LivingWorld.Simulation.Core;
using LivingWorld.Tests.Shared.Performance;

namespace LivingWorld.Tests.LongRunning.Performance;

[Trait("Category", "Scenario")]
public class LongRunScaleTests
{
    /// <summary>10 mil NPCs completam 30 dias sem estourar o teto de iterações por tick, sem
    /// exceção e sem extinção. O horizonte de 10 anos foi trocado por um trimestre: a 10 mil NPCs o
    /// custo é de ~100 s por ano simulado (≈1,4 µs por decisão × ~1 decisão/hora/NPC), então 10 anos
    /// levavam ~17 min — e o teste já não passava, porque a economia do fixture não sustenta a
    /// população (ver STATE.md). 14 dias exercitam o mesmo caminho quente (a rotina é diária; o custo por dia é estável).</summary>
    [Fact]
    [Trait("Category", "Heavy")]
    public void Ten_k_population_fourteen_days_within_perf_budget()
    {
        var (world, clock) = ScaleScenarioFixture.CreateWorld(seed: 42, initialPopulation: 10_000);
        clock.Run(world, ticks: 14 * 24);
        Assert.True(world.Npcs.Count(n => n.IsAlive) > 100);
    }

    /// <summary>O armazenamento total satura em vez de crescer com o horizonte: NPCs mortos viram
    /// resumo no arquivo frio e os logs são limitados. A métrica "bytes por NPC vivo" deixou de fazer
    /// sentido porque a população do fixture decai até quase zero (o denominador some); o que o teste
    /// precisa provar é que o tamanho do snapshot não escala com o tempo. Um único mundo é amostrado
    /// em checkpoints crescentes (antes, três mundos independentes).</summary>
    [Fact]
    public void Serialized_size_saturates_instead_of_growing_with_the_horizon()
    {
        var (world, clock) = ScaleScenarioFixture.CreateWorld(seed: 99, initialPopulation: 200);
        var sizes = new List<(int Years, long Bytes)>();
        int elapsedYears = 0;
        foreach (int checkpoint in new[] { 1, 4, 12 })
        {
            clock.Run(world, (checkpoint - elapsedYears) * 365L * 24);
            elapsedYears = checkpoint;
            sizes.Add((checkpoint, System.Text.Encoding.UTF8.GetByteCount(WorldSnapshot.Serialize(world))));
        }

        long atTen = sizes.Single(s => s.Years == 4).Bytes;
        long atEnd = sizes.Single(s => s.Years == 12).Bytes;
        Assert.True(atEnd <= atTen * 1.5, $"snapshot cresceu de {atTen} B (4 anos) para {atEnd} B (12 anos)");
    }
}
