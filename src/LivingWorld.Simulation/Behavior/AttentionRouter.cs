using LivingWorld.Domain;
using System.Collections.Immutable;

namespace LivingWorld.Simulation.Behavior;

/// <summary>Roteia wakes só para NPCs relevantes a um <see cref="WorldEvent"/> (Fase 16.3 P2a,
/// COH-43 / doc#59) — localização, household, relação, dependência de intent, magnitude
/// econômica, ameaça. Baixa magnitude NÃO acorda a cidade inteira.</summary>
public static class AttentionRouter
{
    /// <summary>Payload econômico de variação de preço:
    /// <c>price-change|{magnitude}|{x}|{y}|{householdId?}</c>.</summary>
    public const string PriceChangePrefix = "price-change|";

    /// <summary>Payload de ameaça local: <c>threat|{x}|{y}</c>.</summary>
    public const string ThreatPrefix = "threat|";

    /// <summary>Payload de household afetado: <c>household|{householdId}</c>.</summary>
    public const string HouseholdPrefix = "household|";

    /// <summary>Payload de NPC sujeito: <c>npc|{npcId}</c>.</summary>
    public const string NpcPrefix = "npc|";

    public static IReadOnlySet<NpcId> RouteRelevantNpcs(
        WorldState world, WorldEvent evt, AttentionRules rules)
    {
        rules = AttentionRules.Resolve(rules);
        if (!rules.Enabled)
            return ImmutableHashSet<NpcId>.Empty;

        var ids = new SortedSet<long>();

        if (TryParsePriceChange(evt.Payload, out var magnitude, out var priceCell, out var priceHousehold))
        {
            RouteEconomic(world, rules, magnitude, priceCell, priceHousehold, ids);
            return ToSet(ids);
        }

        if (TryParseThreat(evt.Payload, out var threatCell))
        {
            RouteByLocation(world, threatCell, rules.ThreatRadiusCells, ids);
            RouteByCapabilityNearThreat(world, threatCell, rules, ids);
            return ToSet(ids);
        }

        if (TryParseHousehold(evt.Payload, out var householdId))
        {
            RouteHouseholdMembers(world, householdId, ids);
            RouteIntentDependentsOnHousehold(world, householdId, ids);
            return ToSet(ids);
        }

        if (TryParseNpc(evt.Payload, out var subjectId))
        {
            ids.Add(subjectId.Value);
            RouteRelated(world, subjectId, rules, ids);
            RouteSameHousehold(world, subjectId, ids);
            return ToSet(ids);
        }

        // Fallback: eventos genéricos com coordenadas em payload "x|y|..." — só vizinhos.
        if (TryParseCell(evt.Payload, out var cell))
            RouteByLocation(world, cell, rules.MaxLocationDistanceCells, ids);

        return ToSet(ids);
    }

    private static void RouteEconomic(
        WorldState world, AttentionRules rules, double magnitude, CellCoord? cell,
        HouseholdId? householdId, SortedSet<long> ids)
    {
        // Magnitude baixa: NÃO varre a cidade — só dependentes de intent Buy/Eat e household.
        if (magnitude < rules.MinPriceChangeMagnitude)
        {
            foreach (var npc in world.Npcs)
            {
                if (!npc.IsAlive) continue;
                if (npc.IntentStatus == IntentStatus.Active
                    && npc.CurrentIntent is ActionType.Buy or ActionType.Eat)
                    ids.Add(npc.Id.Value);
            }

            if (householdId is { } hid)
                RouteHouseholdMembers(world, hid, ids);
            return;
        }

        // Magnitude alta: dependentes econômicos (intent alimentar) + localidade + household.
        foreach (var npc in world.Npcs)
        {
            if (!npc.IsAlive) continue;
            if (npc.IntentStatus == IntentStatus.Active
                && npc.CurrentIntent is ActionType.Buy or ActionType.Eat)
                ids.Add(npc.Id.Value);
        }

        if (cell is { } c)
            RouteByLocation(world, c, rules.MaxLocationDistanceCells, ids);
        if (householdId is { } h)
            RouteHouseholdMembers(world, h, ids);
    }

    private static void RouteByLocation(
        WorldState world, CellCoord origin, int radius, SortedSet<long> ids)
    {
        foreach (var npc in world.Npcs)
        {
            if (!npc.IsAlive) continue;
            if (Chebyshev(npc.CurrentLocation, origin) <= radius)
                ids.Add(npc.Id.Value);
        }
    }

    private static void RouteHouseholdMembers(WorldState world, HouseholdId householdId, SortedSet<long> ids)
    {
        if (world.FindHousehold(householdId) is not { } household) return;
        foreach (var memberId in household.Members)
            ids.Add(memberId.Value);
    }

