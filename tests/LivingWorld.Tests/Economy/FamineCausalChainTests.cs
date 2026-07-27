using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Economy;

/// <summary>Fase 5, T26 (ECON-29) — a cadeia causal completa da fase: quebra de safra (produção
/// de trigo cortada a zero) aumenta a contagem de NPCs com fome acima do limiar do cenário, em
/// 10/10 seeds. Mesmo <see cref="EconomyScenarioHarness"/>/harness base-tratamento de T25.</summary>
public class FamineCausalChainTests
{
    private static readonly ResourceType Trigo = new(1);
    private const long T0 = 0;

    // Dia 20: cedo o bastante pra nenhum braço ter colapsado por morte em massa ainda (o corte a
    // zero, mult=0, mata gente de verdade depois de ~dia 40 — contagem de "fome acima do limiar"
    // sobre só quem está vivo esconderia o efeito num horizonte mais longo, os famintos já
    // teriam morrido); tarde o bastante pro buffer inicial de bootstrap (T20, 50 unidades por
    // Household) já ter esgotado nas casas mais desfavorecidas (achado rodando dia a dia).
    private const long CheckpointHours = 20 * 24;

    private static int HungryCount(ulong seed, double productionMultiplier)
    {
        var (world, clock) = EconomyScenarioHarness.Create(seed, Trigo, productionMultiplier, T0, initialPopulation: 150);
        clock.Run(world, CheckpointHours);

        int threshold = world.NeedsRules.UrgencyThreshold;
        return world.Npcs.Count(n => n.IsAlive && (100 - n.Hunger) >= threshold);
    }

    [Fact]
    public void Famine_raises_the_hungry_count_above_the_scenarios_threshold_in_10_of_10_seeds()
    {
        int seedsWhereTreatmentHigher = 0;

        for (ulong seed = 1; seed <= 10; seed++)
        {
            int baseHungry = HungryCount(seed, productionMultiplier: 1.0);
            int treatmentHungry = HungryCount(seed, productionMultiplier: 0.0);

            if (treatmentHungry > baseHungry) seedsWhereTreatmentHigher++;
        }

        Assert.True(seedsWhereTreatmentHigher == 10,
            $"{seedsWhereTreatmentHigher}/10 seeds tiveram mais NPCs com fome acima do limiar no braço de tratamento (quebra de safra)");
    }
}
