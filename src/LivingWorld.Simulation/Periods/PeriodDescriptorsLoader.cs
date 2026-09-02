using System.Text.Json.Nodes;
using LivingWorld.Domain;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Simulation.Periods;

/// <summary>Descritor legível de um id de catálogo (Fase 15.1, T48/backend-gaps.md G8) — mesmo
/// contrato de <see cref="ProfessionBias.Name"/>/<see cref="SkillBias.Name"/>: puramente
/// descritivo, o motor nunca decide por ele. <see cref="RangeMin"/>/<see cref="RangeMax"/>/<see
/// cref="Unit"/> só fazem sentido para descritores numéricos (ex.: recurso com faixa/unidade de
/// estoque) — ficam nulos quando não aplicável, nunca inventados.</summary>
public sealed record CatalogDescriptor(
    int Id, string Name, string? Explanation = null, double? RangeMin = null, double? RangeMax = null, string? Unit = null);

/// <summary>Catálogo visual legível do período (Fase 15.1, T48): um descritor só existe para um
/// id se o período o declarou explicitamente — id sem descritor declarado simplesmente não
/// aparece em nenhuma destas listas (mesma regra de <see cref="ProfessionBias"/>/<see
/// cref="SkillBias"/>: o motor nunca inventa rótulo).</summary>
public sealed record PeriodDescriptors(
    IReadOnlyList<CatalogDescriptor> Terrain,
    IReadOnlyList<CatalogDescriptor> Biome,
    IReadOnlyList<CatalogDescriptor> Resource,
    IReadOnlyList<CatalogDescriptor> Culture,
    IReadOnlyList<CatalogDescriptor> LocationType,
    IReadOnlyList<CatalogDescriptor> BuildingType,
    IReadOnlyList<CatalogDescriptor> Action)
{
    public static readonly PeriodDescriptors Empty = new([], [], [], [], [], [], []);
}

/// <summary>Carrega o bloco opcional <c>Descriptors</c> de um cenário (Fase 15.1, T48) — mesmo
/// padrão manual-parse + <see cref="Result{T}"/> de <see cref="PeriodDynamicsLoader"/>. Ausente
/// equivale a <see cref="PeriodDescriptors.Empty"/>, nunca a uma falha.</summary>
public static class PeriodDescriptorsLoader
{
    private static readonly string[] Categories =
        ["Terrain", "Biome", "Resource", "Culture", "LocationType", "BuildingType", "Action"];

    public static Result<PeriodDescriptors> Load(string json)
    {
        JsonObject root;
        try
        {
            root = JsonNode.Parse(json)?.AsObject() ?? throw new System.Text.Json.JsonException("corpo vazio");
        }
        catch (System.Text.Json.JsonException ex)
        {
            return Result<PeriodDescriptors>.Fail($"json: {ex.Message}");
        }

        if (root["Descriptors"] is null)
            return Result<PeriodDescriptors>.Ok(PeriodDescriptors.Empty);

        if (root["Descriptors"] is not JsonObject descriptors)
            return Result<PeriodDescriptors>.Fail("Descriptors: campo inválido");

        var byCategory = new Dictionary<string, IReadOnlyList<CatalogDescriptor>>();
        foreach (var field in Categories)
        {
            var result = ParseCategory(descriptors, field);
            if (!result.IsSuccess)
                return Result<PeriodDescriptors>.Fail(result.Error!);
            byCategory[field] = result.Value!;
        }

        return Result<PeriodDescriptors>.Ok(new PeriodDescriptors(
            byCategory["Terrain"], byCategory["Biome"], byCategory["Resource"], byCategory["Culture"],
            byCategory["LocationType"], byCategory["BuildingType"], byCategory["Action"]));
    }

    private static Result<IReadOnlyList<CatalogDescriptor>> ParseCategory(JsonObject descriptors, string field)
    {
        if (descriptors[field] is null)
            return Result<IReadOnlyList<CatalogDescriptor>>.Ok([]);

        if (descriptors[field] is not JsonArray array)
            return Result<IReadOnlyList<CatalogDescriptor>>.Fail($"Descriptors.{field}: campo inválido");

        var items = new List<CatalogDescriptor>();
        foreach (var node in array)
        {
            if (node is not JsonObject item)
                return Result<IReadOnlyList<CatalogDescriptor>>.Fail($"Descriptors.{field}[]: item inválido");
            if (!TryGetInt(item, "Id", out var id))
                return Result<IReadOnlyList<CatalogDescriptor>>.Fail($"Descriptors.{field}[].Id: campo obrigatório ausente ou inválido");
            if (!TryGetString(item, "Name", out var name) || string.IsNullOrWhiteSpace(name))
                return Result<IReadOnlyList<CatalogDescriptor>>.Fail($"Descriptors.{field}[].Name: campo obrigatório ausente ou inválido");
            if (!TryGetOptionalString(item, "Explanation", out var explanation))
                return Result<IReadOnlyList<CatalogDescriptor>>.Fail($"Descriptors.{field}[].Explanation: campo inválido");
            if (!TryGetOptionalString(item, "Unit", out var unit))
                return Result<IReadOnlyList<CatalogDescriptor>>.Fail($"Descriptors.{field}[].Unit: campo inválido");
            if (!TryGetOptionalDouble(item, "RangeMin", out var rangeMin))
                return Result<IReadOnlyList<CatalogDescriptor>>.Fail($"Descriptors.{field}[].RangeMin: campo inválido");
            if (!TryGetOptionalDouble(item, "RangeMax", out var rangeMax))
                return Result<IReadOnlyList<CatalogDescriptor>>.Fail($"Descriptors.{field}[].RangeMax: campo inválido");
            if (rangeMin is { } min && rangeMax is { } max && min > max)
                return Result<IReadOnlyList<CatalogDescriptor>>.Fail($"Descriptors.{field}[]: RangeMin não pode ser maior que RangeMax");

            items.Add(new CatalogDescriptor(id, name!, explanation, rangeMin, rangeMax, unit));
        }

        return Result<IReadOnlyList<CatalogDescriptor>>.Ok(items);
    }

    private static bool TryGetInt(JsonObject root, string field, out int value)
    {
        value = 0;
        return root[field] is JsonValue v && v.TryGetValue(out value);
    }

    private static bool TryGetString(JsonObject root, string field, out string? value)
    {
        value = null;
        return root[field] is JsonValue v && v.TryGetValue(out value);
    }

    private static bool TryGetOptionalString(JsonObject root, string field, out string? value)
    {
        value = null;
        if (root[field] is null) return true;
        return root[field] is JsonValue v && v.TryGetValue(out value);
    }

    private static bool TryGetOptionalDouble(JsonObject root, string field, out double? value)
    {
        value = null;
        if (root[field] is null) return true;
        if (root[field] is not JsonValue v || !v.TryGetValue<double>(out var parsed)) return false;
        value = parsed;
        return true;
    }
}
