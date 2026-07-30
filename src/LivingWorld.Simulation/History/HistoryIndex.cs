using LivingWorld.Domain;

namespace LivingWorld.Simulation.History;

/// <summary>Índice derivado sobre <see cref="Fact"/>s e <see cref="ReportState"/>s (Fase 10,
/// HIST-20/21) — consulta por ano/entidade/tipo sem varrer a base. Reconstruído na rehidratação,
/// nunca serializado.</summary>
public sealed class HistoryIndex
{
    private readonly WorldCalendar _calendar;
    private readonly Dictionary<int, List<FactId>> _factsByYear = [];
    private readonly Dictionary<long, List<FactId>> _factsByEntity = [];
    private readonly Dictionary<WorldEventKind, List<FactId>> _factsByKind = [];
    private readonly Dictionary<int, List<ReportId>> _reportsByYear = [];
    private readonly Dictionary<long, List<ReportId>> _reportsByEntity = [];
    private readonly Dictionary<WorldEventKind, List<ReportId>> _reportsByKind = [];
    private readonly Dictionary<FactId, ReportState> _reportByOriginFact = [];

    private int _lastQueryReads;

    private HistoryIndex(WorldCalendar calendar) => _calendar = calendar;

    /// <summary>Linhas tocadas na última consulta — para prova de complexidade nos testes.</summary>
    public int LastQueryReads => _lastQueryReads;

    public IReadOnlyList<FactId> ByYear(int year)
    {
        _lastQueryReads = 0;
        if (!_factsByYear.TryGetValue(year, out var ids))
            return Array.Empty<FactId>();
        _lastQueryReads = ids.Count;
        return ids;
    }

    public IReadOnlyList<FactId> ByEntity(NpcId id)
    {
        _lastQueryReads = 0;
        if (!_factsByEntity.TryGetValue(id.Value, out var ids))
            return Array.Empty<FactId>();
        _lastQueryReads = ids.Count;
        return ids;
    }

    public IReadOnlyList<FactId> ByKind(WorldEventKind kind)
    {
        _lastQueryReads = 0;
        if (!_factsByKind.TryGetValue(kind, out var ids))
            return Array.Empty<FactId>();
        _lastQueryReads = ids.Count;
        return ids;
    }

    public IReadOnlyList<ReportId> ReportsByYear(int year)
    {
        _lastQueryReads = 0;
        if (!_reportsByYear.TryGetValue(year, out var ids))
            return Array.Empty<ReportId>();
        _lastQueryReads = ids.Count;
        return ids;
    }

    public IReadOnlyList<ReportId> ReportsByEntity(NpcId id)
    {
        _lastQueryReads = 0;
        if (!_reportsByEntity.TryGetValue(id.Value, out var ids))
            return Array.Empty<ReportId>();
        _lastQueryReads = ids.Count;
        return ids;
    }

    public IReadOnlyList<ReportId> ReportsByKind(WorldEventKind kind)
    {
        _lastQueryReads = 0;
        if (!_reportsByKind.TryGetValue(kind, out var ids))
            return Array.Empty<ReportId>();
        _lastQueryReads = ids.Count;
        return ids;
    }

    public static HistoryIndex RebuildFrom(WorldState world)
    {
        var index = new HistoryIndex(world.Calendar);
        foreach (var fact in world.Facts.OrderBy(f => f.Id.Value))
            index.IndexFact(fact);

        foreach (var report in world.Reports.OrderBy(r => r.Id.Value))
            index.IndexReport(report, world);

        foreach (var city in world.Cities.OrderBy(c => c.Id.Value))
        {
            foreach (var report in city.CanonSlots.OrderBy(r => r.Id.Value))
                index.IndexReport(report, world);
        }

        return index;
    }

    internal void OnFactAdded(Fact fact) => IndexFact(fact);

    internal void OnReportAdded(ReportState report, WorldState world) => IndexReport(report, world);

    private void IndexFact(Fact fact)
    {
        int year = YearOf(fact.Tick);
        AddToList(_factsByYear, year, fact.Id);
        AddToList(_factsByKind, fact.Kind, fact.Id);
        foreach (var participant in fact.Participants.OrderBy(p => p.Value))
            AddToList(_factsByEntity, participant.Value, fact.Id);
    }

    private void IndexReport(ReportState report, WorldState world)
    {
        if (_reportByOriginFact.ContainsKey(report.OriginFactId))
            return;

        _reportByOriginFact[report.OriginFactId] = report;
        int year = YearOf(report.CreatedAtTick);
        AddToList(_reportsByYear, year, report.Id);

        var origin = world.FindFact(report.OriginFactId);
        if (origin is null)
            return;

        AddToList(_reportsByKind, origin.Kind, report.Id);
        foreach (var participant in origin.Participants.OrderBy(p => p.Value))
            AddToList(_reportsByEntity, participant.Value, report.Id);
    }

    private int YearOf(long tick) => (int)(tick / _calendar.HoursPerYear);

    private static void AddToList<TKey>(Dictionary<TKey, List<FactId>> map, TKey key, FactId id)
        where TKey : notnull
    {
        if (!map.TryGetValue(key, out var list))
        {
            list = [];
            map[key] = list;
        }
        if (list.Count == 0 || list[^1].Value < id.Value)
            list.Add(id);
    }

    private static void AddToList<TKey>(Dictionary<TKey, List<ReportId>> map, TKey key, ReportId id)
        where TKey : notnull
    {
        if (!map.TryGetValue(key, out var list))
        {
            list = [];
            map[key] = list;
        }
        if (list.Count == 0 || list[^1].Value < id.Value)
            list.Add(id);
    }
}
