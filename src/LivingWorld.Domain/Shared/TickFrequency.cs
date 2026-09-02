namespace LivingWorld.Domain.Shared;

/// <summary>Escala de execução de um sistema de simulação (docs/domain/time-and-ticks.md).
/// Um sistema roda na frequência mais barata que ainda produz o comportamento desejado.</summary>
public enum TickFrequency
{
    Hourly,
    Daily,
    Monthly,
    Yearly,
}
