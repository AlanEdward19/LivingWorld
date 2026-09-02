using LivingWorld.Domain;

namespace LivingWorld.Tests.Economy;

/// <summary>Fase 5, T26 (ECON-29) — a cadeia causal completa da fase: quebra de safra (produção
/// de trigo cortada a zero) aumenta a contagem de NPCs com fome acima do limiar do cenário, em
/// 10/10 seeds. Mesmo <see cref="EconomyScenarioHarness"/>/harness base-tratamento de T25.</summary>
public class FamineCausalChainTests
{
    private static readonly ResourceType Trigo = new(1);
    private const long T0 = 0;

    // Uma fotografia tardia volta a zero quando os famintos já morreram; uma fotografia cedo
    // demais depende do horário da última refeição. A carga horária de fome soma quantos NPCs
    // permanecem acima do limiar a cada hora: distingue o cruzamento normal antes de comer de uma
    // privação sustentada. Mortes não somem da coorte (fome extrema é estado absorvente para esta
    // medida causal). O harness mantém moradia/mercado/mão de obra iguais nos dois braços.
    private const long ObservationHours = 30 * 24;

    private static long HungryNpcHoursDuringWindow(ulong seed, double productionMultiplier)
    {
        var (world, clock) = EconomyScenarioHarness.CreateControlledFamineScenario(
            seed, Trigo, productionMultiplier, T0, initialPopulation: 150);
        int threshold = world.NeedsRules.UrgencyThreshold;
        long hungryNpcHours = 0;
        for (long hour = 0; hour < ObservationHours; hour++)
        {
            clock.Run(world, 1);
            hungryNpcHours += world.Npcs.Count(
                npc => (100 - npc.HungerAt(world.CurrentDate.TotalHours)) >= threshold);
        }
        return hungryNpcHours;
    }

    [Fact]
    public void Famine_raises_the_hungry_count_above_the_scenarios_threshold_in_10_of_10_seeds()
    {
        int seedsWhereTreatmentHigher = 0;
        var evidence = new List<string>();

        for (ulong seed = 1; seed <= 10; seed++)
        {
            long baseHungry = HungryNpcHoursDuringWindow(seed, productionMultiplier: 1.0);
            long treatmentHungry = HungryNpcHoursDuringWindow(seed, productionMultiplier: 0.0);

            if (treatmentHungry > baseHungry) seedsWhereTreatmentHigher++;
            evidence.Add($"{seed}:base={baseHungry},trat={treatmentHungry}");
        }

        Assert.True(seedsWhereTreatmentHigher == 10,
            $"{seedsWhereTreatmentHigher}/10 seeds tiveram mais NPCs com fome acima do limiar no braço de tratamento (quebra de safra); {string.Join("; ", evidence)}");
    }
}
