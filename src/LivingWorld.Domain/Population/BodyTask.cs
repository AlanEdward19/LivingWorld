using System.Text.Json.Serialization;

namespace LivingWorld.Domain.Population;

/// <summary>#241: a tarefa que ocupa o corpo conduzido pelo jogador por um tempo (cortar lenha, trabalhar...): o que é (<see cref="Action"/>),
/// quando começou e quando acaba (ms de mundo) e, na coleta, o que estava na carga cheia (<see cref="ResourceId"/>, <see cref="Units"/>),
/// para que interromper deixe só a parte proporcional. Omitida do JSON (e do hash) enquanto nula.</summary>
public sealed record BodyTask(string Action, long StartedAtMs, long UntilMs)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int ResourceId { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public long Units { get; init; }
}
