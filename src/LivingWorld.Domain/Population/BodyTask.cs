using System.Text.Json.Serialization;

namespace LivingWorld.Domain.Population;

/// <summary>#241: a tarefa que ocupa o corpo conduzido pelo jogador por um tempo (cortar lenha, trabalhar...): o que é (<see cref="Action"/>),
/// quando começou e quando acaba (ms de mundo) e, na coleta, o que estava na carga cheia (<see cref="ResourceId"/>, <see cref="Units"/>),
/// para que interromper deixe só a parte proporcional. Omitida do JSON (e do hash) enquanto nula.</summary>
public sealed record BodyTask(string Action, long StartedAtMs, long UntilMs)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int ResourceId { get; init; }

    /// <summary>Na coleta: quantas unidades a sessão planejou tirar (a carga cheia de antes) e, #243, quantas já saíram (<see cref="Done"/>),
    /// o ritmo (<see cref="PerUnitMs"/>: a unidade <c>k</c> sai em <c>StartedAtMs + k × PerUnitMs</c>) e a árvore (<see cref="CellX"/>,
    /// <see cref="CellY"/>) de onde sai.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public long Units { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public long Done { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public long PerUnitMs { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int CellX { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int CellY { get; init; }
}
