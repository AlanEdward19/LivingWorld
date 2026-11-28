using System.Globalization;
using System.Text.Json.Nodes;
using LivingWorld.Domain.Objects;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Simulation.Objects;

/// <summary>#259: lê do cenário o bloco <c>Social</c> (interações sociais como dado; ver <see cref="SocialCatalog"/>) e o junta ao catálogo padrão
/// <c>scenarios/catalog/social.json</c> que o cenário estende (<c>CatalogExtends</c>). O código não conhece nenhuma interação: id, texto, números e regras são do dado.</summary>
public static class SocialLoader
{
    private static readonly string[] MoodKeys = ["grief", "fear", "anger", "joy", "shame"];
    private static readonly string[] Traits = ["extroversion", "agreeableness", "conscientiousness", "emotionalstability", "openness", "ambition", "loyalty", "altruism", "impulsivity", "riskaversion"];

    /// <summary>Junta o bloco do catálogo-base ao do cenário: categorias, estados e interações de mesmo id ficam como o cenário os tem; o resto entra no fim.</summary>
    public static void Merge(JsonObject root, JsonObject catalog)
    {
        if (catalog["Social"] is not JsonObject baseSocial) return;
        if (root["Social"] is not JsonObject own) { root["Social"] = baseSocial.DeepClone(); return; }
        foreach (var key in new[] { "rules", "mood" })
            if (own[key] is null && baseSocial[key] is { } value) own[key] = value.DeepClone();
        foreach (var key in new[] { "categories", "states", "interactions" })
        {
            if (baseSocial[key] is not JsonArray baseList) continue;
            if (own[key] is not JsonArray list) { own[key] = baseList.DeepClone(); continue; }
            var have = list.Select(o => o?["id"]?.ToString()).ToHashSet();
            foreach (var item in baseList)
                if (item is JsonObject o && have.Add(o["id"]?.ToString())) list.Add(o.DeepClone());
        }
    }

    /// <summary>O bloco <c>Social</c>; ausente = nulo. <paramref name="needs"/> valida os ids de necessidade citados (as de sempre ou as do catálogo).</summary>
    public static Result<SocialCatalog?> Parse(JsonNode? node, NeedCatalog? needs, IReadOnlyList<ObjectDescriptor> objects)
    {
        if (node is null) return Result<SocialCatalog?>.Ok(null);
        if (node is not JsonObject block) return Fail("Social: precisa ser um objeto");
        bool KnownNeed(string id) => NeedIds.IsBuiltin(id) || needs?.Find(id) is not null;

        var rules = ParseRules(block["rules"], KnownNeed);
        if (!rules.IsSuccess) return Fail(rules.Error!);
        var categories = new List<SocialCategory>();
        if (block["categories"] is JsonArray cats)
            for (int i = 0; i < cats.Count; i++)
            {
                if (cats[i] is not JsonObject o || !Text(o["id"], out var id) || id.Length == 0 || !Text(o["name"], out var name) || name.Length is 0 or > 24)
                    return Fail($"Social.categories[{i}]: {{id, name (1 a 24 caracteres)}}");
                if (categories.Any(c => c.Id == id)) return Fail($"Social.categories[{i}].id: '{id}' duplicado");
                categories.Add(new SocialCategory(id, name));
            }

        var states = new List<RelationStateDef>();
        if (block["states"] is JsonArray st)
            for (int i = 0; i < st.Count; i++)
            {
                var at = $"Social.states[{i}]";
                if (st[i] is not JsonObject o || !Text(o["id"], out var id) || id.Length == 0 || !Text(o["label"], out var label) || label.Length is 0 or > 24)
                    return Fail($"{at}: {{id, label (1 a 24 caracteres)}}");
                if (states.Any(s => s.Id == id)) return Fail($"{at}.id: '{id}' duplicado");
                double aMin = Opt(o["affectionMin"], 0), tMin = Opt(o["trustMin"], 0);
                double? aMax = o["affectionMax"] is null ? null : Opt(o["affectionMax"], 100);
                states.Add(new RelationStateDef(id, label, Flag(o["spouse"]), aMin, tMin, aMax));
            }

        var mood = ParseMood(block["mood"], KnownNeed);
        if (!mood.IsSuccess) return Fail(mood.Error!);
        var interactions = new List<SocialInteractionDef>();
        if (block["interactions"] is JsonArray list)
            for (int i = 0; i < list.Count; i++)
            {
                var parsed = ParseInteraction(list[i], $"Social.interactions[{i}]", categories, states, KnownNeed, objects);
                if (!parsed.IsSuccess) return Fail(parsed.Error!);
                if (interactions.Any(x => x.Id == parsed.Value!.Id)) return Fail($"Social.interactions[{i}].id: '{parsed.Value!.Id}' duplicado");
                interactions.Add(parsed.Value!);
            }

        return Result<SocialCatalog?>.Ok(interactions.Count == 0 ? null : new SocialCatalog(rules.Value!, categories, interactions, states, mood.Value!));
    }

