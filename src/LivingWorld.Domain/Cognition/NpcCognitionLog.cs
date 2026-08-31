namespace LivingWorld.Domain;

/// <summary>Entrada de rastro de decisão com tick de gravação (Fase 28, COG-01).</summary>
public sealed record TraceEntry(long Tick, DecisionTrace Trace);

/// <summary>Side-store do rastro de decisão por NPC — ring buffer curto (default 50) ou retenção
/// completa quando watchlisted. Não-canônico; keyed por <see cref="NpcId"/>, fora da entidade
/// <see cref="Npc"/>.</summary>
public sealed class NpcCognitionLog
{
    public const int DefaultWindowSize = 50;

    private readonly Dictionary<long, NpcLogState> _byNpcId = new();
    private readonly int _windowSize;

    public NpcCognitionLog(int windowSize = DefaultWindowSize)
    {
        if (windowSize < 1)
            throw new ArgumentOutOfRangeException(nameof(windowSize), "Window size must be at least 1.");

        _windowSize = windowSize;
    }

    public int WindowSize => _windowSize;

    public void Record(NpcId id, long tick, DecisionTrace trace)
    {
        ArgumentNullException.ThrowIfNull(trace);

        var state = GetOrCreate(id);

        if (state.PostUnmarkCap is int cap)
        {
            state.RecentRing.Add(new TraceEntry(tick, trace));
            TrimPostUnmark(state, cap);
            return;
        }

        if (state.WatchFromTick is long fromTick)
        {
            if (tick > fromTick)
                state.Watchlist.Add(new TraceEntry(tick, trace));
            else
                AddToRing(state.Ring, new TraceEntry(tick, trace), _windowSize);
            return;
        }

        AddToRing(state.Ring, new TraceEntry(tick, trace), _windowSize);
    }

    public IReadOnlyList<TraceEntry> RecentEntries(NpcId id, int count)
    {
        if (count <= 0)
            return Array.Empty<TraceEntry>();

        if (!_byNpcId.TryGetValue(id.Value, out var state))
            return Array.Empty<TraceEntry>();

        var combined = Combine(state);
        if (combined.Count == 0)
            return Array.Empty<TraceEntry>();

        int take = Math.Min(count, combined.Count);
        return combined.GetRange(combined.Count - take, take);
    }

    public void MarkWatchlisted(NpcId id, long fromTick)
    {
        var state = GetOrCreate(id);
        state.WatchFromTick = fromTick;
        state.PostUnmarkCap = null;
        state.Watchlist.Clear();
        state.Preserved.Clear();
        state.RecentRing.Clear();

        state.Ring.RemoveAll(entry => entry.Tick > fromTick);
        TrimFifo(state.Ring, _windowSize);
    }

    public void Unmark(NpcId id)
    {
        if (!_byNpcId.TryGetValue(id.Value, out var state) || state.WatchFromTick is null)
            return;

        state.Preserved.Clear();
        state.Preserved.AddRange(Combine(state));
        state.Ring.Clear();
        state.Watchlist.Clear();
        state.RecentRing.Clear();
        state.WatchFromTick = null;
        state.PostUnmarkCap = Math.Max(_windowSize, state.Preserved.Count);
    }

    public bool IsWatchlisted(NpcId id) =>
        _byNpcId.TryGetValue(id.Value, out var state) && state.WatchFromTick is not null;

    private NpcLogState GetOrCreate(NpcId id)
    {
        if (!_byNpcId.TryGetValue(id.Value, out var state))
        {
            state = new NpcLogState();
            _byNpcId[id.Value] = state;
        }

        return state;
    }

    private static List<TraceEntry> Combine(NpcLogState state)
    {
        if (state.PostUnmarkCap is not null)
        {
            var combined = new List<TraceEntry>(state.Preserved.Count + state.RecentRing.Count);
            combined.AddRange(state.Preserved);
            combined.AddRange(state.RecentRing);
            return combined;
        }

        var entries = new List<TraceEntry>(state.Ring.Count + state.Watchlist.Count);
        entries.AddRange(state.Ring);
        entries.AddRange(state.Watchlist);
        return entries;
    }

    private static void TrimPostUnmark(NpcLogState state, int cap)
    {
        while (state.Preserved.Count + state.RecentRing.Count > cap)
        {
            if (state.Preserved.Count > 0)
                state.Preserved.RemoveAt(0);
            else
                state.RecentRing.RemoveAt(0);
        }
    }

    private static void AddToRing(List<TraceEntry> ring, TraceEntry entry, int maxCount)
    {
        ring.Add(entry);
        TrimFifo(ring, maxCount);
    }

    private static void TrimFifo(List<TraceEntry> entries, int maxCount)
    {
        while (entries.Count > maxCount)
            entries.RemoveAt(0);
    }

    private sealed class NpcLogState
    {
        public List<TraceEntry> Ring { get; } = [];
        public List<TraceEntry> Watchlist { get; } = [];
        public List<TraceEntry> Preserved { get; } = [];
        public List<TraceEntry> RecentRing { get; } = [];
        public long? WatchFromTick { get; set; }
        public int? PostUnmarkCap { get; set; }
    }
}
