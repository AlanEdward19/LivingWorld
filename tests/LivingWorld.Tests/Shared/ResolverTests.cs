using LivingWorld.Domain;

namespace LivingWorld.Tests;

public class ResolverTests
{
    [Fact]
    public void Dramatico_over_many_seeds_produces_all_five_result_bands()
    {
        var profile = VarianceProfile.Dramatico("Dramático");
        var seen = new HashSet<ResolutionResult>();

        for (ulong seed = 1; seed <= 100_000 && seen.Count < 5; seed++)
            seen.Add(Resolver.Resolve(10, 0, profile, new WorldRng(seed)));

        Assert.Equal(5, seen.Count);
    }

    [Fact]
    public void Agregado_over_many_seeds_never_produces_critical()
    {
        var profile = VarianceProfile.Agregado("Agregado");

        for (ulong seed = 1; seed <= 100_000; seed++)
        {
            var outcome = Resolver.Resolve(10, 0, profile, new WorldRng(seed));
            Assert.NotEqual(ResolutionResult.CriticalFailure, outcome);
            Assert.NotEqual(ResolutionResult.CriticalSuccess, outcome);
        }
    }

    [Fact]
    public void Higher_modifier_shifts_distribution_toward_success_same_seed_pairs()
    {
        var profile = VarianceProfile.Dramatico("Dramático");
        int baseSuccesses = 0, treatmentSuccesses = 0;

        for (ulong seed = 1; seed <= 200; seed++)
        {
            if (IsSuccessOrBetter(Resolver.Resolve(15, 0, profile, new WorldRng(seed)))) baseSuccesses++;
            if (IsSuccessOrBetter(Resolver.Resolve(15, 10, profile, new WorldRng(seed)))) treatmentSuccesses++;
        }

        Assert.True(treatmentSuccesses > baseSuccesses,
            $"tratamento ({treatmentSuccesses}) deveria ter mais sucessos que base ({baseSuccesses})");
    }

    private static bool IsSuccessOrBetter(ResolutionResult r) =>
        r is ResolutionResult.Success or ResolutionResult.CriticalSuccess;

    [Fact]
    public void Same_seed_produces_same_outcome_in_independent_instances()
    {
        var profile = VarianceProfile.Dramatico("Dramático");

        var a = Resolver.Resolve(10, 3, profile, new WorldRng(42));
        var b = Resolver.Resolve(10, 3, profile, new WorldRng(42));

        Assert.Equal(a, b);
    }

    [Fact]
    public void Different_seeds_can_produce_different_outcomes()
    {
        // Prova que o teste acima mede alguma coisa: nem toda seed dá o mesmo resultado.
        var profile = VarianceProfile.Dramatico("Dramático");
        var outcomes = new HashSet<ResolutionResult>();

        for (ulong seed = 1; seed <= 50; seed++)
            outcomes.Add(Resolver.Resolve(10, 3, profile, new WorldRng(seed)));

        Assert.True(outcomes.Count > 1);
    }

    [Fact]
    public void Undeclared_profile_fails_at_load()
    {
        var catalog = new VarianceProfileCatalog([VarianceProfile.Dramatico("Dramático")]);

        Assert.Throws<InvalidOperationException>(() => catalog.Get("Agregado"));
    }
}
