using System.Text.Json.Nodes;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Extraordinary;

public sealed class ExtraordinaryScenarioLoaderTests
{
    [Fact]
    public void Missing_block_disables_extraordinary_and_builds_an_empty_runtime_plan()
    {
        var loaded = ExtraordinaryScenarioLoader.Load("{}");
        var plan = ExtraordinaryRuntimePlan.Create(loaded.Value!);

        Assert.Equal(
            (true, false, 0, 0, 0, 0),
            (loaded.IsSuccess, loaded.Value!.Enabled, loaded.Value.Descriptors.Count,
                plan.Value!.CarrierIds.Count, plan.Value.Events.Count, plan.Value.SystemNames.Count));
    }

    [Fact]
    public void Valid_descriptor_preserves_every_compositional_axis_as_scenario_data()
    {
        var result = ExtraordinaryScenarioLoader.Load(ValidScenario());

        Assert.True(result.IsSuccess, result.Error);
        var descriptor = Assert.Single(result.Value!.Descriptors);
        Assert.Equal(
            ("will-shaped-artifact", "responsive-artifact", "Active", "ResolutionCheck",
                "movement:construct", "fatigue:2", "loss-of-control", "source-disruption",
                "visible-energy-form", "item-bond"),
            (descriptor.Id, descriptor.Source, descriptor.Mode, descriptor.Reliability,
                Assert.Single(descriptor.Effects), Assert.Single(descriptor.Costs),
                Assert.Single(descriptor.FailureModes), Assert.Single(descriptor.IntrinsicVulnerabilities),
                Assert.Single(descriptor.Manifestations), Assert.Single(descriptor.AcquisitionRules)));
    }

    [Fact]
    public void Duplicate_descriptor_id_fails_at_the_scenario_boundary()
    {
        var root = JsonNode.Parse(ValidScenario())!.AsObject();
        var descriptors = root["Extraordinary"]!["Descriptors"]!.AsArray();
        descriptors.Add(descriptors[0]!.DeepClone());

        var result = ExtraordinaryScenarioLoader.Load(root.ToJsonString());

        Assert.Equal((false, "Extraordinary.Descriptors[].Id: duplicado 'will-shaped-artifact'"),
            (result.IsSuccess, result.Error));
    }

    [Fact]
    public void Empty_required_axis_fails_naming_the_field()
    {
        var result = ExtraordinaryScenarioLoader.Load(ValidScenario().Replace("responsive-artifact", ""));

        Assert.Equal((false, "Extraordinary.Descriptors[].Source: valor obrigatório vazio"),
            (result.IsSuccess, result.Error));
    }

    [Fact]
    public void Failure_mode_without_resolution_check_is_rejected_at_the_boundary()
    {
        var result = ExtraordinaryScenarioLoader.Load(ValidScenario().Replace("ResolutionCheck", "Guaranteed"));

        Assert.Equal((false, "Extraordinary.Descriptors[].FailureModes: exige Reliability 'ResolutionCheck'"),
            (result.IsSuccess, result.Error));
    }

    [Fact]
    public void Invalid_descriptor_cannot_produce_a_partial_runtime_plan()
    {
        string invalid = ValidScenario().Replace("responsive-artifact", "");

        var result = ExtraordinaryRuntimePlan.Load(invalid);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Contains("Extraordinary.Descriptors[].Source", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Enabled_runtime_plan_registers_only_the_extraordinary_state_system()
    {
        var loaded = ExtraordinaryScenarioLoader.Load(ValidScenario());

        var result = ExtraordinaryRuntimePlan.Create(loaded.Value!);

        Assert.Equal((true, 0, 0, 1, ExtraordinaryStateSystem.SystemName),
            (result.IsSuccess, result.Value!.CarrierIds.Count, result.Value.Events.Count,
                result.Value.SystemNames.Count, Assert.Single(result.Value.SystemNames)));
    }

    private static string ValidScenario() => """
    {
      "Extraordinary": {
        "Enabled": true,
        "Descriptors": [
          {
            "Id": "will-shaped-artifact",
            "Source": "responsive-artifact",
            "Effects": ["movement:construct"],
            "Mode": "Active",
            "Costs": ["fatigue:2"],
            "Reliability": "ResolutionCheck",
            "FailureModes": ["loss-of-control"],
            "IntrinsicVulnerabilities": ["source-disruption"],
            "Manifestations": ["visible-energy-form"],
            "AcquisitionRules": ["item-bond"]
          }
        ]
      }
    }
    """;
}
