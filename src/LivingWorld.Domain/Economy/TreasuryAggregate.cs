using LivingWorld.Domain.Shared;

namespace LivingWorld.Domain.Economy;

/// <summary>Livro do tesouro de uma cidade num mês: o que entrou (imposto retido sobre salário, espólio sem herdeiro) e o que
/// saiu (material de obra comprado). Um registro por cidade e mês, sem evento por cobrança.</summary>
public sealed record TreasuryAggregate(CityId City, long Month, long Revenue, long Spending);
