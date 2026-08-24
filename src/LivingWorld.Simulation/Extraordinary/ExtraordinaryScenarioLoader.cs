using System.Text.Json.Nodes;
using LivingWorld.Domain;

namespace LivingWorld.Simulation;

/// <summary>Valida o bloco extraordinário antes de qualquer estado runtime existir.</summary>
public static class ExtraordinaryScenarioLoader
{
    public static Result<ExtraordinaryScenarioData> Load(string json)
    {
        JsonObject root;
        try
        {
            root = JsonNode.Parse(json)?.AsObject() ?? throw new System.Text.Json.JsonException("corpo vazio");
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or InvalidOperationException)
        {
            return Result<ExtraordinaryScenarioData>.Fail($"json: {ex.Message}");
        }

        if (root["Extraordinary"] is null)
            return Result<ExtraordinaryScenarioData>.Ok(ExtraordinaryScenarioData.Disabled);
        if (root["Extraordinary"] is not JsonObject block)
            return Result<ExtraordinaryScenarioData>.Fail("Extraordinary: objeto inválido");
        if (!TryBool(block, "Enabled", out bool enabled))
            return Result<ExtraordinaryScenarioData>.Fail("Extraordinary.Enabled: campo obrigatório ausente ou inválido");
        if (block["Descriptors"] is not JsonArray descriptorsNode)
            return Result<ExtraordinaryScenarioData>.Fail("Extraordinary.Descriptors: campo obrigatório ausente ou inválido");

        var descriptors = new List<PowerDescriptor>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in descriptorsNode)
        {
            if (node is not JsonObject item)
                return Fail("Extraordinary.Descriptors: item inválido");

            var parsed = ParseDescriptor(item);
            if (!parsed.IsSuccess)
                return Result<ExtraordinaryScenarioData>.Fail(parsed.Error!);
            if (!ids.Add(parsed.Value!.Id))
                return Fail($"Extraordinary.Descriptors[].Id: duplicado '{parsed.Value.Id}'");
            descriptors.Add(parsed.Value);
        }

