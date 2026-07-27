using LivingWorld.Domain;

namespace LivingWorld.Tests.Population;

/// <summary>Fase 7, T6 (FAM-18, FAM-19, FAM-20, FAM-22): hereditariedade genética
/// (<c>Vitality</c>) vs ambiental (<c>Upbringing</c>) — origens distintas por construção.</summary>
public class HeredityServiceTests
{
    private static FamilyRules ValidRules(
        double vitalityMotherWeight = 0.5, double vitalityFatherWeight = 0.5,
        double vitalityMutationStdDev = 5, double upbringingWealthWeight = 0.5)
    {
        var deltas = new Dictionary<(RelationshipEventType, RelationshipAxis), double>();
        foreach (var type in Enum.GetValues<RelationshipEventType>())
        foreach (var axis in Enum.GetValues<RelationshipAxis>())
            deltas[(type, axis)] = 0.0;

        return FamilyRules.Create(
            relationshipDeltas: deltas,
            decayPerDay: 0.5,
            contactLossThresholdDays: 30,
            neutralAxisValue: 50,
            attractionWeights: Enum.GetValues<AttractionFactor>().ToDictionary(f => f, _ => 1.0),
            courtshipThreshold: 0.6,
            courtshipDurationDays: 90,
            marriageInitialStock: new Dictionary<int, long> { [1] = 100 },
            conceptionHealthFloor: 40,
            conceptionRelationshipFloor: 40,
            conceptionResourceFloor: new Dictionary<int, long> { [1] = 10 },
            maternalDeathRisk: 0.02,
            infantDeathRisk: 0.05,
            vitalityMotherWeight: vitalityMotherWeight,
            vitalityFatherWeight: vitalityFatherWeight,
            vitalityMutationStdDev: vitalityMutationStdDev,
            vitalityMortalityWeight: 0.3,
            upbringingWealthWeight: upbringingWealthWeight,
            environmentalWealthChannelEnabled: true,
            neutralDriftEnabled: false).Value!;
    }

    private static Household HouseholdWithStock(long stockAmount) => new(
        new HouseholdId(1), new CellCoord(0, 0), new NpcId(1), [new NpcId(1)],
        stock: new Dictionary<ResourceType, long> { [new ResourceType(1)] = stockAmount });

    [Fact]
    public void RollInitialVitality_never_produces_a_value_outside_0_100_across_many_seeds()
    {
        for (ulong seed = 1; seed <= 200; seed++)
        {
            double vitality = HeredityService.RollInitialVitality(new WorldRng(seed));

            Assert.InRange(vitality, 0, 100);
        }
    }

    [Fact]
    public void RollInitialUpbringing_never_produces_a_value_outside_0_100_across_many_seeds()
    {
        for (ulong seed = 1; seed <= 200; seed++)
        {
            double upbringing = HeredityService.RollInitialUpbringing(new WorldRng(seed));

            Assert.InRange(upbringing, 0, 100);
        }
    }

    [Fact]
    public void InheritVitality_never_produces_a_value_outside_0_100_across_many_seeds()
    {
        var rules = ValidRules(vitalityMutationStdDev: 500);

        for (ulong seed = 1; seed <= 200; seed++)
        {
            double child = HeredityService.InheritVitality(100, 100, rules, new WorldRng(seed));

            Assert.InRange(child, 0, 100);
        }
    }

    [Fact]
    public void InheritVitality_with_identical_parents_varies_by_mutation_across_seeds()
    {
        var rules = ValidRules();

        var results = Enumerable.Range(1, 20)
            .Select(seed => HeredityService.InheritVitality(60, 60, rules, new WorldRng((ulong)seed)))
            .ToList();

        Assert.True(results.Distinct().Count() > 1, "mutação deveria produzir variação entre seeds");
    }

    [Fact]
    public void InheritVitality_centers_around_parents_weighted_average()
    {
        var rules = ValidRules(vitalityMutationStdDev: 2);

        var results = Enumerable.Range(1, 500)
            .Select(seed => HeredityService.InheritVitality(60, 60, rules, new WorldRng((ulong)seed)))
            .ToList();

        Assert.InRange(results.Average(), 55, 65);
    }

    [Fact]
    public void DeriveUpbringing_produces_different_values_for_households_with_different_wealth()
    {
        var rules = ValidRules();
        var richHousehold = HouseholdWithStock(100);
        var poorHousehold = HouseholdWithStock(10);

        double richUpbringing = HeredityService.DeriveUpbringing(richHousehold, rules);
        double poorUpbringing = HeredityService.DeriveUpbringing(poorHousehold, rules);

        Assert.NotEqual(richUpbringing, poorUpbringing);
        Assert.True(richUpbringing > poorUpbringing);
    }

    [Fact]
    public void DeriveUpbringing_signature_has_no_vitality_or_gene_parameter()
    {
        // Prova estrutural (FAM-19/20): reflexão confirma que nenhum parâmetro do método carrega
        // "Vitality"/"Gene" no nome ou no tipo — canais independentes por construção, não por
        // disciplina de quem chama.
        var method = typeof(HeredityService).GetMethod(nameof(HeredityService.DeriveUpbringing))!;

        foreach (var parameter in method.GetParameters())
        {
            Assert.DoesNotContain("Vitality", parameter.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Gene", parameter.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Vitality", parameter.ParameterType.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Gene", parameter.ParameterType.Name, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void DeriveUpbringing_never_produces_a_value_outside_0_100()
    {
        var rules = ValidRules(upbringingWealthWeight: 1000);
        var wealthyHousehold = HouseholdWithStock(1_000_000);

        double upbringing = HeredityService.DeriveUpbringing(wealthyHousehold, rules);

        Assert.InRange(upbringing, 0, 100);
    }
}
