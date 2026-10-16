using LivingWorld.Domain.History;

namespace LivingWorld.Simulation.Core;

/// <summary>Um evento de história, carimbado com o tick em que ocorreu. Sem <c>BranchId</c> —
/// quem persiste (Infrastructure) atribui o branch explicitamente, nunca implícito.
/// <see cref="EventId"/> / <see cref="CauseEventId"/> / <see cref="SourceSystem"/>
/// carregam proveniência causal; <c>RootCauseEventId</c> é derivado sob demanda.</summary>
public sealed record WorldEvent(
    long Tick,
    WorldEventKind Kind,
    string Payload,
    long EventId = 0,
    long? CauseEventId = null,
    string SourceSystem = "Unknown");

/// <summary>Recebe eventos de história emitidos durante o tick. Nulo por padrão — sistemas não
/// exigem log para rodar; só quem persiste fornece um.</summary>
public interface IWorldEventSink
{
    void Record(WorldEvent evt);
}
