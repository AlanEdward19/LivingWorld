using LivingWorld.Domain;

namespace LivingWorld.Simulation;

/// <summary>Fachada que um sistema recebe a cada tick: RNG por stream, agendamento de eventos
/// futuros e o tick atual. Nunca o relógio da máquina.</summary>
public sealed class TickContext(WorldState world, WorldRngRegistry rng, EventScheduler scheduler, IWorldEventSink? sink = null)
{
    public long CurrentTick => world.CurrentDate.TotalHours;

    /// <summary>Registra um evento de história (task 8/10) — sem-op no sink se ninguém persiste,
    /// mas sempre minta <see cref="WorldEvent.EventId"/> (COH-01). Compat: call sites legados
    /// usam <c>SourceSystem = "Unknown"</c> e sem causa.</summary>
    public long LogEvent(WorldEventKind kind, string payload) =>
        LogEvent(kind, payload, sourceSystem: "Unknown", causeEventId: null);

    /// <summary>Registra evento com proveniência causal explícita (COH-01/COH-03).</summary>
    public long LogEvent(WorldEventKind kind, string payload, string sourceSystem, long? causeEventId = null)
    {
        var eventId = world.NextHistoryEventIdAndAdvance();
        sink?.Record(new WorldEvent(CurrentTick, kind, payload, eventId, causeEventId, sourceSystem));
        return eventId;
    }

    /// <summary>RNG derivado do stream desta chave (ADR-0005). Streams independentes:
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