    private static Result<SocialRules> ParseRules(JsonNode? node, Func<string, bool> knownNeed)
    {
        var o = node as JsonObject ?? new JsonObject();
        string need = o["initiateNeed"]?.ToString() ?? "social";
        if (!knownNeed(need)) return Result<SocialRules>.Fail("Social.rules.initiateNeed: necessidade desconhecida");
        var texts = new Dictionary<string, string>();
        if (o["texts"] is JsonObject t)
            foreach (var (key, value) in t)
                if (value is JsonValue v && v.TryGetValue(out string? text) && text is not null) texts[key] = text;
        return Result<SocialRules>.Ok(new SocialRules((int)Opt(o["adultYears"], 18), Opt(o["reachM"], 1.4), (int)Opt(o["cooldownMin"], 20), need,
            Opt(o["initiateBelow"], 45), Math.Max(5, (int)Opt(o["checkEveryMin"], 30)), Math.Max(1, (int)Opt(o["historyHours"], 6)), texts, Opt(o["spouseAffection"], 0), Opt(o["spouseTrust"], 0), Math.Clamp(Opt(o["approachM"], 8), 1, 30)));
    }

    private static Result<MoodDef> ParseMood(JsonNode? node, Func<string, bool> knownNeed)
    {
        if (node is null) return Result<MoodDef>.Ok(new MoodDef(40, [], [new MoodLabel(101, "Normal")]));
        if (node is not JsonObject o) return Result<MoodDef>.Fail("Social.mood: precisa ser um objeto");
        var needs = new List<MoodNeedTerm>();
        if (o["needs"] is JsonArray list)
            foreach (var item in list)
            {
                if (item is not JsonObject n || !Text(n["need"], out var id) || !knownNeed(id) || !Number(n["weight"], out var w))
                    return Result<MoodDef>.Fail("Social.mood.needs: {need (conhecida), weight}");
                needs.Add(new MoodNeedTerm(id, w));
            }

        var labels = new List<MoodLabel>();
        if (o["labels"] is JsonArray ls)
            foreach (var item in ls)
            {
                if (item is not JsonObject l || !Number(l["below"], out var below) || !Text(l["label"], out var label) || label.Length is 0 or > 24)
                    return Result<MoodDef>.Fail("Social.mood.labels: {below, label (1 a 24 caracteres)}");
                labels.Add(new MoodLabel(below, label));
            }

        if (labels.Count == 0) labels.Add(new MoodLabel(101, "Normal"));
        return Result<MoodDef>.Ok(new MoodDef(Opt(o["valenceWeight"], 40), needs, labels.OrderBy(l => l.Below).ToList()));
    }

