using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.LongRunning.Economy;

/// <summary>trade-ledger C7: em 3 anos, amostrado no fim de cada ano, o livro continua limitado (janela de 60 dias mais um agregado
/// por mes, oficina e recurso), foi compactado em meses e todo agregado tem quantidade, valor e compras positivos.</summary>
public class TradeLedgerLongRunTests
{
    private const long OneYearInHours = 12 * 30 * 24;

    [Fact]
    public void Three_year_ledger_stays_bounded_and_every_aggregate_is_consistent()
    {
        var (world, clock) = ScenarioRunner.Create(seed: 42);
        long maxCount = 0;
        for (int year = 0; year < 3; year++)
        {
            clock.Run(world, OneYearInHours);
            maxCount = Math.Max(maxCount, world.TradeLedger.Count);
        }

        var markets = world.Workplaces.Count(w => world.EconomyCatalog.MarketLocationTypeIds.Contains(w.LocationType.Id));
        long bound = markets * 2L * (WorldState.TradeLedgerDetailDays + 12 * 3); // 2 recursos essenciais
        Assert.NotEmpty(world.TradeLedger);
        Assert.True(maxCount <= bound, $"livro chegou a {maxCount} agregados (teto {bound})");
        Assert.All(world.TradeLedger, e =>
        {
            // retirada do poço entra com valor 0 (issue 41): o agregado pode ter só água de graça ou misturar com vendas
            Assert.True(e.Quantity > 0 && e.Value >= 0 && e.Purchases > 0);
        });
        Assert.Contains(world.TradeLedger, e => e.Value >= e.Quantity && e.Value > 0); // vendas a preco minimo de 1 por unidade
        Assert.True(world.TradeLedger.Any(e => e.Monthly), "nada foi compactado em 3 anos");
    }
}
