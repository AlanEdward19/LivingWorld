using System.Runtime.CompilerServices;
using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Cognition;
using LivingWorld.Domain.Objects;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cognition;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Objects;

/// <summary>Um momento como a pessoa o vive agora: o nome já com quem causou, o peso total, quantas vezes empilhou e quanto falta (nulo = dura enquanto a condição valer).</summary>
public sealed record MomentView(string Id, string Name, string Emotion, double Weight, int Count, long? RemainingMs, string? Cause);

/// <summary>Um desejo ativo para o cliente: o texto já escrito (com o nome do alvo), o alvo, quando surgiu, quando expira e quantos pontos vale.</summary>
public sealed record WishView(string Id, string Text, long Other, /* id real, −1 sem alvo */ long SinceMs, long ExpiresMs, int Points);

/// <summary>#262: o humor da pessoa lido do motor: a nota e o rótulo (a mesma que pesa na aceitação social), a emoção dominante (nome, ícone e o rosto que o boneco desenha),
/// os momentos com causa e tempo, a aspiração com o próximo marco e os desejos. Só leitura.</summary>
public sealed record MoodView(
    int Score, string? Label, string Emotion, string EmotionName, string Icon, string? Face, string? Color, IReadOnlyList<MomentView> Moments,
    string? Aspiration, string? AspirationName, int Points, int Done, string? NextMilestone, int? NextMilestonePoints, IReadOnlyList<WishView> Wishes, IReadOnlyList<WishDone> Recent);

/// <summary>#262: o mecanismo genérico de humor. Momentos nascem de gatilhos do catálogo (uso de objeto, ação, interação social, fato da vida) ou valem enquanto uma condição de
/// estado vale (sem guardar nada); somam à nota de humor (que a aceitação social já lê), dão a emoção dominante (rosto e postura), mudam o rendimento dos usos e a vontade de
/// iniciar interações. Desejos curtos nascem do estado e da aspiração (escolhida pela personalidade) e, cumpridos, dão pontos e um momento. Tudo determinístico (RNG semeado do
/// mundo, tempo de mundo), preguiçoso (expira na leitura, sem passo por tick) e no snapshot. O código não conhece nenhum momento, emoção ou desejo pelo nome.</summary>
public static class MoodMechanic
{
    private sealed record Index(MomentDef[] States, MomentDef[] Events);

    private static readonly ConditionalWeakTable<MoodCatalog, Index> Indexes = new();

    private static Index IndexOf(MoodCatalog c) => Indexes.GetValue(c, static m => new Index(
        m.Moments.Where(x => x.Trigger.Kind == "state").ToArray(), m.Moments.Where(x => x.Trigger.Kind != "state").ToArray()));

    public static MoodCatalog? CatalogOf(WorldState world) => world.ObjectCatalog?.Moods;

    // ---------------- gatilhos ----------------

    /// <summary>O uso do catálogo terminou por inteiro (<paramref name="useId"/>): dá os momentos do gatilho e cumpre os desejos que ele atende.</summary>
    public static void OnUse(WorldState world, Npc npc, string useId, long ms)
    {
        if (CatalogOf(world) is not { } catalog) return;
        foreach (var def in IndexOf(catalog).Events)
            if (def.Trigger is { Kind: "use" } t && t.Use == useId) Add(world, catalog, npc, def, ms, 0);
        Fulfill(world, catalog, npc, ms, w => w.Kind == "use" && w.Use == useId, other: 0);
    }

    /// <summary>Uma ação do motor concluída (dormir, comer): momentos e desejos pelo nome da ação.</summary>
    public static void OnAction(WorldState world, Npc npc, ActionType action, long ms)
    {
        if (CatalogOf(world) is not { } catalog) return;
        string name = action.ToString();
        foreach (var def in IndexOf(catalog).Events)
            if (def.Trigger is { Kind: "action" } t && t.Action == name) Add(world, catalog, npc, def, ms, 0);
        Fulfill(world, catalog, npc, ms, w => w.Kind == "action" && w.Action == name, other: 0);
    }

