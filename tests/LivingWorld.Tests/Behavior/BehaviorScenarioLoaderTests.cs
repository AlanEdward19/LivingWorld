using System.Text.Json.Nodes;
using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Behavior;

/// <summary>Task 8: carga de <see cref="NeedsRules"/> + <see cref="ActionCatalog"/> a partir do
/// JSON de cenário, mesmo padrão de <c>PopulationScenarioLoaderTests</c> — campo obrigatório
/// ausente nomeia o campo no erro, happy path parseia tudo.</summary>
public class BehaviorScenarioLoaderTests
{
    private static JsonObject ValidRoot() => new()
    {
        ["HungerDecayPerHour"] = 2.0,
        ["ThirstDecayPerHour"] = 3.0,
        ["SleepDecayPerHour"] = 1.5,
        ["SocialDecayPerHour"] = 1.0,
        ["UrgencyThreshold"] = 70,
        ["MaxActionSelectionSteps"] = 10,
        ["HysteresisEnabled"] = true,
        ["ContinuityBonus"] = 5.0,
        ["HomelessSleepEfficiency"] = 0.5,
        ["MaxDurationHours"] = new JsonObject
        {
            ["Eat"] = 2, ["Sleep"] = 8, ["Work"] = 8, ["Socialize"] = 3, ["Travel"] = 6, ["Idle"] = 4,
        },
        ["RoutineSlots"] = new JsonArray
        {
            new JsonObject { ["ProfessionId"] = null, ["Stage"] = "Adult", ["HourStart"] = 8, ["HourEnd"] = 16, ["Action"] = "Work" },
        },
        ["DefaultAction"] = "Idle",
    };

    [Fact]
    public void Happy_path_parses_needs_rules_and_action_catalog()
    {
        var result = BehaviorScenarioLoader.Load(ValidRoot().ToJsonString());

        Assert.True(result.IsSuccess);
        Assert.Equal(2.0, result.Value!.NeedsRules.HungerDecayPerHour);
        Assert.Equal(10, result.Value.NeedsRules.MaxActionSelectionSteps);
        Assert.True(result.Value.NeedsRules.HysteresisEnabled);
        Assert.Equal(0.5, result.Value.NeedsRules.HomelessSleepEfficiency);
        Assert.Equal(ActionType.Idle, result.Value.ActionCatalog.DefaultAction);
        Assert.Equal(8, result.Value.ActionCatalog.MaxDurationHours[ActionType.Sleep]);
        Assert.Equal(ActionType.Work, result.Value.ActionCatalog.RoutineOf(null, LifeStage.Adult, 10));
    }

    [Theory]
    [InlineData("HungerDecayPerHour")]
    [InlineData("ThirstDecayPerHour")]
    [InlineData("SleepDecayPerHour")]
    [InlineData("SocialDecayPerHour")]
    [InlineData("UrgencyThreshold")]
    [InlineData("MaxActionSelectionSteps")]
    [InlineData("HysteresisEnabled")]
    [InlineData("ContinuityBonus")]
    [InlineData("HomelessSleepEfficiency")]
    [InlineData("MaxDurationHours")]
    [InlineData("RoutineSlots")]
    [InlineData("DefaultAction")]
    public void Missing_required_field_fails_naming_it(string field)
    {
        var root = ValidRoot();
        root.Remove(field);

        var result = BehaviorScenarioLoader.Load(root.ToJsonString());

        Assert.False(result.IsSuccess);
        Assert.Contains(field, result.Error!);
    }

    [Fact]
    public void Unknown_action_name_in_max_duration_hours_fails_naming_the_action()
    {
        var root = ValidRoot();
        ((JsonObject)root["MaxDurationHours"]!)["Flying"] = 3;

        var result = BehaviorScenarioLoader.Load(root.ToJsonString());

        Assert.False(result.IsSuccess);
        Assert.Contains("Flying", result.Error!);
    }

    [Fact]
    public void Missing_duration_for_one_of_the_six_actions_fails_naming_it()
    {
        var root = ValidRoot();
        ((JsonObject)root["MaxDurationHours"]!).Remove("Sleep");

        var result = BehaviorScenarioLoader.Load(root.ToJsonString());

        Assert.False(result.IsSuccess);
        Assert.Contains("Sleep", result.Error!);
    }

    [Fact]
    public void Routine_slot_missing_action_fails_naming_the_field()
    {
        var root = ValidRoot();
        ((JsonObject)((JsonArray)root["RoutineSlots"]!)[0]!).Remove("Action");

        var result = BehaviorScenarioLoader.Load(root.ToJsonString());

        Assert.False(result.IsSuccess);
        Assert.Contains("Action", result.Error!);
    }

    [Fact]
    public void Routine_slot_with_profession_specific_scope_resolves_before_the_any_fallback()
    {
        var root = ValidRoot();
        ((JsonArray)root["RoutineSlots"]!).Add(new JsonObject
        {
            ["ProfessionId"] = 5, ["Stage"] = "Adult", ["HourStart"] = 8, ["HourEnd"] = 16, ["Action"] = "Eat",
        });

        var result = BehaviorScenarioLoader.Load(root.ToJsonString());

        Assert.True(result.IsSuccess);
        Assert.Equal(ActionType.Eat, result.Value!.ActionCatalog.RoutineOf(new ProfessionType(5), LifeStage.Adult, 10));
        Assert.Equal(ActionType.Work, result.Value.ActionCatalog.RoutineOf(new ProfessionType(99), LifeStage.Adult, 10));
    }
}