    private static void RouteSameHousehold(WorldState world, NpcId subjectId, SortedSet<long> ids)
    {
        if (world.FindNpc(subjectId) is not { Household: { } hid }) return;
        RouteHouseholdMembers(world, hid, ids);
    }

    private static void RouteRelated(
        WorldState world, NpcId subjectId, AttentionRules rules, SortedSet<long> ids)
    {
        foreach (var (key, rel) in world.Relationships)
        {
            if (key.From != subjectId && key.To != subjectId) continue;
            double strength = Math.Max(
                Math.Max(rel.Trust, rel.Affection),
                Math.Max(rel.Respect, rel.Debt));
            if (strength < rules.MinRelationshipStrength) continue;
            var other = key.From == subjectId ? key.To : key.From;
            ids.Add(other.Value);
        }
    }

    private static void RouteIntentDependentsOnHousehold(
        WorldState world, HouseholdId householdId, SortedSet<long> ids)
    {
        foreach (var npc in world.Npcs)
        {
            if (!npc.IsAlive) continue;
            if (npc.Household != householdId) continue;
            if (npc.IntentStatus == IntentStatus.Active)
                ids.Add(npc.Id.Value);
        }
    }

    private static void RouteByCapabilityNearThreat(
        WorldState world, CellCoord threatCell, AttentionRules rules, SortedSet<long> ids)
    {
        // Interação de capacidade: NPCs com Intent UsePower / PendingPower perto da ameaça.
        foreach (var npc in world.Npcs)
        {
            if (!npc.IsAlive) continue;
            if (Chebyshev(npc.CurrentLocation, threatCell) > rules.ThreatRadiusCells) continue;
            if (npc.CurrentIntent == ActionType.UsePower || npc.PendingPowerInvocation is not null)
                ids.Add(npc.Id.Value);
        }
    }

    private static int Chebyshev(CellCoord a, CellCoord b) =>
        Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

    private static IReadOnlySet<NpcId> ToSet(SortedSet<long> ids) =>
        ids.Count == 0
            ? ImmutableHashSet<NpcId>.Empty
            : ids.Select(id => new NpcId(id)).ToImmutableHashSet();

    private static bool TryParsePriceChange(
        string payload, out double magnitude, out CellCoord? cell, out HouseholdId? householdId)
    {
        magnitude = 0;
        cell = null;
        householdId = null;
        if (!payload.StartsWith(PriceChangePrefix, StringComparison.Ordinal)) return false;
        var parts = payload.Split('|');
        // price-change|magnitude|x|y|householdId?
        if (parts.Length < 4) return false;
        if (!double.TryParse(parts[1], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out magnitude))
            return false;
        if (!int.TryParse(parts[2], out int x) || !int.TryParse(parts[3], out int y))
            return false;
        cell = new CellCoord(x, y);
        if (parts.Length >= 5 && long.TryParse(parts[4], out long hid))
            householdId = new HouseholdId(hid);
        return true;
    }

    private static bool TryParseThreat(string payload, out CellCoord cell)
    {
        cell = default;
        if (!payload.StartsWith(ThreatPrefix, StringComparison.Ordinal)) return false;
        var parts = payload.Split('|');
        if (parts.Length < 3) return false;
        if (!int.TryParse(parts[1], out int x) || !int.TryParse(parts[2], out int y))
            return false;
        cell = new CellCoord(x, y);
        return true;
    }

    private static bool TryParseHousehold(string payload, out HouseholdId householdId)
    {
        householdId = default;
        if (!payload.StartsWith(HouseholdPrefix, StringComparison.Ordinal)) return false;
        var parts = payload.Split('|');
        if (parts.Length < 2 || !long.TryParse(parts[1], out long id)) return false;
        householdId = new HouseholdId(id);
        return true;
    }

    private static bool TryParseNpc(string payload, out NpcId npcId)
    {
        npcId = default;
        if (!payload.StartsWith(NpcPrefix, StringComparison.Ordinal)) return false;
        var parts = payload.Split('|');
        if (parts.Length < 2 || !long.TryParse(parts[1], out long id)) return false;
        npcId = new NpcId(id);
        return true;
    }

    private static bool TryParseCell(string payload, out CellCoord cell)
    {
        cell = default;
        var parts = payload.Split('|');
        if (parts.Length < 2) return false;
        if (!int.TryParse(parts[0], out int x) || !int.TryParse(parts[1], out int y))
            return false;
        cell = new CellCoord(x, y);
        return true;
    }
}
