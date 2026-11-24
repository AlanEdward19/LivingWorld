using System.Text.Json.Serialization;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Simulation.Scheduling;

/// <summary>Evento agendado no futuro (docs/domain/time-and-ticks.md): troca varredura por
/// população por O(eventos). <see cref="Id"/> é o desempate estável entre processos quando
/// dois eventos vencem no mesmo instante — nunca ordem de inserção. <see cref="TargetMs"/> é o instante de mundo em milissegundos
/// (ADR-0050); no save e no hash continua com o nome e o valor em horas de antes quando cai na hora cheia.</summary>
public sealed record ScheduledEvent(
    long Id,
    [property: JsonPropertyName("TargetTick"), JsonConverter(typeof(HourCompatibleMillisJsonConverter))] long TargetMs,
    string SystemName,
    string? Payload = null)
{
    /// <summary>A hora do calendário em que o evento vence (piso), a mesma unidade de <see cref="TickContext.CurrentTick"/>.</summary>
    [JsonIgnore]
    public long TargetTick => WorldMillis.ToHourFloor(TargetMs);
}
