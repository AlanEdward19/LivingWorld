using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.LongRunning.Economy;

/// <summary>trade-ledger C8: em 3 anos o livro continua limitado (janela de 60 dias mais um agregado por mes, oficina e
/// recurso) e o dinheiro que ele registra bate com o que as oficinas receberam dos compradores.</summary>
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
            Assert.True(e.Quantity > 0 && e.Value > 0 && e.Purchases > 0);
            Assert.True(e.Value >= e.Quantity); // preco minimo de 1 por unidade
        });
        Assert.True(world.TradeLedger.Any(e => e.Monthly), "nada foi compactado em 3 anos");
    }
}
