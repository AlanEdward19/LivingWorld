using System.Text.Json.Nodes;
using LivingWorld.Domain;

namespace LivingWorld.Simulation;

/// <summary>Dado de cidades resolvido de um cenário (Fase 8, T7): <see cref="CityRules"/>,
/// <see cref="CityCatalog"/> e as <see cref="City"/> iniciais (sem id atribuído ainda — quem
/// consome chama <see cref="WorldState.NextCityId"/> ao adicionar cada uma, mesmo padrão de
/// <see cref="EconomyScenarioLoader"/> pra Workplace).</summary>
public sealed record CityScenarioData(
    CityRules Rules, CityCatalog Catalog, IReadOnlyList<InitialCity> Cities, IReadOnlyList<AuthoredBuilding> Buildings,
    IReadOnlyList<AuthoredPortal> Portals);

/// <summary>Uma <see cref="City"/> ainda sem <see cref="CityId"/> — o cenário declara o
/// conteúdo, o mundo atribui o id na hora de adicionar. <see cref="Name"/> vazio significa "o
/// cenário não autorou um nome" (Fase 15.1, T44) — quem consome resolve o fallback
/// determinístico (<see cref="CityNameGenerator"/>), o loader nunca sorteia nada.
/// <see cref="InitialPopulation"/> nulo significa "sem valor autorado" — <c>ScenarioLoaderV2</c>
/// divide o restante de <c>Population.InitialPopulation</c> igualmente entre as cidades sem
/// valor explícito, exatamente como fazia antes desse campo existir (não muda a fórmula de
/// crescimento de <see cref="CityBoundsResolver"/>, só a população com que cada cidade nasce).</summary>
public sealed record InitialCity(CellCoord Location, long FoundedAtTick, AggregatePopulationPool AggregatePool, string Name = "", int? InitialPopulation = null);

/// <summary>Prédio autorado no World Creator (Fase 15.1, T44) — ainda sem <see
/// cref="BuildingId"/>/<see cref="CityId"/> reais: <see cref="CityIndex"/> referencia a posição
/// do prédio dentro de <see cref="CityScenarioData.Cities"/>, já validada em bounds por
/// <see cref="CityScenarioLoader.Load"/>.</summary>
public sealed record AuthoredBuilding(int CityIndex, int BuildingTypeId, CellCoord Position, int Orientation);

/// <summary>Um lado de um <see cref="AuthoredPortal"/> (Fase 15.1, T21) — <see cref="RefIndex"/>
/// referencia a posição do endpoint dentro de <see cref="CityScenarioData.Cities"/> (quando
/// <see cref="Space"/> é <see cref="PortalSpaceKind.City"/>) ou <see
/// cref="CityScenarioData.Buildings"/> (quando <see cref="PortalSpaceKind.Building"/>); ignorado
/// para <see cref="PortalSpaceKind.World"/>, que é único e não precisa de índice. Mesmo papel de
/// <see cref="AuthoredBuilding.CityIndex"/>: id real só existe depois que o mundo atribui.</summary>
public sealed record AuthoredPortalEndpoint(PortalSpaceKind Space, int RefIndex, CellCoord Cell);

/// <summary>Portal autorado no cenário (Fase 15.1, T21, OQ-2) — mesmo papel de <see
/// cref="AuthoredBuilding"/>: ainda sem <see cref="PortalEndpoint.RefId"/> real, resolvido por
/// <c>ScenarioLoaderV2</c> depois que cidades/prédios ganham id.</summary>
public sealed record AuthoredPortal(string Id, string Label, AuthoredPortalEndpoint From, AuthoredPortalEndpoint To);

