using LivingWorld.Domain;

namespace LivingWorld.Tests.Population;

/// <summary>Fase 7, T4 (FAM-03, FAM-04, FAM-06, FAM-07, FAM-12, FAM-13, FAM-16, FAM-18, FAM-19,
/// FAM-21, FAM-23): catálogo cenário-driven de parâmetros de família.</summary>
public class FamilyRulesTests
{
    private static IReadOnlyDictionary<(RelationshipEventType, RelationshipAxis), double> FullDeltas()
    {
        var deltas = new Dictionary<(RelationshipEventType, RelationshipAxis), double>();
        foreach (var type in Enum.GetValues<RelationshipEventType>())
            foreach (var axis in Enum.GetValues<RelationshipAxis>())
                deltas[(type, axis)] = 0.0;
        deltas[(RelationshipEventType.Cohabitation, RelationshipAxis.Trust)] = 1.0;
        deltas[(RelationshipEventType.Betrayal, RelationshipAxis.Trust)] = -10.0;
        return deltas;
    }

    private static IReadOnlyDictionary<AttractionFactor, double> ValidAttractionWeights() =>
        Enum.GetValues<AttractionFactor>().ToDictionary(f => f, _ => 1.0);

    private static Result<FamilyRules> CreateValid(
        IReadOnlyDictionary<(RelationshipEventType, RelationshipAxis), double>? deltas = null,
        int courtshipDurationDays = 90,
        double maternalDeathRisk = 0.02,
        double infantDeathRisk = 0.05,
        double vitalityMotherWeight = 0.5,
        double vitalityFatherWeight = 0.5,
        bool environmentalWealthChannelEnabled = true) =>
        FamilyRules.Create(
            relationshipDeltas: deltas ?? FullDeltas(),
            decayPerDay: 0.5,
            contactLossThresholdDays: 30,
            neutralAxisValue: 50,
            attractionWeights: ValidAttractionWeights(),
            courtshipThreshold: 0.6,
            courtshipDurationDays: courtshipDurationDays,
            marriageInitialStock: new Dictionary<int, long> { [1] = 100 },
            conceptionHealthFloor: 40,
            conceptionRelationshipFloor: 40,
            conceptionResourceFloor: new Dictionary<int, long> { [1] = 10 },
            maternalDeathRisk: maternalDeathRisk,
            infantDeathRisk: infantDeathRisk,
            vitalityMotherWeight: vitalityMotherWeight,
            vitalityFatherWeight: vitalityFatherWeight,
            vitalityMutationStdDev: 5,
            vitalityMortalityWeight: 0.3,
            upbringingWealthWeight: 0.3,
            environmentalWealthChannelEnabled: environmentalWealthChannelEnabled,
            neutralDriftEnabled: false);

    [Fact]
    public void Create_accepts_valid_parameters()
    {
        var result = CreateValid();

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Create_rejects_relationship_deltas_missing_an_event_axis_combination()
    {
        var incomplete = FullDeltas().Where(kv => kv.Key != (RelationshipEventType.Trade, RelationshipAxis.Debt))
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        var result = CreateValid(deltas: incomplete);

        Assert.False(result.IsSuccess);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_rejects_courtship_duration_days_not_positive(int durationDays)
    {
        var result = CreateValid(courtshipDurationDays: durationDays);

        Assert.False(result.IsSuccess);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public void Create_rejects_maternal_death_risk_out_of_range(double risk)
    {
        var result = CreateValid(maternalDeathRisk: risk);

        Assert.False(result.IsSuccess);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public void Create_rejects_infant_death_risk_out_of_range(double risk)
    {
        var result = CreateValid(infantDeathRisk: risk);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Create_rejects_vitality_parent_weights_that_sum_to_zero()
    {
        var result = CreateValid(vitalityMotherWeight: 0, vitalityFatherWeight: 0);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Create_rejects_vitality_parent_weights_that_sum_far_beyond_a_normal_blend()
    {
        var result = CreateValid(vitalityMotherWeight: 5, vitalityFatherWeight: 5);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Create_does_not_require_vitality_parent_weights_to_sum_to_exactly_one()
    {
        // AD: soma documentada como "algo sensato", nunca travada em 1.0 exato.
        var result = CreateValid(vitalityMotherWeight: 0.6, vitalityFatherWeight: 0.6);

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(50.0)]
    [InlineData(100.0)]
    public void EffectiveVitalityMultiplier_never_produces_a_negative_multiplier(double vitality)
    {
        var rules = CreateValid().Value!;

        double multiplier = rules.EffectiveVitalityMultiplier(vitality);

        Assert.True(multiplier >= 0, $"vitality {vitality} produced negative multiplier {multiplier}");
    }

    [Fact]
    public void EffectiveVitalityMultiplier_is_lower_for_higher_vitality()
    {
        var rules = CreateValid().Value!;

        double lowVitalityMultiplier = rules.EffectiveVitalityMultiplier(0);
        double highVitalityMultiplier = rules.EffectiveVitalityMultiplier(100);

        Assert.True(highVitalityMultiplier < lowVitalityMultiplier);
    }

    [Theory]
    [InlineData(-50.0)]
    [InlineData(150.0)]
    public void ApplyUpbringingWeight_clamps_out_of_range_upbringing_before_applying_weight(double upbringing)
    {
        var rules = CreateValid().Value!;

        double result = rules.ApplyUpbringingWeight(wage: 100, upbringing: upbringing);
        double clampedEquivalent = rules.ApplyUpbringingWeight(wage: 100, upbringing: Math.Clamp(upbringing, 0, 100));

        Assert.Equal(clampedEquivalent, result, precision: 10);
    }

    [Fact]
    public void ApplyUpbringingWeight_is_a_no_op_when_environmental_wealth_channel_is_disabled()
    {
        var rules = CreateValid(environmentalWealthChannelEnabled: false).Value!;

        double result = rules.ApplyUpbringingWeight(wage: 100, upbringing: 90);

        Assert.Equal(100, result);
    }
}
