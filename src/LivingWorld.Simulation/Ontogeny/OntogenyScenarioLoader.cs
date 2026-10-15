using System.Text.Json.Nodes;
using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Ontogeny;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Simulation.Ontogeny;

/// <summary>Valida o bloco opcional <c>Ontogeny</c> do cenário (marcos e o que cada ação exige). Sem o bloco, valem os marcos humanos
/// padrão (<see cref="OntogenyDefaults.Human"/>): a ontogenia vem ligada em todo cenário. <c>Enabled: false</c> desliga.</summary>
public static class OntogenyScenarioLoader
{
    public static Result<OntogenyRules> Load(string json)
    {
        JsonObject root;
        try
        {
            root = JsonNode.Parse(json)?.AsObject() ?? throw new System.Text.Json.JsonException("corpo vazio");
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or InvalidOperationException)
        {
            return Result<OntogenyRules>.Fail($"json: {ex.Message}");
        }

        if (root["Ontogeny"] is null) return Result<OntogenyRules>.Ok(OntogenyDefaults.Human);
        if (root["Ontogeny"] is not JsonObject block) return Result<OntogenyRules>.Fail("Ontogeny: objeto inválido");
        if (block["Enabled"] is not JsonValue ev || !ev.TryGetValue(out bool enabled))
            return Result<OntogenyRules>.Fail("Ontogeny.Enabled: campo obrigatório ausente ou inválido");

        var milestones = new List<MilestoneDefinition>();
        if (block["Milestones"] is null) milestones.AddRange(OntogenyDefaults.HumanMilestones);
        else if (block["Milestones"] is not JsonArray list) return Result<OntogenyRules>.Fail("Ontogeny.Milestones: lista inválida");
        else
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] is not JsonObject m) return Result<OntogenyRules>.Fail($"Ontogeny.Milestones[{i}]: objeto inválido");
                string? id = m["Id"] is JsonValue idv && idv.TryGetValue<string>(out var s) ? s : null;
                if (string.IsNullOrWhiteSpace(id)) return Result<OntogenyRules>.Fail($"Ontogeny.Milestones[{i}].Id: obrigatório");
                if (m["Axis"] is not JsonValue av || !av.TryGetValue<string>(out var axisName) || !Enum.TryParse<DevelopmentAxis>(axisName, out var axis))
                    return Result<OntogenyRules>.Fail($"Ontogeny.Milestones[{id}].Axis: eixo desconhecido");
                if (!Dbl(m, "Threshold", out double thr) || !Dbl(m, "WindowStartYears", out double start)
                    || !Dbl(m, "WindowMedianYears", out double median) || !Dbl(m, "WindowLimitYears", out double limit))
                    return Result<OntogenyRules>.Fail($"Ontogeny.Milestones[{id}]: Threshold e Window*Years numéricos são obrigatórios");
                var requires = new List<string>();
                if (m["Requires"] is JsonArray reqs)
                    foreach (var r in reqs)
                    {
                        if (r is not JsonValue rv || !rv.TryGetValue<string>(out var rs)) return Result<OntogenyRules>.Fail($"Ontogeny.Milestones[{id}].Requires: ids inválidos");
                        requires.Add(rs);
                    }
                milestones.Add(new MilestoneDefinition(id, axis, thr, start, median, limit, requires));
            }
        }

        var requirements = new Dictionary<ActionType, MilestoneRequirement>(OntogenyDefaults.HumanRequirements);
        if (block["Requirements"] is not null)
        {
            if (block["Requirements"] is not JsonObject reqObj) return Result<OntogenyRules>.Fail("Ontogeny.Requirements: objeto inválido");
            requirements.Clear();
            foreach (var (key, node) in reqObj)
            {
                if (!Enum.TryParse<ActionType>(key, out var action)) return Result<OntogenyRules>.Fail($"Ontogeny.Requirements: ação desconhecida '{key}'");
                if (node is not JsonObject r || r["Axis"] is not JsonValue rav || !rav.TryGetValue<string>(out var rax)
                    || !Enum.TryParse<DevelopmentAxis>(rax, out var raxis) || !Dbl(r, "Threshold", out double rthr))
                    return Result<OntogenyRules>.Fail($"Ontogeny.Requirements[{key}]: Axis e Threshold obrigatórios");
                requirements[action] = new MilestoneRequirement(raxis, rthr);
            }
        }

        double recovery = OntogenyRules.DefaultLateRecovery, keep = OntogenyRules.DefaultMissedWindowKeep;
        if (block["LateRecoveryFactor"] is not null && !Dbl(block, "LateRecoveryFactor", out recovery))
            return Result<OntogenyRules>.Fail("Ontogeny.LateRecoveryFactor: número inválido");
        if (block["MissedWindowKeep"] is not null && !Dbl(block, "MissedWindowKeep", out keep))
            return Result<OntogenyRules>.Fail("Ontogeny.MissedWindowKeep: número inválido");

        double loss = OntogenyRules.DefaultRegressionLoss;
        if (block["RegressionLoss"] is not null && !Dbl(block, "RegressionLoss", out loss))
            return Result<OntogenyRules>.Fail("Ontogeny.RegressionLoss: número inválido");

        var created = OntogenyRules.Create(enabled, milestones, requirements, recovery, keep, loss);
        return created.IsSuccess ? created : Result<OntogenyRules>.Fail(created.Error!);
    }

    private static bool Dbl(JsonObject o, string key, out double value)
    {
        value = 0;
        return o[key] is JsonValue v && v.TryGetValue(out value);
    }
}
