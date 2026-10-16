using LivingWorld.Api.Visual.Catalogs;
using LivingWorld.Domain.History;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Integration.Stage4;

public sealed class CapabilityCoverageTests
{
    private static readonly IReadOnlyList<Type> ConcreteSystems =
        typeof(ISimulationSystem).Assembly.GetTypes()
            .Where(type => typeof(ISimulationSystem).IsAssignableFrom(type) && type is { IsClass: true, IsAbstract: false })
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();

    [Fact]
    public void Every_concrete_simulation_system_has_exactly_one_classification()
    {
        var counts = LivingWorldCapabilityCatalog.All
            .SelectMany(capability => capability.Systems)
            .GroupBy(type => type)
            .ToDictionary(group => group.Key, group => group.Count());

        var invalid = ConcreteSystems
            .Where(type => counts.GetValueOrDefault(type) != 1)
            .Select(type => $"{type.FullName}:{counts.GetValueOrDefault(type)}");

        Assert.Empty(invalid);
    }

    [Fact]
    public void Every_world_event_kind_has_exactly_one_classification()
    {
        var counts = LivingWorldCapabilityCatalog.All
            .SelectMany(capability => capability.Events)
            .GroupBy(kind => kind)
            .ToDictionary(group => group.Key, group => group.Count());

        var invalid = Enum.GetValues<WorldEventKind>()
            .Where(kind => counts.GetValueOrDefault(kind) != 1)
            .Select(kind => $"{kind}:{counts.GetValueOrDefault(kind)}");

        Assert.Empty(invalid);
    }

    [Fact]
    public void Only_example_counter_is_classified_as_diagnostic()
    {
        var diagnosticSystems = LivingWorldCapabilityCatalog.All
            .Where(capability => capability.Kind == CapabilityKind.DiagnosticOnly)
            .SelectMany(capability => capability.Systems)
            .OrderBy(type => type.FullName, StringComparer.Ordinal);

        Assert.Equal([typeof(ExampleCounterSystem)], diagnosticSystems);
    }

    [Fact]
    public void Diagnostic_classification_has_a_reason_and_no_frontend_consumer()
    {
        var invalid = LivingWorldCapabilityCatalog.All
            .Where(capability => capability.Kind == CapabilityKind.DiagnosticOnly)
            .Where(capability => string.IsNullOrWhiteSpace(capability.DiagnosticReason) || capability.ConsumerKeys.Count != 0)
            .Select(capability => capability.Id);

        Assert.Empty(invalid);
    }

    [Fact]
    public void Every_living_capability_declares_a_frontend_consumer_key()
    {
        var invalid = LivingWorldCapabilityCatalog.All
            .Where(capability => capability.Kind == CapabilityKind.LivingWorld)
            .Where(capability => capability.ConsumerKeys.Count == 0)
            .Select(capability => capability.Id);

        Assert.Empty(invalid);
    }

    [Fact]
    public void Capability_ids_are_unique()
    {
        var duplicateIds = LivingWorldCapabilityCatalog.All
            .GroupBy(capability => capability.Id, StringComparer.Ordinal)
            .Where(group => group.Count() != 1)
            .Select(group => group.Key);

        Assert.Empty(duplicateIds);
    }
}
