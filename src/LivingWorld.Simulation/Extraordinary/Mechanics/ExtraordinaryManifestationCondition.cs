using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Population;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Extraordinary.Mechanics;

/// <summary>Condições genéricas sobre relógio e estado existente; não conhece arquétipos.</summary>
internal static class ExtraordinaryManifestationCondition
{
    public static bool IsMet(string? condition, WorldState world, Npc carrier)
    {
        if (string.IsNullOrWhiteSpace(condition)) return true;
        if (condition == "world:is-night")
            return world.CurrentDate.Hour is >= 18 or < 6;

        const string hourPrefix = "world:hour-range:";
        if (condition.StartsWith(hourPrefix, StringComparison.Ordinal))
        {
            var bounds = condition[hourPrefix.Length..].Split('-', StringSplitOptions.TrimEntries);
            if (bounds.Length != 2 || !int.TryParse(bounds[0], out int start)
                || !int.TryParse(bounds[1], out int end) || start is < 0 or > 23 || end is < 0 or > 23)
                return false;
            int hour = world.CurrentDate.Hour;
            return start <= end ? hour >= start && hour <= end : hour >= start || hour <= end;
        }

        const string cyclePrefix = "world:tick-cycle:";
        if (condition.StartsWith(cyclePrefix, StringComparison.Ordinal))
        {
            var parts = condition[cyclePrefix.Length..].Split(':', StringSplitOptions.TrimEntries);
            if (parts.Length != 3 || !long.TryParse(parts[0], out long period)
                || !long.TryParse(parts[1], out long start) || !long.TryParse(parts[2], out long duration)
                || period <= 0 || start < 0 || start >= period || duration <= 0 || duration > period)
                return false;
            long position = world.CurrentDate.TotalHours % period;
            long end = (start + duration) % period;
            return start + duration <= period
                ? position >= start && position < start + duration
                : position >= start || position < end;
        }

        const string actionPrefix = "carrier:action:";
        return condition.StartsWith(actionPrefix, StringComparison.Ordinal)
            && Enum.TryParse<ActionType>(condition[actionPrefix.Length..], ignoreCase: true, out var action)
            && carrier.CurrentAction == action;
    }
}
