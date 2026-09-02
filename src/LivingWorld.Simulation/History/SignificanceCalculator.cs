using LivingWorld.Domain;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.History;

/// <summary>Calcula significância (0–1) na escrita e decide o que vira <see cref="Fact"/>
/// (Fase 10, HIST-01/HIST-02).</summary>
public static class SignificanceCalculator
{
    private static readonly IReadOnlyDictionary<WorldEventKind, double> BaseWeightByKind =
        new Dictionary<WorldEventKind, double>
        {
            [WorldEventKind.Birth] = 0.85,
            [WorldEventKind.Death] = 0.90,
            [WorldEventKind.Starvation] = 0.75,
            [WorldEventKind.Marriage] = 0.80,
            [WorldEventKind.MaternalDeath] = 0.85,
            [WorldEventKind.StillBirth] = 0.70,
            [WorldEventKind.CourtshipSucceeded] = 0.55,
            [WorldEventKind.Hired] = 0.35,
            [WorldEventKind.Fired] = 0.40,
            [WorldEventKind.WageUnpaid] = 0.45,
            [WorldEventKind.Minted] = 0.30,
            [WorldEventKind.Destroyed] = 0.30,
            [WorldEventKind.ResourceLost] = 0.25,
            [WorldEventKind.CourtshipStarted] = 0.20,
            [WorldEventKind.CourtshipRejected] = 0.15,
            [WorldEventKind.FactRecorded] = 1.0,
            [WorldEventKind.ReportConverted] = 0.5,
            [WorldEventKind.CompensatingCorrection] = 1.0,
        };

    /// <summary>Significância determinística a partir do kind, participantes e escopo.</summary>
    public static double Compute(WorldEvent evt, WorldState world, HistoryRules rules)
    {
        if (!rules.Enabled)
            return 0;

        double baseWeight = BaseWeightByKind.GetValueOrDefault(evt.Kind, 0.5);
        int participantCount = ParseParticipants(evt.Kind, evt.Payload).Count;
        double participantFactor = Math.Min(1.0, 0.5 + participantCount * 0.15);
        double scopeFactor = ResolveScopeFactor(evt, world);
        return Math.Clamp(baseWeight * participantFactor * scopeFactor, 0, 1);
    }

    public static bool MeetsThreshold(WorldEvent evt, WorldState world, HistoryRules rules) =>
        rules.Enabled && Compute(evt, world, rules) >= rules.SkeletonSignificanceThreshold;

    /// <summary>Grava um <see cref="Fact"/> íntegro se a significância ≥ limiar; colapsa
    /// (omissão) caso contrário — nunca deleta linha existente.</summary>
    public static Result<Fact> TryRecord(WorldEvent evt, WorldState world, HistoryRules rules)
    {
        if (!rules.Enabled)
            return Result<Fact>.Fail("history_disabled");

        var significance = Compute(evt, world, rules);
        if (significance < rules.SkeletonSignificanceThreshold)
            return Result<Fact>.Fail("collapsed");

        var participants = ParseParticipants(evt.Kind, evt.Payload);
        var location = ResolveLocation(participants, world);
        var fact = new Fact(
            world.NextFactIdAndAdvance(),
            evt.Tick,
            evt.Kind,
            ParseParticipants(evt.Kind, evt.Payload).ToList(),
            location,
            significance,
            evt.Payload);
        world.AddFact(fact);
        return Result<Fact>.Ok(fact);
    }

    internal static IReadOnlyList<NpcId> ParseParticipants(WorldEventKind kind, string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return [];

        var parts = payload.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return kind switch
        {
            WorldEventKind.Birth when parts.Length >= 3 =>
            [
                new NpcId(long.Parse(parts[2])),
                new NpcId(long.Parse(parts[0])),
                new NpcId(long.Parse(parts[1])),
            ],
            WorldEventKind.Marriage or WorldEventKind.CourtshipStarted or WorldEventKind.CourtshipSucceeded
                or WorldEventKind.StillBirth when parts.Length >= 2 =>
            [new NpcId(long.Parse(parts[0])), new NpcId(long.Parse(parts[1]))],
            WorldEventKind.CourtshipRejected when parts.Length >= 3 =>
            [new NpcId(long.Parse(parts[1])), new NpcId(long.Parse(parts[2]))],
            _ when long.TryParse(parts[0], out var single) => [new NpcId(single)],
            _ => [],
        };
    }

    private static double ResolveScopeFactor(WorldEvent evt, WorldState world)
    {
        var participants = ParseParticipants(evt.Kind, evt.Payload);
        if (participants.Count == 0)
            return 0.8;

        int aliveCount = 0;
        foreach (var id in participants)
        {
            if (world.FindNpc(id) is { IsAlive: true })
                aliveCount++;
        }

        return aliveCount > 0 ? 1.0 : 0.85;
    }

    private static CityId? ResolveLocation(IReadOnlyList<NpcId> participants, WorldState world)
    {
        foreach (var id in participants)
        {
            if (world.FindNpc(id) is { } npc && npc.City != default)
                return npc.City;
        }
        return null;
    }
}
