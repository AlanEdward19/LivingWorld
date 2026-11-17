using System.Text.Json;
using System.Text.Json.Nodes;

namespace LivingWorld.Tests.Integration.Visual;

/// <summary>AD-172: formas de contrato tiradas de várias amostras. Uma forma é a estrutura sem valores: objeto → chaves ordenadas,
/// lista → forma do elemento, folha → <c>number</c>, <c>string</c>, <c>boolean</c>, <c>null</c> (qualquer valor) ou <c>[]</c> (lista
/// sempre vazia). Chave <c>"campo?"</c> = pode faltar ou vir nula; se vier, cumpre a forma. <see cref="Merge"/> junta amostras (o
/// que falta ou vem nulo em alguma vira opcional) e <see cref="Divergences"/> confere um JSON contra a forma gravada.</summary>
internal static class ContractShapes
{
    /// <summary>Forma de um valor, juntando todos os elementos de cada lista (não só o primeiro).</summary>
    public static JsonNode Of(JsonNode? node) => node switch
    {
        JsonObject obj => Sorted(obj.Select(p => (p.Key, Of(p.Value)))),
        JsonArray { Count: 0 } => JsonValue.Create("[]")!,
        JsonArray array => new JsonArray(array.Select(Of).Aggregate(Merge)),
        JsonValue value => JsonValue.Create(value.GetValueKind() switch
        {
            JsonValueKind.True or JsonValueKind.False => "boolean",
            JsonValueKind.Number => "number",
            JsonValueKind.String => "string",
            _ => "null",
        })!,
        _ => JsonValue.Create("null")!,
    };

    public static JsonNode Merge(JsonNode a, JsonNode b)
    {
        if (IsLeaf(a, "null") || IsLeaf(a, "[]") && b is JsonArray) return b.DeepClone();
        if (IsLeaf(b, "null") || IsLeaf(b, "[]") && a is JsonArray) return a.DeepClone();
        if (a is JsonArray la && b is JsonArray lb) return new JsonArray(Merge(la[0]!, lb[0]!));
        if (a is JsonObject oa && b is JsonObject ob)
        {
            var keys = oa.Select(p => Base(p.Key)).Union(ob.Select(p => Base(p.Key))).Distinct();
            return Sorted(keys.Select(key =>
            {
                var (va, optA) = Field(oa, key);
                var (vb, optB) = Field(ob, key);
                var merged = Merge(va ?? JsonValue.Create("null")!, vb ?? JsonValue.Create("null")!);
                bool optional = optA || optB || va is null || vb is null || IsLeaf(va, "null") || IsLeaf(vb, "null");
                return (optional ? key + "?" : key, merged);
            }));
        }

        if (a is JsonValue && b is JsonValue && a.GetValue<string>() == b.GetValue<string>()) return a.DeepClone();
        throw new InvalidOperationException($"formas incompatíveis: {a.ToJsonString()} x {b.ToJsonString()}");
    }

    /// <summary>Onde <paramref name="value"/> não cumpre <paramref name="shape"/> (vazio = cumpre).</summary>
    public static List<string> Divergences(JsonNode? value, JsonNode shape, string path = "$")
    {
        var errors = new List<string>();
        Check(value, shape, path, errors);
        return errors;
    }

    private static void Check(JsonNode? value, JsonNode shape, string path, List<string> errors)
    {
        if (IsLeaf(shape, "null")) return;
        if (value is null) { errors.Add($"{path}: nulo onde a forma pede {shape.ToJsonString()}"); return; }
        switch (shape)
        {
            case JsonObject so when value is JsonObject vo:
                foreach (var (key, fieldShape) in so)
                {
                    string name = Base(key);
                    bool optional = key.EndsWith('?');
                    var field = vo.TryGetPropertyValue(name, out var v) ? v : null;
                    if (field is null) { if (!optional) errors.Add($"{path}.{name}: obrigatório e ausente/nulo"); continue; }
                    Check(field, fieldShape!, $"{path}.{name}", errors);
                }
                foreach (var (key, _) in vo)
                    if (!so.ContainsKey(key) && !so.ContainsKey(key + "?")) errors.Add($"{path}.{key}: campo fora da forma");
                return;
            case JsonArray sa when value is JsonArray va:
                for (int i = 0; i < va.Count; i++) Check(va[i], sa[0]!, $"{path}[{i}]", errors);
                return;
            case JsonValue when IsLeaf(shape, "[]"):
                if (value is not JsonArray { Count: 0 }) errors.Add($"{path}: a forma só conhece lista vazia");
                return;
            case JsonValue:
                string kind = Of(value) is JsonValue leaf ? leaf.GetValue<string>() : "estrutura";
                if (kind != shape.GetValue<string>()) errors.Add($"{path}: {kind} onde a forma pede {shape.GetValue<string>()}");
                return;
            default:
                errors.Add($"{path}: {Of(value).ToJsonString()} onde a forma pede {shape.ToJsonString()}");
                return;
        }
    }

    private static (JsonNode? Value, bool Optional) Field(JsonObject obj, string key) =>
        obj.TryGetPropertyValue(key, out var v) ? (v, false) : obj.TryGetPropertyValue(key + "?", out var o) ? (o, true) : (null, true);

    private static string Base(string key) => key.EndsWith('?') ? key[..^1] : key;

    private static bool IsLeaf(JsonNode? node, string kind) =>
        node is JsonValue v && v.GetValueKind() == JsonValueKind.String && v.GetValue<string>() == kind;

    private static JsonObject Sorted(IEnumerable<(string Key, JsonNode Value)> fields)
    {
        var result = new JsonObject();
        foreach (var (key, value) in fields.OrderBy(f => f.Key, StringComparer.Ordinal)) result[key] = value.DeepClone();
        return result;
    }
}
