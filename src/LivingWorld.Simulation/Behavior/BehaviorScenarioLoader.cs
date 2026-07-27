using System.Text.Json.Nodes;
using LivingWorld.Domain;

namespace LivingWorld.Simulation;

/// <summary>Dado de comportamento resolvido de um cenário (task 8): parâmetros do utility AI
/// (<see cref="NeedsRules"/>) e o catálogo de ações/rotina (<see cref="ActionCatalog"/>).</summary>
public sealed record BehaviorScenarioData(NeedsRules NeedsRules, ActionCatalog ActionCatalog);

/// <summary>Carrega <see cref="NeedsRules"/> e <see cref="ActionCatalog"/> de um cenário (task
/// 8): nenhum parâmetro do utility AI hardcoded em C# (R3). Mesmo padrão de
/// <see cref="PopulationScenarioLoader"/> — parse manual por <see cref="JsonNode"/>, validação
/// na borda, erro nomeia o campo ausente.</summary>
public static class BehaviorScenarioLoader
{
    public static Result<BehaviorScenarioData> Load(string json)
    {
        JsonObject root;
        try
        {
            root = JsonNode.Parse(json)?.AsObject() ?? throw new System.Text.Json.JsonException("corpo vazio");
        }
        catch (System.Text.Json.JsonException ex)
        {
            return Result<BehaviorScenarioData>.Fail($"json: {ex.Message}");
        }

        var needsRulesResult = ParseNeedsRules(root);
        if (!needsRulesResult.IsSuccess)
            return Result<BehaviorScenarioData>.Fail(needsRulesResult.Error!);

        var actionCatalogResult = ParseActionCatalog(root);
        if (!actionCatalogResult.IsSuccess)
            return Result<BehaviorScenarioData>.Fail(actionCatalogResult.Error!);

        return Result<BehaviorScenarioData>.Ok(new BehaviorScenarioData(needsRulesResult.Value!, actionCatalogResult.Value!));
    }

    private static Result<NeedsRules> ParseNeedsRules(JsonObject root)
    {
        if (!TryGetDouble(root, "HungerDecayPerHour", out var hungerDecay))
            return Result<NeedsRules>.Fail("HungerDecayPerHour: campo obrigatório ausente ou inválido");
        if (!TryGetDouble(root, "ThirstDecayPerHour", out var thirstDecay))
            return Result<NeedsRules>.Fail("ThirstDecayPerHour: campo obrigatório ausente ou inválido");
        if (!TryGetDouble(root, "SleepDecayPerHour", out var sleepDecay))
            return Result<NeedsRules>.Fail("SleepDecayPerHour: campo obrigatório ausente ou inválido");
        if (!TryGetDouble(root, "SocialDecayPerHour", out var socialDecay))
            return Result<NeedsRules>.Fail("SocialDecayPerHour: campo obrigatório ausente ou inválido");
        if (!TryGetInt(root, "UrgencyThreshold", out var urgencyThreshold))
            return Result<NeedsRules>.Fail("UrgencyThreshold: campo obrigatório ausente ou inválido");
        if (!TryGetInt(root, "MaxActionSelectionSteps", out var maxActionSelectionSteps))
            return Result<NeedsRules>.Fail("MaxActionSelectionSteps: campo obrigatório ausente ou inválido");
        if (!TryGetBool(root, "HysteresisEnabled", out var hysteresisEnabled))
            return Result<NeedsRules>.Fail("HysteresisEnabled: campo obrigatório ausente ou inválido");
        if (!TryGetDouble(root, "ContinuityBonus", out var continuityBonus))
            return Result<NeedsRules>.Fail("ContinuityBonus: campo obrigatório ausente ou inválido");
        if (!TryGetDouble(root, "HomelessSleepEfficiency", out var homelessSleepEfficiency))
            return Result<NeedsRules>.Fail("HomelessSleepEfficiency: campo obrigatório ausente ou inválido");

        return NeedsRules.Create(
            hungerDecay, thirstDecay, sleepDecay, socialDecay,
            urgencyThreshold, maxActionSelectionSteps, hysteresisEnabled, continuityBonus, homelessSleepEfficiency);
    }

