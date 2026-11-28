using System.Globalization;
using System.Text.Json.Nodes;
using LivingWorld.Domain.Objects;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Simulation.Objects;

/// <summary>#255: lê do cenário o que a vida da casa precisa, tudo em dado: o bloco <c>Needs</c> (necessidades do catálogo) e o <c>uses</c> de cada
/// objeto (o que se faz com ele, quanto leva, o que entrega). Também resolve <c>CatalogExtends</c>: catálogos padrão embutidos
/// (<c>scenarios/catalog/*.json</c>) que o cenário estende, sobrescrevendo por id (objetos, mobília por tipo, cômodos).</summary>
public static class HomeLifeLoader
{
    /// <summary>Lê o catálogo embutido (<c>scenarios/catalog/{name}.json</c>); nulo se não existe.</summary>
    public static JsonObject? Embedded(string name)
    {
        var assembly = typeof(HomeLifeLoader).Assembly;
        using var stream = assembly.GetManifestResourceStream($"catalog.{name}.json");
        return stream is null ? null : JsonNode.Parse(stream) as JsonObject;
    }

    /// <summary>Acrescenta ao <paramref name="root"/> os catálogos de <c>CatalogExtends</c> (lista de nomes): o que o cenário já declara vence por id.</summary>
    public static Result<Unit> Extend(JsonObject root)
    {
        if (root["CatalogExtends"] is null) return Result<Unit>.Ok(Unit.Value);
        if (root["CatalogExtends"] is not JsonArray names) return Result<Unit>.Fail("CatalogExtends: precisa ser uma lista de nomes");
        foreach (var node in names)
        {
            if (node is not JsonValue v || !v.TryGetValue(out string? name) || Embedded(name) is not { } catalog)
                return Result<Unit>.Fail($"CatalogExtends: catálogo '{node}' não existe");
            Merge(root, catalog);
        }

        root.Remove("CatalogExtends");
        return Result<Unit>.Ok(Unit.Value);
    }

    /// <summary>Junta o catálogo-base ao cenário: o que o cenário tem vence (objeto de mesmo id, mobília e plano de mesma chave, cômodo de mesmo id,
    /// necessidade de mesmo id); o que só o catálogo tem entra no fim.</summary>
    public static void Merge(JsonObject root, JsonObject catalog)
    {
        if (catalog["objectsFormat"] is { } format && root["objectsFormat"] is null) root["objectsFormat"] = format.DeepClone();
        MergeById(root, catalog, "Objects");
        MergeKeys(root, catalog, "Furnishings");
        if (catalog["Rooms"] is JsonObject baseRooms)
        {
            if (root["Rooms"] is not JsonObject rooms) root["Rooms"] = baseRooms.DeepClone();
            else
            {
                MergeById(rooms, baseRooms, "kinds");
                MergeKeys(rooms, baseRooms, "plans");
                MergeKeys(rooms, baseRooms, "shapes");
            }
        }

        SocialLoader.Merge(root, catalog); // #259
        if (catalog["NeedStartJitter"] is { } jitter && root["NeedStartJitter"] is null) root["NeedStartJitter"] = jitter.DeepClone(); // #261
        MoodLoader.Merge(root, catalog); // #262
        HomeLoader.Merge(root, catalog); // #264
        if (catalog["Needs"] is JsonArray baseNeeds)
        {
            if (root["Needs"] is not JsonArray own) root["Needs"] = baseNeeds.DeepClone();
            else
                foreach (var need in baseNeeds)
                    if (need is JsonObject n && own.All(o => o?["id"]?.ToString() != n["id"]?.ToString())) own.Add(n.DeepClone());
        }
    }

    private static void MergeById(JsonObject root, JsonObject catalog, string key)
    {
        if (catalog[key] is not JsonArray baseList) return;
        if (root[key] is not JsonArray list) { root[key] = baseList.DeepClone(); return; }
        var have = list.Select(o => o?["id"]?.ToString()).ToHashSet();
        foreach (var item in baseList)
            if (item is JsonObject o && have.Add(o["id"]?.ToString())) list.Add(o.DeepClone());
    }

