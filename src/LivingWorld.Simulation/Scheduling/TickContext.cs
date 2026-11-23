using LivingWorld.Domain.History;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.History;

namespace LivingWorld.Simulation.Scheduling;

/// <summary>Fachada que um sistema recebe a cada tick: RNG por stream, agendamento de eventos
/// futuros e o tick atual. Nunca o relógio da máquina.</summary>
public sealed class TickContext(WorldState world, WorldRngRegistry rng, EventScheduler scheduler, IWorldEventSink? sink = null)
{
    /// <summary>A hora do calendário em que o mundo está (ADR-0050: o tempo é guardado em segundos; os sistemas de fronteira rodam
    /// na hora cheia e contam em horas).</summary>
    public long CurrentTick => world.CurrentDate.TotalHours;

    /// <summary>O instante do mundo em segundos (ADR-0050).</summary>
    public long CurrentSecond => world.CurrentDate.TotalSeconds;

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
        // AD-138: quem viveu o evento lembra e sente (consumidor do log, no mesmo instante; eventos sem vivência saem cedo)
        if (world.NeedsRules.Psyche.Enabled)
            LivingWorld.Simulation.Cognition.LifeEventIntake.OnEvent(world, kind, payload, sourceSystem, CurrentTick);
        // AD-141: quem morreu leva o que sabia (o último que sabia leva o design)
        if (world.InventionOn) LivingWorld.Simulation.Invention.InventionIntake.OnEvent(world, kind, payload, sourceSystem, CurrentTick, this, eventId);
        // AD-183: o feito que a cidade comenta vira boa fama de quem o fez (e respinga na linhagem)
        if (world.FamilyRules.Names.HonorOn) LivingWorld.Simulation.Population.Naming.Honor.OnEvent(world, kind, payload, CurrentTick);
        // AD-150: o traído guarda ciúme do amante (mágoa que pode virar briga)
        if (kind == WorldEventKind.AffairDiscovered && world.CrimeOn) LivingWorld.Simulation.Crime.CrimeSystem.OnEvent(world, kind, payload, CurrentTick);
        return eventId;
    }

    /// <summary>RNG derivado do stream desta chave. Streams independentes:
    /// consumir um stream novo não desloca a sequência dos outros.</summary>
    public WorldRng Rng(string streamKey) => rng.Stream(streamKey);

    public WorldRng StreamFor(string purpose, long id) => rng.StreamFor(purpose, id);

    /// <summary>O primeiro sorteio de <see cref="StreamFor"/>, sem alocar o stream (AD-167).</summary>
    public double DrawFor(string purpose, long id) => rng.DrawFor(purpose, id);

    /// <summary>Stream de uso único para uma chave que nunca se repete (traz o tick, o dia ou o id do evento): o mesmo
    /// primeiro sorteio de <see cref="Rng"/>, sem deixar o stream no snapshot para sempre (#157).</summary>
    public WorldRng OneShot(string uniqueKey) => rng.OneShot(uniqueKey);

    /// <summary>Agenda um evento para o sistema <paramref name="systemName"/> no começo da hora <paramref name="targetTick"/> (a
    /// hora do calendário, como <see cref="CurrentTick"/>). O ID é atribuído pelo <see cref="WorldState"/> — monotônico e
    /// determinístico entre processos.</summary>
    public ScheduledEvent ScheduleEvent(long targetTick, string systemName, string? payload = null) =>
        ScheduleEventAtSecond(targetTick >= long.MaxValue / WorldSeconds.PerHour ? long.MaxValue : WorldSeconds.FromHours(targetTick),
            systemName, payload);

    /// <summary>Agenda um evento para o instante <paramref name="targetSecond"/> do mundo, em segundos (ADR-0050): vence no
    /// primeiro tick em que o relógio chega a ele, também dentro da hora.</summary>
    public ScheduledEvent ScheduleEventAtSecond(long targetSecond, string systemName, string? payload = null)
    {
        var evt = new ScheduledEvent(world.NextEventIdAndAdvance(), targetSecond, systemName, payload);
        scheduler.Schedule(evt);
        return evt;
    }

    public bool CancelEvent(long id) => scheduler.Cancel(id);
}
