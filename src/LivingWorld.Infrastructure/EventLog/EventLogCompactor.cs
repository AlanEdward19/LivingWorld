using System.Globalization;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Infrastructure.EventLog;

/// <summary>Compactação do event log na fronteira de snapshot (event-log-size-budget): os eventos de ruído (perda de
/// recurso, salário não pago, planta madura) viram um agregado por mês e tipo dentro do lote, com a contagem e a soma do último
/// campo numérico do payload; o resto (história, morte, contratação, nascimento, espólio...) passa byte a byte igual e na
/// mesma ordem. O agregado é gravado como um evento a mais, sem <c>UPDATE</c>: o mesmo tipo do ruído, <c>SourceSystem</c>
/// igual a <see cref="AggregateSource"/> e payload <c>contagem|soma</c>. Determinístico: depende só dos eventos do lote e do
/// calendário.</summary>
public static class EventLogCompactor
{
    public const string AggregateSource = "EventLogCompactor";

    private static readonly HashSet<WorldEventKind> NoiseKinds =
    [
        WorldEventKind.ResourceLost, WorldEventKind.WageUnpaid, WorldEventKind.PlantMatured,
    ];

    /// <summary>Eventos que viram agregado, no tipo dado.</summary>
    public static bool IsNoise(WorldEventKind kind) => NoiseKinds.Contains(kind);

    public static IReadOnlyList<WorldEvent> Compact(IReadOnlyList<WorldEvent> events, WorldCalendar calendar)
    {
        if (events.Count == 0) return events;

        long hoursPerMonth = calendar.HoursPerMonth;
        var aggregates = new SortedDictionary<(long Month, int Kind), (long FirstTick, long Count, long Sum)>();
        var kept = new List<WorldEvent>(events.Count);
        foreach (var evt in events)
        {
            if (!NoiseKinds.Contains(evt.Kind))
            {
                kept.Add(evt);
                continue;
            }

            var key = (evt.Tick / hoursPerMonth, (int)evt.Kind);
            aggregates.TryGetValue(key, out var current);
            aggregates[key] = (
                current.Count == 0 ? evt.Tick : Math.Min(current.FirstTick, evt.Tick),
                current.Count + 1,
                current.Sum + LastNumber(evt.Payload));
        }

        if (aggregates.Count == 0) return events;

        foreach (var ((_, kind), (firstTick, count, sum)) in aggregates)
            kept.Add(new WorldEvent(firstTick, (WorldEventKind)kind, string.Create(CultureInfo.InvariantCulture, $"{count}|{sum}"), SourceSystem: AggregateSource));
        return kept;
    }

    private static long LastNumber(string payload)
    {
        int bar = payload.LastIndexOf('|');
        var tail = bar < 0 ? payload : payload.AsSpan(bar + 1).ToString();
        return long.TryParse(tail, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;
    }
}
