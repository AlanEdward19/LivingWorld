using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using LivingWorld.Domain.Objects;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Simulation.Objects;

/// <summary>Lê os blocos opcionais <c>objectsFormat</c> + <c>Objects</c> (+ <c>Furnishings</c>) do cenário (ADR-0034). Ausentes = <c>null</c>
/// (comportamento e hash de antes). Forma incoerente é recusada com o caminho do campo.</summary>
public static class ObjectCatalogScenarioLoader
{
    private static readonly string[] Shapes = ["box", "cylinder", "sphere", "capsule"];
    private static readonly string[] Symmetries = ["none", "x", "z", "xz"];
    private static readonly int[] ShapeDims = [3, 2, 1, 2];
    private static readonly Regex HexColor = new("^[0-9a-fA-F]{6}$", RegexOptions.Compiled);

    public static Result<ObjectCatalog?> Load(JsonObject root)
    {
        if (root["Objects"] is null)
            return root["objectsFormat"] is null && root["Furnishings"] is null
                ? Result<ObjectCatalog?>.Ok(null)
                : Fail("Objects: objectsFormat/Furnishings sem o bloco Objects");
        if (root["objectsFormat"] is not JsonValue fv || !fv.TryGetValue(out int format))
            return Fail("objectsFormat: obrigatório (inteiro) quando há Objects");
        if (format != ObjectCatalog.CurrentFormat)
            return Fail($"objectsFormat: versão {format} desconhecida (suportada: {ObjectCatalog.CurrentFormat})");
        if (root["Objects"] is not JsonArray array) return Fail("Objects: precisa ser uma lista");

        var objects = new List<ObjectDescriptor>();
        for (int i = 0; i < array.Count; i++)
        {
            var parsed = ParseObject(array[i], $"Objects[{i}]");
            if (!parsed.IsSuccess) return Fail(parsed.Error!);
            if (objects.Any(o => o.Id == parsed.Value!.Id)) return Fail($"Objects[{i}].id: '{parsed.Value!.Id}' duplicado");
            objects.Add(parsed.Value!);
        }

        var furnishings = new Dictionary<string, IReadOnlyList<string>>();
        if (root["Furnishings"] is not null)
        {
            if (root["Furnishings"] is not JsonObject table) return Fail("Furnishings: precisa ser um objeto");
            foreach (var (key, value) in table)
            {
                if (key != "default" && !int.TryParse(key, out _)) return Fail($"Furnishings.{key}: a chave é 'default' ou o id do tipo de prédio");
                if (value is not JsonArray ids) return Fail($"Furnishings.{key}: precisa ser uma lista de ids");
                var list = new List<string>();
                foreach (var node in ids)
                {
                    if (node is not JsonValue v || !v.TryGetValue(out string? id) || objects.All(o => o.Id != id))
                        return Fail($"Furnishings.{key}: objeto '{node}' não existe em Objects");
                    list.Add(id);
                }
                furnishings[key] = list;
            }
        }
        return Result<ObjectCatalog?>.Ok(new ObjectCatalog(format, objects, furnishings));
    }

    private static Result<ObjectCatalog?> Fail(string error) => Result<ObjectCatalog?>.Fail(error);

