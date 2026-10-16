using LivingWorld.Domain.History;
using LivingWorld.Domain.History.Distortion;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.History;

/// <summary>Fase 10, T1: <see cref="HistoryRules"/> — parâmetros cenário-driven (HIST-08).</summary>
public class HistoryRulesTests
{
    private static readonly IReadOnlyDictionary<TransmissionMediumType, MediumFidelity> ValidMedium =
        new Dictionary<TransmissionMediumType, MediumFidelity>
        {
            [TransmissionMediumType.Book] = new(0.1, 5, DeathConditionType.Decay),
        };

    private static readonly IReadOnlyDictionary<DistortionOperator, double> ValidOperators =
        new Dictionary<DistortionOperator, double> { [DistortionOperator.Moralization] = 0.2 };

    private static Result<HistoryRules> CreateWith(
        double threshold = 0.5,
        int canonSize = 10,
        IReadOnlyDictionary<TransmissionMediumType, MediumFidelity>? medium = null,
        IReadOnlyDictionary<DistortionOperator, double>? operators = null,
        double importance = 1,
        double transmissibility = 1,
        double recency = 1) =>
        HistoryRules.Create(
            enabled: true,
            skeletonSignificanceThreshold: threshold,
            canonSizePerCommunity: canonSize,
            mediumFidelityByType: medium ?? ValidMedium,
            operatorProbability: operators ?? ValidOperators,
            importanceWeight: importance,
            transmissibilityWeight: transmissibility,
            recencyWeight: recency);

    [Fact]
    public void Create_succeeds_with_valid_ranges()
    {
        var result = CreateWith();
        Assert.True(result.IsSuccess);
        Assert.Equal(0.5, result.Value!.SkeletonSignificanceThreshold);
    }

    [Fact]
    public void Create_fails_naming_the_field_for_non_positive_canon_size()
    {
        var result = CreateWith(canonSize: 0);
        Assert.False(result.IsSuccess);
        Assert.Contains("CanonSizePerCommunity", result.Error);
    }

    [Fact]
    public void Create_fails_naming_the_field_for_threshold_below_zero()
    {
        var result = CreateWith(threshold: -0.1);
        Assert.False(result.IsSuccess);
        Assert.Contains("SkeletonSignificanceThreshold", result.Error);
    }

    [Fact]
    public void Create_fails_naming_the_field_for_threshold_above_one()
    {
        var result = CreateWith(threshold: 1.1);
        Assert.False(result.IsSuccess);
        Assert.Contains("SkeletonSignificanceThreshold", result.Error);
    }

    [Fact]
    public void Create_fails_naming_the_field_for_distortion_rate_outside_unit_interval()
    {
        var medium = new Dictionary<TransmissionMediumType, MediumFidelity>
        {
            [TransmissionMediumType.Song] = new(1.5, 1, DeathConditionType.Decay),
        };
        var result = CreateWith(medium: medium);
        Assert.False(result.IsSuccess);
        Assert.Contains("DistortionRatePerHop", result.Error);
    }

    [Fact]
    public void Create_fails_naming_the_field_for_operator_probability_outside_unit_interval()
    {
        var operators = new Dictionary<DistortionOperator, double>
        {
            [DistortionOperator.CausalLoss] = 1.2,
        };
        var result = CreateWith(operators: operators);
        Assert.False(result.IsSuccess);
        Assert.Contains("OperatorProbability", result.Error);
    }

    [Fact]
    public void Create_fails_naming_the_field_for_negative_importance_weight()
    {
        var result = CreateWith(importance: -1);
        Assert.False(result.IsSuccess);
        Assert.Contains("ImportanceWeight", result.Error);
    }

    [Fact]
    public void ScenarioRunner_Create_accepts_optional_history_rules()
    {
        var (world, _) = ScenarioRunner.Create(1, historyRules: HistoryRules.Default);
        Assert.True(world.HistoryRules.Enabled);
    }
}
