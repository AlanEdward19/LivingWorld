namespace LivingWorld.Domain.Objects;

/// <summary>#262: momentos (moodlets), emoções, aspirações e desejos como catálogo do cenário (<c>scenarios/catalog/moods.json</c>). O código só conhece
/// mecanismos genéricos (gatilho, peso, duração, empilhar, efeito, condição de desejo); nome, número e frase são dado. Nulo = cenário sem humor (omitido do snapshot e do hash).</summary>
public sealed record MoodCatalog(
    MoodRules Rules,
    IReadOnlyList<MoodEmotion> Emotions,
    IReadOnlyList<MomentDef> Moments,
    IReadOnlyList<AspirationDef> Aspirations,
    IReadOnlyList<WishDef> Wishes)
{
    public MomentDef? Moment(string id) => IndexOf(this).Moments.GetValueOrDefault(id);
    public MoodEmotion? Emotion(string id) => IndexOf(this).Emotions.GetValueOrDefault(id);
    public AspirationDef? Aspiration(string id) => IndexOf(this).Aspirations.GetValueOrDefault(id);
    public WishDef? Wish(string id) => IndexOf(this).Wishes.GetValueOrDefault(id);

    // índices fora do record (a igualdade compararia campos privados): a leitura do humor roda por NPC e por hora, sem varrer lista nem alocar
    private sealed record Index(Dictionary<string, MomentDef> Moments, Dictionary<string, MoodEmotion> Emotions, Dictionary<string, AspirationDef> Aspirations, Dictionary<string, WishDef> Wishes);

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<MoodCatalog, Index> Indexes = new();

    private static Index IndexOf(MoodCatalog c) => Indexes.GetValue(c, static m => new Index(
        Pick(m.Moments, x => x.Id), Pick(m.Emotions, x => x.Id), Pick(m.Aspirations, x => x.Id), Pick(m.Wishes, x => x.Id)));

    private static Dictionary<string, T> Pick<T>(IReadOnlyList<T> list, Func<T, string> id)
    {
        var map = new Dictionary<string, T>(list.Count);
        foreach (var item in list) map.TryAdd(id(item), item); // o primeiro de um id repetido vale (a validação do carregador reprova repetido)
        return map;
    }
}

/// <summary><c>ScorePerWeight</c>: pontos da nota de humor (0–100) por ponto de peso dos momentos; <c>PsycheWeight</c>: o peso (em pontos de momento) da emoção
/// da psique no máximo; <c>WishSlots</c>/<c>WishHours</c>: quantos desejos de cada vez e por quanto tempo valem; <c>WishPoints</c>: pontos de satisfação padrão;
/// <c>RewardMoment</c>: momento que o desejo cumprido dá; <c>GoalUrgency</c>: urgência do desejo na lista de metas; <c>Calm</c>: emoção quando nada pesa.</summary>
public sealed record MoodRules(
    double ScorePerWeight = 4, double PsycheWeight = 4, int WishSlots = 3, int WishHours = 6, int WishPoints = 10, string? RewardMoment = null,
    int GoalUrgency = 25, string? Calm = null, int MaxMoments = 16, double EmotionMin = 1, double AspirationBoost = 3);

/// <summary>Emoção do humor: <c>Face</c> é a emoção da psique que o rosto do boneco já sabe desenhar (joy, grief, fear, anger, shame, loneliness); <c>Psyche</c> liga a emoção
/// da psique a esta (a mais forte entra no humor); <c>Icon</c> é o nome do ícone do cliente.</summary>
public sealed record MoodEmotion(string Id, string Name, string Icon, string? Face = null, string? Psyche = null, string? Color = null);

/// <summary>Quando o momento surge. <c>Kind</c>: <c>use</c> (fim natural do uso do catálogo), <c>action</c> (ação do motor concluída), <c>social</c> (interação aceita ou
/// recusada), <c>life</c> (fato da vida, <c>LifeEventKind</c>), <c>reward</c> (só o motor o dá: desejo cumprido, marco) ou <c>state</c> (enquanto a condição vale, sem estado guardado: necessidade abaixo ou acima de um nível).</summary>
public sealed record MomentTrigger(
    string Kind, string? Use = null, string? Action = null, string? Interaction = null, string? Category = null, string? Result = null, string? Side = null,
    string? Life = null, string? Need = null, double? Below = null, double? Above = null);