    private static void MergeKeys(JsonObject root, JsonObject catalog, string key)
    {
        if (catalog[key] is not JsonObject baseTable) return;
        if (root[key] is not JsonObject table) { root[key] = baseTable.DeepClone(); return; }
        foreach (var (k, v) in baseTable)
            if (!table.ContainsKey(k)) table[k] = v?.DeepClone();
    }

    /// <summary>Os traços de personalidade que a variação por pessoa aceita (os mesmos nomes do catálogo social).</summary>
    public static readonly string[] Traits = ["extroversion", "agreeableness", "conscientiousness", "emotionalstability", "openness", "ambition", "loyalty", "altruism", "impulsivity", "riskaversion"];

    /// <summary>O bloco <c>Needs</c>: <c>[{id, name, start, decayPerHour, urgentBelow, color?, effects?:[{below, target, perHour}]}]</c>. Ausente = nulo.</summary>
    public static Result<NeedCatalog?> ParseNeeds(JsonNode? node, JsonNode? jitterNode = null)
    {
        if (node is null) return Result<NeedCatalog?>.Ok(null);
        if (node is not JsonArray array) return Result<NeedCatalog?>.Fail("Needs: precisa ser uma lista");
        var needs = new List<NeedDef>();
        for (int i = 0; i < array.Count; i++)
        {
            var at = $"Needs[{i}]";
            if (array[i] is not JsonObject o || !Text(o["id"], out var id) || id.Length == 0) return Result<NeedCatalog?>.Fail($"{at}.id: texto obrigatório");
            if (NeedIds.IsBuiltin(id) || needs.Any(n => n.Id == id)) return Result<NeedCatalog?>.Fail($"{at}.id: '{id}' já existe (as de sempre não se redeclaram)");
            if (!Text(o["name"], out var name) || name.Length == 0 || name.Length > 24) return Result<NeedCatalog?>.Fail($"{at}.name: texto de 1 a 24 caracteres");
            if (!Number(o["start"], out var start) || start is < 0 or > 100) return Result<NeedCatalog?>.Fail($"{at}.start: número de 0 a 100");
            if (!Number(o["decayPerHour"], out var decay) || decay < 0) return Result<NeedCatalog?>.Fail($"{at}.decayPerHour: número maior ou igual a 0");
            if (!Number(o["urgentBelow"], out var urgent) || urgent is < 0 or > 100) return Result<NeedCatalog?>.Fail($"{at}.urgentBelow: número de 0 a 100");
            string? color = null;
            if (o["color"] is not null && (!Text(o["color"], out var c) || !System.Text.RegularExpressions.Regex.IsMatch(c, "^[0-9a-fA-F]{6}$")))
                return Result<NeedCatalog?>.Fail($"{at}.color: 6 dígitos hexadecimais");
            else if (o["color"] is not null) color = o["color"]!.ToString().ToLowerInvariant();
            var effects = new List<NeedEffect>();
            if (o["effects"] is not null)
            {
                if (o["effects"] is not JsonArray list) return Result<NeedCatalog?>.Fail($"{at}.effects: precisa ser uma lista");
                for (int e = 0; e < list.Count; e++)
                {
                    var path = $"{at}.effects[{e}]";
                    if (list[e] is not JsonObject eo || !Number(eo["below"], out var below) || below is < 0 or > 100) return Result<NeedCatalog?>.Fail($"{path}.below: número de 0 a 100");
                    if (!Text(eo["target"], out var target) || !NeedEffectTargets.Known.Contains(target)) return Result<NeedCatalog?>.Fail($"{path}.target: use {string.Join("|", NeedEffectTargets.Known)}");
                    if (!Number(eo["perHour"], out var per)) return Result<NeedCatalog?>.Fail($"{path}.perHour: número");
                    effects.Add(new NeedEffect(below, target, per));
                }
            }

            string? alert = null;
            if (o["alert"] is not null && (!Text(o["alert"], out alert) || alert.Length > 60)) return Result<NeedCatalog?>.Fail($"{at}.alert: texto de até 60 caracteres");
            string? icon = null;
            if (o["icon"] is not null && (!Text(o["icon"], out icon) || icon.Length > 24)) return Result<NeedCatalog?>.Fail($"{at}.icon: nome de até 24 caracteres");
            NeedVariation? variation = null;
            if (o["variation"] is JsonObject vo)
            {
                if (!Number(vo["jitter"] ?? 0, out var jitter) || jitter is < 0 or > 50) return Result<NeedCatalog?>.Fail($"{at}.variation.jitter: número de 0 a 50");
                Dictionary<string, double>? traits = null;
                if (vo["traits"] is JsonObject to)
                {
                    traits = [];
                    foreach (var (name2, value) in to)
                    {
                        if (!Number(value, out var weight) || weight is < -2 or > 2) return Result<NeedCatalog?>.Fail($"{at}.variation.traits.{name2}: número de -2 a 2");
                        if (!Traits.Contains(name2)) return Result<NeedCatalog?>.Fail($"{at}.variation.traits.{name2}: traço desconhecido ({string.Join("|", Traits)})");
                        traits[name2] = weight;
                    }
                }

                variation = new NeedVariation(jitter, traits);
            }

            double? seek = null;
            if (o["seekBelow"] is not null)
            {
                if (!Number(o["seekBelow"], out var sb) || sb < urgent || sb > 100) return Result<NeedCatalog?>.Fail($"{at}.seekBelow: número de urgentBelow a 100");
                seek = sb;
            }

            needs.Add(new NeedDef(id, name, start, decay, urgent, color, effects.Count > 0 ? effects : null, alert, icon, variation, seek));
        }

        Dictionary<string, double>? startJitter = null;
        if (jitterNode is JsonObject jo)
        {
            startJitter = [];
            foreach (var (key, value) in jo)
            {
                if (!NeedIds.IsBuiltin(key) || !Number(value, out var points) || points is < 0 or > 50)
                    return Result<NeedCatalog?>.Fail($"NeedStartJitter.{key}: hunger|thirst|sleep|social com 0 a 50 pontos");
                startJitter[key] = points;
            }
        }

        return Result<NeedCatalog?>.Ok(needs.Count > 0 ? new NeedCatalog(needs, startJitter) : null);
    }

