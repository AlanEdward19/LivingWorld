using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Economy;

/// <summary>AD-140: o caixa do empregador acima da reserva de giro paga dízimo ao tesouro da cidade, que paga a esmola. Sem isso
/// a sobra da venda de comida ficava parada na fazenda e a moeda das casas secava em décadas (fome com o celeiro cheio).</summary>
public sealed class SurplusLevyTests
{
    private const long OneMonth = 30 * 24;

    private static (long Workplaces, long Cities, long Total) MoneyAfterAMonth(double share)
    {
        var rules = ScenarioRunner.DefaultEconomyRules with { SurplusLevyShare = share };
        var (world, clock) = ScenarioRunner.Create(seed: 5, economyRules: rules);
        clock.Run(world, OneMonth + 1);
        long workplaces = world.Workplaces.Sum(w => w.Treasury.Amount);
        long cities = world.Cities.Sum(c => c.Treasury.Amount);
        long wallets = world.Npcs.Sum(n => n.Wallet.Amount);
        return (workplaces, cities, workplaces + cities + wallets);
    }

    [Fact]
    public void Surplus_above_the_working_reserve_moves_from_the_employer_to_the_city()
    {
        var levied = MoneyAfterAMonth(0.05);
        var kept = MoneyAfterAMonth(0);

        Assert.True(levied.Cities > kept.Cities + 10_000, $"{levied.Cities} vs {kept.Cities}");
        Assert.True(levied.Workplaces < kept.Workplaces, $"{levied.Workplaces} vs {kept.Workplaces}");
    }

    [Fact]
    public void The_levy_only_moves_coin_and_never_mints_it()
    {
        Assert.Equal(MoneyAfterAMonth(0).Total, MoneyAfterAMonth(0.05).Total);
    }

    [Fact]
    public void The_default_village_levies_and_a_scenario_without_the_field_does_not()
    {
        Assert.True(ScenarioRunner.DefaultEconomyRules.SurplusLevyShare > 0);
        Assert.Equal(0, LivingWorld.Domain.Economy.EconomyRules.Disabled.SurplusLevyShare);
    }
}