    /// <summary>Uma interação social do catálogo acabou aceita ou foi recusada: cada lado ganha os momentos do papel dele, com a outra pessoa como causa; só a aceita cumpre desejos.</summary>
    public static void OnSocial(WorldState world, SocialInteractionDef social, Npc actor, Npc target, bool accepted, long ms)
    {
        if (CatalogOf(world) is not { } catalog) return;
        string result = accepted ? "accepted" : "refused";
        foreach (var def in IndexOf(catalog).Events)
        {
            if (def.Trigger is not { Kind: "social" } t || t.Result != result) continue;
            if (t.Interaction is not null && t.Interaction != social.Id) continue;
            if (t.Category is not null && t.Category != social.Category) continue;
            if (t.Side is null or "both" or "actor") Add(world, catalog, actor, def, ms, target.Id.Value + 1); // quem causou, como id + 1 (0 = ninguém; o primeiro NPC tem id 0)
            if (t.Side is null or "both" or "target") Add(world, catalog, target, def, ms, actor.Id.Value + 1);
        }

        if (!accepted) return;
        bool Matches(WishFulfill w) => w.Kind == "social" && (w.Interaction is null || w.Interaction == social.Id) && (w.Category is null || w.Category == social.Category)
            && (w.Interaction is not null || w.Category is not null);
        Fulfill(world, catalog, actor, ms, Matches, target.Id.Value + 1);
        Fulfill(world, catalog, target, ms, Matches, actor.Id.Value + 1);
    }

    /// <summary>Um fato da vida (<see cref="LifeEventKind"/>) viveu: os momentos do gatilho <c>life</c>.</summary>
    public static void OnLife(WorldState world, Npc npc, LifeEventKind kind, long other)
    {
        if (kind == LifeEventKind.Social || CatalogOf(world) is not { } catalog) return;
        string name = kind.ToString();
        long ms = world.CurrentDate.TotalMilliseconds;
        foreach (var def in IndexOf(catalog).Events)
            if (def.Trigger is { Kind: "life" } t && t.Life == name) Add(world, catalog, npc, def, ms, other != 0 ? other + 1 : 0);
    }

    // ---------------- momentos ----------------

    private static void Add(WorldState world, MoodCatalog catalog, Npc npc, MomentDef def, long ms, long other)
    {
        if (!npc.IsAlive) return;
        var state = npc.Moods ?? MoodState.Empty;
        var list = new List<MomentInstance>(state.Moments.Count + 1);
        bool found = false;
        long until = ms + def.DurationMin * 60_000L;
        foreach (var m in state.Moments)
        {
            if (m.UntilMs <= ms) continue;
            if (m.Id == def.Id)
            {
                found = true;
                list.Add(new MomentInstance(def.Id, until, def.Stack ? Math.Min(Math.Max(2, def.MaxStack), m.Count + 1) : 1, other != 0 ? other : m.Other));
            }
            else list.Add(m);
        }

        if (!found) list.Add(new MomentInstance(def.Id, until, 1, other));
        while (list.Count > catalog.Rules.MaxMoments)
        {
            int soonest = 0;
            for (int i = 1; i < list.Count; i++)
                if (list[i].UntilMs < list[soonest].UntilMs) soonest = i;
            list.RemoveAt(soonest);
        }

        list.Sort(static (a, b) => string.CompareOrdinal(a.Id, b.Id));
        npc.SetMoods(state with { Moments = list });
    }

    /// <summary>Os momentos de agora: os de evento ainda vivos e os de estado cuja condição vale. Vazio sem catálogo.</summary>
    public static IReadOnlyList<MomentView> Moments(WorldState world, Npc npc, long ms)
    {
        if (CatalogOf(world) is not { } catalog) return [];
        var list = new List<MomentView>();
        if (npc.Moods is { } state)
            foreach (var m in state.Moments)
            {
                if (m.UntilMs <= ms || catalog.Moment(m.Id) is not { } def) continue;
                list.Add(ViewOf(world, def, m.Count, m.UntilMs - ms, m.Other));
            }

        foreach (var def in IndexOf(catalog).States)
            if (StateHolds(world, npc, def.Trigger, ms)) list.Add(ViewOf(world, def, 1, null, 0));
        list.Sort(static (a, b) => Math.Abs(b.Weight).CompareTo(Math.Abs(a.Weight)) is var c and not 0 ? c : string.CompareOrdinal(a.Id, b.Id));
        return list;
    }

    private static MomentView ViewOf(WorldState world, MomentDef def, int count, long? remaining, long other)
    {
        string? cause = null;
        string name = def.Name;
        if (name.Contains("{other}", StringComparison.Ordinal))
        {
            cause = other != 0 && world.FindNpc(new NpcId(other - 1)) is { } who ? who.Name : "alguém";
            name = name.Replace("{other}", cause, StringComparison.Ordinal);
        }

        return new MomentView(def.Id, name, def.Emotion, def.Weight * count, count, remaining, cause);
    }

