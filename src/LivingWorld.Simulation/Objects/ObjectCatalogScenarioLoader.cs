using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using LivingWorld.Domain.Cities.Buildings;
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
        var extended = HomeLifeLoader.Extend(root); // #255: catálogos padrão embutidos que o cenário estende (o do cenário vence por id)
        if (!extended.IsSuccess) return Fail(extended.Error!);
        if (root["Objects"] is null)
            return root["objectsFormat"] is null && root["Furnishings"] is null
                ? Result<ObjectCatalog?>.Ok(null)
                : Fail("Objects: objectsFormat/Furnishings sem o bloco Objects");
        if (root["objectsFormat"] is not JsonValue fv || !fv.TryGetValue(out int format))
            return Fail("objectsFormat: obrigatório (inteiro) quando há Objects");
        if (format != ObjectCatalog.CurrentFormat)
            return Fail($"objectsFormat: versão {format} desconhecida (suportada: {ObjectCatalog.CurrentFormat})");
        if (root["Objects"] is not JsonArray array) return Fail("Objects: precisa ser uma lista");

        var needs = HomeLifeLoader.ParseNeeds(root["Needs"]);
        if (!needs.IsSuccess) return Fail(needs.Error!);
        var objects = new List<ObjectDescriptor>();
        for (int i = 0; i < array.Count; i++)
        {
            var parsed = ParseObject(array[i], $"Objects[{i}]");
            if (!parsed.IsSuccess) return Fail(parsed.Error!);
            var uses = HomeLifeLoader.ParseUses((array[i] as JsonObject)?["uses"], $"Objects[{i}]", needs.Value);
            if (!uses.IsSuccess) return Fail(uses.Error!);
            if (uses.Value is not null) parsed = Result<ObjectDescriptor>.Ok(parsed.Value! with { Uses = uses.Value });
            var inRooms = HomeLifeLoader.ParseRooms((array[i] as JsonObject)?["rooms"], $"Objects[{i}]");
            if (!inRooms.IsSuccess) return Fail(inRooms.Error!);
            if (inRooms.Value is not null) parsed = Result<ObjectDescriptor>.Ok(parsed.Value! with { Rooms = inRooms.Value });
            if (objects.Any(o => o.Id == parsed.Value!.Id)) return Fail($"Objects[{i}].id: '{parsed.Value!.Id}' duplicado");
            if (parsed.Value!.ResourceId is int resourceId && objects.Any(o => o.ResourceId == resourceId))
                return Fail($"Objects[{i}].resourceId: o recurso {resourceId} já tem descritor");
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
        var rooms = ParseRooms(root["Rooms"]);
        if (!rooms.IsSuccess) return Fail(rooms.Error!);
        var social = SocialLoader.Parse(root["Social"], needs.Value, objects);
        if (!social.IsSuccess) return Fail(social.Error!);
        return Result<ObjectCatalog?>.Ok(new ObjectCatalog(format, objects, furnishings, rooms.Value, needs.Value, social.Value));
    }

    /// <summary>#226: o bloco opcional <c>Rooms</c> ({kinds:[{id,uses:[ActionType],weight,minCells}], plans:{"default"|id[@up]:[ids]}}): os tipos de cômodo e o
    /// plano de cada tipo de prédio. Ausente = sem cômodos (um cômodo só por andar).</summary>
    private static Result<RoomCatalog?> ParseRooms(JsonNode? node)
    {
        if (node is null) return Result<RoomCatalog?>.Ok(null);
        if (node is not JsonObject block || block["kinds"] is not JsonArray kindArray) return Result<RoomCatalog?>.Fail("Rooms.kinds: precisa ser uma lista");
        var kinds = new List<RoomKind>();
        for (int i = 0; i < kindArray.Count; i++)
        {
            var at = $"Rooms.kinds[{i}]";
            if (kindArray[i] is not JsonObject o || !TryString(o["id"], out var id) || id.Length == 0) return Result<RoomCatalog?>.Fail($"{at}.id: texto obrigatório");
            if (kinds.Any(k => k.Id == id)) return Result<RoomCatalog?>.Fail($"{at}.id: '{id}' duplicado");
            var uses = new List<int>();
            if (o["uses"] is not null)
            {
                if (o["uses"] is not JsonArray useArray) return Result<RoomCatalog?>.Fail($"{at}.uses: precisa ser uma lista de ids de ActionType");
                foreach (var use in useArray)
                {
                    if (use is not JsonValue uv || !uv.TryGetValue(out int action) || !Enum.IsDefined((LivingWorld.Domain.Behavior.ActionType)action))
                        return Result<RoomCatalog?>.Fail($"{at}.uses: id de ActionType inválido");
                    uses.Add(action);
                }
            }

            double weight = 1;
            if (o["weight"] is not null && !(o["weight"] is JsonValue wv && wv.TryGetValue(out weight) && double.IsFinite(weight) && weight > 0))
                return Result<RoomCatalog?>.Fail($"{at}.weight: número maior que 0");
            int minCells = 1;
            if (o["minCells"] is not null && !(o["minCells"] is JsonValue mv && mv.TryGetValue(out minCells) && minCells >= 1))
                return Result<RoomCatalog?>.Fail($"{at}.minCells: inteiro maior ou igual a 1");
            kinds.Add(new RoomKind(id, uses, weight, minCells));
        }

        var plans = new Dictionary<string, IReadOnlyList<string>>();
        if (block["plans"] is not null)
        {
            if (block["plans"] is not JsonObject table) return Result<RoomCatalog?>.Fail("Rooms.plans: precisa ser um objeto");
            foreach (var (key, value) in table)
            {
                var bare = key.EndsWith(RoomCatalog.UpperSuffix, StringComparison.Ordinal) ? key[..^RoomCatalog.UpperSuffix.Length] : key;
                if (bare != "default" && !(int.TryParse(bare, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int typeId)
                        && typeId.ToString(System.Globalization.CultureInfo.InvariantCulture) == bare))
                    return Result<RoomCatalog?>.Fail($"Rooms.plans.{key}: a chave é 'default' ou o id do tipo de prédio (com '@up' para os andares de cima)");
                if (value is not JsonArray ids) return Result<RoomCatalog?>.Fail($"Rooms.plans.{key}: precisa ser uma lista de ids");
                var list = new List<string>();
                foreach (var entry in ids)
                {
                    if (entry is not JsonValue v || !v.TryGetValue(out string? kindId) || kinds.All(k => k.Id != kindId))
                        return Result<RoomCatalog?>.Fail($"Rooms.plans.{key}: cômodo '{entry}' não existe em Rooms.kinds");
                    list.Add(kindId);
                }

                plans[key] = list;
            }
        }

        var shapes = new Dictionary<string, BuildingShape>();
        if (block["shapes"] is not null)
        {
            if (block["shapes"] is not JsonObject shapeTable) return Result<RoomCatalog?>.Fail("Rooms.shapes: precisa ser um objeto");
            foreach (var (key, value) in shapeTable)
            {
                if (!(int.TryParse(key, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int typeId)
                        && typeId.ToString(System.Globalization.CultureInfo.InvariantCulture) == key))
                    return Result<RoomCatalog?>.Fail($"Rooms.shapes.{key}: a chave é o id do tipo de prédio");
                if (value is not JsonObject s) return Result<RoomCatalog?>.Fail($"Rooms.shapes.{key}: precisa ser um objeto {{width, depth, floors}}");
                int width = 0, depth = 0, floors = 1;
                if (!(s["width"] is JsonValue wv && wv.TryGetValue(out width)) || !(s["depth"] is JsonValue dv && dv.TryGetValue(out depth)))
                    return Result<RoomCatalog?>.Fail($"Rooms.shapes.{key}: width e depth são inteiros obrigatórios");
                if (s["floors"] is not null && !(s["floors"] is JsonValue fv && fv.TryGetValue(out floors)))
                    return Result<RoomCatalog?>.Fail($"Rooms.shapes.{key}.floors: inteiro inválido");
                var shape = new BuildingShape(width, depth, floors);
                if (!shape.IsValid)
                    return Result<RoomCatalog?>.Fail($"Rooms.shapes.{key}: width e depth em [3,{BuildingShape.MaxSide}] e floors em [1,{BuildingShape.MaxFloors}]");
                shapes[key] = shape;
            }
        }

        return Result<RoomCatalog?>.Ok(new RoomCatalog(kinds, plans, shapes.Count > 0 ? shapes : null));
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
        int? resourceId = null;
        if (o["resourceId"] is not null)
        {
            if (o["resourceId"] is not JsonValue rv || !rv.TryGetValue(out int resource) || resource <= 0)
                return Result<ObjectDescriptor>.Fail($"{at}.resourceId: id de recurso inteiro maior que 0");
            resourceId = resource;
        }

        var effects = ParseEffects(o["effects"], at, isResource: resourceId is not null);
        if (!effects.IsSuccess) return Result<ObjectDescriptor>.Fail(effects.Error!);
        string? display = null;
        if (o["name"] is not null)
        {
            if (!TryString(o["name"], out var named) || named.Trim().Length == 0 || named.Length > 40) return Result<ObjectDescriptor>.Fail($"{at}.name: texto de 1 a 40 caracteres");
            display = named.Trim();
        }
        return Result<ObjectDescriptor>.Ok(new ObjectDescriptor(id, category, parts, seed, points, interactions.Value, effects.Value, resourceId, display));
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

    private static Result<IReadOnlyList<ObjectEffect>?> ParseEffects(JsonNode? node, string at, bool isResource)
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
            var allowed = isResource ? ObjectEffectTargets.ResourceTargets : ObjectEffectTargets.Known;
            if (!allowed.Contains(target))
                return Result<IReadOnlyList<ObjectEffect>?>.Fail($"{path}: alvo '{target}' inválido para {(isResource ? "um objeto de recurso" : "um objeto sem resourceId")} (use {string.Join("|", allowed)})");
            bool parsed = double.TryParse(token[(cut + 1)..], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var magnitude)
                && double.IsFinite(magnitude);
            if (isResource)
            {
                if (!parsed || magnitude < 0 || magnitude > 100)
                    return Result<IReadOnlyList<ObjectEffect>?>.Fail($"{path}: o nível de '{target}' deve ser um número de 0 a 100");
                if (list.Any(e => e.Target == target))
                    return Result<IReadOnlyList<ObjectEffect>?>.Fail($"{path}: alvo '{target}' repetido no mesmo objeto");
                list.Add(new ObjectEffect(target, magnitude));
                continue;
            }

            if (!parsed || magnitude <= -1.0)
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
