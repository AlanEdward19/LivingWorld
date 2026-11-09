using System.Text.Json.Nodes;
using LivingWorld.Domain.Climate;
using LivingWorld.Domain.Crime;
using LivingWorld.Domain.Health;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Simulation.Scenarios;

/// <summary>Blocos opcionais <c>Disease</c>, <c>Climate</c> e <c>Theft</c> do cenário (ADR-0043). Bloco ausente devolve o
/// <c>Disabled</c> da regra: o mundo e o hash ficam como antes. Bloco presente exige <c>Enabled</c>; os demais campos
/// de <c>Climate</c> e <c>Theft</c> usam o valor de <c>Disabled</c> quando faltam. <c>Disease</c> exige as doenças
/// quando ligado (o motor não tem doença nomeada). Campo inválido falha apontando o caminho.</summary>
public static class WorldRulesScenarioLoader
{
    public static Result<DiseaseRules> LoadDisease(string json)
    {
        var block = Block(json, "Disease", out var error);
        if (error is not null) return Result<DiseaseRules>.Fail(error);
        if (block is null) return Result<DiseaseRules>.Ok(DiseaseRules.Disabled);
        if (!Bool(block, "Enabled", out bool enabled)) return Result<DiseaseRules>.Fail("Disease.Enabled: campo obrigatório ausente ou inválido");

        var d = DiseaseRules.Disabled;
        if (!Int(block, "OutbreakPopulationThreshold", d.OutbreakPopulationThreshold, out int threshold)
            || !Dbl(block, "OutbreakDailyChance", d.OutbreakDailyChance, out double chance))
            return Result<DiseaseRules>.Fail("Disease: campo numérico inválido");
        if (threshold < 1) return Result<DiseaseRules>.Fail("Disease.OutbreakPopulationThreshold: deve ser >= 1");
        if (chance is < 0 or > 1) return Result<DiseaseRules>.Fail("Disease.OutbreakDailyChance: fora de [0,1]");

        var profiles = new List<DiseaseProfile>();
        if (block["Diseases"] is not null)
        {
            if (block["Diseases"] is not JsonArray array) return Result<DiseaseRules>.Fail("Disease.Diseases: lista inválida");
            foreach (var node in array)
            {
                if (node is not JsonObject o || !Int(o, "Id", int.MinValue, out int id) || id == int.MinValue
                    || !Dbl(o, "ContagionPerContactDay", double.NaN, out double contagion) || double.IsNaN(contagion)
                    || !Dbl(o, "Lethality", double.NaN, out double lethality) || double.IsNaN(lethality)
                    || !Int(o, "DurationDays", 0, out int duration) || !Int(o, "ImmunityDays", 0, out int immunity))
                    return Result<DiseaseRules>.Fail("Disease.Diseases[]: precisa de Id, ContagionPerContactDay, Lethality, DurationDays e ImmunityDays");
                if (contagion is < 0 or > 1) return Result<DiseaseRules>.Fail($"Disease.Diseases[{id}].ContagionPerContactDay: fora de [0,1]");
                if (lethality is < 0 or > 1) return Result<DiseaseRules>.Fail($"Disease.Diseases[{id}].Lethality: fora de [0,1]");
                if (duration < 1) return Result<DiseaseRules>.Fail($"Disease.Diseases[{id}].DurationDays: deve ser >= 1");
                if (immunity < 0) return Result<DiseaseRules>.Fail($"Disease.Diseases[{id}].ImmunityDays: não pode ser negativo");
                if (profiles.Any(p => p.Id == id)) return Result<DiseaseRules>.Fail($"Disease.Diseases: id {id} duplicado");
                profiles.Add(new DiseaseProfile(id, contagion, lethality, duration, immunity));
            }
        }

        if (enabled && profiles.Count == 0) return Result<DiseaseRules>.Fail("Disease.Diseases: ligado exige ao menos uma doença");
        return Result<DiseaseRules>.Ok(new DiseaseRules(enabled, profiles, threshold, chance));
    }

    public static Result<ClimateRules> LoadClimate(string json)
    {
        var block = Block(json, "Climate", out var error);
        if (error is not null) return Result<ClimateRules>.Fail(error);
        if (block is null) return Result<ClimateRules>.Ok(ClimateRules.Disabled);
        if (!Bool(block, "Enabled", out bool enabled)) return Result<ClimateRules>.Fail("Climate.Enabled: campo obrigatório ausente ou inválido");

        var d = ClimateRules.Disabled;
        if (!Int(block, "MinMilli", d.MinMilli, out int min) || !Int(block, "MaxMilli", d.MaxMilli, out int max))
            return Result<ClimateRules>.Fail("Climate: campo numérico inválido");
        if (min < 0) return Result<ClimateRules>.Fail("Climate.MinMilli: não pode ser negativo");
        if (max < min) return Result<ClimateRules>.Fail("Climate.MaxMilli: menor que MinMilli");
        return Result<ClimateRules>.Ok(new ClimateRules(enabled, min, max));
    }

    public static Result<TheftRules> LoadTheft(string json)
    {
        var block = Block(json, "Theft", out var error);
        if (error is not null) return Result<TheftRules>.Fail(error);
        if (block is null) return Result<TheftRules>.Ok(TheftRules.Disabled);
        if (!Bool(block, "Enabled", out bool enabled)) return Result<TheftRules>.Fail("Theft.Enabled: campo obrigatório ausente ou inválido");

        var d = TheftRules.Disabled;
        if (!Int(block, "DesperateBelow", d.DesperateBelow, out int desperate)
            || !Dbl(block, "PersonalityThreshold", d.PersonalityThreshold, out double personality)
            || !Dbl(block, "BaseSuccess", d.BaseSuccess, out double success)
            || !Dbl(block, "DiscoveryChance", d.DiscoveryChance, out double discovery)
            || !Dbl(block, "MaxLootFraction", d.MaxLootFraction, out double loot)
            || !Int(block, "GuardProfessionId", d.GuardProfessionId, out int guard)
            || !Dbl(block, "GuardEffect", d.GuardEffect, out double guardEffect))
            return Result<TheftRules>.Fail("Theft: campo numérico inválido");
        if (desperate is < 0 or > 100) return Result<TheftRules>.Fail("Theft.DesperateBelow: fora de [0,100]");
        foreach (var (name, value) in new[] { ("BaseSuccess", success), ("DiscoveryChance", discovery), ("MaxLootFraction", loot), ("GuardEffect", guardEffect) })
            if (value is < 0 or > 1) return Result<TheftRules>.Fail($"Theft.{name}: fora de [0,1]");
        return Result<TheftRules>.Ok(new TheftRules(enabled, desperate, personality, success, discovery, loot, guard, guardEffect));
    }

    private static JsonObject? Block(string json, string name, out string? error)
    {
        error = null;
        JsonObject root;
        try
        {
            root = JsonNode.Parse(json)?.AsObject() ?? throw new System.Text.Json.JsonException("corpo vazio");
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or InvalidOperationException)
        {
            error = $"json: {ex.Message}";
            return null;
        }

        if (root[name] is null) return null;
        if (root[name] is JsonObject block) return block;
        error = $"{name}: objeto inválido";
        return null;
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

    private static bool Dbl(JsonObject o, string key, double fallback, out double value)
    {
        value = fallback;
        return o[key] is null || (o[key] is JsonValue v && v.TryGetValue(out value));
    }
}