/// <summary>Carrega <see cref="CityRules"/>/<see cref="CityCatalog"/>/<see cref="City"/> iniciais
/// de um cenário (Fase 8, T7): nenhum parâmetro de cidade hardcoded em C# (R3). Mesmo padrão
/// manual-parse + <see cref="Result{T}"/> de <see cref="EconomyScenarioLoader"/> — campo
/// obrigatório ausente nomeia o campo no erro.</summary>
public static class CityScenarioLoader
{
    public static Result<CityScenarioData> Load(string json, int mapWidth = int.MaxValue, int mapHeight = int.MaxValue)
    {
        JsonObject root;
        try
        {
            root = JsonNode.Parse(json)?.AsObject() ?? throw new System.Text.Json.JsonException("corpo vazio");
        }
        catch (System.Text.Json.JsonException ex)
        {
            return Result<CityScenarioData>.Fail($"json: {ex.Message}");
        }

        var rulesResult = ParseRules(root);
        if (!rulesResult.IsSuccess)
            return Result<CityScenarioData>.Fail(rulesResult.Error!);

        var catalogResult = ParseCatalog(root);
        if (!catalogResult.IsSuccess)
            return Result<CityScenarioData>.Fail(catalogResult.Error!);

        var citiesResult = ParseCities(root);
        if (!citiesResult.IsSuccess)
            return Result<CityScenarioData>.Fail(citiesResult.Error!);

        var buildingsResult = ParseBuildings(root, mapWidth, mapHeight, citiesResult.Value!.Count);
        if (!buildingsResult.IsSuccess)
            return Result<CityScenarioData>.Fail(buildingsResult.Error!);

        var portalsResult = ParsePortals(root, citiesResult.Value!.Count, buildingsResult.Value!.Count);
        if (!portalsResult.IsSuccess)
            return Result<CityScenarioData>.Fail(portalsResult.Error!);

        return Result<CityScenarioData>.Ok(
            new CityScenarioData(
                rulesResult.Value!, catalogResult.Value!, citiesResult.Value!, buildingsResult.Value!, portalsResult.Value!));
    }

    private static Result<CityRules> ParseRules(JsonObject root)
    {
        if (!TryGetBool(root, "CitiesEnabled", out var enabled))
            return Result<CityRules>.Fail("CitiesEnabled: campo obrigatório ausente ou inválido");
        if (!TryGetDouble(root, "FoodShortageThreshold", out var foodShortage))
            return Result<CityRules>.Fail("FoodShortageThreshold: campo obrigatório ausente ou inválido");
        if (!TryGetDouble(root, "HousingShortageThreshold", out var housingShortage))
            return Result<CityRules>.Fail("HousingShortageThreshold: campo obrigatório ausente ou inválido");
        if (!TryGetDouble(root, "SecurityShortageThreshold", out var securityShortage))
            return Result<CityRules>.Fail("SecurityShortageThreshold: campo obrigatório ausente ou inválido");
        if (!TryGetDouble(root, "EmigrationRatePerDeficitUnit", out var emigrationRate))
            return Result<CityRules>.Fail("EmigrationRatePerDeficitUnit: campo obrigatório ausente ou inválido");
        if (!TryGetDouble(root, "MigrationEmploymentWeight", out var employmentWeight))
            return Result<CityRules>.Fail("MigrationEmploymentWeight: campo obrigatório ausente ou inválido");
        if (!TryGetDouble(root, "MigrationFoodWeight", out var foodWeight))
            return Result<CityRules>.Fail("MigrationFoodWeight: campo obrigatório ausente ou inválido");
        if (!TryGetDouble(root, "MigrationSecurityWeight", out var securityWeight))
            return Result<CityRules>.Fail("MigrationSecurityWeight: campo obrigatório ausente ou inválido");
        if (!TryGetDouble(root, "MigrationFamilyTiesWeight", out var familyTiesWeight))
            return Result<CityRules>.Fail("MigrationFamilyTiesWeight: campo obrigatório ausente ou inválido");
        if (!TryGetDouble(root, "FoundingConcentrationThreshold", out var concentration))
            return Result<CityRules>.Fail("FoundingConcentrationThreshold: campo obrigatório ausente ou inválido");
        if (!TryGetDouble(root, "FoundingResourceThreshold", out var resource))
            return Result<CityRules>.Fail("FoundingResourceThreshold: campo obrigatório ausente ou inválido");
        if (!TryGetDouble(root, "FoundingRouteThreshold", out var route))
            return Result<CityRules>.Fail("FoundingRouteThreshold: campo obrigatório ausente ou inválido");
        if (!TryGetDouble(root, "FoundingDefensibilityThreshold", out var defensibility))
            return Result<CityRules>.Fail("FoundingDefensibilityThreshold: campo obrigatório ausente ou inválido");
        if (!TryGetDouble(root, "FoundingLeadershipThreshold", out var leadership))
            return Result<CityRules>.Fail("FoundingLeadershipThreshold: campo obrigatório ausente ou inválido");
        if (!TryGetLong(root, "OrganizationTicks", out var organizationTicks))
            return Result<CityRules>.Fail("OrganizationTicks: campo obrigatório ausente ou inválido");
        if (!TryGetLong(root, "MaterializationIdleTicksBeforeEligible", out var idleTicks))
            return Result<CityRules>.Fail("MaterializationIdleTicksBeforeEligible: campo obrigatório ausente ou inválido");

        return CityRules.Create(
            enabled, foodShortage, housingShortage, securityShortage, emigrationRate,
            employmentWeight, foodWeight, securityWeight, familyTiesWeight,
            concentration, resource, route, defensibility, leadership,
            organizationTicks, idleTicks);
    }