    private static Result<ActionCatalog> ParseActionCatalog(JsonObject root)
    {
        if (root["MaxDurationHours"] is not JsonObject durationsNode)
            return Result<ActionCatalog>.Fail("MaxDurationHours: campo obrigatório ausente");

        var maxDurationHours = new Dictionary<ActionType, int>();
        foreach (var (key, node) in durationsNode)
        {
            if (!Enum.TryParse<ActionType>(key, out var action))
                return Result<ActionCatalog>.Fail($"MaxDurationHours: ação desconhecida \"{key}\"");
            if (node is not JsonValue v || !v.TryGetValue<int>(out var hours))
                return Result<ActionCatalog>.Fail($"MaxDurationHours[{key}]: valor inválido");
            maxDurationHours[action] = hours;
        }

        if (root["RoutineSlots"] is not JsonArray slotsNode)
            return Result<ActionCatalog>.Fail("RoutineSlots: campo obrigatório ausente");

        var routineSlots = new List<RoutineSlot>();
        foreach (var node in slotsNode)
        {
            if (node is not JsonObject slot)
                return Result<ActionCatalog>.Fail("RoutineSlots: item inválido");

            int? professionId = null;
            if (slot.ContainsKey("ProfessionId") && slot["ProfessionId"] is JsonValue profNode)
            {
                if (!profNode.TryGetValue<int>(out var profId))
                    return Result<ActionCatalog>.Fail("RoutineSlots[].ProfessionId: valor inválido");
                professionId = profId;
            }

            if (slot["Stage"] is not JsonValue stageNode || !stageNode.TryGetValue<string>(out var stageText)
                || !Enum.TryParse<LifeStage>(stageText, out var stage))
                return Result<ActionCatalog>.Fail("RoutineSlots[].Stage: campo obrigatório ausente ou inválido");
            if (slot["HourStart"] is not JsonValue hourStartNode || !hourStartNode.TryGetValue<int>(out var hourStart))
                return Result<ActionCatalog>.Fail("RoutineSlots[].HourStart: campo obrigatório ausente ou inválido");
            if (slot["HourEnd"] is not JsonValue hourEndNode || !hourEndNode.TryGetValue<int>(out var hourEnd))
                return Result<ActionCatalog>.Fail("RoutineSlots[].HourEnd: campo obrigatório ausente ou inválido");
            if (slot["Action"] is not JsonValue actionNode || !actionNode.TryGetValue<string>(out var actionText)
                || !Enum.TryParse<ActionType>(actionText, out var slotAction))
                return Result<ActionCatalog>.Fail("RoutineSlots[].Action: campo obrigatório ausente ou inválido");

            routineSlots.Add(new RoutineSlot(professionId, stage, hourStart, hourEnd, slotAction));
        }

        if (root["DefaultAction"] is not JsonValue defaultActionNode || !defaultActionNode.TryGetValue<string>(out var defaultActionText)
            || !Enum.TryParse<ActionType>(defaultActionText, out var defaultAction))
            return Result<ActionCatalog>.Fail("DefaultAction: campo obrigatório ausente ou inválido");

        return ActionCatalog.Create(maxDurationHours, routineSlots, defaultAction);
    }

    private static bool TryGetInt(JsonObject root, string field, out int value)
    {
        value = 0;
        return root[field] is JsonValue v && v.TryGetValue(out value);
    }

    private static bool TryGetDouble(JsonObject root, string field, out double value)
    {
        value = 0;
        return root[field] is JsonValue v && v.TryGetValue(out value);
    }

    private static bool TryGetBool(JsonObject root, string field, out bool value)
    {
        value = false;
        return root[field] is JsonValue v && v.TryGetValue(out value);
    }
}
