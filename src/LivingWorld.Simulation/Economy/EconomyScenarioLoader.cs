using System.Text.Json.Nodes;
using LivingWorld.Domain;

namespace LivingWorld.Simulation;

/// <summary>Dado de economia resolvido de um cenário (Fase 5, T13): <see cref="EconomyRules"/>,
/// <see cref="EconomyCatalog"/> e os <see cref="Workplace"/> iniciais (sem id atribuído ainda —
/// quem consome chama <see cref="WorldState.NextWorkplaceIdAndAdvance"/> ao adicionar cada um,
/// mesmo padrão de <see cref="PopulationScenarioLoader"/> pra NPC).</summary>
public sealed record EconomyScenarioData(
    EconomyRules Rules, EconomyCatalog Catalog, IReadOnlyList<InitialWorkplace> Workplaces);

/// <summary>Um <see cref="Workplace"/> ainda sem <see cref="WorkplaceId"/> — o cenário declara o
/// conteúdo, o mundo atribui o id na hora de adicionar (mesma disciplina de
/// <see cref="WorldState.AddNpc"/>/<see cref="WorldState.AddHousehold"/>).</summary>
public sealed record InitialWorkplace(
    LocationType LocationType, CellCoord Location, int MaxVacancies,
    IReadOnlyDictionary<ResourceType, long> Stock, Money Treasury, IReadOnlyDictionary<ResourceType, long> Prices);

/// <summary>Carrega <see cref="EconomyRules"/>/<see cref="EconomyCatalog"/>/<see
/// cref="Workplace"/> iniciais de um cenário (Fase 5, T13): nenhum parâmetro econômico
/// hardcoded em C# (R3). Mesmo padrão manual-parse + <see cref="Result{T}"/> de <see
/// cref="BehaviorScenarioLoader"/> — campo obrigatório ausente nomeia o campo no erro.</summary>
public static class EconomyScenarioLoader
{
    public static Result<EconomyScenarioData> Load(string json)
    {
        JsonObject root;
        try
        {
            root = JsonNode.Parse(json)?.AsObject() ?? throw new System.Text.Json.JsonException("corpo vazio");
        }
        catch (System.Text.Json.JsonException ex)
        {
            return Result<EconomyScenarioData>.Fail($"json: {ex.Message}");
        }

        var rulesResult = ParseRules(root);
        if (!rulesResult.IsSuccess)
            return Result<EconomyScenarioData>.Fail(rulesResult.Error!);

        var catalogResult = ParseCatalog(root);
        if (!catalogResult.IsSuccess)
            return Result<EconomyScenarioData>.Fail(catalogResult.Error!);

        var workplacesResult = ParseWorkplaces(root);
        if (!workplacesResult.IsSuccess)
            return Result<EconomyScenarioData>.Fail(workplacesResult.Error!);

        return Result<EconomyScenarioData>.Ok(new EconomyScenarioData(rulesResult.Value!, catalogResult.Value!, workplacesResult.Value!));
    }

    private static Result<EconomyRules> ParseRules(JsonObject root)
    {
        if (!TryGetBool(root, "EconomyEnabled", out var enabled))
            return Result<EconomyRules>.Fail("EconomyEnabled: campo obrigatório ausente ou inválido");
        if (!TryGetInt(root, "FoodResourceId", out var foodResourceId))
            return Result<EconomyRules>.Fail("FoodResourceId: campo obrigatório ausente ou inválido");
        if (!TryGetInt(root, "WaterResourceId", out var waterResourceId))
            return Result<EconomyRules>.Fail("WaterResourceId: campo obrigatório ausente ou inválido");
        if (!TryGetDouble(root, "PriceSensitivity", out var priceSensitivity))
            return Result<EconomyRules>.Fail("PriceSensitivity: campo obrigatório ausente ou inválido");

        if (!TryGetResourceLocationLongMap(root, "CapacityByResourceLocation", out var capacity))
            return Result<EconomyRules>.Fail("CapacityByResourceLocation: campo obrigatório ausente ou inválido");
        if (!TryGetIntDoubleMap(root, "SpoilagePerDayByResource", out var spoilage))
            return Result<EconomyRules>.Fail("SpoilagePerDayByResource: campo obrigatório ausente ou inválido");
        if (!TryGetIntLongMap(root, "WageByProfession", out var wage))
            return Result<EconomyRules>.Fail("WageByProfession: campo obrigatório ausente ou inválido");
        if (!TryGetIntLongMap(root, "PriceFloor", out var priceFloor))
            return Result<EconomyRules>.Fail("PriceFloor: campo obrigatório ausente ou inválido");
        if (!TryGetIntLongMap(root, "PriceCeiling", out var priceCeiling))
            return Result<EconomyRules>.Fail("PriceCeiling: campo obrigatório ausente ou inválido");
        if (!TryGetIntDoubleMap(root, "DemandBaselinePerNpc", out var demand))
            return Result<EconomyRules>.Fail("DemandBaselinePerNpc: campo obrigatório ausente ou inválido");

        return EconomyRules.Create(
            enabled, foodResourceId, waterResourceId, capacity, spoilage, wage, priceFloor, priceCeiling,
            priceSensitivity, demand);
    }

