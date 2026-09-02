using LivingWorld.Domain;

namespace LivingWorld.Simulation;

/// <summary>Um evento de história, carimbado com o tick em que ocorreu. Sem <c>BranchId</c> —
/// quem persiste (Infrastructure) atribui o branch explicitamente, nunca implícito
/// (ADR-0009). <see cref="EventId"/> / <see cref="CauseEventId"/> / <see cref="SourceSystem"/>
/// carregam proveniência causal (COH-01); <c>RootCauseEventId</c> é derivado sob demanda.</summary>
public sealed record WorldEvent(
    long Tick,
    WorldEventKind Kind,
    string Payload,
    long EventId = 0,
    long? CauseEventId = null,
    string SourceSystem = "Unknown");

/// <summary>Recebe eventos de história emitidos durante o tick. Nulo por padrão — sistemas não
/// exigem log para rodar; só quem persiste (Fase 3, task 10) fornece um.</summary>
public interface IWorldEventSink
{
    void Record(WorldEvent evt);
}
