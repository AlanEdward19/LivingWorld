using System.Text.Json.Serialization;
using LivingWorld.Domain.Geography;

namespace LivingWorld.Simulation.Cities.Streets;

/// <summary>Superfície do chão (AD-176), da mais pobre à mais cara. Trilha e terra batida nascem do pisoteio; cascalho e pedra,
/// só de obra.</summary>
public enum GroundSurface : byte { None = 0, Trail = 1, Dirt = 2, Gravel = 3, Stone = 4 }

/// <summary>Uma célula do chão com algo a guardar (linha do snapshot): pisoteio acumulado, passagens do último mês, superfície em
/// vigor, calçamento (cascalho/pedra) e a condição dele em milésimos.</summary>
public sealed class GroundCellRow
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Wear { get; set; }
    public int Passes { get; set; }
    public GroundSurface Surface { get; set; }
    public GroundSurface Paving { get; set; }
    public int Condition { get; set; }
}

/// <summary>Viagens feitas de uma célula a outra desde o último mês (o pisoteio sai delas no passo mensal).</summary>
public sealed class GroundTripRow
{
    public long Key { get; set; }
    public int Count { get; set; }
}

/// <summary>Obra de calçamento (AD-176): células, o que vira (cascalho ou pedra), horas de trabalho que faltam (material e
/// transporte inclusos), quem paga (o tesouro da cidade ou as casas da frente) e quanto já foi pago.</summary>
public sealed class PavingWork
{
    public long Id { get; set; }
    public Guid City { get; set; }
    public GroundSurface Surface { get; set; }
    public bool Repair { get; set; }
    public List<int> Cells { get; set; } = [];
    public long HoursTotal { get; set; }
    public long HoursLeft { get; set; }
    public long StartTick { get; set; }
    /// <summary>Casas da frente que pagam a parte delas (vazio = o tesouro da cidade paga).</summary>
    public List<long> Payers { get; set; } = [];
    public long Paid { get; set; }
}

/// <summary>Estado do chão das cidades (AD-176), canônico: pisoteio por célula, superfície em vigor, calçamento e obras. Grade
/// densa por dentro (caminho quente), linhas esparsas no snapshot. A superfície só muda na revisão (de
/// <see cref="LivingWorld.Domain.Cities.StreetRules.ReviewMonths"/> em <see cref="LivingWorld.Domain.Cities.StreetRules.ReviewMonths"/>
/// meses) e os prédios só viram obstáculo da rota na virada do mês (<see cref="ObstacleTick"/>): entre uma e outra, a rede de
/// caminhos é fixa e a rota de cada par fica em cache.</summary>
public sealed class GroundLedger
{
    private int[] _wear = [];
    private int[] _passes = [];
    private byte[] _surface = [];
    private byte[] _paving = [];
    private short[] _condition = [];
    private readonly Dictionary<long, int> _trips = [];
    private List<GroundCellRow>? _pendingRows;

    public GroundLedger() { }

    public GroundLedger(int width, int height, long tick)
    {
        Width = width;
        Height = height;
        ObstacleTick = tick;
        Allocate();
    }

    public int Width { get; set; }
    public int Height { get; set; }

    /// <summary>Prédios concluídos até este tick são obstáculo da rota (revisto a cada mês).</summary>
    public long ObstacleTick { get; set; }

    /// <summary>Muda a cada revisão que mudou alguma superfície (a rede de caminhos).</summary>
    public int SurfaceVersion { get; set; }

    /// <summary>Meses contados desde a criação (a revisão da superfície cai a cada <c>ReviewMonths</c>).</summary>
    public int Months { get; set; }

    public long NextWorkId { get; set; }

    /// <summary>Obras de calçamento concluídas (novas e consertos).</summary>
    public int WorksDone { get; set; }

    public List<PavingWork> Works { get; set; } = [];

    public List<GroundCellRow> Cells
    {
        get
        {
            EnsureArrays();
            var rows = new List<GroundCellRow>();
            for (int i = 0; i < _wear.Length; i++)
                if (_wear[i] != 0 || _passes[i] != 0 || _surface[i] != 0 || _paving[i] != 0)
                    rows.Add(new GroundCellRow
                    {
                        X = i % Width, Y = i / Width, Wear = _wear[i], Passes = _passes[i], Surface = (GroundSurface)_surface[i],
                        Paving = (GroundSurface)_paving[i], Condition = _condition[i],
                    });
            return rows;
        }
        set => _pendingRows = value;
    }

