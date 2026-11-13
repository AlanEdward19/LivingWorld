namespace LivingWorld.Domain.Cognition;

/// <summary>Tipo de lembrança da própria vida (AD-138). Só o motor cria lembranças, a partir dos eventos que já emite.</summary>
public enum LifeEventKind : byte
{
    None = 0,
    ChildBorn = 1,
    ChildDied = 2,
    SpouseDied = 3,
    ParentDied = 4,
    SiblingDied = 5,
    FriendDied = 6,
    StillBirth = 7,
    Famine = 8,
    Betrayed = 9,
    BetrayedSpouse = 10,
    Robbed = 11,
    CaughtStealing = 12,
    AidReceived = 13,
    AidGiven = 14,
    Married = 15,
    Separated = 16,
    Abandoned = 17,
    Hired = 18,
    Fired = 19,
    Evicted = 20,
    TakenIn = 21,
    Violence = 22,
    FellIll = 23,
    HouseholdMemberIll = 24,
    InventionFailed = 25,
    InventionWorked = 26,
    UnwedPregnancy = 27,
    ChildUnwedPregnancy = 28,
}

/// <summary>Uma lembrança episódica: o que aconteceu (<see cref="Kind"/>), com quem (<see cref="Other"/>, 0 = ninguém),
/// quando (<see cref="Tick"/>), quão marcante foi (<see cref="Salience"/>, 0..100) e a última vez que algo relacionado a
/// reavivou (<see cref="RehearsedTick"/>). A força atual decai a partir do último reforço; ver <c>PsycheRules.StrengthOf</c>.</summary>
public readonly record struct EpisodicMemory(LifeEventKind Kind, long Other, long Tick, int Salience, long RehearsedTick);

/// <summary>Vida interior do NPC (AD-138): lembranças da própria vida e emoções com duração. As emoções são guardadas em
/// milésimos no tick <see cref="AffectTick"/> e decaem sozinhas com o tempo (meia-vida por emoção), sem passo por tick:
/// quem lê calcula o valor atual. <see cref="CalmFromTick"/> é o tick a partir do qual todas já estão abaixo de 2% (leitura
/// barata). Estado canônico; nulo = nada vivido ainda (snapshot antigo abre com nulo).</summary>
public sealed record PsycheState(
    IReadOnlyList<EpisodicMemory> Memories,
    long AffectTick = 0,
    long CalmFromTick = 0,
    int Grief = 0,
    int Fear = 0,
    int Anger = 0,
    int Joy = 0,
    int Shame = 0,
    int Loneliness = 0)
{
    public static PsycheState Empty { get; } = new(Array.Empty<EpisodicMemory>());
}

/// <summary>Emoção discreta com duração.</summary>
public enum Emotion : byte
{
    Grief,
    Fear,
    Anger,
    Joy,
    Shame,
    Loneliness,
}

/// <summary>Emoções atuais (0..1), já decaídas para o tick lido.</summary>
public readonly record struct Affect(double Grief, double Fear, double Anger, double Joy, double Shame, double Loneliness)
{
    public static Affect Calm => default;

    public bool IsCalm => Grief <= 0 && Fear <= 0 && Anger <= 0 && Joy <= 0 && Shame <= 0 && Loneliness <= 0;

    /// <summary>Valência (−1..1): agradável menos desagradável.</summary>
    public double Valence => Math.Clamp(Joy - Grief - 0.6 * Fear - 0.4 * Anger - 0.6 * Shame - 0.5 * Loneliness, -1, 1);

    /// <summary>Ativação (0..1): medo, raiva e alegria agitam; tristeza não.</summary>
    public double Arousal => Math.Clamp(Fear + Anger + 0.5 * Joy, 0, 1);

    public double Of(Emotion emotion) => emotion switch
    {
        Emotion.Grief => Grief,
        Emotion.Fear => Fear,
        Emotion.Anger => Anger,
        Emotion.Joy => Joy,
        Emotion.Shame => Shame,
        _ => Loneliness,
    };
}