    /// <summary>#258: os <c>toggle</c> que o motor entende (liga e desliga a luz do objeto).</summary>
    public static readonly IReadOnlyList<string> ToggleKinds = [ObjectToggles.LightOn, ObjectToggles.LightOff];

    /// <summary>O <c>uses</c> de um objeto: <c>[{id, name, durationMin, effects:[{need, perHour}], consumes?:[{resource, quantity}], pose?, poseFallback?,
    /// slot?:[x,z], facing?, action?, untilFull?, occupies?, hidden?}]</c>. <paramref name="needs"/> valida os ids das necessidades citadas.</summary>
    public static Result<IReadOnlyList<ObjectUseDef>?> ParseUses(JsonNode? node, string at, NeedCatalog? needs)
    {
        if (node is null) return Result<IReadOnlyList<ObjectUseDef>?>.Ok(null);
        if (node is not JsonArray array) return Result<IReadOnlyList<ObjectUseDef>?>.Fail($"{at}.uses: precisa ser uma lista");
        var uses = new List<ObjectUseDef>();
        for (int i = 0; i < array.Count; i++)
        {
            var path = $"{at}.uses[{i}]";
            if (array[i] is not JsonObject o || !Text(o["id"], out var id) || id.Length == 0) return Fail($"{path}.id: texto obrigatório");
            if (uses.Any(u => u.Id == id)) return Fail($"{path}.id: '{id}' duplicado");
            if (!Text(o["name"], out var name) || name.Length == 0 || name.Length > 40) return Fail($"{path}.name: texto de 1 a 40 caracteres");
            if (!Number(o["durationMin"], out var minutes) || minutes is < 1 or > 1440) return Fail($"{path}.durationMin: minutos de mundo, de 1 a 1440");
            var effects = new List<UseEffect>();
            if (o["effects"] is not null)
            {
                if (o["effects"] is not JsonArray list) return Fail($"{path}.effects: precisa ser uma lista");
                for (int e = 0; e < list.Count; e++)
                {
                    if (list[e] is not JsonObject eo || !Text(eo["need"], out var need) || !(NeedIds.IsBuiltin(need) || needs?.Find(need) is not null))
                        return Fail($"{path}.effects[{e}].need: necessidade desconhecida (as de sempre ou as de Needs)");
                    if (!Number(eo["perHour"], out var per)) return Fail($"{path}.effects[{e}].perHour: número");
                    effects.Add(new UseEffect(need, per));
                }
            }

            List<UseConsume>? consumes = null;
            if (o["consumes"] is not null)
            {
                if (o["consumes"] is not JsonArray list) return Fail($"{path}.consumes: precisa ser uma lista");
                consumes = [];
                for (int c = 0; c < list.Count; c++)
                {
                    if (list[c] is not JsonObject co || !Number(co["resource"], out var resource) || resource < 1 || !Number(co["quantity"], out var quantity) || quantity < 1)
                        return Fail($"{path}.consumes[{c}]: {{resource, quantity}} inteiros maiores que 0");
                    consumes.Add(new UseConsume((int)resource, (long)quantity));
                }
            }

            string? pose = null, fallback = null, untilFull = null;
            if (o["pose"] is not null && !Text(o["pose"], out pose)) return Fail($"{path}.pose: texto");
            if (o["poseFallback"] is not null && (!Text(o["poseFallback"], out fallback) || !ObjectPoses.Known.Contains(fallback))) return Fail($"{path}.poseFallback: use {string.Join("|", ObjectPoses.Known)}");
            if (pose is not null && !ObjectPoses.Known.Contains(pose) && fallback is null) return Fail($"{path}.pose: '{pose}' não é uma pose conhecida; declare poseFallback");
            if (o["untilFull"] is not null && (!Text(o["untilFull"], out untilFull) || !(NeedIds.IsBuiltin(untilFull) || needs?.Find(untilFull) is not null))) return Fail($"{path}.untilFull: necessidade desconhecida");
            List<double>? slot = null;
            if (o["slot"] is not null)
            {
                if (o["slot"] is not JsonArray s || s.Count != 2 || !Number(s[0], out var sx) || !Number(s[1], out var sz)) return Fail($"{path}.slot: [x, z] em metros");
                slot = [sx, sz];
            }

            double facing = 0;
            if (o["facing"] is not null && !Number(o["facing"], out facing)) return Fail($"{path}.facing: graus");
            int? action = null;
            if (o["action"] is not null)
            {
                if (!Number(o["action"], out var a) || !Enum.IsDefined((LivingWorld.Domain.Behavior.ActionType)(int)a)) return Fail($"{path}.action: id de ActionType inválido");
                action = (int)a;
            }

            bool occupies = true;
            if (o["occupies"] is not null && !(o["occupies"] is JsonValue ov && ov.TryGetValue(out occupies))) return Fail($"{path}.occupies: verdadeiro ou falso");
            List<UseConsume>? produces = null;
            if (o["produces"] is not null)
            {
                if (o["produces"] is not JsonArray plist) return Fail($"{path}.produces: precisa ser uma lista");
                produces = [];
                for (int c = 0; c < plist.Count; c++)
                {
                    if (plist[c] is not JsonObject po || !Number(po["resource"], out var presource) || presource < 1 || !Number(po["quantity"], out var pquantity) || pquantity < 1)
                        return Fail($"{path}.produces[{c}]: {{resource, quantity}} inteiros maiores que 0");
                    produces.Add(new UseConsume((int)presource, (long)pquantity));
                }
            }

            string? toggle = null;
            if (o["toggle"] is not null && (!Text(o["toggle"], out toggle) || !ToggleKinds.Contains(toggle))) return Fail($"{path}.toggle: use {string.Join("|", ToggleKinds)}");
            if (effects.Count == 0 && action is null && consumes is null && produces is null && toggle is null) return Fail($"{path}: sem effects, consumes, produces, toggle nem action, o uso não faz nada");
            bool hidden = o["hidden"] is JsonValue hv && hv.TryGetValue(out bool h) && h;
            uses.Add(new ObjectUseDef(id, name, (int)minutes, effects, consumes, pose, slot, facing, action, untilFull, occupies, fallback, hidden, produces, toggle));
        }

        return Result<IReadOnlyList<ObjectUseDef>?>.Ok(uses.Count > 0 ? uses : null);
    }