    public List<GroundTripRow> Trips
    {
        get => [.. _trips.OrderBy(t => t.Key).Select(t => new GroundTripRow { Key = t.Key, Count = t.Value })];
        set
        {
            _trips.Clear();
            foreach (var row in value) _trips[row.Key] = row.Count;
        }
    }

    private void Allocate()
    {
        int n = Math.Max(0, Width * Height);
        _wear = new int[n];
        _passes = new int[n];
        _surface = new byte[n];
        _paving = new byte[n];
        _condition = new short[n];
    }

    private void EnsureArrays()
    {
        if (_wear.Length == Width * Height && _pendingRows is null) return;
        if (_wear.Length != Width * Height) Allocate();
        if (_pendingRows is { } rows)
        {
            _pendingRows = null;
            foreach (var row in rows)
            {
                if (!InMap(row.X, row.Y)) continue;
                int i = row.Y * Width + row.X;
                _wear[i] = row.Wear;
                _passes[i] = row.Passes;
                _surface[i] = (byte)row.Surface;
                _paving[i] = (byte)row.Paving;
                _condition[i] = (short)row.Condition;
            }
        }
    }

    internal bool InMap(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

    internal int IndexOf(CellCoord c) => InMap(c.X, c.Y) ? c.Y * Width + c.X : -1;

    internal CellCoord CellOf(int index) => new(index % Width, index / Width);

    internal int Count { get { EnsureArrays(); return _wear.Length; } }

    internal GroundSurface SurfaceAt(int index) { EnsureArrays(); return (GroundSurface)_surface[index]; }

    internal GroundSurface SurfaceAt(CellCoord c) => IndexOf(c) is var i and >= 0 ? SurfaceAt(i) : GroundSurface.None;

    internal int WearAt(int index) { EnsureArrays(); return _wear[index]; }

    internal int PassesAt(int index) { EnsureArrays(); return _passes[index]; }

    internal GroundSurface PavingAt(int index) { EnsureArrays(); return (GroundSurface)_paving[index]; }

    internal int ConditionAt(int index) { EnsureArrays(); return _condition[index]; }

    internal ReadOnlySpan<byte> Surfaces { get { EnsureArrays(); return _surface; } }

    /// <summary>Uma viagem que chegou (de origem a destino, em células do mapa).</summary>
    internal void RecordTrip(CellCoord from, CellCoord to)
    {
        int a = IndexOf(from), b = IndexOf(to);
        if (a < 0 || b < 0 || a == b) return;
        long key = ((long)a << 32) | (uint)b;
        _trips[key] = _trips.GetValueOrDefault(key) + 1;
    }

    internal int PendingTripPairs => _trips.Count;

    /// <summary>As viagens do mês, em ordem de chave, e esvazia a conta.</summary>
    internal List<(int From, int To, int Count)> DrainTrips()
    {
        var list = new List<(int, int, int)>(_trips.Count);
        foreach (var (key, count) in _trips.OrderBy(t => t.Key))
            list.Add(((int)(key >> 32), (int)(key & 0xFFFFFFFF), count));
        _trips.Clear();
        return list;
    }

    /// <summary>Começa o mês: zera as passagens do mês anterior.</summary>
    internal void ClearPasses() { EnsureArrays(); Array.Clear(_passes); }

    internal void AddPasses(int index, int count)
    {
        EnsureArrays();
        _passes[index] += count;
        _wear[index] += count;
    }

    internal void SetWear(int index, int wear) { EnsureArrays(); _wear[index] = wear; }

    internal void SetSurface(int index, GroundSurface surface) { EnsureArrays(); _surface[index] = (byte)surface; }

    internal void SetPaving(int index, GroundSurface paving, int condition)
    {
        EnsureArrays();
        _paving[index] = (byte)paving;
        _condition[index] = (short)Math.Clamp(condition, 0, 1000);
    }

    /// <summary>Célula sob obra em andamento (não entra em outra obra).</summary>
    internal bool UnderWork(int index)
    {
        foreach (var work in Works)
            if (work.Cells.Contains(index)) return true;
        return false;
    }
}
