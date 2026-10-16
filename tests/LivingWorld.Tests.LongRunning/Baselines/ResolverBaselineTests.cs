using LivingWorld.Domain.Shared;
using LivingWorld.Tests.Shared.Baselines;

namespace LivingWorld.Tests.LongRunning.Baselines;

/// <summary>Uso real da infra de baseline (task 7) sobre o primitivo que já existe na Fase 0:
/// o Resolver. 20 seeds, dificuldade e modificador fixos — baseline gravado em
/// tests/baselines/resolver-sample.json.</summary>
public class ResolverBaselineTests
{
    private static readonly string BaselinesDir = Path.Combine(FindRepoRoot(), "tests", "baselines");
    private static readonly VarianceProfile Dramatico = VarianceProfile.Dramatico("Dramático");
    private static readonly VarianceProfile Agregado = VarianceProfile.Agregado("Agregado");

    private sealed record ResolverSample(string DramaticoOutcome, string AgregadoOutcome);

    private static Dictionary<int, ResolverSample> ComputeSamples()
    {
        var samples = new Dictionary<int, ResolverSample>();
        for (int seed = 1; seed <= 20; seed++)
        {
            var baseRng = new WorldRng((ulong)seed);
            var dramaticoOutcome = Resolver.Resolve(10, 2, Dramatico, baseRng.Derive(1));
            var agregadoOutcome = Resolver.Resolve(10, 2, Agregado, baseRng.Derive(2));
            samples[seed] = new ResolverSample(dramaticoOutcome.ToString(), agregadoOutcome.ToString());
        }
        return samples;
    }

    [Fact(Skip = "regravação manual — remove o Skip, rode uma vez, reverta")]
    public void ZZZ_record_baseline()
    {
        BaselineFixture.Record(BaselinesDir, "resolver-sample", ComputeSamples());
    }

    [Fact]
    public void Matches_committed_baseline()
    {
        BaselineFixture.AssertMatches(BaselinesDir, "resolver-sample", ComputeSamples());
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "LivingWorld.sln")))
            dir = Directory.GetParent(dir)?.FullName;
        return dir ?? throw new InvalidOperationException("LivingWorld.sln não encontrado a partir de " + AppContext.BaseDirectory);
    }
}
