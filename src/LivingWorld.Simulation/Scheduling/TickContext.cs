using LivingWorld.Domain.History;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.History;

namespace LivingWorld.Simulation.Scheduling;

/// <summary>Fachada que um sistema recebe a cada tick: RNG por stream, agendamento de eventos
/// futuros e o tick atual. Nunca o relógio da máquina.</summary>
public sealed class TickContext(WorldState world, WorldRngRegistry rng, EventScheduler scheduler, IWorldEventSink? sink = null)
{
    public long CurrentTick => world.CurrentDate.TotalHours;

    /// <summary>Registra um evento de história — sem-op no sink se ninguém persiste,
    /// mas sempre gera <see cref="WorldEvent.EventId"/>. Compat: call sites legados
    /// usam <c>SourceSystem = "Unknown"</c> e sem causa.</summary>
    public long LogEvent(WorldEventKind kind, string payload) =>
        LogEvent(kind, payload, sourceSystem: "Unknown", causeEventId: null);

    public long LogEvent(WorldEventKind kind, string payload, string sourceSystem, long? causeEventId = null)
    {
        var eventId = world.NextHistoryEventIdAndAdvance();
        // Record before witness-loss handling, even when there is no external sink.
        if (world.HistoryRules.Enabled && kind is not (WorldEventKind.FactRecorded
            or WorldEventKind.ReportConverted or WorldEventKind.CompensatingCorrection
            or WorldEventKind.PossessionResisted or WorldEventKind.NpcInstantiated)
            && SignificanceCalculator.CanMeetThreshold(kind, world.HistoryRules))
            SignificanceCalculator.TryRecord(
                new WorldEvent(CurrentTick, kind, payload, eventId, causeEventId, sourceSystem), world, world.HistoryRules);
        sink?.Record(new WorldEvent(CurrentTick, kind, payload, eventId, causeEventId, sourceSystem));
        return eventId;
    }

    /// <summary>RNG derivado do stream desta chave. Streams independentes:
    /// consumir um stream novo não desloca a sequência dos outros.</summary>
    public WorldRng Rng(string streamKey) => rng.Stream(streamKey);

    public WorldRng StreamFor(string purpose, long id) => rng.StreamFor(purpose, id);

    /// <summary>Agenda um evento futuro para o sistema <paramref name="systemName"/>. O ID é
    /// atribuído pelo <see cref="WorldState"/> — monotônico e determinístico entre processos.</summary>
    public ScheduledEvent ScheduleEvent(long targetTick, string systemName, string? payload = null)
    {
        var evt = new ScheduledEvent(world.NextEventIdAndAdvance(), targetTick, systemName, payload);
        scheduler.Schedule(evt);
        return evt;
    }

    public bool CancelEvent(long id) => scheduler.Cancel(id);
}