    private static Result<CityCatalog> ParseCatalog(JsonObject root)
    {
        if (root["BuildingRecipes"] is not JsonObject recipesNode)
            return Result<CityCatalog>.Fail("BuildingRecipes: campo obrigatório ausente");

        var recipes = new Dictionary<int, BuildingRecipe>();
        foreach (var (key, node) in recipesNode)
        {
            if (!int.TryParse(key, out var buildingTypeId))
                return Result<CityCatalog>.Fail($"BuildingRecipes[{key}]: chave de tipo de edifício inválida");
            if (node is not JsonObject recipeNode)
                return Result<CityCatalog>.Fail($"BuildingRecipes[{key}]: item inválido");

            if (!TryGetResourceLongMap(recipeNode, "Inputs", out var inputs))
                return Result<CityCatalog>.Fail($"BuildingRecipes[{key}].Inputs: campo obrigatório ausente ou inválido");
            if (!TryGetLong(recipeNode, "TicksToBuild", out var ticksToBuild))
                return Result<CityCatalog>.Fail($"BuildingRecipes[{key}].TicksToBuild: campo obrigatório ausente ou inválido");
            if (!TryGetLong(recipeNode, "HousingCapacityProvided", out var housingCapacity))
                return Result<CityCatalog>.Fail($"BuildingRecipes[{key}].HousingCapacityProvided: campo obrigatório ausente ou inválido");

            WorkplaceProvision? workplace = null;
            if (recipeNode["Workplace"] is JsonObject workplaceNode)
            {
                if (!TryGetInt(workplaceNode, "LocationTypeId", out var locationTypeId))
                    return Result<CityCatalog>.Fail($"BuildingRecipes[{key}].Workplace.LocationTypeId: campo obrigatório ausente ou inválido");
                if (!TryGetInt(workplaceNode, "MaxVacancies", out var maxVacancies))
                    return Result<CityCatalog>.Fail($"BuildingRecipes[{key}].Workplace.MaxVacancies: campo obrigatório ausente ou inválido");
                workplace = new WorkplaceProvision(locationTypeId, maxVacancies);
            }

            var recipeResult = BuildingRecipe.Create(inputs, ticksToBuild, housingCapacity, workplace);
            if (!recipeResult.IsSuccess)
                return Result<CityCatalog>.Fail($"BuildingRecipes[{key}]: {recipeResult.Error}");

            recipes[buildingTypeId] = recipeResult.Value!;
        }

        return Result<CityCatalog>.Ok(new CityCatalog(recipes));
    }