/// <summary>Efeitos reais do momento: <c>UseRate</c> multiplica o que os usos de objeto entregam (rendimento); <c>SocialBias</c> multiplica a vontade de iniciar interações de cada
/// categoria (o que o NPC escolhe fazer).</summary>
public sealed record MomentEffects(double UseRate = 1, IReadOnlyDictionary<string, double>? SocialBias = null);

/// <summary>Um momento: o nome pode citar <c>{other}</c> (quem causou). <c>Weight</c> pode ser negativo; <c>DurationMin</c> (de mundo) só vale para os de evento;
/// <c>Stack</c> soma até <c>MaxStack</c> ocorrências (senão renova).</summary>
public sealed record MomentDef(
    string Id, string Name, string Emotion, double Weight, MomentTrigger Trigger, int DurationMin = 0, bool Stack = false, int MaxStack = 1,
    MomentEffects? Effects = null);

public sealed record AspirationMilestone(int Points, string Name, string? Moment = null);

/// <summary>Aspiração de vida: escolhida pela personalidade (<c>Affinity</c>: traço → peso, a soma de peso × (traço − 50)/50 mais alta vence); <c>Wishes</c> amplia o peso dos desejos
/// ligados a ela; <c>Milestones</c> são os marcos em pontos de satisfação.</summary>
public sealed record AspirationDef(
    string Id, string Name, string Text, IReadOnlyDictionary<string, double> Affinity, IReadOnlyList<AspirationMilestone> Milestones);

/// <summary>Condição de estado de um desejo ou de um momento: necessidade abaixo ou acima de um nível, e se tem cônjuge.</summary>
public sealed record WishWhen(string? Need = null, double? Below = null, double? Above = null, bool? Spouse = null);

/// <summary>Como o desejo se cumpre: pelo mesmo vocabulário dos gatilhos; <c>Target</c> (<c>spouse</c>|<c>housemate</c>) liga a uma pessoa e vale só com ela.</summary>
public sealed record WishFulfill(
    string Kind, string? Use = null, string? Interaction = null, string? Category = null, string? Action = null, string? Need = null, double? Above = null);

/// <summary>Desejo curto: <c>Text</c> cita <c>{other}</c>; <c>When</c> é o estado que o faz surgir; <c>Aspirations</c> lista as que o favorecem (peso × <c>AspirationBoost</c>).</summary>
public sealed record WishDef(
    string Id, string Text, WishFulfill Fulfill, double Weight = 1, WishWhen? When = null, string? Target = null, int Points = 0,
    IReadOnlyList<string>? Aspirations = null, string? Reward = null);

/// <summary>Um momento vivo na pessoa: o id do catálogo, quando acaba (ms de mundo), quantas vezes empilhou e quem causou (0 = ninguém).</summary>
public readonly record struct MomentInstance(string Id, long UntilMs, int Count, long Other);

/// <summary>Um desejo ativo: o id, quem é o alvo (0 = ninguém) e desde quando (ms).</summary>
public readonly record struct WishInstance(string Id, long Other, long SinceMs);

/// <summary>Um desejo cumprido ou marco alcançado, para o aviso do cliente: texto já escrito, quando (ms) e pontos ganhos.</summary>
public readonly record struct WishDone(string Id, string Text, long AtMs, int Points, bool Milestone = false);

/// <summary>#262: o que a pessoa guarda do humor: momentos vivos, desejos ativos, pontos de satisfação e os últimos cumpridos. Nulo = nada ainda (omitido do snapshot e do hash).</summary>
public sealed record MoodState(
    IReadOnlyList<MomentInstance> Moments, IReadOnlyList<WishInstance> Wishes, int Points = 0, int Done = 0, IReadOnlyList<WishDone>? Recent = null)
{
    public static MoodState Empty { get; } = new([], []);
}
