using LivingWorld.Domain.History;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.History.Causality;

/// <summary>Diagnósticos sobre cadeias <see cref="WorldEvent.CauseEventId"/> (COH-62 / doc#30–31).
/// Reusa o mesmo <c>maxDepth</c> guard de <see cref="CausalProvenance"/>.</summary>
public static class CausalDiagnostics
{
    /// <summary>Número de passos CauseEventId da raiz até <paramref name="eventId"/>
    /// (0 se o evento é raiz ou inexistente).</summary>
    public static int CausalDepth(
        IReadOnlyList<WorldEvent> events, long eventId, int maxDepth)
    {
        if (maxDepth <= 0)
            throw new CausalChainTooDeepException(eventId, maxDepth);

        var byId = IndexById(events);
        if (!byId.TryGetValue(eventId, out var current))
            return 0;

        var visited = new HashSet<long> { current.EventId };
        int depth = 0;

        while (current.CauseEventId is long causeId)
        {
            depth++;
            if (depth > maxDepth || !visited.Add(causeId))
                throw new CausalChainTooDeepException(current.EventId, maxDepth);

            if (!byId.TryGetValue(causeId, out var cause))
                return depth;

            current = cause;
        }

        return depth;
    }

    public static int CausalDepth(
        IReadOnlyList<WorldEvent> events, long eventId, CausalRules rules) =>
        CausalDepth(events, eventId, rules.MaxCauseChainDepth);

    /// <summary>Conjunto de <see cref="WorldEvent.SourceSystem"/> na cadeia de
    /// <paramref name="eventId"/> até a raiz (inclusive).</summary>
    public static IReadOnlySet<string> SystemsTouchedByCausalChain(
        IReadOnlyList<WorldEvent> events, long eventId, int maxDepth)
    {
        if (maxDepth <= 0)
            throw new CausalChainTooDeepException(eventId, maxDepth);

        var byId = IndexById(events);
        var systems = new SortedSet<string>(StringComparer.Ordinal);
        if (!byId.TryGetValue(eventId, out var current))
            return systems;

        var visited = new HashSet<long> { current.EventId };
        systems.Add(current.SourceSystem);
        int depth = 0;

        while (current.CauseEventId is long causeId)
        {
            depth++;
            if (depth > maxDepth || !visited.Add(causeId))
                throw new CausalChainTooDeepException(current.EventId, maxDepth);

            if (!byId.TryGetValue(causeId, out var cause))
                break;

            current = cause;
            systems.Add(current.SourceSystem);
        }

        return systems;
    }

    public static IReadOnlySet<string> SystemsTouchedByCausalChain(
        IReadOnlyList<WorldEvent> events, long eventId, CausalRules rules) =>
        SystemsTouchedByCausalChain(events, eventId, rules.MaxCauseChainDepth);

    private static Dictionary<long, WorldEvent> IndexById(IReadOnlyList<WorldEvent> events)
    {
        var byId = new Dictionary<long, WorldEvent>(events.Count);
        foreach (var evt in events)
            byId[evt.EventId] = evt;
        return byId;
    }
}
