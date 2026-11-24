using System.Text.Json.Nodes;
using LivingWorld.Domain.Climate;
using LivingWorld.Domain.Crime;
using LivingWorld.Domain.Health;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Simulation.Scenarios;

/// <summary>Blocos opcionais <c>Disease</c>, <c>Climate</c> e <c>Theft</c> do cenário (ADR-0043). Bloco ausente devolve o
/// <c>Disabled</c> da regra (o <c>Theft</c>, desde o AD-150, devolve o <c>Default</c>, ligado). Bloco presente exige
/// <c>Enabled</c>; os demais campos de <c>Climate</c> usam o de <c>Disabled</c> e os de <c>Theft</c>, o de <c>Default</c>. <c>Disease</c> exige as doenças
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
        // AD-190: média, desvio, persistência e parte comum do ano de safra (AD-135) também são dado do cenário; ausentes = o
        // sorteio uniforme antigo entre MinMilli e MaxMilli (o JSON sem os campos não muda)
        if (!Int(block, "MeanMilli", d.MeanMilli, out int mean) || !Int(block, "StdDevMilli", d.StdDevMilli, out int sd)
            || !Dbl(block, "Persistence", d.Persistence, out double persistence) || !Dbl(block, "SharedShare", d.SharedShare, out double shared))
            return Result<ClimateRules>.Fail("Climate: campo numérico inválido");
        if (sd < 0) return Result<ClimateRules>.Fail("Climate.StdDevMilli: não pode ser negativo");
        if (persistence is < 0 or >= 1) return Result<ClimateRules>.Fail("Climate.Persistence: fora de [0,1)");
        if (shared is < 0 or > 1) return Result<ClimateRules>.Fail("Climate.SharedShare: fora de [0,1]");
        // AD-180: o tempo diário vem ligado com o clima; o sub-bloco Weather muda a climatologia do lugar ou o desliga
        var weather = LoadWeather(block, out var weatherError);
        if (weatherError is not null) return Result<ClimateRules>.Fail(weatherError);
        return Result<ClimateRules>.Ok(new ClimateRules(enabled, min, max, mean, sd, persistence, shared, Weather: enabled ? weather : null));
    }

    private static readonly System.Text.Json.JsonSerializerOptions WeatherJson = new() { PropertyNameCaseInsensitive = true };

    private static WeatherRules LoadWeather(JsonObject climate, out string? error)
    {
        error = null;
        if (climate["Weather"] is null) return WeatherRules.Default;
        if (climate["Weather"] is not JsonObject block) { error = "Climate.Weather: bloco inválido"; return WeatherRules.Default; }
        WeatherRules? rules;
        try { rules = System.Text.Json.JsonSerializer.Deserialize<WeatherRules>(block.ToJsonString(), WeatherJson); }
        catch (System.Text.Json.JsonException e) { error = $"Climate.Weather: {e.Message}"; return WeatherRules.Default; }
        if (rules is null) { error = "Climate.Weather: bloco inválido"; return WeatherRules.Default; }
        foreach (var (name, list) in new[] { ("MeanTempC", rules.MeanTempC), ("DiurnalRangeC", rules.DiurnalRangeC),
            ("WetDaysPerMonth", rules.WetDaysPerMonth), ("RainMmPerMonth", rules.RainMmPerMonth), ("AnomalySdC", rules.AnomalySdC), ("WindMs", rules.WindMs) })
            if (list.Count != 12 || list.Any(v => !double.IsFinite(v))) { error = $"Climate.Weather.{name}: precisa de 12 valores"; return WeatherRules.Default; }
        if (rules.LatitudeDeg is < -90 or > 90) { error = "Climate.Weather.LatitudeDeg: fora de [-90,90]"; return WeatherRules.Default; }
        if (rules.WetDaysPerMonth.Any(v => v < 0) || rules.RainMmPerMonth.Any(v => v < 0)) { error = "Climate.Weather: chuva negativa"; return WeatherRules.Default; }
        return rules;
    }

    public static Result<TheftRules> LoadTheft(string json)
    {
        var block = Block(json, "Theft", out var error);
        if (error is not null) return Result<TheftRules>.Fail(error);
        if (block is null) return Result<TheftRules>.Ok(TheftRules.Default); // AD-150: sem bloco, o padrão (ligado)
        if (!Bool(block, "Enabled", out bool enabled)) return Result<TheftRules>.Fail("Theft.Enabled: campo obrigatório ausente ou inválido");

        var d = TheftRules.Default;
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
        return Result<TheftRules>.Ok(d with { Enabled = enabled, DesperateBelow = desperate, PersonalityThreshold = personality, BaseSuccess = success, DiscoveryChance = discovery, MaxLootFraction = loot, GuardProfessionId = guard, GuardEffect = guardEffect });
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