    private static bool StateHolds(WorldState world, Npc npc, MomentTrigger t, long ms)
    {
        double? value = t.Ambient is not null ? RoomBeauty.Ambient(world, npc, t.Ambient) : t.Need is null ? null : HomeLife.Value(world, npc, t.Need, ms); // #264: ambiente = o cômodo onde está
        if (value is not { } v) return false;
        return (t.Below is null || v < t.Below) && (t.Above is null || v >= t.Above);
    }

    /// <summary>A soma dos pesos dos momentos de agora (para a nota de humor); 0 sem catálogo.</summary>
    public static double Sum(WorldState world, Npc npc, long ms)
    {
        if (CatalogOf(world) is not { } catalog) return 0;
        double sum = 0;
        if (npc.Moods is { } state)
            foreach (var m in state.Moments)
                if (m.UntilMs > ms && catalog.Moment(m.Id) is { } def) sum += def.Weight * m.Count;
        foreach (var def in IndexOf(catalog).States)
            if (StateHolds(world, npc, def.Trigger, ms)) sum += def.Weight;
        return sum;
    }

    // ---------------- emoção dominante ----------------

    /// <summary>A emoção dominante: a de maior força entre os momentos (|peso| × vezes) e a emoção da psique ligada (<c>psyche</c> × peso da psique); nula se nada passa do mínimo
    /// e o catálogo não tem emoção de calma.</summary>
    public static MoodEmotion? EmotionOf(WorldState world, Npc npc, long ms)
    {
        if (CatalogOf(world) is not { } catalog) return null;
        if (catalog.Emotions.Count == 0) return null;
        var strength = new double[catalog.Emotions.Count];
        if (npc.Moods is { } state)
            foreach (var m in state.Moments)
                if (m.UntilMs > ms && catalog.Moment(m.Id) is { } def) Strengthen(catalog, strength, def, m.Count);
        foreach (var def in IndexOf(catalog).States)
            if (StateHolds(world, npc, def.Trigger, ms)) Strengthen(catalog, strength, def, 1);
        if (npc.Psyche is not null)
        {
            var affect = PsycheMechanic.AffectOf(world, npc, ms / WorldMillis.PerHour);
            for (int i = 0; i < catalog.Emotions.Count; i++)
                if (catalog.Emotions[i].Psyche is { } p && Enum.TryParse<Emotion>(p, true, out var e)) strength[i] += catalog.Rules.PsycheWeight * affect.Of(e);
        }

        int best = -1;
        double top = catalog.Rules.EmotionMin;
        for (int i = 0; i < strength.Length; i++)
            if (strength[i] >= top && (best < 0 || strength[i] > strength[best])) (best, top) = (i, strength[i]);
        return best >= 0 ? catalog.Emotions[best] : catalog.Rules.Calm is { } calm ? catalog.Emotion(calm) : null;
    }

    private static void Strengthen(MoodCatalog catalog, double[] strength, MomentDef def, int count)
    {
        for (int i = 0; i < catalog.Emotions.Count; i++)
            if (catalog.Emotions[i].Id == def.Emotion) { strength[i] += Math.Abs(def.Weight) * count; return; }
    }

    /// <summary>O rosto que o boneco desenha pelo humor (emoção da psique que o cliente sabe mostrar), ou nulo. Só quando a psique não manda nenhuma.</summary>
    public static string? FaceOf(WorldState world, Npc npc, long ms) => CatalogOf(world) is null ? null : EmotionOf(world, npc, ms)?.Face;

    // ---------------- efeitos ----------------

    /// <summary>O quanto os momentos de agora multiplicam o que um uso de objeto entrega (1 = nada).</summary>
    public static double UseRate(WorldState world, Npc npc, long ms)
    {
        if (CatalogOf(world) is not { } catalog) return 1;
        double rate = 1;
        if (npc.Moods is { } state)
            foreach (var m in state.Moments)
                if (m.UntilMs > ms && catalog.Moment(m.Id) is { Effects: { } fx }) for (int k = 0; k < m.Count; k++) rate *= fx.UseRate;
        foreach (var def in IndexOf(catalog).States)
            if (def.Effects is { } fx && StateHolds(world, npc, def.Trigger, ms)) rate *= fx.UseRate;
        return Math.Clamp(rate, 0.5, 2);
    }