    private static Result<EconomyCatalog> ParseCatalog(JsonObject root)
    {
        if (root["Recipes"] is not JsonObject recipesNode)
            return Result<EconomyCatalog>.Fail("Recipes: campo obrigatório ausente");

        var recipes = new Dictionary<int, ProductionRecipe>();
        foreach (var (key, node) in recipesNode)
        {
            if (!int.TryParse(key, out var locationTypeId))
                return Result<EconomyCatalog>.Fail($"Recipes[{key}]: chave de LocationType inválida");
            if (node is not JsonObject recipeNode)
                return Result<EconomyCatalog>.Fail($"Recipes[{key}]: item inválido");

            if (!TryGetIntLongMap(recipeNode, "Inputs", out var inputs))
                return Result<EconomyCatalog>.Fail($"Recipes[{key}].Inputs: campo obrigatório ausente ou inválido");
            if (!TryGetIntLongMap(recipeNode, "Outputs", out var outputs))
                return Result<EconomyCatalog>.Fail($"Recipes[{key}].Outputs: campo obrigatório ausente ou inválido");
            if (!TryGetInt(recipeNode, "MaxWorkersPerCycle", out var maxWorkers))
                return Result<EconomyCatalog>.Fail($"Recipes[{key}].MaxWorkersPerCycle: campo obrigatório ausente ou inválido");

            int? requiresCellResource = null;
            if (recipeNode["RequiresCellResource"] is JsonValue reqNode && reqNode.TryGetValue<int>(out var reqValue))
                requiresCellResource = reqValue;

            var recipeResult = ProductionRecipe.Create(inputs, outputs, requiresCellResource, maxWorkers);
            if (!recipeResult.IsSuccess)
                return Result<EconomyCatalog>.Fail($"Recipes[{key}]: {recipeResult.Error}");

            recipes[locationTypeId] = recipeResult.Value!;
        }

        if (root["MarketLocationTypeIds"] is not JsonArray marketNode)
            return Result<EconomyCatalog>.Fail("MarketLocationTypeIds: campo obrigatório ausente");

        var marketIds = new HashSet<int>();
        foreach (var node in marketNode)
        {
            if (node is not JsonValue v || !v.TryGetValue<int>(out var id))
                return Result<EconomyCatalog>.Fail("MarketLocationTypeIds: valor inválido");
            marketIds.Add(id);
        }

        if (!TryGetIntIntMap(root, "LocationTypeByProfession", out var locationByProfession))
            return Result<EconomyCatalog>.Fail("LocationTypeByProfession: campo obrigatório ausente ou inválido");

        return Result<EconomyCatalog>.Ok(new EconomyCatalog(recipes, marketIds, locationByProfession));
    }

