using LivingWorld.Domain.History;
using LivingWorld.Domain.History.Distortion;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.History.Books;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.History.Distortion;

/// <summary>Converte fato em relato hop-0 quando a última testemunha morre (Fase 10, HIST-03)
/// — via <see cref="EventScheduler"/>, nunca varredura por tick.</summary>
public sealed class FactToReportConversionScheduler : ISimulationSystem
{
    public const string SystemName = "history-fact-to-report";

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Hourly;

    public void Tick(WorldState world, TickContext ctx)
    {
    }

    public void HandleEvent(WorldState world, TickContext ctx, ScheduledEvent evt)
    {
        if (!world.HistoryRules.Enabled) return;

        var parts = evt.Payload!.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2) return;

        var factId = new FactId(long.Parse(parts[0]));
        var communityId = new CityId(Guid.Parse(parts[1]));
        Convert(world, ctx, factId, communityId);
    }

    public static void OnWitnessDied(NpcId witnessId, WorldState world, TickContext ctx)
    {
        if (!world.HistoryRules.Enabled) return;

        foreach (var fact in world.Facts.OrderBy(f => f.Id.Value))
        {
            if (!fact.Participants.Contains(witnessId)) continue;
            if (LivingMemoryWindow.HasLivingWitness(fact, world)) continue;

            foreach (var communityId in ResolveCommunities(fact, world))
            {
                string payload = $"{fact.Id.Value}|{communityId.Value}";
                ctx.ScheduleEvent(ctx.CurrentTick, SystemName, payload);
            }
        }
    }

    internal static void Convert(WorldState world, TickContext ctx, FactId factId, CityId communityId)
    {
        var fact = world.FindFact(factId);
        var city = world.FindCity(communityId);
        if (fact is null || city is null) return;

        var report = new ReportState(
            world.NextReportIdAndAdvance(),
            factId,
            communityId,
            TransmissionMediumType.OralTradition,
            HopCount: 0,
            Weight: fact.Significance,
            CreatedAtTick: ctx.CurrentTick,
            LastHopTick: ctx.CurrentTick);

        CanonSlotManager.Admit(city, report, world.HistoryRules, ctx.CurrentTick);
        world.RegisterReport(report);
        ctx.LogEvent(
            WorldEventKind.ReportConverted,
            $"{report.Id.Value}|{factId.Value}|{communityId.Value}", sourceSystem: "FactToReportConversionScheduler");
    }

    private static IEnumerable<CityId> ResolveCommunities(Fact fact, WorldState world)
    {
        var communities = new SortedSet<CityId>(
            Comparer<CityId>.Create((left, right) => left.Value.CompareTo(right.Value)));
        if (fact.Location is { } location)
            communities.Add(location);

        foreach (var participant in fact.Participants.OrderBy(p => p.Value))
        {
            if (world.FindNpc(participant) is { } npc && npc.City != default)
                communities.Add(npc.City);
        }

        return communities;
    }
}
