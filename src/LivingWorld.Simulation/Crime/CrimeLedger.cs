namespace LivingWorld.Simulation.Crime;

public enum CrimeKind : byte
{
    Theft = 1,
    Assault = 2,
    Homicide = 3,
    DomesticAssault = 4,
    Kidnapping = 5,
    SexualAssault = 6,
}

/// <summary>Um cativeiro em curso (AD-154): quem está preso, por quem, por quê, desde quando, onde (a casa do sequestrador
/// na hora do sequestro), o resgate pedido e o caso aberto (-1 = ninguém viu ainda).</summary>
public sealed class Captivity
{
    public long Victim { get; set; }
    public long Offender { get; set; }
    public string Motive { get; set; } = "";
    public long StartTick { get; set; }
    public int HoldX { get; set; }
    public int HoldY { get; set; }
    public long Ransom { get; set; }
    public long CaseId { get; set; } = -1;
}

/// <summary>Mágoa de <see cref="Holder"/> contra <see cref="Target"/> (AD-150): inimizade duradoura com motivo, intensidade
/// (0..100) no tick <see cref="Tick"/>, que decai sozinha (meia-vida do cenário; lida na hora, sem passo por dia).</summary>
public sealed class Grudge
{
    public long Holder { get; set; }
    public long Target { get; set; }
    public string Motive { get; set; } = "";
    public int Intensity { get; set; }
    public long Tick { get; set; }
}

/// <summary>Quem sabe de um caso pelo boato e o que acredita: quem fez (pode ser outro, a memória e a conversa distorcem) e
/// quanto foi levado (o boato aumenta).</summary>
public sealed class CaseKnower
{
    public long Npc { get; set; }
    public long Accused { get; set; }
    public long Value { get; set; }
}

/// <summary>Um crime conhecido (AD-150): o que foi, quem fez de verdade (<see cref="Offender"/>, verdade do motor) e quem é
/// acusado (<see cref="Accused"/>, -1 = ninguém sabe: impunidade), vítima, valor, testemunhas que confirmam, flagrante, quem
/// denunciou e se já foi julgado. <see cref="Knowers"/> é o boato, em ordem de chegada.</summary>
public sealed class CrimeCase
{
    public long Id { get; set; }
    public CrimeKind Kind { get; set; }
    public long Offender { get; set; }
    public long Victim { get; set; }
    public long Tick { get; set; }
    public Guid City { get; set; }
    public long Value { get; set; }
    public string Good { get; set; } = "";
    public string Motive { get; set; } = "";

    /// <summary>AD-183: por que o acusado foi apontado sem testemunha (<c>fama-da-linhagem</c>: a fama da linhagem pesou na
    /// suspeita). Nulo = testemunha ou a própria fama; fora do snapshot quando nulo.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? Why { get; set; }

    public long Accused { get; set; } = -1;
    public int Witnesses { get; set; }
    public bool Caught { get; set; }
    public long ReportedBy { get; set; } = -1;
    public bool Closed { get; set; }
    public List<CaseKnower> Knowers { get; set; } = [];
}

/// <summary>Ficha de alguém (AD-150): má fama (0..100 no tick <see cref="InfamyTick"/>, decai), a parte violenta dela e
/// condenações. Só existe para quem já foi apontado em algum caso.</summary>
public sealed class CriminalRecord
{
    public long Npc { get; set; }
    public int Infamy { get; set; }
    public int ViolentInfamy { get; set; }
    public long InfamyTick { get; set; }
    public int Convictions { get; set; }
}

/// <summary>Medo da punição numa cidade (0..1 no tick <see cref="Tick"/>), que decai: o exemplo da última punição.</summary>
public sealed class CityDeterrence
{
    public Guid City { get; set; }
    public double Level { get; set; }
    public long Tick { get; set; }
}

/// <summary>Tudo o que o crime guarda no mundo (canônico): mágoas (ordem de holder e alvo), casos abertos (ordem de id),
/// fichas (ordem de pessoa) e o medo por cidade (ordem de cidade). Nulo no mundo sem violência nem justiça.</summary>
public sealed class CrimeLedger
{
    public List<Grudge> Grudges { get; init; } = [];
    public List<CrimeCase> Cases { get; init; } = [];
    public List<CriminalRecord> Records { get; init; } = [];
    public List<CityDeterrence> Deterrence { get; init; } = [];
    public long NextCaseId { get; set; } = 1;

    /// <summary>Cativeiros em curso (AD-154), em ordem de vítima. Nulo quando não há nenhum: fora do snapshot e do hash.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public List<Captivity>? Captives { get; set; }

    public Captivity? CaptivityOf(long victim)
    {
        if (Captives is null) return null;
        foreach (var c in Captives) if (c.Victim == victim) return c;
        return null;
    }

    public void AddCaptivity(Captivity captivity)
    {
        Captives ??= [];
        int i = 0;
        while (i < Captives.Count && Captives[i].Victim < captivity.Victim) i++;
        Captives.Insert(i, captivity);
    }

    public void RemoveCaptivity(Captivity captivity)
    {
        if (Captives is null) return;
        Captives.Remove(captivity);
        if (Captives.Count == 0) Captives = null;
    }

    public CriminalRecord? RecordOf(long npc)
    {
        int i = Records.BinarySearch(new CriminalRecord { Npc = npc }, RecordOrder.Instance);
        return i >= 0 ? Records[i] : null;
    }

    public CriminalRecord EnsureRecord(long npc)
    {
        var probe = new CriminalRecord { Npc = npc };
        int i = Records.BinarySearch(probe, RecordOrder.Instance);
        if (i >= 0) return Records[i];
        Records.Insert(~i, probe);
        return probe;
    }

    public Grudge? GrudgeOf(long holder, long target)
    {
        int i = Grudges.BinarySearch(new Grudge { Holder = holder, Target = target }, GrudgeOrder.Instance);
        return i >= 0 ? Grudges[i] : null;
    }

    public void InsertGrudge(Grudge grudge)
    {
        int i = Grudges.BinarySearch(grudge, GrudgeOrder.Instance);
        if (i >= 0) Grudges[i] = grudge;
        else Grudges.Insert(~i, grudge);
    }

    public CityDeterrence EnsureDeterrence(Guid city)
    {
        foreach (var d in Deterrence) if (d.City == city) return d;
        var fresh = new CityDeterrence { City = city };
        Deterrence.Add(fresh);
        Deterrence.Sort((a, b) => a.City.CompareTo(b.City));
        return fresh;
    }

    private sealed class RecordOrder : IComparer<CriminalRecord>
    {
        public static readonly RecordOrder Instance = new();
        public int Compare(CriminalRecord? a, CriminalRecord? b) => a!.Npc.CompareTo(b!.Npc);
    }

    private sealed class GrudgeOrder : IComparer<Grudge>
    {
        public static readonly GrudgeOrder Instance = new();
        public int Compare(Grudge? a, Grudge? b)
        {
            int c = a!.Holder.CompareTo(b!.Holder);
            return c != 0 ? c : a.Target.CompareTo(b.Target);
        }
    }
}
