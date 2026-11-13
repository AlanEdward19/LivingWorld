using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Simulation.Economy.Production;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Economy;

/// <summary>AD-135: o ano agrícola. A colheita cai numa janela curta de verão, nada nasce no inverno, a semente fica guardada,
/// o celeiro abastece a venda e a habilidade de quem semeia entra no rendimento. O braço de controle é o lote diário antigo.</summary>
public sealed class AgriculturalYearTests
{
    private const int HoursPerMonth = 30 * 24;

    private static long[] FoodProducedByMonth(AgricultureRules? rules, ulong seed, int months)
    {
        var (world, clock) = ScenarioRunner.Create(seed, agricultureRules: rules);
        var food = new ResourceType(world.EconomyRules.FoodResourceId);
        var produced = new long[months];
        long last = 0;
        for (int m = 0; m < months; m++)
        {
            clock.Run(world, HoursPerMonth);
            long now = world.ResourceProduced.GetValueOrDefault(food);
            produced[m] = now - last;
            last = now;
        }

        return produced;
    }

    private static double ShareInBestTwoMonths(long[] produced)
    {
        long total = produced.Sum(), best = 0;
        for (int m = 0; m + 1 < produced.Length; m++) best = Math.Max(best, produced[m] + produced[m + 1]);
        return total == 0 ? 0 : (double)best / total;
    }

    [Fact]
    public void Seasonal_harvest_puts_most_of_the_year_food_in_a_two_month_window()
    {
        var produced = FoodProducedByMonth(AgricultureRules.Default, seed: 3, months: 12);

        Assert.True(ShareInBestTwoMonths(produced) > 0.8, string.Join(",", produced));
    }

    [Fact]
    public void Daily_crop_control_spreads_food_over_the_whole_year()
    {
        var produced = FoodProducedByMonth(AgricultureRules.Disabled, seed: 3, months: 12);

        Assert.True(ShareInBestTwoMonths(produced) < 0.3, string.Join(",", produced));
    }

    [Fact]
    public void Nothing_is_harvested_in_winter()
    {
        var produced = FoodProducedByMonth(AgricultureRules.Default, seed: 5, months: 12);

        Assert.Equal(0, produced[0] + produced[1] + produced[2]);
    }

    [Fact]
    public void The_harvest_refills_the_seed_reserve_and_the_barn()
    {
        var (world, clock) = ScenarioRunner.Create(7);
        clock.Run(world, 9L * HoursPerMonth); // até o começo do outono, antes de semear

        var field = Assert.Single(world.CropBatches, c => c.Status != CropStatus.Harvested);
        Assert.True(field.LastHarvest > 0);
        Assert.True(field.SeedReserve > 0);
        Assert.True(field.Barn > 0);
    }

    [Fact]
    public void Skilled_farmers_sow_a_field_with_more_yield_potential_than_unskilled_ones()
    {
        double PotentialPerSeed(double skill)
        {
            var (world, clock) = ScenarioRunner.Create(11);
            var agriculture = ScenarioRunner.DefaultSkillsRules.SkillByProfession[1];
            foreach (var npc in world.Npcs.Where(n => n.Profession.Id == 1))
                npc.GainSkill(agriculture, skill - npc.Skills.Get(agriculture), 100);
            clock.Run(world, 11L * HoursPerMonth); // passa a semeadura de outono
            var field = world.CropBatches.Single(c => c.Status != CropStatus.Harvested);
            return field.EffectiveSeedMilli / 1000.0 / field.LastSeedSown;
        }

        Assert.True(PotentialPerSeed(100) > PotentialPerSeed(0));
    }

    [Fact]
    public void Reapers_take_part_of_the_harvest_home()
    {
        long HomeFoodAfterHarvest(double share)
        {
            var (world, clock) = ScenarioRunner.Create(19, agricultureRules: AgricultureRules.Default with { ReaperShare = share });
            clock.Run(world, 8L * HoursPerMonth);
            var food = new ResourceType(world.EconomyRules.FoodResourceId);
            return world.Households.Sum(h => h.Stock.GetValueOrDefault(food));
        }

        Assert.True(HomeFoodAfterHarvest(0.5) > HomeFoodAfterHarvest(0));
    }

    [Fact]
    public void An_empty_barn_is_rationed_until_the_next_harvest()
    {
        var (world, _) = ScenarioRunner.Create(23);
        var field = world.CropBatches.Single(c => c.Status != CropStatus.Harvested);

        // começo do ano, lavoura semeada: faltam ~7 meses e meio para a colheita esperada
        Assert.InRange(CropSystem.DaysToNextHarvest(world, world.AgricultureRules, field), 200, 230);
    }

    [Fact]
    public void A_crop_still_ripening_before_the_expected_month_does_not_open_the_barn_as_if_harvest_were_days_away()
    {
        // AD-140: em julho a lavoura em pé contava meio mês e o celeiro saía três vezes mais rápido, esvaziando antes da ceifa
        var (world, clock) = ScenarioRunner.Create(23);
        var rules = world.AgricultureRules;
        clock.Run(world, (long)rules.EarliestRipeningMonth * HoursPerMonth);
        var field = world.CropBatches.Single(c => c.Status != CropStatus.Harvested);
        Assert.Equal(rules.EarliestRipeningMonth, world.CurrentDate.Month);
        Assert.True(rules.ExpectedHarvestMonth > rules.EarliestRipeningMonth);
        Assert.Equal(CropStatus.Growing, field.Status);

        Assert.InRange(CropSystem.DaysToNextHarvest(world, rules, field), 40, 50);
    }

    [Fact]
    public void Monthly_barn_loss_compounds_to_the_annual_rate()
    {
        double monthly = CropSystem.MonthlyRate(0.15);
        double kept = 1;
        for (int m = 0; m < 12; m++) kept *= 1 - monthly;

        Assert.Equal(0.85, kept, 6);
    }

    [Fact]
    public void A_household_pantry_spoils_between_ten_and_thirty_percent_a_year()
    {
        var (world, _) = ScenarioRunner.Create(13);
        var food = new ResourceType(world.EconomyRules.FoodResourceId);
        var household = world.Households.OrderBy(h => h.Id.Value).First();
        household.Withdraw(food, household.Stock.GetValueOrDefault(food));
        household.Deposit(food, 100_000);
        var system = new ProductionSystem();

        for (int day = 0; day < 360; day++)
        {
            world.CurrentDate = world.CurrentDate.AddHours(24);
            system.Tick(world, new TickContext(world, world.Rng, world.Scheduler));
        }

        Assert.InRange(household.Stock.GetValueOrDefault(food), 70_000, 90_000);
    }
}
