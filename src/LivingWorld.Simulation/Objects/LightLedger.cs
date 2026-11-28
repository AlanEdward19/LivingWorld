namespace LivingWorld.Simulation.Objects;

/// <summary>#258: uma luz ligada: o prédio e a peça (índice na mobília do tipo do prédio, o mesmo de <see cref="HomeFurniture"/>).</summary>
public sealed class LightOn
{
    public long Building { get; set; }
    public int Piece { get; set; }
}

/// <summary>#258: as luzes LIGADAS do mundo (esparso: luz apagada não ocupa nada), ordenadas por (prédio, peça). Nulo no mundo enquanto ninguém ligou
/// nada: omitido do snapshot e do hash. O estado é do motor (quem liga e desliga é o uso do objeto, de jogador ou de NPC); o cliente só acende o que
/// o motor diz que está ligado.</summary>
public sealed class LightLedger
{
    public List<LightOn> On { get; set; } = [];

    public bool IsOn(long building, int piece)
    {
        foreach (var light in On)
            if (light.Building == building && light.Piece == piece) return true;
        return false;
    }

    /// <summary>Liga ou desliga; devolve se o estado mudou.</summary>
    public bool Set(long building, int piece, bool on)
    {
        int i = On.FindIndex(l => l.Building == building && l.Piece == piece);
        if (on == (i >= 0)) return false;
        if (!on) { On.RemoveAt(i); return true; }
        var created = new LightOn { Building = building, Piece = piece };
        int at = On.BinarySearch(created, Order.Instance);
        On.Insert(~at, created);
        return true;
    }

    private sealed class Order : IComparer<LightOn>
    {
        public static readonly Order Instance = new();
        public int Compare(LightOn? a, LightOn? b) => a!.Building != b!.Building ? a.Building.CompareTo(b.Building) : a.Piece.CompareTo(b.Piece);
    }
}
