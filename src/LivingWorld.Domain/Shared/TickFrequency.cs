namespace LivingWorld.Domain.Shared;

/// <summary>Escala de execução de um sistema de simulação (docs/domain/time-and-ticks.md).
/// Um sistema roda na frequência mais barata que ainda produz o comportamento desejado.</summary>
public enum TickFrequency
{
    Hourly,
    Daily,
    Monthly,
    Yearly,

    /// <summary>Uma vez por dia, no meio do turno de trabalho (≈10h de um dia de 24h; no tick do dia
    /// quando o dia tem 1 hora). Para sistemas que dependem de quem está trabalhando agora — à meia-noite,
    /// instante do <see cref="Daily"/>, todo mundo dorme em casa e nenhum trabalho acontece.</summary>
    Midshift,
}
