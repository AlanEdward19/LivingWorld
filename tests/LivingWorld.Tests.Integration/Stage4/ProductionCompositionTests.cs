using LivingWorld.Api.Visual.Catalogs;
using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cities;
using LivingWorld.Simulation.Cities.Construction;
using LivingWorld.Simulation.Cities.Founding;
using LivingWorld.Simulation.Cities.Migration;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Hosting;
using LivingWorld.Simulation.Llm;
using LivingWorld.Simulation.Narrative;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;
using LivingWorld.Tests.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace LivingWorld.Tests.Integration.Stage4;

[Collection(ApiEndpointCollection.Name)]
public sealed class ProductionCompositionTests
{
    private readonly LivingWorldApiFactory _factory;

    public ProductionCompositionTests(LivingWorldApiFactory factory) => _factory = factory;

    [Fact]
    public void Api_world_clock_contains_every_living_system_exactly_once()
    {
        var host = _factory.Services.GetRequiredService<WorldHost>();
        var clock = host.Clock;
        var expected = LivingWorldCapabilityCatalog.All
            .Where(capability => capability.Kind == CapabilityKind.LivingWorld)
            .SelectMany(capability => capability.Systems)
            .Where(type => !LivingWorldCapabilityCatalog.All
                    .Single(capability => capability.Id == "EXTRAORDINARY")
                    .Systems.Contains(type)
                || host.Current.Extraordinary.Enabled)
            .OrderBy(type => type.FullName, StringComparer.Ordinal);
        var actual = clock.Systems
            .Where(system => system is not ExampleCounterSystem)
            .Select(system => system.GetType())
            .OrderBy(type => type.FullName, StringComparer.Ordinal);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Default_system_order_satisfies_every_declared_RunsAfter_constraint()
    {
        var order = ScenarioRunner.DefaultSystems(extraordinary: new LivingWorld.Domain.Extraordinary.ExtraordinaryScenarioData(enabled: true, descriptors: []))
            .Select(system => system.GetType())
            .ToList();

        var violations = new List<string>();
        foreach (var type in order)
            foreach (var attribute in type.GetCustomAttributes(typeof(RunsAfterAttribute), inherit: false).Cast<RunsAfterAttribute>())
            {
                int predecessorAt = order.IndexOf(attribute.Predecessor);
                if (predecessorAt < 0 || predecessorAt > order.IndexOf(type))
                    violations.Add($"{type.Name} deve rodar depois de {attribute.Predecessor.Name}");
            }

        Assert.Empty(violations);
        Assert.Contains(order, type => type.GetCustomAttributes(typeof(RunsAfterAttribute), false).Length > 0);
    }

    [Fact]
    public void Every_simulation_system_declares_its_capability()
    {
        var undeclared = typeof(ISimulationSystem).Assembly.GetTypes()
            .Where(type => typeof(ISimulationSystem).IsAssignableFrom(type) && type is { IsClass: true, IsAbstract: false })
            .Where(type => type.GetCustomAttributes(typeof(LivingCapabilityAttribute), inherit: false).Length == 0)
            .Select(type => type.Name);

        Assert.Empty(undeclared);
    }

    [Fact]
    public void Api_clock_uses_the_conversation_store_registered_for_endpoints()
    {
        var services = _factory.Services;
        var registered = services.GetRequiredService<ConversationSessionStore>();
        var clockStore = services.GetRequiredService<WorldHost>().Clock.Systems.OfType<ConversationSessionStore>().Single();

        Assert.Same(registered, clockStore);
    }

    [Fact]
    public void Api_clock_uses_the_chronicle_system_registered_for_endpoints()
    {
        var services = _factory.Services;
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
            typeof(MigrationSystem), typeof(RelocationArrivalSystem), typeof(MaterializationSystem),
            typeof(SettlementFoundingSystem), typeof(SpatialSettlementFoundingSystem),
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
