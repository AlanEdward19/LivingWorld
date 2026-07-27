using System.Text.Json.Nodes;
using LivingWorld.Domain;

namespace LivingWorld.Simulation;

/// <summary>Dado de população resolvido de um cenário (task 7): catálogo, tabela de vida,
/// regras de fertilidade, tamanho e local da população inicial. Mesmo padrão de
/// <see cref="MapScenarioLoader"/> — o motor só vê isto depois de validado na borda.</summary>
public sealed record PopulationScenarioData(
    PopulationCatalog Catalog, LifeTable LifeTable, PopulationRules Rules,
    int InitialPopulation, CultureId Culture, CellCoord Village, long MaxBytesPerNpcPerYear);

/// <summary>Carrega população de um cenário (task 7): profissão, recurso e tipo de local vêm
/// só daqui — nunca de enum em C#. Validação na borda, erro nomeia o campo.</summary>
public static class PopulationScenarioLoader
{
    public static Result<PopulationScenarioData> Load(string json)
    {
        JsonObject root;
        try
        {
            root = JsonNode.Parse(json)?.AsObject() ?? throw new System.Text.Json.JsonException("corpo vazio");
        }
        catch (System.Text.Json.JsonException ex)
        {
            return Result<PopulationScenarioData>.Fail($"json: {ex.Message}");
        }

        if (!TryGetInt(root, "InitialPopulation", out var initialPopulation))
            return Result<PopulationScenarioData>.Fail("InitialPopulation: campo obrigatório ausente ou inválido");
        if (!TryGetInt(root, "Culture", out var cultureId))
            return Result<PopulationScenarioData>.Fail("Culture: campo obrigatório ausente ou inválido");
        if (!TryGetInt(root, "VillageX", out var villageX))
            return Result<PopulationScenarioData>.Fail("VillageX: campo obrigatório ausente ou inválido");
        if (!TryGetInt(root, "VillageY", out var villageY))
            return Result<PopulationScenarioData>.Fail("VillageY: campo obrigatório ausente ou inválido");

        var catalog = new PopulationCatalog(
            ReadIntSet(root, "CultureIds"), ReadIntSet(root, "ProfessionIds"), ReadIntSet(root, "LocationTypeIds"));

        if (!TryGetInt(root, "MaxLongevityYears", out var maxLongevity))
            return Result<PopulationScenarioData>.Fail("MaxLongevityYears: campo obrigatório ausente ou inválido");

        if (root["LifeTableBrackets"] is not JsonArray bracketsNode)
            return Result<PopulationScenarioData>.Fail("LifeTableBrackets: campo obrigatório ausente");

        var brackets = new List<LifeTableBracket>();
        foreach (var node in bracketsNode)
        {
            if (node is not JsonObject b
                || b["MinAgeYears"]?.GetValue<int>() is not { } min
                || b["MaxAgeYears"]?.GetValue<int>() is not { } max
                || b["BaseAnnualMortality"]?.GetValue<double>() is not { } mortality)
                return Result<PopulationScenarioData>.Fail("LifeTableBrackets: item precisa de MinAgeYears, MaxAgeYears e BaseAnnualMortality");
            brackets.Add(new LifeTableBracket(min, max, mortality));
        }

        var lifeTableResult = LifeTable.Create(maxLongevity, brackets);
        if (!lifeTableResult.IsSuccess)
            return Result<PopulationScenarioData>.Fail(lifeTableResult.Error!);

        if (!TryGetInt(root, "FertilityMinAge", out var fertilityMinAge))
            return Result<PopulationScenarioData>.Fail("FertilityMinAge: campo obrigatório ausente ou inválido");
        if (!TryGetInt(root, "FertilityMaxAge", out var fertilityMaxAge))
            return Result<PopulationScenarioData>.Fail("FertilityMaxAge: campo obrigatório ausente ou inválido");
        if (root["AnnualConceptionChance"] is not JsonValue conceptionNode || !conceptionNode.TryGetValue<double>(out var conceptionChance))
            return Result<PopulationScenarioData>.Fail("AnnualConceptionChance: campo obrigatório ausente ou inválido");
        if (!TryGetInt(root, "GestationDays", out var gestationDays))
            return Result<PopulationScenarioData>.Fail("GestationDays: campo obrigatório ausente ou inválido");

        var rulesResult = PopulationRules.Create(lifeTableResult.Value!, fertilityMinAge, fertilityMaxAge, conceptionChance, gestationDays);
        if (!rulesResult.IsSuccess)
            return Result<PopulationScenarioData>.Fail(rulesResult.Error!);

        if (root["MaxBytesPerNpcPerYear"] is not JsonValue maxBytesNode || !maxBytesNode.TryGetValue<long>(out var maxBytesPerNpcPerYear))
            return Result<PopulationScenarioData>.Fail("MaxBytesPerNpcPerYear: campo obrigatório ausente ou inválido");

        return Result<PopulationScenarioData>.Ok(new PopulationScenarioData(
            catalog, lifeTableResult.Value!, rulesResult.Value!, initialPopulation,
            new CultureId(cultureId), new CellCoord(villageX, villageY), maxBytesPerNpcPerYear));
    }

    private static bool TryGetInt(JsonObject root, string field, out int value)
    {
        value = 0;
        return root[field] is JsonValue v && v.TryGetValue(out value);
    }

    private static HashSet<int> ReadIntSet(JsonObject root, string field) =>
        root[field] is JsonArray arr
            ? arr.Select(n => n!.GetValue<int>()).ToHashSet()
            : new HashSet<int>();
}