    /// <summary>O <c>rooms</c> de um objeto: os tipos de cômodo onde a peça fica (o chuveiro no banheiro). Ausente = pelos usos de sempre (<c>usePoints</c>).</summary>
    public static Result<IReadOnlyList<string>?> ParseRooms(JsonNode? node, string at)
    {
        if (node is null) return Result<IReadOnlyList<string>?>.Ok(null);
        if (node is not JsonArray array) return Result<IReadOnlyList<string>?>.Fail($"{at}.rooms: precisa ser uma lista de tipos de cômodo");
        var list = new List<string>();
        foreach (var item in array)
        {
            if (!Text(item, out var kind) || kind.Length == 0) return Result<IReadOnlyList<string>?>.Fail($"{at}.rooms: tipo de cômodo inválido");
            list.Add(kind);
        }

        return Result<IReadOnlyList<string>?>.Ok(list.Count > 0 ? list : null);
    }

    /// <summary>#260: o <c>light</c> de um objeto: <c>{color, rangeM, energy, heightM}</c>; ausente = o objeto não ilumina.</summary>
    public static Result<ObjectLight?> ParseLight(JsonNode? node, string at)
    {
        if (node is null) return Result<ObjectLight?>.Ok(null);
        if (node is not JsonObject o) return Result<ObjectLight?>.Fail($"{at}.light: precisa ser um objeto");
        if (!Text(o["color"], out var color) || color.Length != 6 || !color.All(Uri.IsHexDigit)) return Result<ObjectLight?>.Fail($"{at}.light.color: seis dígitos hexadecimais");
        if (!Number(o["rangeM"], out var range) || range is <= 0 or > 20) return Result<ObjectLight?>.Fail($"{at}.light.rangeM: metros de 0 a 20");
        if (!Number(o["energy"], out var energy) || energy is <= 0 or > 10) return Result<ObjectLight?>.Fail($"{at}.light.energy: de 0 a 10");
        if (!Number(o["heightM"], out var height) || height is < 0 or > 4) return Result<ObjectLight?>.Fail($"{at}.light.heightM: metros de 0 a 4");
        return Result<ObjectLight?>.Ok(new ObjectLight(color, range, energy, height));
    }

    private static Result<IReadOnlyList<ObjectUseDef>?> Fail(string error) => Result<IReadOnlyList<ObjectUseDef>?>.Fail(error);

    private static bool Text(JsonNode? node, out string value)
    {
        value = "";
        if (node is JsonValue v && v.TryGetValue(out string? s) && s is not null) { value = s; return true; }
        return false;
    }

    private static bool Number(JsonNode? node, out double value)
    {
        value = 0;
        if (node is not JsonValue v) return false;
        if (v.TryGetValue(out double d)) value = d;
        else if (v.TryGetValue(out int n)) value = n;
        else if (v.TryGetValue(out string? s) && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)) value = parsed;
        else return false;
        return double.IsFinite(value);
    }
}