    private static Result<IReadOnlyList<InitialCity>> ParseCities(JsonObject root)
    {
        if (root["Cities"] is not JsonArray citiesNode)
            return Result<IReadOnlyList<InitialCity>>.Fail("Cities: campo obrigatório ausente");

        var cities = new List<InitialCity>();
        foreach (var node in citiesNode)
        {
            if (node is not JsonObject city)
                return Result<IReadOnlyList<InitialCity>>.Fail("Cities: item inválido");

            if (!TryGetInt(city, "X", out var x))
                return Result<IReadOnlyList<InitialCity>>.Fail("Cities[].X: campo obrigatório ausente ou inválido");
            if (!TryGetInt(city, "Y", out var y))
                return Result<IReadOnlyList<InitialCity>>.Fail("Cities[].Y: campo obrigatório ausente ou inválido");
            if (!TryGetLong(city, "FoundedAtTick", out var foundedAtTick))
                return Result<IReadOnlyList<InitialCity>>.Fail("Cities[].FoundedAtTick: campo obrigatório ausente ou inválido");

            if (city["AggregatePool"] is not JsonObject poolNode)
                return Result<IReadOnlyList<InitialCity>>.Fail("Cities[].AggregatePool: campo obrigatório ausente");
            if (!TryGetLong(poolNode, "Count", out var count))
                return Result<IReadOnlyList<InitialCity>>.Fail("Cities[].AggregatePool.Count: campo obrigatório ausente ou inválido");
            if (!TryGetLong(poolNode, "WealthSum", out var wealthSum))
                return Result<IReadOnlyList<InitialCity>>.Fail("Cities[].AggregatePool.WealthSum: campo obrigatório ausente ou inválido");
            if (!TryGetLong(poolNode, "HealthSum", out var healthSum))
                return Result<IReadOnlyList<InitialCity>>.Fail("Cities[].AggregatePool.HealthSum: campo obrigatório ausente ou inválido");

            string name = city["Name"]?.GetValue<string>() ?? "";
            int? initialPopulation = TryGetInt(city, "InitialPopulation", out var explicitPopulation) ? explicitPopulation : null;
            if (initialPopulation is < 0)
                return Result<IReadOnlyList<InitialCity>>.Fail("Cities[].InitialPopulation: não pode ser negativo");

            cities.Add(new InitialCity(new CellCoord(x, y), foundedAtTick, new AggregatePopulationPool(count, wealthSum, healthSum), name, initialPopulation));
        }

        return Result<IReadOnlyList<InitialCity>>.Ok(cities);
    }

    /// <summary>Prédios autorados (Fase 15.1, T44): valida bounds (célula dentro do mapa),
    /// overlap (duas células iguais entre prédios) e referência (índice de cidade existente) —
    /// falha na borda, nomeando o campo, nunca ao adicionar o prédio ao mundo.</summary>
    private static Result<IReadOnlyList<AuthoredBuilding>> ParseBuildings(JsonObject root, int mapWidth, int mapHeight, int cityCount)
    {
        var buildings = new List<AuthoredBuilding>();
        if (root["Buildings"] is not JsonArray buildingsNode)
            return Result<IReadOnlyList<AuthoredBuilding>>.Ok(buildings);

        var occupied = new HashSet<CellCoord>();
        foreach (var node in buildingsNode)
        {
            if (node is not JsonObject b)
                return Result<IReadOnlyList<AuthoredBuilding>>.Fail("Buildings: item inválido");
            if (!TryGetInt(b, "CityIndex", out var cityIndex))
                return Result<IReadOnlyList<AuthoredBuilding>>.Fail("Buildings[].CityIndex: campo obrigatório ausente ou inválido");
            if (!TryGetInt(b, "BuildingTypeId", out var buildingTypeId))
                return Result<IReadOnlyList<AuthoredBuilding>>.Fail("Buildings[].BuildingTypeId: campo obrigatório ausente ou inválido");
            if (!TryGetInt(b, "X", out var x))
                return Result<IReadOnlyList<AuthoredBuilding>>.Fail("Buildings[].X: campo obrigatório ausente ou inválido");
            if (!TryGetInt(b, "Y", out var y))
                return Result<IReadOnlyList<AuthoredBuilding>>.Fail("Buildings[].Y: campo obrigatório ausente ou inválido");
            int orientation = b["Orientation"]?.GetValue<int>() ?? 0;

            if (cityIndex < 0 || cityIndex >= cityCount)
                return Result<IReadOnlyList<AuthoredBuilding>>.Fail($"Buildings[].CityIndex: {cityIndex} não referencia nenhuma cidade autorada (0..{cityCount - 1})");
            if (x < 0 || x >= mapWidth || y < 0 || y >= mapHeight)
                return Result<IReadOnlyList<AuthoredBuilding>>.Fail($"Buildings[].Position: célula ({x},{y}) fora do grid {mapWidth}x{mapHeight}");

            var position = new CellCoord(x, y);
            if (!occupied.Add(position))
                return Result<IReadOnlyList<AuthoredBuilding>>.Fail($"Buildings[].Position: célula ({x},{y}) ocupada por outro prédio autorado");

            buildings.Add(new AuthoredBuilding(cityIndex, buildingTypeId, position, orientation));
        }

        return Result<IReadOnlyList<AuthoredBuilding>>.Ok(buildings);
    }

