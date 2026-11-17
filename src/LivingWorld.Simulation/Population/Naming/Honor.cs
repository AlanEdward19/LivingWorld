using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Population.Naming;

/// <summary>Boa fama de uma pessoa (0..100 em <see cref="Tick"/>, decai à metade em <c>HonorHalfLifeDays</c>), com o nome de
/// família e a cidade de quando fez o último feito: a linhagem lembra o feito mesmo depois que a pessoa morre.</summary>
public sealed class HonorRecord
{
    public long Npc { get; set; }
    public string Family { get; set; } = "";
    public Guid City { get; set; }
    public int Honor { get; set; }
    public long Tick { get; set; }
}

/// <summary>O que o mundo guarda de feito honroso (AD-183), em ordem de pessoa. Canônico; nulo enquanto vazio.</summary>
public sealed class HonorLedger
{
    public List<HonorRecord> Records { get; init; } = [];

    /// <summary>Última limpeza dos esquecidos (uma por ano, quando alguém faz um feito).</summary>
    public long PrunedAtTick { get; set; }

    public HonorRecord? RecordOf(long npc)
    {
        int i = Records.BinarySearch(new HonorRecord { Npc = npc }, Order.Instance);
        return i >= 0 ? Records[i] : null;
    }

    public HonorRecord EnsureRecord(long npc)
    {
        var probe = new HonorRecord { Npc = npc };
        int i = Records.BinarySearch(probe, Order.Instance);
        if (i >= 0) return Records[i];
        Records.Insert(~i, probe);
        return probe;
    }

    private sealed class Order : IComparer<HonorRecord>
    {
        public static readonly Order Instance = new();
        public int Compare(HonorRecord? a, HonorRecord? b) => a!.Npc.CompareTo(b!.Npc);
    }
}

/// <summary>Honra (AD-183): o feito que a cidade comenta vira boa fama de quem o fez e, atenuada, da linhagem dele na cidade —
/// simétrica à má fama (<c>Justice.ReputationOf</c>). Consumidor do log de eventos (chamado por <c>TickContext.LogEvent</c>):
/// socorro dado a outra casa, coisa nova que funcionou, negócio aberto, cidade-filha fundada. Quem a lê: a suspeita e a troca
/// de culpado (a honra desconta a má fama vista), o crédito (confiança do credor), o namoro e a contratação (o nome da pessoa).</summary>
public static class Honor
{
    private const double Ln2 = 0.6931471805599453;

    public static void OnEvent(WorldState world, WorldEventKind kind, string payload, long tick)
    {
        var rules = world.FamilyRules.Names;
        if (!rules.HonorOn) return;
        switch (kind)
        {
            case WorldEventKind.AidGiven when !payload.StartsWith("city-", StringComparison.Ordinal):
                Credit(world, FirstId(payload), rules.HonorPerAid, tick);
                return;
            case WorldEventKind.DesignPrototyped:
                Credit(world, FirstId(payload), rules.HonorPerInvention, tick);
                return;
            case WorldEventKind.VentureFounded:
                Credit(world, SecondId(payload), rules.HonorPerVenture, tick);
                return;
        }
    }

    /// <summary>Soma <paramref name="amount"/> à boa fama de <paramref name="npcId"/> (teto 100).</summary>
    public static void Credit(WorldState world, long npcId, double amount, long now)
    {
        if (amount <= 0 || npcId < 0 || world.FindNpc(new NpcId(npcId)) is not { } npc) return;
        var book = world.HonorBook;
        var record = book.EnsureRecord(npcId);
        double current = Decayed(world, record, now);
        record.Honor = (int)Math.Round(Math.Min(100, current + amount), MidpointRounding.AwayFromZero);
        record.Tick = now;
        record.City = npc.City.Value;
        record.Family = FamilyOf(npc.Name);
        long year = world.Calendar.HoursPerYear;
        if (now - book.PrunedAtTick >= year)
        {
            book.Records.RemoveAll(r => Decayed(world, r, now) < 1);
            book.PrunedAtTick = now;
        }
    }

    /// <summary>Boa fama própria (0..100) agora.</summary>
    public static double OwnOf(WorldState world, long npc, long now) =>
        world.Honor?.RecordOf(npc) is { } record ? Decayed(world, record, now) : 0;

    /// <summary>Boa fama vista (0..100): a própria e, atenuada, a do mais honrado da mesma linhagem na mesma cidade.</summary>
    public static double StandingOf(WorldState world, Npc npc, long now)
    {
        if (world.Honor is null) return 0;
        double own = OwnOf(world, npc.Id.Value, now);
        double spill = world.FamilyRules.Names.LineageHonorSpill;
        if (spill <= 0) return own;
        return Math.Min(100, own + spill * LineageFame.Of(world, now).BestHonorExcept(npc));
    }

    /// <summary>O nome de alguém (−1..1): boa fama vista menos má fama vista, cada uma com a da linhagem.</summary>
    public static double RegardOf(WorldState world, Npc npc, long now)
    {
        double honor = StandingOf(world, npc, now);
        double infamy = 0;
        if (world.CrimeOn)
        {
            infamy = LivingWorld.Simulation.Crime.Justice.InfamyOf(world, npc.Id.Value, now).All;
            double spill = world.FamilyRules.Names.LineageInfamySpill;
            if (spill > 0) infamy = Math.Min(100, infamy + spill * LineageFame.Of(world, now).WorstInfamyExcept(npc));
        }
        return Math.Clamp((honor - infamy) / 100.0, -1, 1);
    }

    internal static double Decayed(WorldState world, HonorRecord record, long now) =>
        record.Honor * DeterministicMath.Exp(-Ln2 * Math.Max(0, now - record.Tick) / (double)world.Calendar.HoursPerDay
            / Math.Max(1, world.FamilyRules.Names.HonorHalfLifeDays));

    /// <summary>Nome de família (depois do último espaço); vazio no nome legado de uma palavra.</summary>
    public static string FamilyOf(string name)
    {
        int cut = name.LastIndexOf(' ');
        return cut > 0 ? name[(cut + 1)..] : "";
    }

    private static long FirstId(string payload) => IdAt(payload, 0);

    private static long SecondId(string payload) => IdAt(payload, 1);

    private static long IdAt(string payload, int index)
    {
        var span = payload.AsSpan();
        for (int i = 0; i < index; i++)
        {
            int bar = span.IndexOf('|');
            if (bar < 0) return -1;
            span = span[(bar + 1)..];
        }
        int end = span.IndexOf('|');
        if (end >= 0) span = span[..end];
        return long.TryParse(span, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out long id)
            && id >= 0 ? id : -1;
    }
}
