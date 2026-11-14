using LivingWorld.Simulation.Core;
using LivingWorld.Tests.LongRunning.Population;

namespace LivingWorld.Tests.LongRunning.Economy;

/// <summary>trade-ledger C7: em 3 anos, amostrado no fim de cada ano, o livro continua limitado (janela de 60 dias mais um agregado
/// por mes, oficina e recurso), foi compactado em meses e todo agregado tem quantidade, valor e compras positivos.
/// A vila padrão da seed 42 vem da corrida compartilhada <see cref="DefaultVillageSeed42"/> (ADR-0044): o maior tamanho do
/// livro nos fins dos anos 1–3 e uma cópia do livro no fim do ano 3.</summary>
public class TradeLedgerLongRunTests
{
    [Fact]
    public void Three_year_ledger_stays_bounded_and_every_aggregate_is_consistent()
    {
        var run = DefaultVillageSeed42.Value;
        long maxCount = run.LedgerMaxCount;
        var ledger = run.LedgerYear3;

        long bound = run.LedgerMarkets * 2L * (WorldState.TradeLedgerDetailDays + 12 * 3); // 2 recursos essenciais
        Assert.NotEmpty(ledger);
        Assert.True(maxCount <= bound, $"livro chegou a {maxCount} agregados (teto {bound})");
        Assert.All(ledger, e =>
        {
            // retirada do poço entra com valor 0 (issue 41): o agregado pode ter só água de graça ou misturar com vendas
            Assert.True(e.Quantity > 0 && e.Value >= 0 && e.Purchases > 0);
        });
        Assert.Contains(ledger, e => e.Value >= e.Quantity && e.Value > 0); // vendas a preco minimo de 1 por unidade
        Assert.True(ledger.Any(e => e.Monthly), "nada foi compactado em 3 anos");
    }
}
