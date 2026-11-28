using System.Text.Json.Nodes;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Population;
using LivingWorld.Simulation.Serialization;

namespace LivingWorld.Simulation.Economy;

/// <summary>Dado de economia resolvido de um cenário: <see cref="EconomyRules"/>,
/// <see cref="EconomyCatalog"/> e os <see cref="Workplace"/> iniciais (sem id atribuído ainda —
/// quem consome chama <see cref="WorldState.NextWorkplaceIdAndAdvance"/> ao adicionar cada um,
/// mesmo padrão de <see cref="PopulationScenarioLoader"/> pra NPC).</summary>
public sealed record EconomyScenarioData(
    EconomyRules Rules, EconomyCatalog Catalog, IReadOnlyList<InitialWorkplace> Workplaces)
{
    /// <summary>Tipos de local que são oficina de ofício, do mestre na fundação (AD-144); vazio = ninguém é dono.</summary>
    public IReadOnlyList<int> CraftLocationTypeIds { get; init; } = [];
}

/// <summary>Um <see cref="Workplace"/> ainda sem <see cref="WorkplaceId"/> — o cenário declara o
/// conteúdo, o mundo atribui o id na hora de adicionar (mesma disciplina de
/// <see cref="WorldState.AddNpc"/>/<see cref="WorldState.AddHousehold"/>).</summary>
public sealed record InitialWorkplace(
    LocationType LocationType, CellCoord Location, int MaxVacancies,
    IReadOnlyDictionary<ResourceType, long> Stock, Money Treasury, IReadOnlyDictionary<ResourceType, long> Prices)
{
    /// <summary>Dono declarado (id de NPC do cenário, AD-144); <c>null</c> = a regra do mundo decide.</summary>
    public NpcId? Owner { get; init; }
}

