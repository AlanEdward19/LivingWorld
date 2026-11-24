namespace LivingWorld.Simulation.Players;

/// <summary>O estranhamento de <see cref="Observer"/> com o corpo <see cref="Body"/> (AD-197f): quanto a conduta dele fugiu do que
/// a pessoa faria (<see cref="Value"/>), desde quando, se já comentou (boato), se concluiu possessão (crença, não verdade) e se
/// passou a vigiar. Canônico: alimenta o boato, a conclusão e a reação.</summary>
public sealed class Suspicion
{
    public long Observer { get; set; }
    public long Body { get; set; }
    public double Value { get; set; }
    /// <summary>Desde quando (ADR-0050: guardado em ms; no save e no hash, <c>Tick</c> em horas na hora cheia).</summary>
    [System.Text.Json.Serialization.JsonPropertyName("Tick")]
    [System.Text.Json.Serialization.JsonConverter(typeof(LivingWorld.Domain.Shared.HourCompatibleMillisJsonConverter))]
    public long AtMs { get; set; }

    /// <summary>A hora do calendário de <see cref="AtMs"/> (piso), a unidade dos sistemas de fronteira.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public long Tick
    {
        get => LivingWorld.Domain.Shared.WorldMillis.ToHourFloor(AtMs);
        set => AtMs = LivingWorld.Domain.Shared.WorldMillis.FromHours(value);
    }
    public bool Rumored { get; set; }
    public bool Concluded { get; set; }
    public bool Vigil { get; set; }
}

/// <summary>Livro do estranhamento (AD-197f), ordenado por (observador, corpo). Nulo no mundo enquanto ninguém estranhou ninguém:
/// o mundo sem jogador fica com o hash de antes.</summary>
public sealed class PossessionLedger
{
    public List<Suspicion> Suspicions { get; set; } = [];

    internal Suspicion Of(long observer, long body)
    {
        int i = Suspicions.BinarySearch(new Suspicion { Observer = observer, Body = body }, Order.Instance);
        if (i >= 0) return Suspicions[i];
        var created = new Suspicion { Observer = observer, Body = body };
        Suspicions.Insert(~i, created);
        return created;
    }

    private sealed class Order : IComparer<Suspicion>
    {
        public static readonly Order Instance = new();
        public int Compare(Suspicion? a, Suspicion? b) =>
            a!.Observer != b!.Observer ? a.Observer.CompareTo(b.Observer) : a.Body.CompareTo(b.Body);
    }
}
