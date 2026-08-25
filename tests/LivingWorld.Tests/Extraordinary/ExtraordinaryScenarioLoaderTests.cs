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
    public void Descriptor_accepts_any_mechanic_token_as_scenario_data_without_named_power_types()
    {
        var root = JsonNode.Parse(ValidScenario())!.AsObject();
        root["Extraordinary"]!["Descriptors"]![0]!["Effects"] = new JsonArray(
            "mind.read", "transfer.health:20", "area:radius:3", "luck.capacity-bonus:10");
        root["Extraordinary"]!["Descriptors"]![0]!["Costs"] = new JsonArray();
        root["Extraordinary"]!["Descriptors"]![0]!["FailureModes"] = new JsonArray();
        root["Extraordinary"]!["Descriptors"]![0]!["Reliability"] = "Guaranteed";

        var result = ExtraordinaryScenarioLoader.Load(root.ToJsonString());

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(
            ["mind.read", "transfer.health:20", "area:radius:3", "luck.capacity-bonus:10"],
            result.Value!.Descriptors[0].Effects);
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
    public void Cultural_response_is_loaded_as_culture_owned_scenario_data()
    {
        var root = JsonNode.Parse(ValidScenario())!.AsObject();
        root["Extraordinary"]!["CulturalResponses"] = new JsonArray(new JsonObject
        {
            ["CultureId"] = 2,
            ["Manifestation"] = "visible-energy-form",
            ["Response"] = "fear",
        });

        var result = ExtraordinaryScenarioLoader.Load(root.ToJsonString());

        var response = Assert.Single(result.Value!.CulturalResponses);
        Assert.Equal((2, "visible-energy-form", "fear"),
            (response.CultureId, response.Manifestation, response.Response));
    }

    [Fact]
    public void Empty_required_axis_fails_naming_the_field()
    {
        var result = ExtraordinaryScenarioLoader.Load(ValidScenario().Replace("responsive-artifact", ""));

        Assert.Equal((false, "Extraordinary.Descriptors[].Source: valor obrigatório vazio"),
            (result.IsSuccess, result.Error));
    }

    [Fact]
    public void Empty_id_fails_with_no_partial_runtime_plan()
    {
        string invalid = ValidScenario().Replace(
            "\"Id\": \"will-shaped-artifact\"", "\"Id\": \"\"");

        var result = ExtraordinaryRuntimePlan.Load(invalid);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Contains("Extraordinary.Descriptors[].Id", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Failure_mode_without_resolution_check_is_rejected_at_the_boundary()
    {
        var result = ExtraordinaryScenarioLoader.Load(ValidScenario().Replace("ResolutionCheck", "Guaranteed"));

        Assert.Equal((false, "Extraordinary.Descriptors[].FailureModes: exige Reliability 'ResolutionCheck'"),
            (result.IsSuccess, result.Error));
    }

    [Theory]
    [InlineData("Unknown", "Mode")]
    [InlineData("Maybe", "Reliability")]
    public void Unknown_operational_token_fails_at_the_boundary(string token, string field)
    {
        string json = field == "Mode"
            ? ValidScenario().Replace("\"Mode\": \"Active\"", $"\"Mode\": \"{token}\"")
            : ValidScenario().Replace("\"Reliability\": \"ResolutionCheck\"", $"\"Reliability\": \"{token}\"");

        var result = ExtraordinaryRuntimePlan.Load(json);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Contains($"Extraordinary.Descriptors[].{field}", result.Error, StringComparison.Ordinal);
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

        Assert.Equal(
            [
                ExtraordinaryStateSystem.SystemName,
                ExtraordinaryPassiveTickSystem.SystemName,
                DimensionPortalSystem.SystemName,
                FaunaDominateSystem.SystemName,
                FloraGrowthSystem.SystemName,
            ],
            result.Value!.SystemNames);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public void Prevalence_outside_probability_range_fails_without_a_runtime_plan(double prevalence)
    {
        var root = JsonNode.Parse(ValidScenario())!.AsObject();
        root["Extraordinary"]!["Prevalence"] = prevalence;

        var result = ExtraordinaryRuntimePlan.Load(root.ToJsonString());

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Contains("Extraordinary.Prevalence", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Positive_prevalence_requires_at_least_one_descriptor()
    {
        var root = JsonNode.Parse(ValidScenario())!.AsObject();
        root["Extraordinary"]!["Prevalence"] = 0.5;
        root["Extraordinary"]!["Descriptors"] = new JsonArray();

        var result = ExtraordinaryRuntimePlan.Load(root.ToJsonString());

        Assert.Equal((false, "Extraordinary.Prevalence: exige ao menos um descritor"),
            (result.IsSuccess, result.Error));
    }

    [Fact]
    public void Sample_pack_of_eighteen_descriptors_loads_as_scenario_data()
    {
        string[] effects =
        [
            "npc.health:1", "npc.teleport:2", "npc.force-action:1",
            "construct.create:1x1:1:1:stone", "movement.flight:1", "attribute.fertility:0",
            "area:radius:3", "transfer.health:5", "mind.read", "luck.capacity-bonus:2",
            "luck.curse:10:100", "skill.copy:1", "environment.temperature:0:-1:4",
            "dimension.pocket-store", "fauna.dominate:1", "flora.growth-rate:1",
            "combat.strike:1", "gravity.self:0",
        ];
        var descriptors = new JsonArray();
        for (int i = 0; i < effects.Length; i++)
        {
            descriptors.Add(new JsonObject
            {
                ["Id"] = $"sample-{i:00}",
                ["Source"] = "sample-source",
                ["Effects"] = new JsonArray(effects[i]),
                ["Mode"] = "Active",
                ["Costs"] = new JsonArray(),
                ["Reliability"] = "Guaranteed",
                ["FailureModes"] = new JsonArray(),
                ["IntrinsicVulnerabilities"] = new JsonArray(),
                ["Manifestations"] = new JsonArray("visible-form"),
                ["AcquisitionRules"] = new JsonArray("authored"),
            });
        }

        var result = ExtraordinaryScenarioLoader.Load(new JsonObject
        {
            ["Extraordinary"] = new JsonObject
            {
                ["Enabled"] = true,
                ["Descriptors"] = descriptors,
            },
        }.ToJsonString());

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(18, result.Value!.Descriptors.Count);
        Assert.Equal(effects, result.Value.Descriptors.Select(item => Assert.Single(item.Effects)));
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
