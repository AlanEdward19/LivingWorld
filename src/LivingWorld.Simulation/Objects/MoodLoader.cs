using System.Text.Json;
using System.Text.Json.Nodes;
using LivingWorld.Domain.Objects;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Simulation.Objects;

/// <summary>#262: lê do cenário o bloco <c>Moods</c> (momentos, emoções, aspirações e desejos como dado; ver <see cref="MoodCatalog"/>) e o junta ao catálogo padrão
/// <c>scenarios/catalog/moods.json</c> que o cenário estende. O código não conhece nenhum momento: id, texto, números e gatilhos são do dado.</summary>
public static class MoodLoader
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { ReadCommentHandling = JsonCommentHandling.Skip };
    private static readonly string[] TriggerKinds = ["use", "action", "social", "life", "state", "reward"];
    private static readonly string[] FulfillKinds = ["use", "action", "social", "state"];
    private static readonly string[] Faces = ["grief", "fear", "anger", "joy", "shame", "loneliness"];

    /// <summary>Junta o bloco do catálogo-base ao do cenário: emoção, momento, aspiração e desejo de mesmo id ficam como o cenário os tem; o resto entra no fim.</summary>
    public static void Merge(JsonObject root, JsonObject catalog)
    {
        if (catalog["Moods"] is not JsonObject baseMoods) return;
        if (root["Moods"] is not JsonObject own) { root["Moods"] = baseMoods.DeepClone(); return; }
        if (own["rules"] is null && baseMoods["rules"] is { } rules) own["rules"] = rules.DeepClone();
        foreach (var key in new[] { "emotions", "moments", "aspirations", "wishes" })
        {
            if (baseMoods[key] is not JsonArray baseList) continue;
            if (own[key] is not JsonArray list) { own[key] = baseList.DeepClone(); continue; }
            var have = list.Select(o => o?["id"]?.ToString()).ToHashSet();
            foreach (var item in baseList)
                if (item is JsonObject o && have.Add(o["id"]?.ToString())) list.Add(o.DeepClone());
        }
    }

    /// <summary>O bloco <c>Moods</c>; ausente = nulo. Valida ids duplicados, emoções, momentos, aspirações e o vocabulário do motor.</summary>
    public static Result<MoodCatalog?> Parse(JsonNode? node)
    {
        if (node is null) return Result<MoodCatalog?>.Ok(null);
        MoodCatalog? catalog;
        try { catalog = node.Deserialize<MoodCatalog>(Options); }
        catch (JsonException e) { return Fail($"Moods: {e.Message}"); }
        if (catalog is null) return Fail("Moods: precisa ser um objeto");
        // uso, interação, categoria e necessidade citados que o cenário não tem ficam inertes (um cenário que troca o chuveiro não perde o catálogo inteiro);
        // o vocabulário do motor (ação, fato da vida) e as referências internas do catálogo (emoção, momento, aspiração) são validados.
        bool KnownAction(string? id) => id is null || Enum.TryParse<LivingWorld.Domain.Behavior.ActionType>(id, out _);
        bool KnownLife(string? id) => id is null || Enum.TryParse<LivingWorld.Domain.Cognition.LifeEventKind>(id, out _);

        var r = catalog.Rules;
        if (r.ScorePerWeight < 0 || r.WishSlots is < 0 or > 6 || r.WishHours < 1 || r.MaxMoments is < 1 or > 64) return Fail("Moods.rules: números fora do limite");
        if (!string.IsNullOrEmpty(r.RewardMoment) && catalog.Moment(r.RewardMoment) is null) return Fail($"Moods.rules.rewardMoment: '{r.RewardMoment}' não existe");
        for (int i = 0; i < catalog.Emotions.Count; i++)
        {
            var e = catalog.Emotions[i];
            if (string.IsNullOrWhiteSpace(e.Id) || e.Name.Length is 0 or > 24) return Fail($"Moods.emotions[{i}]: {{id, name (1 a 24 caracteres)}}");
            if (catalog.Emotions.Count(x => x.Id == e.Id) > 1) return Fail($"Moods.emotions[{i}].id: '{e.Id}' duplicado");
            if (e.Face is not null && !Faces.Contains(e.Face)) return Fail($"Moods.emotions[{i}].face: use {string.Join("|", Faces)}");
            if (e.Psyche is not null && !Faces.Contains(e.Psyche)) return Fail($"Moods.emotions[{i}].psyche: use {string.Join("|", Faces)}");
        }

        for (int i = 0; i < catalog.Moments.Count; i++)
        {
            var m = catalog.Moments[i];
            var at = $"Moods.moments[{i}]";
            if (string.IsNullOrWhiteSpace(m.Id) || m.Name.Length is 0 or > 40) return Fail($"{at}: {{id, name (1 a 40 caracteres)}}");
            if (catalog.Moments.Count(x => x.Id == m.Id) > 1) return Fail($"{at}.id: '{m.Id}' duplicado");
            if (catalog.Emotion(m.Emotion) is null) return Fail($"{at}.emotion: '{m.Emotion}' não existe em Moods.emotions");
            if (!double.IsFinite(m.Weight) || Math.Abs(m.Weight) > 20) return Fail($"{at}.weight: número de -20 a 20");
            var t = m.Trigger;
            if (!TriggerKinds.Contains(t.Kind)) return Fail($"{at}.trigger.kind: use {string.Join("|", TriggerKinds)}");
            if (t.Kind != "state" && m.DurationMin < 1) return Fail($"{at}.durationMin: maior ou igual a 1");
            if (m.Stack && m.MaxStack < 2) return Fail($"{at}.maxStack: maior ou igual a 2 quando empilha");
            if (t.Kind == "state" && t.Need is null) return Fail($"{at}.trigger.need: o gatilho de estado precisa de necessidade");
            if (t.Kind == "use" && t.Use is null) return Fail($"{at}.trigger.use: o gatilho de uso precisa do uso");
            if (!KnownAction(t.Action) || (t.Kind == "action" && t.Action is null)) return Fail($"{at}.trigger.action: ação desconhecida");
            if (!KnownLife(t.Life) || (t.Kind == "life" && t.Life is null)) return Fail($"{at}.trigger.life: fato desconhecido");
            if (t.Kind == "social" && (t.Result is not ("accepted" or "refused") || t.Side is not (null or "actor" or "target" or "both"))) return Fail($"{at}.trigger: social precisa de result accepted|refused e side actor|target|both");
            if (m.Effects is { } fx && (fx.UseRate is < 0.25 or > 4)) return Fail($"{at}.effects.useRate: de 0,25 a 4");
        }

        for (int i = 0; i < catalog.Aspirations.Count; i++)
        {
            var a = catalog.Aspirations[i];
            var at = $"Moods.aspirations[{i}]";
            if (string.IsNullOrWhiteSpace(a.Id) || a.Name.Length is 0 or > 32 || catalog.Aspirations.Count(x => x.Id == a.Id) > 1) return Fail($"{at}: {{id único, name (1 a 32 caracteres)}}");
            if (a.Milestones.Any(x => x.Moment is not null && catalog.Moment(x.Moment) is null)) return Fail($"{at}.milestones: momento desconhecido");
        }

        for (int i = 0; i < catalog.Wishes.Count; i++)
        {
            var w = catalog.Wishes[i];
            var at = $"Moods.wishes[{i}]";
            if (string.IsNullOrWhiteSpace(w.Id) || w.Text.Length is 0 or > 60 || catalog.Wishes.Count(x => x.Id == w.Id) > 1) return Fail($"{at}: {{id único, text (1 a 60 caracteres)}}");
            if (!FulfillKinds.Contains(w.Fulfill.Kind)) return Fail($"{at}.fulfill.kind: use {string.Join("|", FulfillKinds)}");
            if (!KnownAction(w.Fulfill.Action))
                return Fail($"{at}.fulfill.action: ação desconhecida");
            if (w.Target is not (null or "spouse" or "housemate")) return Fail($"{at}.target: spouse|housemate");
            if (w.Reward is not null && catalog.Moment(w.Reward) is null) return Fail($"{at}.reward: momento desconhecido");
            if (w.Aspirations?.Any(id => catalog.Aspiration(id) is null) == true) return Fail($"{at}.aspirations: aspiração desconhecida");
        }

        return Result<MoodCatalog?>.Ok(catalog.Moments.Count == 0 && catalog.Wishes.Count == 0 ? null : catalog);
    }

    private static Result<MoodCatalog?> Fail(string error) => Result<MoodCatalog?>.Fail(error);
}