/// <summary>Carrega <see cref="EconomyRules"/>/<see cref="EconomyCatalog"/>/<see
/// cref="Workplace"/> iniciais de um cenário: nenhum parâmetro econômico
/// hardcoded em C#. Mesmo padrão manual-parse + <see cref="Result{T}"/> de <see
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

        var crafts = new List<int>();
        if (root["CraftLocationTypeIds"] is { } craftNode)
        {
            if (craftNode is not JsonArray craftArray) return Result<EconomyScenarioData>.Fail("CraftLocationTypeIds: deve ser uma lista de inteiros");
            foreach (var item in craftArray)
            {
                if (item is not JsonValue v || !v.TryGetValue<int>(out var id)) return Result<EconomyScenarioData>.Fail("CraftLocationTypeIds: valor inválido");
                crafts.Add(id);
            }
        }

        return Result<EconomyScenarioData>.Ok(new EconomyScenarioData(rulesResult.Value!, catalogResult.Value!, workplacesResult.Value!)
        {
            CraftLocationTypeIds = crafts,
        });
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

        double taxRate = 0.0;
        if (root["WageTaxRate"] is { } taxNode && !(taxNode is JsonValue taxValue && taxValue.TryGetValue<double>(out taxRate)))
            return Result<EconomyRules>.Fail("WageTaxRate: deve ser um número");

        // retirada do lucro acima da reserva (AD-144); "SurplusLevyShare" é o nome antigo (dízimo da AD-140); ausente = desligado
        double levy = 0.0;
        foreach (var field in new[] { "SurplusDrawShare", "SurplusLevyShare" })
        {
            if (root[field] is not { } levyNode) continue;
            if (!(levyNode is JsonValue levyValue && levyValue.TryGetValue<double>(out levy) && levy is >= 0 and <= 1))
                return Result<EconomyRules>.Fail($"{field}: deve ser um número entre 0 e 1");
            break;
        }

        var created = EconomyRules.Create(
            enabled, foodResourceId, waterResourceId, capacity, spoilage, wage, priceFloor, priceCeiling,
            priceSensitivity, demand, taxRate);
        if (!created.IsSuccess) return created;
        var rules = levy > 0 ? created.Value! with { SurplusDrawShare = levy } : created.Value!;
        // AD-179: o que o cenário de código já liga, agora também por dado (os períodos do menu são mundos completos). Ausente = como antes.
        if (root["PresenceNeedsWork"] is { } presenceNode)
        {
            if (!(presenceNode is JsonValue pv && pv.TryGetValue<bool>(out bool presence)))
                return Result<EconomyRules>.Fail("PresenceNeedsWork: deve ser booleano");
            rules = rules with { PresenceNeedsWork = presence };
        }
        if (root["InitialPantryPerMember"] is { } pantryNode)
        {
            if (!(pantryNode is JsonValue pantryValue && pantryValue.TryGetValue<long>(out long pantry) && pantry is >= 0 and <= 100_000))
                return Result<EconomyRules>.Fail("InitialPantryPerMember: deve ser um inteiro de 0 a 100000");
            rules = rules with { InitialPantryPerMember = pantry };
        }
        if (root["Venture"] is { } ventureNode)
        {
            if (ventureNode is not JsonObject venture) return Result<EconomyRules>.Fail("Venture: objeto inválido");
            var types = new List<int>();
            if (venture["LocationTypeIds"] is JsonArray typesNode)
                foreach (var item in typesNode)
                {
                    if (item is not JsonValue v || !v.TryGetValue<int>(out int id)) return Result<EconomyRules>.Fail("Venture.LocationTypeIds: valor inválido");
                    types.Add(id);
                }
            int equipment = venture["EquipmentResourceId"] is JsonValue ev && ev.TryGetValue<int>(out int e) ? e : 0;
            long units = venture["EquipmentUnits"] is JsonValue uv && uv.TryGetValue<long>(out long u) ? u : 0;
            rules = rules with { Venture = VentureRules.Default with { LocationTypeIds = types, EquipmentResourceId = equipment, EquipmentUnits = units } };
        }
        if (root["Credit"] is { } creditNode)
        {
            if (creditNode is not JsonObject credit) return Result<EconomyRules>.Fail("Credit: objeto inválido");
            if (!(credit["Enabled"] is null || credit["Enabled"] is JsonValue cv && cv.TryGetValue<bool>(out _)))
                return Result<EconomyRules>.Fail("Credit.Enabled: deve ser booleano");
            if (credit["Enabled"]?.GetValue<bool>() != false) rules = rules with { Credit = CreditRules.Default };
        }
        return Result<EconomyRules>.Ok(rules);
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

            var recipe = recipeResult.Value!;
            if (recipeNode["Tool"] is { } toolNode) // ferramenta de ofício (AD-144), opcional
            {
                if (toolNode is not JsonObject tool || !TryGetInt(tool, "ResourceId", out var toolResource)
                    || !TryGetDouble(tool, "KitPerWorker", out var kit) || !TryGetDouble(tool, "WearPerWorkerYear", out var wear)
                    || !TryGetDouble(tool, "UntooledOutput", out var untooled))
                    return Result<EconomyCatalog>.Fail($"Recipes[{key}].Tool: ResourceId, KitPerWorker, WearPerWorkerYear e UntooledOutput são obrigatórios");
                var toolResult = ToolUse.Create(toolResource, kit, wear, untooled);
                if (!toolResult.IsSuccess) return Result<EconomyCatalog>.Fail($"Recipes[{key}].{toolResult.Error}");
                recipe = recipe with { Tool = toolResult.Value };
            }

            recipes[locationTypeId] = recipe;
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

    /// <summary>Um item de <c>Workplaces[]</c>; é o que o cenário e a rota ao vivo (#46 fatia 4) validam.</summary>
    public static Result<InitialWorkplace> LoadWorkplace(JsonNode? node)
    {
        if (node is not JsonObject wp)
            return Result<InitialWorkplace>.Fail("Workplaces: item inválido");

        if (!TryGetInt(wp, "LocationTypeId", out var locationTypeId))
            return Result<InitialWorkplace>.Fail("Workplaces[].LocationTypeId: campo obrigatório ausente ou inválido");
        if (!TryGetInt(wp, "X", out var x))
            return Result<InitialWorkplace>.Fail("Workplaces[].X: campo obrigatório ausente ou inválido");
        if (!TryGetInt(wp, "Y", out var y))
            return Result<InitialWorkplace>.Fail("Workplaces[].Y: campo obrigatório ausente ou inválido");
        if (!TryGetInt(wp, "MaxVacancies", out var maxVacancies))
            return Result<InitialWorkplace>.Fail("Workplaces[].MaxVacancies: campo obrigatório ausente ou inválido");
        if (!TryGetLong(wp, "Treasury", out var treasury))
            return Result<InitialWorkplace>.Fail("Workplaces[].Treasury: campo obrigatório ausente ou inválido");
        if (treasury < 0)
            return Result<InitialWorkplace>.Fail("Workplaces[].Treasury: não pode ser negativo");
        if (!TryGetResourceLongMap(wp, "Stock", out var stock))
            return Result<InitialWorkplace>.Fail("Workplaces[].Stock: campo obrigatório ausente ou inválido");
        if (!TryGetResourceLongMap(wp, "Prices", out var prices))
            return Result<InitialWorkplace>.Fail("Workplaces[].Prices: campo obrigatório ausente ou inválido");

        NpcId? owner = null; // dono declarado (AD-144), opcional
        if (wp["Owner"] is { } ownerNode)
        {
            if (ownerNode is not JsonValue ownerValue || !ownerValue.TryGetValue<long>(out var ownerId) || ownerId <= 0)
                return Result<InitialWorkplace>.Fail("Workplaces[].Owner: deve ser o id (positivo) de um NPC");
            owner = new NpcId(ownerId);
        }

        return Result<InitialWorkplace>.Ok(new InitialWorkplace(
            new LocationType(locationTypeId), new CellCoord(x, y), maxVacancies, stock, new Money(treasury), prices)
        { Owner = owner });
    }

    private static Result<IReadOnlyList<InitialWorkplace>> ParseWorkplaces(JsonObject root)
    {
        if (root["Workplaces"] is not JsonArray workplacesNode)
            return Result<IReadOnlyList<InitialWorkplace>>.Fail("Workplaces: campo obrigatório ausente");

        var workplaces = new List<InitialWorkplace>();
        foreach (var node in workplacesNode)
        {
            var parsed = LoadWorkplace(node);
            if (!parsed.IsSuccess) return Result<IReadOnlyList<InitialWorkplace>>.Fail(parsed.Error!);
            workplaces.Add(parsed.Value!);
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