        return Result<ExtraordinaryScenarioData>.Ok(new ExtraordinaryScenarioData(enabled, descriptors));
    }

    private static Result<PowerDescriptor> ParseDescriptor(JsonObject item)
    {
        var id = RequiredText(item, "Id");
        if (!id.IsSuccess) return Result<PowerDescriptor>.Fail(id.Error!);
        var source = RequiredText(item, "Source");
        if (!source.IsSuccess) return Result<PowerDescriptor>.Fail(source.Error!);
        var effects = TextList(item, "Effects", required: true);
        if (!effects.IsSuccess) return Result<PowerDescriptor>.Fail(effects.Error!);
        if (effects.Value!.Any(effect => !HasTwoParts(effect)))
            return Result<PowerDescriptor>.Fail("Extraordinary.Descriptors[].Effects: use 'alvo:magnitude'");
        var mode = RequiredText(item, "Mode");
        if (!mode.IsSuccess) return Result<PowerDescriptor>.Fail(mode.Error!);
        var reliability = RequiredText(item, "Reliability");
        if (!reliability.IsSuccess) return Result<PowerDescriptor>.Fail(reliability.Error!);

        var costs = TextList(item, "Costs");
        var failures = TextList(item, "FailureModes");
        var vulnerabilities = TextList(item, "IntrinsicVulnerabilities");
        var manifestations = TextList(item, "Manifestations");
        var acquisitions = TextList(item, "AcquisitionRules");
        foreach (var list in new[] { costs, failures, vulnerabilities, manifestations, acquisitions })
            if (!list.IsSuccess) return Result<PowerDescriptor>.Fail(list.Error!);

        if (failures.Value!.Count > 0 && reliability.Value != "ResolutionCheck")
            return Result<PowerDescriptor>.Fail(
                "Extraordinary.Descriptors[].FailureModes: exige Reliability 'ResolutionCheck'");

        var appearance = ParseAppearance(item);
        if (!appearance.IsSuccess) return Result<PowerDescriptor>.Fail(appearance.Error!);
        var need = ParseNeedSubstitution(item);
        if (!need.IsSuccess) return Result<PowerDescriptor>.Fail(need.Error!);
        double senescence = 1;
        if (item["SenescenceRateMultiplier"] is not null
            && (!TryDouble(item, "SenescenceRateMultiplier", out senescence) || senescence < 0))
            return Result<PowerDescriptor>.Fail(
                "Extraordinary.Descriptors[].SenescenceRateMultiplier: deve ser não negativo");
        string? manifestationCondition = null;
        if (item["ManifestationCondition"] is not null)
        {
            var condition = RequiredText(item, "ManifestationCondition");
            if (!condition.IsSuccess) return Result<PowerDescriptor>.Fail(condition.Error!);
            manifestationCondition = condition.Value;
        }

        return Result<PowerDescriptor>.Ok(new PowerDescriptor(
            id.Value!, source.Value!, effects.Value!, mode.Value!, costs.Value!, reliability.Value!,
            failures.Value, vulnerabilities.Value!, manifestations.Value!, acquisitions.Value!,
            appearance.Value, need.Value, senescence, manifestationCondition));
    }

    private static Result<ExtraordinaryAppearanceDescriptor?> ParseAppearance(JsonObject item)
    {
        if (item["Appearance"] is null)
            return Result<ExtraordinaryAppearanceDescriptor?>.Ok(null);
        if (item["Appearance"] is not JsonObject appearance
            || !TryDouble(appearance, "ScaleMultiplier", out double scale) || scale <= 0)
            return Result<ExtraordinaryAppearanceDescriptor?>.Fail(
                "Extraordinary.Descriptors[].Appearance.ScaleMultiplier: deve ser positivo");
        string skinTint = OptionalText(appearance, "SkinTint");
        string movementTrail = OptionalText(appearance, "MovementTrail");
        return Result<ExtraordinaryAppearanceDescriptor?>.Ok(
            new ExtraordinaryAppearanceDescriptor(scale, skinTint, movementTrail));
    }

    private static Result<NeedSubstitutionDescriptor?> ParseNeedSubstitution(JsonObject item)
    {
        if (item["NeedSubstitution"] is null)
            return Result<NeedSubstitutionDescriptor?>.Ok(null);
        if (item["NeedSubstitution"] is not JsonObject need)
            return Result<NeedSubstitutionDescriptor?>.Fail(
                "Extraordinary.Descriptors[].NeedSubstitution: objeto inválido");
        var replaces = RequiredNestedText(need, "NeedSubstitution", "ReplacesNeed");
        if (!replaces.IsSuccess) return Result<NeedSubstitutionDescriptor?>.Fail(replaces.Error!);
        if (!TryInt(need, "ResourceId", out int resourceId) || resourceId < 0)
            return Result<NeedSubstitutionDescriptor?>.Fail(
                "Extraordinary.Descriptors[].NeedSubstitution.ResourceId: deve ser não negativo");
        if (!TryLong(need, "UnitsPerUse", out long units) || units <= 0)
            return Result<NeedSubstitutionDescriptor?>.Fail(
                "Extraordinary.Descriptors[].NeedSubstitution.UnitsPerUse: deve ser positivo");
        return Result<NeedSubstitutionDescriptor?>.Ok(
            new NeedSubstitutionDescriptor(replaces.Value!, new ResourceType(resourceId), units));
    }

    private static Result<string> RequiredText(JsonObject item, string field)
    {
        string prefix = $"Extraordinary.Descriptors[].{field}";
        if (item[field] is not JsonValue value || !value.TryGetValue<string>(out var text))
            return Result<string>.Fail($"{prefix}: campo obrigatório ausente ou inválido");
        if (string.IsNullOrWhiteSpace(text))
            return Result<string>.Fail($"{prefix}: valor obrigatório vazio");
        return Result<string>.Ok(text);
    }

    private static Result<string> RequiredNestedText(JsonObject item, string parent, string field)
    {
        if (item[field] is not JsonValue value || !value.TryGetValue<string>(out var text))
            return Result<string>.Fail(
                $"Extraordinary.Descriptors[].{parent}.{field}: campo obrigatório ausente ou inválido");
        if (string.IsNullOrWhiteSpace(text))
            return Result<string>.Fail($"Extraordinary.Descriptors[].{parent}.{field}: valor obrigatório vazio");
        return Result<string>.Ok(text);
    }

    private static string OptionalText(JsonObject item, string field) =>
        item[field] is JsonValue value && value.TryGetValue<string>(out var text) ? text : "";

    private static Result<IReadOnlyList<string>> TextList(JsonObject item, string field, bool required = false)
    {
        string prefix = $"Extraordinary.Descriptors[].{field}";
        if (item[field] is null && !required)
            return Result<IReadOnlyList<string>>.Ok([]);
        if (item[field] is not JsonArray array || required && array.Count == 0)
            return Result<IReadOnlyList<string>>.Fail($"{prefix}: campo obrigatório ausente ou inválido");

        var values = new List<string>();
        foreach (var node in array)
        {
            if (node is not JsonValue value || !value.TryGetValue<string>(out var text) || string.IsNullOrWhiteSpace(text))
                return Result<IReadOnlyList<string>>.Fail($"{prefix}: item vazio ou inválido");
            values.Add(text);
        }
        return Result<IReadOnlyList<string>>.Ok(values);
    }

    private static bool HasTwoParts(string effect)
    {
        int separator = effect.IndexOf(':');
        return separator > 0 && separator < effect.Length - 1;
    }

    private static bool TryBool(JsonObject node, string field, out bool value)
    {
        value = false;
        return node[field] is JsonValue jsonValue && jsonValue.TryGetValue(out value);
    }

    private static bool TryDouble(JsonObject node, string field, out double value)
    {
        value = 0;
        return node[field] is JsonValue jsonValue && jsonValue.TryGetValue(out value)
            && !double.IsNaN(value) && !double.IsInfinity(value);
    }

    private static bool TryInt(JsonObject node, string field, out int value)
    {
        value = 0;
        return node[field] is JsonValue jsonValue && jsonValue.TryGetValue(out value);
    }

    private static bool TryLong(JsonObject node, string field, out long value)
    {
        value = 0;
        return node[field] is JsonValue jsonValue && jsonValue.TryGetValue(out value);
    }

    private static Result<ExtraordinaryScenarioData> Fail(string error) =>
        Result<ExtraordinaryScenarioData>.Fail(error);
}