    private static Result<IReadOnlyList<InitialWorkplace>> ParseWorkplaces(JsonObject root)
    {
        if (root["Workplaces"] is not JsonArray workplacesNode)
            return Result<IReadOnlyList<InitialWorkplace>>.Fail("Workplaces: campo obrigatório ausente");

        var workplaces = new List<InitialWorkplace>();
        foreach (var node in workplacesNode)
        {
            if (node is not JsonObject wp)
                return Result<IReadOnlyList<InitialWorkplace>>.Fail("Workplaces: item inválido");

            if (!TryGetInt(wp, "LocationTypeId", out var locationTypeId))
                return Result<IReadOnlyList<InitialWorkplace>>.Fail("Workplaces[].LocationTypeId: campo obrigatório ausente ou inválido");
            if (!TryGetInt(wp, "X", out var x))
                return Result<IReadOnlyList<InitialWorkplace>>.Fail("Workplaces[].X: campo obrigatório ausente ou inválido");
            if (!TryGetInt(wp, "Y", out var y))
                return Result<IReadOnlyList<InitialWorkplace>>.Fail("Workplaces[].Y: campo obrigatório ausente ou inválido");
            if (!TryGetInt(wp, "MaxVacancies", out var maxVacancies))
                return Result<IReadOnlyList<InitialWorkplace>>.Fail("Workplaces[].MaxVacancies: campo obrigatório ausente ou inválido");
            if (!TryGetLong(wp, "Treasury", out var treasury))
                return Result<IReadOnlyList<InitialWorkplace>>.Fail("Workplaces[].Treasury: campo obrigatório ausente ou inválido");
            if (!TryGetResourceLongMap(wp, "Stock", out var stock))
                return Result<IReadOnlyList<InitialWorkplace>>.Fail("Workplaces[].Stock: campo obrigatório ausente ou inválido");
            if (!TryGetResourceLongMap(wp, "Prices", out var prices))
                return Result<IReadOnlyList<InitialWorkplace>>.Fail("Workplaces[].Prices: campo obrigatório ausente ou inválido");

            workplaces.Add(new InitialWorkplace(
                new LocationType(locationTypeId), new CellCoord(x, y), maxVacancies, stock, new Money(treasury), prices));
        }

        return Result<IReadOnlyList<InitialWorkplace>>.Ok(workplaces);
    }

    private static bool TryGetInt(JsonObject root, string field, out int value)
    {
        value = 0;
        return root[field] is JsonValue v && v.TryGetValue(out value);
    }

    private static bool TryGetLong(JsonObject root, string field, out long value)
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

    private static bool TryGetIntLongMap(JsonObject root, string field, out Dictionary<int, long> value)
    {
        value = [];
        if (root[field] is not JsonObject obj) return false;
        foreach (var (key, node) in obj)
        {
            if (!int.TryParse(key, out var id) || node is not JsonValue v || !v.TryGetValue<long>(out var amount)) return false;
            value[id] = amount;
        }
        return true;
    }

    private static bool TryGetIntIntMap(JsonObject root, string field, out Dictionary<int, int> value)
    {
        value = [];
        if (root[field] is not JsonObject obj) return false;
        foreach (var (key, node) in obj)
        {
            if (!int.TryParse(key, out var id) || node is not JsonValue v || !v.TryGetValue<int>(out var mapped)) return false;
            value[id] = mapped;
        }
        return true;
    }

    private static bool TryGetIntDoubleMap(JsonObject root, string field, out Dictionary<int, double> value)
    {
        value = [];
        if (root[field] is not JsonObject obj) return false;
        foreach (var (key, node) in obj)
        {
            if (!int.TryParse(key, out var id) || node is not JsonValue v || !v.TryGetValue<double>(out var amount)) return false;
            value[id] = amount;
        }
        return true;
    }

    private static bool TryGetResourceLongMap(JsonObject root, string field, out Dictionary<ResourceType, long> value)
    {
        value = [];
        if (root[field] is not JsonObject obj) return false;
        foreach (var (key, node) in obj)
        {
            if (!int.TryParse(key, out var id) || node is not JsonValue v || !v.TryGetValue<long>(out var amount)) return false;
            value[new ResourceType(id)] = amount;
        }
        return true;
    }

    /// <summary>Chave composta "resourceId,locationTypeId" — mesmo formato de
    /// <see cref="ResourceLocationKeyConverter"/>, reaproveitado aqui só como convenção de texto
    /// (a economia serializada usa o conversor; o cenário JSON é escrito à mão).</summary>
    private static bool TryGetResourceLocationLongMap(
        JsonObject root, string field, out Dictionary<(int ResourceId, int LocationTypeId), long> value)
    {
        value = [];
        if (root[field] is not JsonObject obj) return false;
        foreach (var (key, node) in obj)
        {
            var parts = key.Split(',');
            if (parts.Length != 2 || !int.TryParse(parts[0], out var resourceId) || !int.TryParse(parts[1], out var locationTypeId))
                return false;
            if (node is not JsonValue v || !v.TryGetValue<long>(out var amount)) return false;
            value[(resourceId, locationTypeId)] = amount;
        }
        return true;
    }
}
