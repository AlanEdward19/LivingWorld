using LivingWorld.Domain.Economy;
using LivingWorld.Domain.History;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Economy;

/// <summary>AD-144 (absorve o dízimo da AD-140): o caixa acima da reserva de giro sai todo mês; com dono vai à carteira dele,
/// sem dono (a terra comum da fazenda) ao tesouro da cidade, que paga a esmola. A moeda só muda de dono.</summary>
public sealed class OwnerDrawTests
{
    private const long OneMonth = 30 * 24;

    private sealed record Observed(long Farm, long FarmReserve, long Forge, long Cities, long OwnerWallet, long Total, int Draws);

    private sealed class DrawCounter : IWorldEventSink
    {
        public int Draws;
        public void Record(WorldEvent evt) { if (evt.Kind == WorldEventKind.ProfitDrawn) Draws++; }
    }

    private static Observed AfterAMonth(double share)
    {
        var rules = ScenarioRunner.DefaultEconomyRules with { SurplusDrawShare = share };
        var (world, baseClock) = ScenarioRunner.Create(seed: 5, economyRules: rules);
        var sink = new DrawCounter();
        new WorldClock(baseClock.Systems, baseClock.MaxIterationsPerTick, sink).Run(world, OneMonth + 1);
        var farm = world.Workplaces.First(w => w.LocationType.Id == 1);
        var forge = world.Workplaces.First(w => w.LocationType.Id == 2);
        long owner = forge.Owner is { } id ? world.FindNpc(id)!.Wallet.Amount : 0;
        long cities = world.Cities.Sum(c => c.Treasury.Amount);
        long total = world.Workplaces.Sum(w => w.Treasury.Amount) + cities + world.Npcs.Sum(n => n.Wallet.Amount);
        return new Observed(farm.Treasury.Amount, LivingWorld.Simulation.Economy.Labor.Payroll.WorkingReserve(world, farm), forge.Treasury.Amount,
            cities, owner, total, sink.Draws);
    }

    private static readonly Lazy<Observed> Drawn = new(() => AfterAMonth(1.0));
    private static readonly Lazy<Observed> Kept = new(() => AfterAMonth(0));

    [Fact]
    public void Surplus_of_the_ownerless_farm_moves_to_the_city_treasury()
    {
        Assert.True(Drawn.Value.Cities > Kept.Value.Cities + 100_000, $"{Drawn.Value.Cities} vs {Kept.Value.Cities}");
    }

    [Fact]
    public void Surplus_of_the_forge_moves_to_the_master_who_owns_it()
    {
        Assert.True(Drawn.Value.OwnerWallet > Kept.Value.OwnerWallet + 100_000, $"{Drawn.Value.OwnerWallet} vs {Kept.Value.OwnerWallet}");
    }

    [Fact]
    public void The_draw_leaves_the_working_reserve_in_the_farm()
    {
        Assert.True(Drawn.Value.Farm >= Drawn.Value.FarmReserve && Drawn.Value.Farm < Kept.Value.Farm, $"{Drawn.Value.Farm} reserva {Drawn.Value.FarmReserve}");
    }

    [Fact]
    public void The_draw_only_moves_coin_and_never_mints_it()
    {
        Assert.Equal(Kept.Value.Total, Drawn.Value.Total);
    }

    [Fact]
    public void Every_draw_is_logged_and_a_zero_share_draws_nothing()
    {
        Assert.True(Drawn.Value.Draws >= 2 && Kept.Value.Draws == 0, $"{Drawn.Value.Draws} / {Kept.Value.Draws}");
    }

    [Fact]
    public void The_default_village_draws_and_a_scenario_without_the_field_does_not()
    {
        Assert.True(ScenarioRunner.DefaultEconomyRules.SurplusDrawShare > 0);
        Assert.Equal(0, EconomyRules.Disabled.SurplusDrawShare);
    }

    [Fact]
    public void The_draw_keeps_the_old_serialized_name_so_old_snapshots_still_draw()
    {
        string json = System.Text.Json.JsonSerializer.Serialize(EconomyRules.Disabled with { SurplusDrawShare = 0.05 });
        Assert.Contains("\"SurplusLevyShare\":0.05", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Same_seed_gives_the_same_world_and_another_seed_does_not()
    {
        static string Hash(ulong seed)
        {
            var (world, clock) = ScenarioRunner.Create(seed, initialPopulation: 30);
            clock.Run(world, OneMonth + 1);
            return WorldSnapshot.CanonicalHash(world);
        }

        Assert.Equal(Hash(3), Hash(3));
        Assert.NotEqual(Hash(3), Hash(4));
    }

    [Fact]
    public void Money_is_conserved_every_day_across_the_monthly_draw()
    {
        var (world, clock) = ScenarioRunner.Create(seed: 9, initialPopulation: 30);
        long Total() => world.Npcs.Sum(n => n.Wallet.Amount) + world.Workplaces.Sum(w => w.Treasury.Amount) + world.Cities.Sum(c => c.Treasury.Amount);
        long initial = Total();
        for (int day = 0; day < 62; day++)
        {
            clock.Run(world, 24);
            Assert.Equal(initial + world.MoneyMinted.Amount - world.MoneyDestroyed.Amount, Total());
        }
    }
}
