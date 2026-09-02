using LivingWorld.Domain.History;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.History.Causality;

/// <summary>Resolução sob demanda de <c>RootCauseEventId</c> (COH-02) — percorre
/// <see cref="WorldEvent.CauseEventId"/> sem grafo persistido à parte.</summary>
public static class CausalProvenance
{
    public static long? ResolveRootCauseEventId(
        IReadOnlyList<WorldEvent> events, long eventId, int maxDepth)
    {
        if (maxDepth <= 0)
            throw new CausalChainTooDeepException(eventId, maxDepth);

        var byId = new Dictionary<long, WorldEvent>(events.Count);
        foreach (var evt in events)
            byId[evt.EventId] = evt;

        if (!byId.TryGetValue(eventId, out var current))
            return null;

        var visited = new HashSet<long> { current.EventId };
        int depth = 0;

        while (current.CauseEventId is long causeId)
        {
            depth++;
            if (depth > maxDepth || !visited.Add(causeId))
                throw new CausalChainTooDeepException(current.EventId, maxDepth);

            if (!byId.TryGetValue(causeId, out var cause))
                return causeId;

            current = cause;
        }

        return current.EventId;
    }

    public static long? ResolveRootCauseEventId(
        IReadOnlyList<WorldEvent> events, long eventId, CausalRules rules) =>
        ResolveRootCauseEventId(events, eventId, rules.MaxCauseChainDepth);
}