    private static Result<SocialInteractionDef> ParseInteraction(JsonNode? node, string at, List<SocialCategory> categories, List<RelationStateDef> states,
        Func<string, bool> knownNeed, IReadOnlyList<ObjectDescriptor> objects)
    {
        if (node is not JsonObject o || !Text(o["id"], out var id) || id.Length == 0 || id.Contains(':') || id.Contains('|')) return Result<SocialInteractionDef>.Fail($"{at}.id: texto obrigatório (sem ':' nem '|')");
        if (!Text(o["name"], out var name) || name.Length is 0 or > 40) return Result<SocialInteractionDef>.Fail($"{at}.name: texto de 1 a 40 caracteres");
        if (!Text(o["category"], out var category) || categories.All(c => c.Id != category)) return Result<SocialInteractionDef>.Fail($"{at}.category: use uma de Social.categories");
        SocialJoint? joint = null;
        if (o["joint"] is JsonObject j)
        {
            if (!Text(j["use"], out var use) || !Text(j["partnerUse"], out var partnerUse)) return Result<SocialInteractionDef>.Fail($"{at}.joint: {{use, partnerUse}}");
            foreach (var u in new[] { use, partnerUse })
                if (!objects.Any(d => d.Uses?.Any(x => x.Id == u) == true)) return Result<SocialInteractionDef>.Fail($"{at}.joint: nenhum objeto tem o uso '{u}'");
            joint = new SocialJoint(use, partnerUse);
        }
        else if (o["joint"] is not null) return Result<SocialInteractionDef>.Fail($"{at}.joint: precisa ser um objeto");

        double dur = Opt(o["durationSec"], 10);
        if (joint is null && dur is < 1 or > 3600) return Result<SocialInteractionDef>.Fail($"{at}.durationSec: de 1 a 3600 segundos de mundo");
        var requires = ParseRequire(o["requires"], at, states);
        if (!requires.IsSuccess) return Result<SocialInteractionDef>.Fail(requires.Error!);
        var accept = ParseAccept(o["accept"], at, categories, knownNeed);
        if (!accept.IsSuccess) return Result<SocialInteractionDef>.Fail(accept.Error!);
        var effects = ParseEffects(o["effects"], $"{at}.effects", knownNeed);
        if (!effects.IsSuccess) return Result<SocialInteractionDef>.Fail(effects.Error!);
        SocialEffects? refuse = null;
        if (o["onRefuse"] is not null)
        {
            var parsed = ParseEffects(o["onRefuse"], $"{at}.onRefuse", knownNeed);
            if (!parsed.IsSuccess) return Result<SocialInteractionDef>.Fail(parsed.Error!);
            refuse = parsed.Value;
        }

        SocialInitiative? initiative = null;
        if (o["initiative"] is JsonObject init)
        {
            string? need = init["need"]?.ToString();
            if (need is not null && !knownNeed(need)) return Result<SocialInteractionDef>.Fail($"{at}.initiative.need: necessidade desconhecida");
            initiative = new SocialInitiative(Opt(init["weight"], 1), init["toPlayer"] is null || Flag(init["toPlayer"]), need,
                init["below"] is null ? null : Opt(init["below"], 45), Opt(init["affectionMin"], 0));
        }

        string? pose = o["pose"]?.ToString(), targetPose = o["targetPose"]?.ToString();
        if (pose is { Length: > 24 } || targetPose is { Length: > 24 }) return Result<SocialInteractionDef>.Fail($"{at}.pose: nome de até 24 caracteres");
        return Result<SocialInteractionDef>.Ok(new SocialInteractionDef(id, name, category, (int)dur, pose, targetPose ?? pose, requires.Value!, accept.Value!, effects.Value!,
            refuse, joint, initiative, o["accepted"]?.ToString(), o["refused"]?.ToString(), o["compatibleWithUse"] is not null && Flag(o["compatibleWithUse"])));
    }

    private static Result<SocialRequire> ParseRequire(JsonNode? node, string at, List<RelationStateDef> states)
    {
        if (node is null) return Result<SocialRequire>.Ok(new SocialRequire());
        if (node is not JsonObject o) return Result<SocialRequire>.Fail($"{at}.requires: precisa ser um objeto");
        List<string>? list = null;
        if (o["states"] is JsonArray arr)
        {
            list = [];
            foreach (var item in arr)
            {
                string s = item?.ToString() ?? "";
                if (states.All(x => x.Id != s)) return Result<SocialRequire>.Fail($"{at}.requires.states: '{s}' não está em Social.states");
                list.Add(s);
            }
        }

        return Result<SocialRequire>.Ok(new SocialRequire(Opt(o["affectionMin"], 0), Opt(o["trustMin"], 0), list, Flag(o["adultsOnly"]), o["reason"]?.ToString()));
    }

