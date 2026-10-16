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
                if (key != "default" && !(int.TryParse(key, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int typeId) && typeId.ToString(System.Globalization.CultureInfo.InvariantCulture) == key)) return Fail($"Furnishings.{key}: a chave é 'default' ou o id do tipo de prédio");
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
                int? action = null;
                if (po["action"] is not null)
                {
                    if (po["action"] is not JsonValue av || !av.TryGetValue(out int actionId) || !Enum.IsDefined((LivingWorld.Domain.Behavior.ActionType)actionId))
                        return Result<ObjectDescriptor>.Fail($"{path}.action: id de ActionType inválido");
                    action = actionId;
                }
                string? pose = null;
                if (po["pose"] is not null)
                {
                    if (!TryString(po["pose"], out var poseName) || !ObjectPoses.Known.Contains(poseName))
                        return Result<ObjectDescriptor>.Fail($"{path}.pose: use {string.Join("|", ObjectPoses.Known)}");
                    pose = poseName;
                }
                points.Add(new ObjectUsePoint(name, offset.Value!, facing, action, pose));
            }
        }
        var interactions = ParseInteractions(o["interactions"], at);
        if (!interactions.IsSuccess) return Result<ObjectDescriptor>.Fail(interactions.Error!);
        var effects = ParseEffects(o["effects"], at);
        if (!effects.IsSuccess) return Result<ObjectDescriptor>.Fail(effects.Error!);
        return Result<ObjectDescriptor>.Ok(new ObjectDescriptor(id, category, parts, seed, points, interactions.Value, effects.Value));
    }

    private static Result<IReadOnlyList<ObjectInteraction>?> ParseInteractions(JsonNode? node, string at)
    {
        if (node is null) return Result<IReadOnlyList<ObjectInteraction>?>.Ok(null);
        if (node is not JsonArray array) return Result<IReadOnlyList<ObjectInteraction>?>.Fail($"{at}.interactions: precisa ser uma lista");
        var list = new List<ObjectInteraction>();
        for (int i = 0; i < array.Count; i++)
        {
            var path = $"{at}.interactions[{i}]";
            if (array[i] is not JsonObject o) return Result<IReadOnlyList<ObjectInteraction>?>.Fail($"{path}: objeto inválido");
            if (o["action"] is not JsonValue av || !av.TryGetValue(out int action) || !Enum.IsDefined((LivingWorld.Domain.Behavior.ActionType)action))
                return Result<IReadOnlyList<ObjectInteraction>?>.Fail($"{path}.action: id de ActionType inválido");
            if (list.Any(x => x.Action == action)) return Result<IReadOnlyList<ObjectInteraction>?>.Fail($"{path}.action: ação {action} repetida");
            int? profession = null;
            if (o["profession"] is not null)
            {
                if (o["profession"] is not JsonValue pv || !pv.TryGetValue(out int professionId) || professionId < 0)
                    return Result<IReadOnlyList<ObjectInteraction>?>.Fail($"{path}.profession: id inteiro inválido");
                profession = professionId;
            }
            int? skillId = null;
            double? skillMin = null;
            if (o["skill"] is not null)
            {
                if (o["skill"] is not JsonObject skill || skill["id"] is not JsonValue sv || !sv.TryGetValue(out int id) || id < 0)
                    return Result<IReadOnlyList<ObjectInteraction>?>.Fail($"{path}.skill.id: id inteiro obrigatório");
                if (skill["min"] is not JsonValue mv || !mv.TryGetValue(out double min) || !double.IsFinite(min) || min < 0)
                    return Result<IReadOnlyList<ObjectInteraction>?>.Fail($"{path}.skill.min: número maior ou igual a 0 obrigatório");
                skillId = id;
                skillMin = min;
            }
            list.Add(new ObjectInteraction(action, profession, skillId, skillMin));
        }
        return Result<IReadOnlyList<ObjectInteraction>?>.Ok(list);
    }

    private static Result<IReadOnlyList<ObjectEffect>?> ParseEffects(JsonNode? node, string at)
    {
        if (node is null) return Result<IReadOnlyList<ObjectEffect>?>.Ok(null);
        if (node is not JsonArray array) return Result<IReadOnlyList<ObjectEffect>?>.Fail($"{at}.effects: precisa ser uma lista");
        var list = new List<ObjectEffect>();
        for (int i = 0; i < array.Count; i++)
        {
            var path = $"{at}.effects[{i}]";
            string token = "";
            int cut = -1;
            if (TryString(array[i], out token)) cut = token.IndexOf(':');
            if (cut <= 0)
                return Result<IReadOnlyList<ObjectEffect>?>.Fail($"{path}: use 'alvo:magnitude'");
            var target = token[..cut];
            if (!ObjectEffectTargets.Known.Contains(target))
                return Result<IReadOnlyList<ObjectEffect>?>.Fail($"{path}: alvo '{target}' desconhecido (use {string.Join("|", ObjectEffectTargets.Known)})");
            if (!double.TryParse(token[(cut + 1)..], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var magnitude)
                || !double.IsFinite(magnitude) || magnitude <= -1.0)
                return Result<IReadOnlyList<ObjectEffect>?>.Fail($"{path}: magnitude deve ser um número maior que -1");
            list.Add(new ObjectEffect(target, magnitude));
            if (list.Where(e => e.Target == target).Sum(e => e.Magnitude) <= -1.0)
                return Result<IReadOnlyList<ObjectEffect>?>.Fail($"{path}: a soma dos efeitos '{target}' do objeto deve ser maior que -1 (a duração deixaria de ter fim)");
        }
        return Result<IReadOnlyList<ObjectEffect>?>.Ok(list);
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
