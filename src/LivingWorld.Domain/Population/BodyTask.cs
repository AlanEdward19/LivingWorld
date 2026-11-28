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

    /// <summary>#255: o uso de objeto em curso. <see cref="Use"/> é o id do uso no catálogo, <see cref="Building"/> e <see cref="Piece"/> dizem qual peça
    /// de qual prédio; <see cref="Doing"/> é falso enquanto o corpo anda até o lugar de uso e verdadeiro quando a ação começou;
    /// <see cref="LastMs"/> é até quando os efeitos já foram aplicados (o resto entra a cada fatia, ao interromper e ao fim).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Use { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public long Building { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int Piece { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public bool Doing { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public long LastMs { get; init; }
}