    private static Result<SocialAccept> ParseAccept(JsonNode? node, string at, List<SocialCategory> categories, Func<string, bool> knownNeed)
    {
        if (node is null) return Result<SocialAccept>.Ok(new SocialAccept(100, []));
        if (node is not JsonObject o) return Result<SocialAccept>.Fail($"{at}.accept: precisa ser um objeto");
        var terms = new List<SocialTerm>();
        if (o["terms"] is JsonArray arr)
            for (int i = 0; i < arr.Count; i++)
            {
                if (arr[i] is not JsonObject t || !Text(t["of"], out var of) || !Number(t["weight"], out var weight))
                    return Result<SocialAccept>.Fail($"{at}.accept.terms[{i}]: {{of, weight, from?, reason?}}");
                if (!ValidSource(of, categories, knownNeed)) return Result<SocialAccept>.Fail($"{at}.accept.terms[{i}].of: '{of}' desconhecido");
                terms.Add(new SocialTerm(of, weight, Opt(t["from"], 50), t["reason"]?.ToString()));
            }

        return Result<SocialAccept>.Ok(new SocialAccept(Opt(o["base"], 50), terms, o["refuseReason"]?.ToString()));
    }

    private static bool ValidSource(string of, List<SocialCategory> categories, Func<string, bool> knownNeed)
    {
        var parts = of.Split('.');
        if (parts.Length == 2 && parts[0] == "relation") return parts[1] is "affection" or "trust" or "respect";
        if (parts.Length == 2 && parts[1] == "mood") return parts[0] is "actor" or "target";
        if (parts.Length == 3 && parts[0] is "actor" or "target" && parts[1] == "need") return knownNeed(parts[2]);
        if (parts.Length == 3 && parts[0] == "target" && parts[1] == "trait") return Traits.Contains(parts[2]);
        if (parts.Length == 2 && parts[0] == "recent") return categories.Any(c => c.Id == parts[1]);
        return false;
    }

    private static Result<SocialEffects> ParseEffects(JsonNode? node, string at, Func<string, bool> knownNeed)
    {
        if (node is null) return Result<SocialEffects>.Ok(new SocialEffects(new SocialSide(), new SocialSide()));
        if (node is not JsonObject o) return Result<SocialEffects>.Fail($"{at}: precisa ser um objeto");
        var actor = ParseSide(o["actor"], $"{at}.actor", knownNeed);
        if (!actor.IsSuccess) return Result<SocialEffects>.Fail(actor.Error!);
        var target = ParseSide(o["target"], $"{at}.target", knownNeed);
        if (!target.IsSuccess) return Result<SocialEffects>.Fail(target.Error!);
        return Result<SocialEffects>.Ok(new SocialEffects(actor.Value!, target.Value!));
    }

    private static Result<SocialSide> ParseSide(JsonNode? node, string at, Func<string, bool> knownNeed)
    {
        if (node is null) return Result<SocialSide>.Ok(new SocialSide());
        if (node is not JsonObject o) return Result<SocialSide>.Fail($"{at}: precisa ser um objeto");
        var needs = new List<SocialNeedShift>();
        if (o["needs"] is JsonObject shifts)
            foreach (var (need, amount) in shifts)
            {
                if (!knownNeed(need) || !Number(amount, out var value)) return Result<SocialSide>.Fail($"{at}.needs.{need}: necessidade desconhecida ou valor inválido");
                needs.Add(new SocialNeedShift(need, value));
            }

        Dictionary<string, double>? mood = null;
        if (o["mood"] is JsonObject m)
        {
            mood = [];
            foreach (var (key, value) in m)
            {
                if (!MoodKeys.Contains(key) || !Number(value, out var v) || v is < 0 or > 1) return Result<SocialSide>.Fail($"{at}.mood.{key}: use {string.Join("|", MoodKeys)} de 0 a 1");
                mood[key] = v;
            }
        }

        return Result<SocialSide>.Ok(new SocialSide(Opt(o["affection"], 0), Opt(o["trust"], 0), Opt(o["respect"], 0), needs.Count > 0 ? needs : null, mood, (int)Opt(o["salience"], 0)));
    }

    private static Result<SocialCatalog?> Fail(string error) => Result<SocialCatalog?>.Fail(error);

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

    private static double Opt(JsonNode? node, double fallback) => node is not null && Number(node, out var v) ? v : fallback;

    private static bool Flag(JsonNode? node) => node is JsonValue v && v.TryGetValue(out bool b) && b;
}
