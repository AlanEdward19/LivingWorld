namespace LivingWorld.Domain;

/// <summary>Todo limiar/peso/duração de crescimento, migração, fundação e materialização da Fase
/// 8, cenário-driven (R3) — nenhum literal em C#, mesmo padrão de <see cref="EconomyRules"/>/
/// <see cref="FamilyRules"/>.</summary>
public sealed record CityRules(
    bool Enabled,
    double FoodShortageThreshold,
    double HousingShortageThreshold,
    double SecurityShortageThreshold,
    double EmigrationRatePerDeficitUnit,
    double MigrationEmploymentWeight,
    double MigrationFoodWeight,
    double MigrationSecurityWeight,
    double MigrationFamilyTiesWeight,
    double FoundingConcentrationThreshold,
    double FoundingResourceThreshold,
    double FoundingRouteThreshold,
    double FoundingDefensibilityThreshold,
    double FoundingLeadershipThreshold,
    long OrganizationTicks,
    long MaterializationIdleTicksBeforeEligible,
    int AbsorptionRingCells = 3)
{
    public static Result<CityRules> Create(
        bool enabled,
        double foodShortageThreshold,
        double housingShortageThreshold,
        double securityShortageThreshold,
        double emigrationRatePerDeficitUnit,
        double migrationEmploymentWeight,
        double migrationFoodWeight,
        double migrationSecurityWeight,
        double migrationFamilyTiesWeight,
        double foundingConcentrationThreshold,
        double foundingResourceThreshold,
        double foundingRouteThreshold,
        double foundingDefensibilityThreshold,
        double foundingLeadershipThreshold,
        long organizationTicks,
        long materializationIdleTicksBeforeEligible,
        int absorptionRingCells = 3)
    {
        if (foodShortageThreshold is < 0 or > 100)
            return Result<CityRules>.Fail("FoodShortageThreshold: fora de [0,100]");
        if (housingShortageThreshold is < 0 or > 100)
            return Result<CityRules>.Fail("HousingShortageThreshold: fora de [0,100]");
        if (securityShortageThreshold is < 0 or > 100)
            return Result<CityRules>.Fail("SecurityShortageThreshold: fora de [0,100]");
        if (emigrationRatePerDeficitUnit < 0)
            return Result<CityRules>.Fail("EmigrationRatePerDeficitUnit: deve ser >= 0");

        if (migrationEmploymentWeight < 0)
            return Result<CityRules>.Fail("MigrationEmploymentWeight: deve ser >= 0");
        if (migrationFoodWeight < 0)
            return Result<CityRules>.Fail("MigrationFoodWeight: deve ser >= 0");
        if (migrationSecurityWeight < 0)
            return Result<CityRules>.Fail("MigrationSecurityWeight: deve ser >= 0");
        if (migrationFamilyTiesWeight < 0)
            return Result<CityRules>.Fail("MigrationFamilyTiesWeight: deve ser >= 0");

        if (foundingConcentrationThreshold is < 0 or > 1)
            return Result<CityRules>.Fail("FoundingConcentrationThreshold: fora de [0,1]");
        if (foundingResourceThreshold is < 0 or > 1)
            return Result<CityRules>.Fail("FoundingResourceThreshold: fora de [0,1]");
        if (foundingRouteThreshold is < 0 or > 1)
            return Result<CityRules>.Fail("FoundingRouteThreshold: fora de [0,1]");
        if (foundingDefensibilityThreshold is < 0 or > 1)
            return Result<CityRules>.Fail("FoundingDefensibilityThreshold: fora de [0,1]");
        if (foundingLeadershipThreshold is < 0 or > 1)
            return Result<CityRules>.Fail("FoundingLeadershipThreshold: fora de [0,1]");

        if (organizationTicks <= 0)
            return Result<CityRules>.Fail("OrganizationTicks: deve ser > 0");
        if (materializationIdleTicksBeforeEligible <= 0)
            return Result<CityRules>.Fail("MaterializationIdleTicksBeforeEligible: deve ser > 0");
        if (absorptionRingCells < 0)
            return Result<CityRules>.Fail("AbsorptionRingCells: deve ser >= 0");

        return Result<CityRules>.Ok(new CityRules(
            enabled, foodShortageThreshold, housingShortageThreshold, securityShortageThreshold,
            emigrationRatePerDeficitUnit, migrationEmploymentWeight, migrationFoodWeight,
            migrationSecurityWeight, migrationFamilyTiesWeight, foundingConcentrationThreshold,
            foundingResourceThreshold, foundingRouteThreshold, foundingDefensibilityThreshold,
            foundingLeadershipThreshold, organizationTicks, materializationIdleTicksBeforeEligible,
            absorptionRingCells));
    }

    /// <summary>Default de <see cref="WorldState"/> para cenário que ainda não declara cidades —
    /// mesma disciplina de <see cref="EconomyRules.Disabled"/>: <see cref="Enabled"/> falso,
    /// faixas mínimas válidas. Nunca usado por um cenário real.</summary>
    public static readonly CityRules Disabled = new(
        Enabled: false,
        FoodShortageThreshold: 0, HousingShortageThreshold: 0, SecurityShortageThreshold: 0,
        EmigrationRatePerDeficitUnit: 0,
        MigrationEmploymentWeight: 0, MigrationFoodWeight: 0, MigrationSecurityWeight: 0, MigrationFamilyTiesWeight: 0,
        FoundingConcentrationThreshold: 0, FoundingResourceThreshold: 0, FoundingRouteThreshold: 0,
        FoundingDefensibilityThreshold: 0, FoundingLeadershipThreshold: 0,
        OrganizationTicks: 1, MaterializationIdleTicksBeforeEligible: 1);
}