    /// <summary>O quanto os momentos de agora multiplicam a vontade de iniciar interações da <paramref name="category"/> (1 = nada).</summary>
    public static double SocialBias(WorldState world, Npc npc, string category, long ms)
    {
        if (CatalogOf(world) is not { } catalog) return 1;
        double bias = 1;
        if (npc.Moods is { } state)
            foreach (var m in state.Moments)
                if (m.UntilMs > ms && catalog.Moment(m.Id) is { Effects.SocialBias: { } map } && map.TryGetValue(category, out var f)) for (int k = 0; k < m.Count; k++) bias *= f;
        foreach (var def in IndexOf(catalog).States)
            if (def.Effects?.SocialBias is { } map && map.TryGetValue(category, out var f) && StateHolds(world, npc, def.Trigger, ms)) bias *= f;
        return Math.Clamp(bias, 0.1, 5);
    }

    // ---------------- aspiração e desejos ----------------

    /// <summary>A aspiração da pessoa: a que mais combina com a personalidade (soma de peso × (traço − 50)/50; empate pela ordem do catálogo). Sem estado guardado.</summary>
    public static AspirationDef? AspirationOf(WorldState world, Npc npc)
    {
        if (CatalogOf(world) is not { Aspirations.Count: > 0 } catalog) return null;
        // a conta só muda se a personalidade (imutável: trocá-la cria outro registro) ou o catálogo mudam: lembra o resultado
        if (Chosen.TryGetValue(npc, out var cached) && ReferenceEquals(cached.Personality, npc.Personality) && ReferenceEquals(cached.Catalog, catalog)) return cached.Aspiration;
        AspirationDef? best = null;
        double top = double.NegativeInfinity;
        foreach (var a in catalog.Aspirations)
        {
            double score = 0;
            foreach (var (trait, weight) in a.Affinity) score += weight * (SocialInteractions.TraitOf(npc.Personality, trait) - 50) / 50.0;
            if (score > top) (top, best) = (score, a);
        }

        Chosen.Remove(npc);
        Chosen.Add(npc, new Choice(npc.Personality, catalog, best));
        return best;
    }

    private sealed record Choice(Personality Personality, MoodCatalog Catalog, AspirationDef? Aspiration);

    private static readonly ConditionalWeakTable<Npc, Choice> Chosen = new();

    /// <summary>De hora em hora (só quem tem necessidades do catálogo): tira o que venceu, cumpre desejos de estado, deixa expirar os velhos e sorteia os que faltam.</summary>
    public static void Hourly(WorldState world, Npc npc, long ms)
    {
        if (CatalogOf(world) is not { } catalog || !npc.IsAlive) return;
        // quem ninguém conduz é revisto a cada poucas horas (a conta é a mesma para todos; só o instante muda): o que venceu já não aparece na leitura, então atrasar o
        // descarte e o sorteio não muda nada que se veja; o corpo do jogador é revisto a toda hora
        if (npc.ControllerId is null && (ms / WorldMillis.PerHour + npc.Id.Value) % RefillEveryHours != 0) return;
        Prune(world, catalog, npc, ms);
        if (npc.Moods is { } state)
            foreach (var wish in state.Wishes) // só quem tem desejo de estado paga a conta
                if (catalog.Wish(wish.Id) is { Fulfill: { Kind: "state", Need: { } need, Above: { } above } } && (HomeLife.Value(world, npc, need, ms) ?? 0) >= above)
                {
                    Complete(world, catalog, npc, wish, catalog.Wish(wish.Id)!, ms);
                    break;
                }

        Refill(world, catalog, npc, ms);
    }

    private static void Prune(WorldState world, MoodCatalog catalog, Npc npc, long ms)
    {
        if (npc.Moods is not { } state) return;
        long life = catalog.Rules.WishHours * WorldMillis.PerHour;
        bool stale = false;
        foreach (var m in state.Moments)
            if (m.UntilMs <= ms) { stale = true; break; }
        if (!stale)
            foreach (var w in state.Wishes)
                if (Gone(world, catalog, w, ms, life)) { stale = true; break; }
        if (!stale) return; // nada venceu: nada é alocado nem escrito
        var moments = state.Moments.Where(m => m.UntilMs > ms).ToList();
        var wishes = state.Wishes.Where(w => !Gone(world, catalog, w, ms, life)).ToList();
        npc.SetMoods(state with { Moments = moments, Wishes = wishes });
    }

