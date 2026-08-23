using LivingWorld.Api.Visual;
using LivingWorld.Domain;
using LivingWorld.Simulation;
using LivingWorld.Simulation.Economy;
using LivingWorld.Simulation.History;
using LivingWorld.Simulation.Narrative;
using LivingWorld.Simulation.Periods;
using LivingWorld.Simulation.Population;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace LivingWorld.Tests.Stage4;

public sealed class ProductionCompositionTests
{
    private static readonly Type[] ExpectedLivingOrder =
    [
        typeof(PeriodEvolutionSystem),
        typeof(MortalitySystem),
        typeof(FactToReportConversionScheduler),
        typeof(BookRediscoverySystem),
        typeof(ColdArchiveSystem),
        typeof(CourtshipSystem),
        typeof(NatalitySystem),
        typeof(NeedsDecaySystem),
        typeof(BehaviorDecisionSystem),
        typeof(ResourceProcessSystem),
        typeof(EmploymentSystem),
        typeof(RelationshipSystem),
        typeof(SkillPracticeSystem),
        typeof(SkillTeachingSystem),
        typeof(ProductionSystem),
        typeof(CropSystem),
        typeof(MarketPricingSystem),
        typeof(WagePaymentSystem),
        typeof(CityGrowthSystem),
        typeof(ConstructionDemandSystem),
        typeof(ConstructionSystem),
        typeof(MigrationSystem),
        typeof(RelocationArrivalSystem),
        typeof(MaterializationSystem),
        typeof(SettlementFoundingSystem),
        typeof(ChronicleGenerationSystem),
        typeof(ConversationSessionStore),
    ];

    [Fact]
    public void Api_world_clock_contains_every_living_system_exactly_once()
    {
        using var factory = new WebApplicationFactory<Program>();
        var clock = factory.Services.GetRequiredService<WorldHost>().Clock;
        var expected = LivingWorldCapabilityCatalog.All
            .Where(capability => capability.Kind == CapabilityKind.LivingWorld)
            .SelectMany(capability => capability.Systems)
            .OrderBy(type => type.FullName, StringComparer.Ordinal);
        var actual = clock.Systems
            .Where(system => system is not ExampleCounterSystem)
            .Select(system => system.GetType())
            .OrderBy(type => type.FullName, StringComparer.Ordinal);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Production_living_system_order_is_explicit_and_stable()
    {
        var actual = ScenarioRunner.DefaultSystems()
            .Where(system => system is not ExampleCounterSystem)
            .Select(system => system.GetType());

        Assert.Equal(ExpectedLivingOrder, actual);
    }

    [Fact]
    public void Api_clock_uses_the_conversation_store_registered_for_endpoints()
    {
        using var factory = new WebApplicationFactory<Program>();
        var services = factory.Services;
        var registered = services.GetRequiredService<ConversationSessionStore>();
        var clockStore = services.GetRequiredService<WorldHost>().Clock.Systems.OfType<ConversationSessionStore>().Single();

        Assert.Same(registered, clockStore);
    }

    [Fact]
    public void Api_clock_uses_the_chronicle_system_registered_for_endpoints()
    {
        using var factory = new WebApplicationFactory<Program>();
        var services = factory.Services;
        var registered = services.GetRequiredService<ChronicleGenerationSystem>();
        var clockSystem = services.GetRequiredService<WorldHost>().Clock.Systems.OfType<ChronicleGenerationSystem>().Single();

        Assert.Same(registered, clockSystem);
    }

    [Fact]
    public void Disabling_the_city_group_changes_the_bounded_canonical_result()
    {
        var enabled = CreateCityPressureWorld();
        var disabled = CreateCityPressureWorld();
        var cityTypes = new HashSet<Type>
        {
            typeof(CityGrowthSystem), typeof(ConstructionDemandSystem), typeof(ConstructionSystem),
            typeof(MigrationSystem), typeof(RelocationArrivalSystem), typeof(MaterializationSystem), typeof(SettlementFoundingSystem),
        };
        var systemsWithoutCities = ScenarioRunner.DefaultSystems()
            .Where(system => !cityTypes.Contains(system.GetType()))
            .ToArray();

        new WorldClock(ScenarioRunner.DefaultSystems()).Run(enabled, enabled.Calendar.HoursPerDay);
        new WorldClock(systemsWithoutCities).Run(disabled, disabled.Calendar.HoursPerDay);

        Assert.NotEqual(WorldSnapshot.CanonicalHash(enabled), WorldSnapshot.CanonicalHash(disabled));
    }

    private static WorldState CreateCityPressureWorld()
    {
        var cityRules = CityRules.Create(
            enabled: true, foodShortageThreshold: 0, housingShortageThreshold: 0,
            securityShortageThreshold: 100, emigrationRatePerDeficitUnit: 0.1,
            migrationEmploymentWeight: 1, migrationFoodWeight: 1, migrationSecurityWeight: 1,
            migrationFamilyTiesWeight: 1, foundingConcentrationThreshold: 1,
            foundingResourceThreshold: 1, foundingRouteThreshold: 1,
            foundingDefensibilityThreshold: 1, foundingLeadershipThreshold: 1,
            organizationTicks: 10, materializationIdleTicksBeforeEligible: 5).Value!;
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, seed: 91, ScenarioRunner.DefaultMap(91),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules, economyRules: ScenarioRunner.DefaultEconomyRules,
            cityRules: cityRules);
        world.AddCity(new City(
            new CityId(Guid.Parse("00000000-0000-0000-0000-000000000091")),
            ScenarioRunner.DefaultVillageLocation, 0, null, new AggregatePopulationPool(10, 100, 100),
            poolNpcIds: world.ReserveNpcIdBlock(10)));
        return world;
    }
}
