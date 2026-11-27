namespace LivingWorld.Simulation.Players;

/// <summary>#243: uma árvore tocada (cortada em parte ou até o toco): a célula, a madeira que tinha ao último corte (<see cref="Left"/>) e o
/// instante a partir do qual ela volta a crescer (<see cref="AtMs"/>, em ms de mundo). A madeira de agora é conta pura de (<c>Left</c>,
/// <c>AtMs</c>, agora): <see cref="TreeLedger.Left"/>; nada varre as árvores por tick.</summary>
public sealed class TreeCut
{
    public int X { get; set; }
    public int Y { get; set; }
    public long Left { get; set; }
    public long AtMs { get; set; }
}

/// <summary>#243: as árvores TOCADAS do mundo (esparso: a árvore natural, sem ninguém ter cortado, vem da regra de <see cref="PlayerNature"/> e
/// não ocupa nada aqui), ordenadas por (linha, coluna). Nulo no mundo enquanto ninguém cortou nada: omitido do snapshot e do hash. A árvore
/// que volta a ter toda a madeira sai do livro.</summary>
public sealed class TreeLedger
{
    public List<TreeCut> Cuts { get; set; } = [];

    private Dictionary<long, TreeCut>? _index;

    private static long Key(int x, int y) => ((long)x << 32) | (uint)y;

    internal TreeCut? Find(int x, int y)
    {
        if (_index is null)
        {
            _index = new Dictionary<long, TreeCut>(Cuts.Count);
            foreach (var cut in Cuts) _index[Key(cut.X, cut.Y)] = cut;
        }
        return _index.GetValueOrDefault(Key(x, y));
    }

    internal TreeCut Touch(int x, int y)
    {
        if (Find(x, y) is { } found) return found;
        var created = new TreeCut { X = x, Y = y };
        int i = Cuts.BinarySearch(created, Order.Instance);
        Cuts.Insert(~i, created);
        _index![Key(x, y)] = created;
        return created;
    }

    internal void Remove(TreeCut cut)
    {
        Cuts.Remove(cut);
        _index?.Remove(Key(cut.X, cut.Y));
    }

    private sealed class Order : IComparer<TreeCut>
    {
        public static readonly Order Instance = new();
        public int Compare(TreeCut? a, TreeCut? b) => a!.Y != b!.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X);
    }
}