    private static bool Gone(WorldState world, MoodCatalog catalog, WishInstance w, long ms, long life) =>
        ms - w.SinceMs >= life || catalog.Wish(w.Id) is null || (w.Other != 0 && world.FindNpc(new NpcId(w.Other - 1)) is not { IsAlive: true });

    private static void Refill(WorldState world, MoodCatalog catalog, Npc npc, long ms)
    {
        var state = npc.Moods ?? MoodState.Empty;
        int free = catalog.Rules.WishSlots - state.Wishes.Count;
        if (free <= 0 || catalog.Wishes.Count == 0) return;
        long hour = ms / WorldMillis.PerHour;
        List<(WishDef Def, long Other, double Weight)>? pool = null;
        AspirationDef? aspiration = null;
        bool aspirationRead = false;
        foreach (var w in catalog.Wishes)
        {
            if (!WhenHolds(world, npc, w.When, ms) || Has(state, w.Id)) continue;
            long other = 0;
            if (w.Target is not null && (other = TargetOf(world, npc, w.Target, hour)) == 0) continue;
            if (!aspirationRead) { aspiration = AspirationOf(world, npc); aspirationRead = true; }
            double weight = w.Weight * (aspiration is not null && w.Aspirations?.Contains(aspiration.Id) == true ? catalog.Rules.AspirationBoost : 1);
            if (weight > 0) (pool ??= []).Add((w, other, weight));
        }

        if (pool is null) return; // nada pede nada agora: sem sorteio, sem escrita
        var rng = world.Rng.OneShot($"wish-{npc.Id.Value}-{hour}");
        var wishes = state.Wishes.ToList();
        for (int slot = 0; slot < free && pool.Count > 0; slot++)
        {
            double pick = rng.NextDouble() * pool.Sum(p => p.Weight);
            int at = pool.Count - 1;
            for (int i = 0; i < pool.Count; i++)
            {
                pick -= pool[i].Weight;
                if (pick < 0) { at = i; break; }
            }

            wishes.Add(new WishInstance(pool[at].Def.Id, pool[at].Other, ms));
            pool.RemoveAt(at);
        }

        npc.SetMoods(state with { Wishes = wishes.OrderBy(w => w.SinceMs).ThenBy(w => w.Id, StringComparer.Ordinal).ToList() });
    }

    /// <summary>Horas entre uma tentativa de sortear desejos e a seguinte para quem ninguém conduz (a conta é a mesma para todos; só o instante muda).</summary>
    private const int RefillEveryHours = 4;

    private static bool Has(MoodState state, string id)
    {
        foreach (var w in state.Wishes)
            if (w.Id == id) return true;
        return false;
    }

    private static bool WhenHolds(WorldState world, Npc npc, WishWhen? when, long ms)
    {
        if (when is null) return true;
        if (when.Spouse is { } wantSpouse && wantSpouse != (npc.Spouse is { } s && world.FindNpc(s) is { IsAlive: true })) return false;
        if (when.Need is null) return true;
        if (HomeLife.Value(world, npc, when.Need, ms) is not { } v) return false;
        return (when.Below is null || v < when.Below) && (when.Above is null || v >= when.Above);
    }

    private static long TargetOf(WorldState world, Npc npc, string target, long hour)
    {
        if (target == "spouse") return npc.Spouse is { } s && world.FindNpc(s) is { IsAlive: true, City: var c } && c == npc.City ? s.Value + 1 : 0;
        if (npc.Household is not { } h || world.FindHousehold(h) is not { } home) return 0;
        int count = 0;
        foreach (var id in home.Members)
            if (id != npc.Id && world.FindNpc(id) is { IsAlive: true }) count++;
        if (count == 0) return 0;
        long want = (npc.Id.Value + hour) % count, seen = 0, best = 0;
        foreach (var id in home.Members.OrderBy(m => m.Value)) // quem, entre os da casa, gira com a hora (determinístico)
            if (id != npc.Id && world.FindNpc(id) is { IsAlive: true } && seen++ == want) { best = id.Value + 1; break; }
        return best;
    }

