using System.Text.Json.Serialization;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Simulation.Behavior.Needs;

/// <summary>Despertar pendente de um NPC, como entra no snapshot (AD-129). <see cref="Sequence"/> sai do mesmo
/// contador de <c>WorldState.NextEventId</c> no momento do agendamento e decide a ordem do lote: no mesmo tick,
/// quem foi agendado por último acorda primeiro (a ordem de quando o despertar era um <c>ScheduledEvent</c>).
/// <see cref="TargetSecond"/> é o instante em segundos (ADR-0050); no save e no hash continua em horas na hora cheia.</summary>
public sealed record PendingNpcWake(
    long NpcId,
    [property: JsonPropertyName("TargetTick"), JsonConverter(typeof(HourCompatibleSecondsJsonConverter))] long TargetSecond,
    long Sequence)
{
    /// <summary>A hora do calendário em que o despertar vence (piso).</summary>
    [JsonIgnore]
    public long TargetTick => WorldSeconds.ToHourFloor(TargetSecond);
}
