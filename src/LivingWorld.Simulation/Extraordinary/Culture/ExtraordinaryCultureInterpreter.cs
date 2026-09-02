using LivingWorld.Domain;

namespace LivingWorld.Simulation;

/// <summary>Resolve respostas por cultura e assinatura manifestada; o poder não contém julgamento.</summary>
internal static class ExtraordinaryCultureInterpreter
{
    public static IReadOnlyList<(int CultureId, string Manifestation, string Response)> Responses(
        WorldState world, Npc carrier, IReadOnlyList<string> activePowerIds)
    {
        var manifestations = world.Extraordinary.Descriptors
            .Where(descriptor => activePowerIds.Contains(descriptor.Id, StringComparer.Ordinal)
                && ExtraordinaryManifestationCondition.IsMet(descriptor.ManifestationCondition, world, carrier))
            .SelectMany(descriptor => descriptor.Manifestations)
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);
        var observerCultures = world.Npcs
            .Where(npc => npc.IsAlive && npc.City == carrier.City)
            .Select(npc => npc.Culture.Id)
            .Distinct()
            .ToHashSet();

        return world.Extraordinary.CulturalResponses
            .Where(rule => observerCultures.Contains(rule.CultureId) && manifestations.Contains(rule.Manifestation))
            .OrderBy(rule => rule.CultureId)
            .ThenBy(rule => rule.Manifestation, StringComparer.Ordinal)
            .ThenBy(rule => rule.Response, StringComparer.Ordinal)
            .Select(rule => (rule.CultureId, rule.Manifestation, rule.Response))
            .ToList();
    }
}
