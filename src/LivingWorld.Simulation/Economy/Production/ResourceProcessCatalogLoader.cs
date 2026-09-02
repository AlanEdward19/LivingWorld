using System.Text.Json.Nodes;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Simulation.Economy.Production;

/// <summary>Carrega catálogo de recursos/processos estagiados (Fase 15.1, Stage 4, T14).
/// Seções opcionais: cenário legado sem <c>Resources</c>/<c>ProcessRecipes</c> continua válido.</summary>
public static class ResourceProcessCatalogLoader
{
    public static Result<(ResourceCatalog Catalog, IReadOnlyList<ProcessRecipe> Recipes)> Load(string json)
    {
        JsonObject root;
        try
        {
            root = JsonNode.Parse(json)?.AsObject() ?? throw new System.Text.Json.JsonException("corpo vazio");
        }
        catch (System.Text.Json.JsonException ex)
        {
            return Result<(ResourceCatalog, IReadOnlyList<ProcessRecipe>)>.Fail($"json: {ex.Message}");
        }

        var catalogResult = ParseResources(root);
        if (!catalogResult.IsSuccess)
            return Result<(ResourceCatalog, IReadOnlyList<ProcessRecipe>)>.Fail(catalogResult.Error!);

        var recipesResult = ParseRecipes(root);
        if (!recipesResult.IsSuccess)
            return Result<(ResourceCatalog, IReadOnlyList<ProcessRecipe>)>.Fail(recipesResult.Error!);

        return Result<(ResourceCatalog, IReadOnlyList<ProcessRecipe>)>.Ok((catalogResult.Value!, recipesResult.Value!));
    }

    private static Result<ResourceCatalog> ParseResources(JsonObject root)
    {
        if (root["Resources"] is not JsonArray resources)
            return Result<ResourceCatalog>.Ok(ResourceCatalog.Empty);

        var specs = new Dictionary<int, ResourceSpec>();
        foreach (var node in resources)
        {
            if (node is not JsonObject item)
                return Result<ResourceCatalog>.Fail("Resources[]: item inválido");
            if (item["Id"] is not JsonValue idNode || !idNode.TryGetValue<int>(out var id))
                return Result<ResourceCatalog>.Fail("Resources[].Id: campo obrigatório ausente ou inválido");
            if (item["Preparation"] is not JsonValue prepNode || !prepNode.TryGetValue<string>(out var prepText)
                || !Enum.TryParse<PreparationState>(prepText, out var preparation))
                return Result<ResourceCatalog>.Fail("Resources[].Preparation: campo obrigatório ausente ou inválido");
            if (item["Edible"] is not JsonValue edibleNode || !edibleNode.TryGetValue<bool>(out var edible))
                return Result<ResourceCatalog>.Fail("Resources[].Edible: campo obrigatório ausente ou inválido");

            var specResult = ResourceSpec.Create(id, preparation, edible);
            if (!specResult.IsSuccess)
                return Result<ResourceCatalog>.Fail(specResult.Error!);
            if (!specs.TryAdd(id, specResult.Value!))
                return Result<ResourceCatalog>.Fail($"Resources[{id}]: id duplicado");
        }

        return Result<ResourceCatalog>.Ok(new ResourceCatalog(specs));
    }

    private static Result<IReadOnlyList<ProcessRecipe>> ParseRecipes(JsonObject root)
    {
        if (root["ProcessRecipes"] is not JsonArray recipes)
            return Result<IReadOnlyList<ProcessRecipe>>.Ok([]);

        var parsed = new List<ProcessRecipe>();
        foreach (var node in recipes)
        {
            if (node is not JsonObject item)
                return Result<IReadOnlyList<ProcessRecipe>>.Fail("ProcessRecipes[]: item inválido");
            if (item["Kind"] is not JsonValue kindNode || !kindNode.TryGetValue<string>(out var kindText)
                || !Enum.TryParse<ProcessKind>(kindText, out var kind))
                return Result<IReadOnlyList<ProcessRecipe>>.Fail("ProcessRecipes[].Kind: campo obrigatório ausente ou inválido");
            if (item["OutputResourceId"] is not JsonValue outNode || !outNode.TryGetValue<int>(out var outputId))
                return Result<IReadOnlyList<ProcessRecipe>>.Fail("ProcessRecipes[].OutputResourceId: campo obrigatório ausente ou inválido");
            if (item["OutputQuantity"] is not JsonValue qtyNode || !qtyNode.TryGetValue<long>(out var quantity))
                return Result<IReadOnlyList<ProcessRecipe>>.Fail("ProcessRecipes[].OutputQuantity: campo obrigatório ausente ou inválido");
            if (item["DurationTicks"] is not JsonValue durNode || !durNode.TryGetValue<long>(out var duration))
                return Result<IReadOnlyList<ProcessRecipe>>.Fail("ProcessRecipes[].DurationTicks: campo obrigatório ausente ou inválido");

            int? workplace = null;
            if (item.ContainsKey("WorkplaceTypeId"))
            {
                if (item["WorkplaceTypeId"] is not JsonValue workNode || !workNode.TryGetValue<int>(out var workplaceId))
                    return Result<IReadOnlyList<ProcessRecipe>>.Fail("ProcessRecipes[].WorkplaceTypeId: valor inválido");
                workplace = workplaceId;
            }

            var inputs = new Dictionary<int, long>();
            if (item["Inputs"] is JsonObject inputsNode)
            {
                foreach (var (key, value) in inputsNode)
                {
                    if (!int.TryParse(key, out var resourceId) || value is not JsonValue amountNode
                        || !amountNode.TryGetValue<long>(out var amount))
                        return Result<IReadOnlyList<ProcessRecipe>>.Fail($"ProcessRecipes[].Inputs[{key}]: valor inválido");
                    inputs[resourceId] = amount;
                }
            }

            var recipeResult = ProcessRecipe.Create(kind, inputs, outputId, quantity, workplace, duration);
            if (!recipeResult.IsSuccess)
                return Result<IReadOnlyList<ProcessRecipe>>.Fail(recipeResult.Error!);
            parsed.Add(recipeResult.Value!);
        }

        return Result<IReadOnlyList<ProcessRecipe>>.Ok(parsed);
    }
}
