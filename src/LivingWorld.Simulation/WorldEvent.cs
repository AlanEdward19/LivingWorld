namespace LivingWorld.Simulation;

/// <summary>Esqueleto imutável do log Tier A (ADR-0006/rules/database-entities.md): só o que
/// vira história consultável. Fase 3 cobre nascimento e morte; guerra/fundação/invenção chegam
/// com as fases que as introduzem.</summary>
public enum WorldEventKind
{
    Birth,
    Death,

    /// <summary>Morte por fome sustentada (Fase 4, task 10/NEEDS-03) — valor de enum próprio em
    /// vez de reusar <see cref="Death"/> com causa no payload: mesmo padrão de "kind carrega o
    /// que aconteceu, payload carrega quem" já usado por Birth/Death.</summary>
    Starvation,
}

/// <summary>Um evento de história, carimbado com o tick em que ocorreu. Sem <c>BranchId</c> —
/// quem persiste (Infrastructure) atribui o branch explicitamente, nunca implícito
/// (ADR-0009).</summary>
public sealed record WorldEvent(long Tick, WorldEventKind Kind, string Payload);

/// <summary>Recebe eventos de história emitidos durante o tick. Nulo por padrão — sistemas não
/// exigem log para rodar; só quem persiste (Fase 3, task 10) fornece um.</summary>
public interface IWorldEventSink
{
    void Record(WorldEvent evt);
}