    /// <summary>Cumpre os desejos ativos cujo critério <paramref name="matches"/> atende; <paramref name="other"/> é a pessoa do fato (0 = sem pessoa): um desejo com alvo só vale com ela.</summary>
    private static void Fulfill(WorldState world, MoodCatalog catalog, Npc npc, long ms, Func<WishFulfill, bool> matches, long other)
    {
        if (npc.Moods is not { Wishes.Count: > 0 } state) return;
        foreach (var w in state.Wishes)
        {
            if (ms - w.SinceMs >= catalog.Rules.WishHours * WorldMillis.PerHour || catalog.Wish(w.Id) is not { } def || !matches(def.Fulfill)) continue; // desejo vencido não se cumpre
            if (w.Other != 0 && w.Other != other) continue;
            Complete(world, catalog, npc, w, def, ms);
            return; // um por fato; o próximo fato cumpre o seguinte
        }
    }

    private static void Complete(WorldState world, MoodCatalog catalog, Npc npc, WishInstance wish, WishDef def, long ms)
    {
        var state = npc.Moods!;
        int gain = def.Points > 0 ? def.Points : catalog.Rules.WishPoints;
        var recent = new List<WishDone> { new(def.Id, WishText(world, def, wish.Other), ms, gain) };
        int before = state.Points, after = before + gain;
        if (AspirationOf(world, npc) is { } aspiration)
            foreach (var m in aspiration.Milestones)
                if (before < m.Points && after >= m.Points)
                {
                    recent.Add(new WishDone("milestone", m.Name, ms, 0, true));
                    if (m.Moment is not null && catalog.Moment(m.Moment) is { } md) Add(world, catalog, npc, md, ms, 0);
                }

        state = npc.Moods ?? state;
        recent.AddRange(state.Recent ?? []);
        npc.SetMoods(state with { Wishes = state.Wishes.Where(x => x != wish).ToList(), Points = after, Done = state.Done + 1, Recent = recent.Take(4).ToList() });
        string? reward = def.Reward ?? catalog.Rules.RewardMoment;
        if (reward is not null && catalog.Moment(reward) is { } rd) Add(world, catalog, npc, rd, ms, wish.Other);
    }

    private static string WishText(WorldState world, WishDef def, long other) =>
        def.Text.Replace("{other}", other != 0 && world.FindNpc(new NpcId(other - 1)) is { } who ? who.Name : "alguém", StringComparison.Ordinal);

    /// <summary>Os desejos ativos para o cliente (<c>Other</c>: o id real do alvo, −1 sem alvo), em ordem de surgimento.</summary>
    public static IReadOnlyList<WishView> Wishes(WorldState world, Npc npc, long ms)
    {
        if (CatalogOf(world) is not { } catalog || npc.Moods is not { Wishes.Count: > 0 } state) return [];
        var list = new List<WishView>(state.Wishes.Count);
        foreach (var w in state.Wishes)
            if (catalog.Wish(w.Id) is { } def && ms - w.SinceMs < catalog.Rules.WishHours * WorldMillis.PerHour)
                list.Add(new WishView(def.Id, WishText(world, def, w.Other), w.Other - 1, w.SinceMs, w.SinceMs + catalog.Rules.WishHours * WorldMillis.PerHour, def.Points > 0 ? def.Points : catalog.Rules.WishPoints));
        return list;
    }

    /// <summary>O humor completo da pessoa para a ficha; nulo sem catálogo de humor.</summary>
    public static MoodView? ViewOf(WorldState world, Npc npc, long ms)
    {
        if (CatalogOf(world) is not { } catalog) return null;
        var emotion = EmotionOf(world, npc, ms);
        int score;
        string? label = null;
        if (world.ObjectCatalog?.Social is { } social) (score, label) = SocialInteractions.Mood(world, social, npc, ms);
        else score = (int)Math.Round(Math.Clamp(50 + Sum(world, npc, ms) * catalog.Rules.ScorePerWeight, 0, 100));
        var aspiration = AspirationOf(world, npc);
        int points = npc.Moods?.Points ?? 0;
        var next = aspiration?.Milestones.Where(m => m.Points > points).OrderBy(m => m.Points).FirstOrDefault();
        return new MoodView(score, label, emotion?.Id ?? "", emotion?.Name ?? "", emotion?.Icon ?? "", emotion?.Face, emotion?.Color, Moments(world, npc, ms),
            aspiration?.Id, aspiration?.Name, points, npc.Moods?.Done ?? 0, next?.Name, next?.Points, Wishes(world, npc, ms), npc.Moods?.Recent ?? []);
    }
}