    private static Result<ObjectDescriptor> ParseObject(JsonNode? node, string at)
    {
        if (node is not JsonObject o) return Result<ObjectDescriptor>.Fail($"{at}: objeto inválido");
        if (!TryString(o["id"], out var id) || id.Length == 0) return Result<ObjectDescriptor>.Fail($"{at}.id: texto obrigatório");
        if (!TryString(o["category"], out var category) || category.Length == 0) return Result<ObjectDescriptor>.Fail($"{at}.category: texto obrigatório");
        int seed = 0;
        if (o["variationSeed"] is not null && !(o["variationSeed"] is JsonValue sv && sv.TryGetValue(out seed)))
            return Result<ObjectDescriptor>.Fail($"{at}.variationSeed: inteiro inválido");
        if (o["shape"] is not JsonArray shape || shape.Count == 0) return Result<ObjectDescriptor>.Fail($"{at}.shape: lista com ao menos uma parte");

        var parts = new List<ObjectPart>();
        for (int p = 0; p < shape.Count; p++)
        {
            var part = ParsePart(shape[p], $"{at}.shape[{p}]");
            if (!part.IsSuccess) return Result<ObjectDescriptor>.Fail(part.Error!);
            parts.Add(part.Value!);
        }

        var points = new List<ObjectUsePoint>();
        if (o["usePoints"] is not null)
        {
            if (o["usePoints"] is not JsonArray pointArray) return Result<ObjectDescriptor>.Fail($"{at}.usePoints: precisa ser uma lista");
            for (int u = 0; u < pointArray.Count; u++)
            {
                var path = $"{at}.usePoints[{u}]";
                if (pointArray[u] is not JsonObject po || !TryString(po["name"], out var name) || name.Length == 0)
                    return Result<ObjectDescriptor>.Fail($"{path}.name: texto obrigatório");
                var offset = Numbers(po["offset"], 3, $"{path}.offset", requirePositive: false);
                if (!offset.IsSuccess) return Result<ObjectDescriptor>.Fail(offset.Error!);
                double facing = 0;
                if (po["facing"] is not null && !(po["facing"] is JsonValue fv && fv.TryGetValue(out facing) && double.IsFinite(facing)))
                    return Result<ObjectDescriptor>.Fail($"{path}.facing: número inválido");
                points.Add(new ObjectUsePoint(name, offset.Value!, facing));
            }
        }
        return Result<ObjectDescriptor>.Ok(new ObjectDescriptor(id, category, parts, seed, points));
    }

    private static Result<ObjectPart> ParsePart(JsonNode? node, string at)
    {
        if (node is not JsonObject o) return Result<ObjectPart>.Fail($"{at}: objeto inválido");
        if (!TryString(o["shape"], out var kind) || Array.IndexOf(Shapes, kind) < 0)
            return Result<ObjectPart>.Fail($"{at}.shape: parte desconhecida (use {string.Join("|", Shapes)})");
        var size = Numbers(o["size"], ShapeDims[Array.IndexOf(Shapes, kind)], $"{at}.size", requirePositive: true);
        if (!size.IsSuccess) return Result<ObjectPart>.Fail(size.Error!);
        IReadOnlyList<double> offset = [0, 0, 0];
        if (o["offset"] is not null)
        {
            var parsed = Numbers(o["offset"], 3, $"{at}.offset", requirePositive: false);
            if (!parsed.IsSuccess) return Result<ObjectPart>.Fail(parsed.Error!);
            offset = parsed.Value!;
        }
        if (!TryString(o["color"], out var color) || !HexColor.IsMatch(color))
            return Result<ObjectPart>.Fail($"{at}.color: use 6 dígitos hexadecimais (ex.: 8a6a43)");
        var symmetry = "none";
        if (o["symmetry"] is not null && (!TryString(o["symmetry"], out symmetry) || Array.IndexOf(Symmetries, symmetry) < 0))
            return Result<ObjectPart>.Fail($"{at}.symmetry: use {string.Join("|", Symmetries)}");
        return Result<ObjectPart>.Ok(new ObjectPart(kind, size.Value!, offset, color.ToLowerInvariant(), symmetry));
    }

    private static Result<IReadOnlyList<double>> Numbers(JsonNode? node, int count, string at, bool requirePositive)
    {
        if (node is not JsonArray array || array.Count != count)
            return Result<IReadOnlyList<double>>.Fail($"{at}: lista de {count} números");
        var values = new List<double>(count);
        for (int i = 0; i < count; i++)
        {
            if (array[i] is not JsonValue v || !v.TryGetValue(out double d) || !double.IsFinite(d))
                return Result<IReadOnlyList<double>>.Fail($"{at}[{i}]: número inválido");
            if (requirePositive && d <= 0)
                return Result<IReadOnlyList<double>>.Fail($"{at}[{i}]: deve ser maior que 0 (veio {d})");
            values.Add(d);
        }
        return Result<IReadOnlyList<double>>.Ok(values);
    }

    private static bool TryString(JsonNode? node, out string value)
    {
        value = "";
        if (node is JsonValue v && v.TryGetValue(out string? s) && s is not null) { value = s; return true; }
        return false;
    }
}
