using LivingWorld.Domain;

namespace LivingWorld.Tests.Cities;

/// <summary>Fase 8, T2 (CITY-02, CITY-05, CITY-07, CITY-08): <see cref="CityRules"/> —
/// cenário-driven, todo limiar/peso/duração validado, mesmo padrão de <see cref="EconomyRules"/>.</summary>
public class CityRulesTests
{
    private static Result<CityRules> CreateWith(
        double foodShortageThreshold = 20, double housingShortageThreshold = 20, double securityShortageThreshold = 20,
        double emigrationRatePerDeficitUnit = 0.1,
        double migrationEmploymentWeight = 1, double migrationFoodWeight = 1,
        double migrationSecurityWeight = 1, double migrationFamilyTiesWeight = 1,
        double foundingConcentrationThreshold = 0.5, double foundingResourceThreshold = 0.5,
        double foundingRouteThreshold = 0.5, double foundingDefensibilityThreshold = 0.5,
        double foundingLeadershipThreshold = 0.5,
        long organizationTicks = 10, long materializationIdleTicksBeforeEligible = 5) =>
        CityRules.Create(
            enabled: true, foodShortageThreshold, housingShortageThreshold, securityShortageThreshold,
            emigrationRatePerDeficitUnit, migrationEmploymentWeight, migrationFoodWeight,
            migrationSecurityWeight, migrationFamilyTiesWeight, foundingConcentrationThreshold,
            foundingResourceThreshold, foundingRouteThreshold, foundingDefensibilityThreshold,
            foundingLeadershipThreshold, organizationTicks, materializationIdleTicksBeforeEligible);

    [Fact]
    public void Create_succeeds_with_valid_ranges()
    {
        var result = CreateWith();

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Create_rejects_food_shortage_threshold_out_of_range(double threshold)
    {
        var result = CreateWith(foodShortageThreshold: threshold);

        Assert.False(result.IsSuccess);
        Assert.Contains("FoodShortageThreshold", result.Error);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Create_rejects_housing_shortage_threshold_out_of_range(double threshold)
    {
        var result = CreateWith(housingShortageThreshold: threshold);

        Assert.False(result.IsSuccess);
        Assert.Contains("HousingShortageThreshold", result.Error);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Create_rejects_security_shortage_threshold_out_of_range(double threshold)
    {
        var result = CreateWith(securityShortageThreshold: threshold);

        Assert.False(result.IsSuccess);
        Assert.Contains("SecurityShortageThreshold", result.Error);
    }

    [Fact]
    public void Create_rejects_negative_emigration_rate()
    {
        var result = CreateWith(emigrationRatePerDeficitUnit: -0.1);

        Assert.False(result.IsSuccess);
        Assert.Contains("EmigrationRatePerDeficitUnit", result.Error);
    }

    [Theory]
    [InlineData(-1.0, 1, 1, 1)]
    [InlineData(1, -1.0, 1, 1)]
    [InlineData(1, 1, -1.0, 1)]
    [InlineData(1, 1, 1, -1.0)]
    public void Create_rejects_negative_migration_weights(
        double employment, double food, double security, double familyTies)
    {
        var result = CreateWith(
            migrationEmploymentWeight: employment, migrationFoodWeight: food,
            migrationSecurityWeight: security, migrationFamilyTiesWeight: familyTies);

        Assert.False(result.IsSuccess);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public void Create_rejects_founding_concentration_threshold_out_of_range(double threshold)
    {
        var result = CreateWith(foundingConcentrationThreshold: threshold);

        Assert.False(result.IsSuccess);
        Assert.Contains("FoundingConcentrationThreshold", result.Error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_rejects_organization_ticks_not_positive(long ticks)
    {
        var result = CreateWith(organizationTicks: ticks);

        Assert.False(result.IsSuccess);
        Assert.Contains("OrganizationTicks", result.Error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_rejects_materialization_idle_ticks_not_positive(long ticks)
    {
        var result = CreateWith(materializationIdleTicksBeforeEligible: ticks);

        Assert.False(result.IsSuccess);
        Assert.Contains("MaterializationIdleTicksBeforeEligible", result.Error);
    }

    [Fact]
    public void Disabled_default_is_never_enabled()
    {
        Assert.False(CityRules.Disabled.Enabled);
    }

    [Fact]
    public void Disabled_default_satisfies_its_own_valid_ranges()
    {
        var d = CityRules.Disabled;
        var result = CityRules.Create(
            d.Enabled, d.FoodShortageThreshold, d.HousingShortageThreshold, d.SecurityShortageThreshold,
            d.EmigrationRatePerDeficitUnit, d.MigrationEmploymentWeight, d.MigrationFoodWeight,
            d.MigrationSecurityWeight, d.MigrationFamilyTiesWeight, d.FoundingConcentrationThreshold,
            d.FoundingResourceThreshold, d.FoundingRouteThreshold, d.FoundingDefensibilityThreshold,
            d.FoundingLeadershipThreshold, d.OrganizationTicks, d.MaterializationIdleTicksBeforeEligible);

        Assert.True(result.IsSuccess);
    }
}
