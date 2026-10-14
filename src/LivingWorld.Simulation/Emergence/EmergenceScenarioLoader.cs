using System.Text.Json.Nodes;
using LivingWorld.Domain.Emergence;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Simulation.Emergence;

/// <summary>Valida o bloco opcional <c>Emergence</c> do cenário (eixos, limiar de novidade e o que abre um protótipo). Sem o
/// bloco, a emergência fica desligada e o mundo é o de sempre. Campo ausente usa o valor de <see cref="EmergenceRules.Mobility"/>.</summary>
public static class EmergenceScenarioLoader
{
    public static Result<EmergenceRules> Load(string json)
    {
        JsonObject root;
        try
        {
            root = JsonNode.Parse(json)?.AsObject() ?? throw new System.Text.Json.JsonException("corpo vazio");
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or InvalidOperationException)
        {
            return Result<EmergenceRules>.Fail($"json: {ex.Message}");
        }

        if (root["Emergence"] is null) return Result<EmergenceRules>.Ok(EmergenceRules.Disabled);
        if (root["Emergence"] is not JsonObject block) return Result<EmergenceRules>.Fail("Emergence: objeto inválido");
        if (!Bool(block, "Enabled", out bool enabled)) return Result<EmergenceRules>.Fail("Emergence.Enabled: campo obrigatório ausente ou inválido");

        var d = EmergenceRules.Mobility;
        IReadOnlyList<string> axes = d.AxisNames;
        if (block["AxisNames"] is not null)
        {
            if (block["AxisNames"] is not JsonArray array || array.Any(n => n is not JsonValue v || !v.TryGetValue<string>(out var s) || string.IsNullOrWhiteSpace(s)))
                return Result<EmergenceRules>.Fail("Emergence.AxisNames: lista de nomes inválida");
            axes = array.Select(n => n!.GetValue<string>()).ToList();
            if (axes.Distinct(StringComparer.Ordinal).Count() != axes.Count)
                return Result<EmergenceRules>.Fail("Emergence.AxisNames: nomes duplicados");
        }

        if (!Int(block, "AxisMax", d.AxisMax, out int axisMax)
            || !Int(block, "NoveltyThreshold", d.NoveltyThreshold, out int novelty)
            || !Int(block, "PressureTravelTicks", d.PressureTravelTicks, out int pressure)
            || !Int(block, "MinAffectedHouseholds", d.MinAffectedHouseholds, out int affected)
            || !Int(block, "PrototypeResourceId", d.PrototypeResourceId, out int resourceId)
            || !Int(block, "PaceBonusPctPerPoint", d.PaceBonusPctPerPoint, out int pace)
            || !Long(block, "PrototypeResourceCost", d.PrototypeResourceCost, out long cost)
            || !Long(block, "PrototypeTicks", d.PrototypeTicks, out long ticks)
            || !Dbl(block, "MinSkill", d.MinSkill, out double minSkill)
            || !Dbl(block, "SpreadAffinity", d.SpreadAffinity, out double affinity))
            return Result<EmergenceRules>.Fail("Emergence: campo numérico inválido");

        var validated = new EmergenceRules(
            enabled, axes, axisMax, novelty, pressure, affected, minSkill, resourceId, cost, ticks, pace, affinity).Validate();
        return validated.IsSuccess ? validated : Result<EmergenceRules>.Fail($"Emergence.{validated.Error}");
    }

    private static bool Bool(JsonObject o, string key, out bool value)
    {
        value = false;
        return o[key] is JsonValue v && v.TryGetValue(out value);
    }

    private static bool Int(JsonObject o, string key, int fallback, out int value)
    {
        value = fallback;
        return o[key] is null || (o[key] is JsonValue v && v.TryGetValue(out value));
    }

    private static bool Long(JsonObject o, string key, long fallback, out long value)
    {
        value = fallback;
        return o[key] is null || (o[key] is JsonValue v && v.TryGetValue(out value));
    }

    private static bool Dbl(JsonObject o, string key, double fallback, out double value)
    {
        value = fallback;
        return o[key] is null || (o[key] is JsonValue v && v.TryGetValue(out value));
    }
}