    /// <summary>Portais autorados (Fase 15.1, T21): campo opcional — cenário sem <c>Portals</c>
    /// declarado continua válido (spec.md AC4), mesmo padrão de <see cref="ParseBuildings"/> pra
    /// "Buildings" ausente. Cada endpoint referencia índice de cidade/prédio já parseado (mesma
    /// validação de referência de <see cref="AuthoredBuilding.CityIndex"/>).</summary>
    private static Result<IReadOnlyList<AuthoredPortal>> ParsePortals(JsonObject root, int cityCount, int buildingCount)
    {
        var portals = new List<AuthoredPortal>();
        if (root["Portals"] is not JsonArray portalsNode)
            return Result<IReadOnlyList<AuthoredPortal>>.Ok(portals);

        foreach (var node in portalsNode)
        {
            if (node is not JsonObject p)
                return Result<IReadOnlyList<AuthoredPortal>>.Fail("Portals: item inválido");
            if (p["Id"] is not JsonValue idValue || !idValue.TryGetValue<string>(out var id) || string.IsNullOrEmpty(id))
                return Result<IReadOnlyList<AuthoredPortal>>.Fail("Portals[].Id: campo obrigatório ausente ou inválido");
            string label = p["Label"]?.GetValue<string>() ?? "";

            if (p["From"] is not JsonObject fromNode)
                return Result<IReadOnlyList<AuthoredPortal>>.Fail("Portals[].From: campo obrigatório ausente");
            var fromResult = ParsePortalEndpoint(fromNode, "Portals[].From", cityCount, buildingCount);
            if (!fromResult.IsSuccess)
                return Result<IReadOnlyList<AuthoredPortal>>.Fail(fromResult.Error!);

            if (p["To"] is not JsonObject toNode)
                return Result<IReadOnlyList<AuthoredPortal>>.Fail("Portals[].To: campo obrigatório ausente");
            var toResult = ParsePortalEndpoint(toNode, "Portals[].To", cityCount, buildingCount);
            if (!toResult.IsSuccess)
                return Result<IReadOnlyList<AuthoredPortal>>.Fail(toResult.Error!);

            portals.Add(new AuthoredPortal(id, label, fromResult.Value!, toResult.Value!));
        }

        return Result<IReadOnlyList<AuthoredPortal>>.Ok(portals);
    }

    private static Result<AuthoredPortalEndpoint> ParsePortalEndpoint(JsonObject node, string fieldPrefix, int cityCount, int buildingCount)
    {
        if (node["Space"] is not JsonValue spaceValue || !spaceValue.TryGetValue<string>(out var spaceText)
            || !Enum.TryParse<PortalSpaceKind>(spaceText, out var space))
            return Result<AuthoredPortalEndpoint>.Fail($"{fieldPrefix}.Space: campo obrigatório ausente ou inválido");
        if (!TryGetInt(node, "X", out var x))
            return Result<AuthoredPortalEndpoint>.Fail($"{fieldPrefix}.X: campo obrigatório ausente ou inválido");
        if (!TryGetInt(node, "Y", out var y))
            return Result<AuthoredPortalEndpoint>.Fail($"{fieldPrefix}.Y: campo obrigatório ausente ou inválido");
        int refIndex = node["RefIndex"]?.GetValue<int>() ?? 0;

        if (space == PortalSpaceKind.City && (refIndex < 0 || refIndex >= cityCount))
            return Result<AuthoredPortalEndpoint>.Fail($"{fieldPrefix}.RefIndex: {refIndex} não referencia nenhuma cidade autorada (0..{cityCount - 1})");
        if (space == PortalSpaceKind.Building && (refIndex < 0 || refIndex >= buildingCount))
            return Result<AuthoredPortalEndpoint>.Fail($"{fieldPrefix}.RefIndex: {refIndex} não referencia nenhum prédio autorado (0..{buildingCount - 1})");

        return Result<AuthoredPortalEndpoint>.Ok(new AuthoredPortalEndpoint(space, refIndex, new CellCoord(x, y)));
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
}
