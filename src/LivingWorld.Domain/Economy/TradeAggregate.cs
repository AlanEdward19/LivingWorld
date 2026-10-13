using LivingWorld.Domain.Shared;

namespace LivingWorld.Domain.Economy;

/// <summary>Livro de trocas: quanto uma oficina vendeu de um recurso num período. Período é o dia (<see cref="Monthly"/>
/// falso) ou o mês (verdadeiro, o dia compactado depois da janela de detalhe). <see cref="Purchases"/> conta as compras,
/// não os compradores distintos: o livro não guarda quem comprou, só o que moveu.</summary>
public sealed record TradeAggregate(
    WorkplaceId Workplace, int ResourceId, long Period, bool Monthly, long Quantity, long Value, long Purchases);
