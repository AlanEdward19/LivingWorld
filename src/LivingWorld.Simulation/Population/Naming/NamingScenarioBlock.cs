using System.Text.Json;
using System.Text.Json.Nodes;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Simulation.Population.Naming;

/// <summary>Bloco opcional <c>Naming</c> do cenário/período (AD-183): <c>{"Naming": {"Cultures": [{"Culture": 1, "Lineage":
/// "Matrilineal", "SpouseTakesName": false, "AncestorNameChance": 0.3, "DaughterCity": "Founder"}]}}</c>. Ausente = o sorteio
/// por cultura do AD-178; campo ausente numa cultura = o sorteado. Validado na carga (erro recusa o cenário).</summary>
public static class NamingScenarioBlock
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public static Result<FamilyRules> Apply(string json, FamilyRules rules)
    {
        if (JsonNode.Parse(json)?["Naming"] is not { } block) return Result<FamilyRules>.Ok(rules);
        if (block["Cultures"] is not JsonArray cultures) return Result<FamilyRules>.Fail("Naming: o bloco exige a lista Cultures");
        List<NamingCustom>? parsed;
        try { parsed = cultures.Deserialize<List<NamingCustom>>(Options); }
        catch (JsonException e) { return Result<FamilyRules>.Fail($"Naming.Cultures: {e.Message}"); }
        var valid = NamingCustom.Validate(parsed ?? []);
        if (!valid.IsSuccess) return Result<FamilyRules>.Fail(valid.Error!);
        return Result<FamilyRules>.Ok(valid.Value!.Count == 0 ? rules : rules with { Names = rules.Names with { Customs = valid.Value } });
    }
}
