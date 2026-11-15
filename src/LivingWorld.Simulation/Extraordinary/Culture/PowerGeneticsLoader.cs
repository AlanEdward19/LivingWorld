using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using LivingWorld.Domain.Extraordinary;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Simulation.Extraordinary.Culture;

/// <summary>Genética dos poderes no cenário JSON (AD-156), opcional: <c>Extraordinary.Genetics</c> (padrão do cenário) e
/// <c>Extraordinary.Descriptors[].Genetics</c> (do poder). Campo omitido herda o padrão de <see cref="PowerGenetics"/>; cenário
/// sem nenhum dos dois blocos fica com regra nula (o padrão documentado).</summary>
internal static class PowerGeneticsLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public static Result<PowerInheritanceRules?> Load(JsonObject block, JsonArray descriptors)
    {
        var scenarioDefault = Parse(block["Genetics"], "Extraordinary.Genetics");
        if (!scenarioDefault.IsSuccess) return Result<PowerInheritanceRules?>.Fail(scenarioDefault.Error!);
        var perPower = new Dictionary<string, PowerGenetics>(StringComparer.Ordinal);
        foreach (var node in descriptors)
        {
            if (node is not JsonObject item || item["Genetics"] is null) continue;
            string id = item["Id"]?.GetValue<string>() ?? "";
            var own = Parse(item["Genetics"], $"Extraordinary.Descriptors[{id}].Genetics");
            if (!own.IsSuccess) return Result<PowerInheritanceRules?>.Fail(own.Error!);
            perPower[id] = own.Value!;
        }
        if (scenarioDefault.Value is null && perPower.Count == 0) return Result<PowerInheritanceRules?>.Ok(null);
        var rules = new PowerInheritanceRules(scenarioDefault.Value ?? new PowerGenetics(), perPower.Count == 0 ? null : perPower);
        return rules.Validate() is { } error
            ? Result<PowerInheritanceRules?>.Fail($"Extraordinary.Genetics: {error}")
            : Result<PowerInheritanceRules?>.Ok(rules);
    }

    private static Result<PowerGenetics?> Parse(JsonNode? node, string path)
    {
        if (node is null) return Result<PowerGenetics?>.Ok(null);
        if (node is not JsonObject) return Result<PowerGenetics?>.Fail($"{path}: objeto inválido");
        try
        {
            return Result<PowerGenetics?>.Ok(node.Deserialize<PowerGenetics>(Options));
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or NotSupportedException)
        {
            return Result<PowerGenetics?>.Fail($"{path}: {ex.Message}");
        }
    }
}
