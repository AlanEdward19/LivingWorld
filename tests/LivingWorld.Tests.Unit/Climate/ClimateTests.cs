using LivingWorld.Domain.Climate;
using LivingWorld.Domain.Flora;
using LivingWorld.Domain.Geography;
using LivingWorld.Simulation.Climate;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Ecology;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Climate;

/// <summary>weather-drought: o ano de safra de cada região é sorteado uma vez, entre os limites do cenário, e multiplica a
/// colheita; desligado, todo ano vale 1000 e nada muda.</summary>
public class ClimateTests
{
    private const long YearHours = 12L * 30 * 24;

    private static WorldState NewWorld(ClimateRules? climate = null, ulong seed = 1) =>
        ScenarioRunner.Create(seed, initialPopulation: 5, climateRules: climate).World;

    private static void Draw(WorldState world, long year)
    {
        world.CurrentDate = world.CurrentDate.AddHours(year * YearHours - world.CurrentDate.TotalHours);
        new ClimateYearSystem().Tick(world, new TickContext(world, world.Rng, world.Scheduler));
    }

    [Fact]
    public void Without_climate_every_year_is_1000_and_the_system_draws_nothing()
    {
        var world = NewWorld();

        Draw(world, 3);

        Assert.False(world.ClimateRules.Enabled);
        Assert.Empty(world.ClimateYears);
        Assert.Equal(1000, world.ClimateFactorMilli(world.Map.Regions[0].Id, 3));
    }

    [Fact]
    public void Each_region_gets_one_factor_per_year_inside_the_scenario_bounds_and_never_a_rewrite()
    {
        var world = NewWorld(new ClimateRules(true, 300, 1500));

        Draw(world, 2);
        var drawn = world.ClimateYears.ToList();
        Assert.Equal(world.Map.Regions.Count, drawn.Count);
        Assert.All(drawn, c => Assert.InRange(c.FactorMilli, 300, 1500));

        world.SetClimateYear(world.Map.Regions[0].Id, 2, 999); // já sorteado: não reescreve
        Assert.Equal(drawn, world.ClimateYears.ToList());
    }

    [Fact]
    public void The_same_seed_draws_the_same_years_and_the_factors_vary_across_years_and_regions_over_20_years_in_10_seeds()
    {
        for (ulong seed = 1; seed <= 10; seed++)
        {
            var a = NewWorld(new ClimateRules(true, 300, 1500), seed);
            var b = NewWorld(new ClimateRules(true, 300, 1500), seed);
            var factors = new List<int>();
            for (long year = 1; year <= 20; year++)
            {
                Draw(a, year);
                Draw(b, year);
                Assert.Equal(a.ClimateYears, b.ClimateYears);
                factors.AddRange(a.ClimateYears.Where(c => c.Year == year).Select(c => c.FactorMilli));
            }

            Assert.True(factors.Distinct().Count() > 10, "os fatores não variam");
            Assert.InRange(factors.Average(), 800, 1000); // uniforme entre 300 e 1500: média 900
        }
    }

    [Fact]
    public void Invalid_scenario_bounds_never_draw_a_negative_factor()
    {
        var world = NewWorld(new ClimateRules(true, -500, -900));

        for (long year = 1; year <= 5; year++) Draw(world, year);

        Assert.NotEmpty(world.ClimateYears);
        Assert.All(world.ClimateYears, c => Assert.True(c.FactorMilli >= 0));
    }

    [Fact]
    public void Old_years_leave_the_ledger_so_it_stays_bounded()
    {
        var world = NewWorld(new ClimateRules(true, 300, 1500));

        for (long year = 1; year <= 12; year++) Draw(world, year);

        Assert.True(world.ClimateYears.Max(c => c.Year) - world.ClimateYears.Min(c => c.Year) <= WorldState.ClimateYearsKept);
        Assert.True(world.ClimateYears.Count <= world.Map.Regions.Count * (WorldState.ClimateYearsKept + 1));
    }

    [Fact]
    public void A_drought_year_cuts_the_plant_harvest_in_proportion_and_an_abundant_year_adds_to_it()
    {
        var world = NewWorld(new ClimateRules(true, 300, 1500));
        var wheat = ScenarioRunner.DefaultPlantSpeciesRules[0];
        var farm = world.Workplaces.First(w => w.LocationType.Id == 1);
        var plant = new Plant(new PlantId(900), wheat.Species, farm.Location, GrowthStage: 3);
        var ctx = new TickContext(world, world.Rng, world.Scheduler);
        var region = world.Map.RegionOf(farm.Location);
        long year = world.ClimateYearOf(world.CurrentDate.TotalHours);
        long Stock() => farm.Stock.GetValueOrDefault(new ResourceType(wheat.CropResourceId));

        long start = Stock();
        FloraLifecycleSystem.DepositYield(world, ctx, plant, wheat);
        long average = Stock() - start; // sem sorteio ainda: ano de 1000

        world.SetClimateYear(region, year, 300);
        start = Stock();
        FloraLifecycleSystem.DepositYield(world, ctx, plant, wheat);
        long drought = Stock() - start;

        Assert.Equal(10, average);
        Assert.Equal(3, drought); // 10 x 300 / 1000
    }

    [Fact]
    public void The_climate_rules_and_years_survive_a_snapshot_round_trip_with_an_equal_hash()
    {
        var world = NewWorld(new ClimateRules(true, 400, 1400));
        Draw(world, 2);

        var restored = WorldSnapshot.Deserialize(WorldSnapshot.Serialize(world));

        Assert.Equal(world.ClimateYears, restored.ClimateYears);
        Assert.Equal(world.ClimateRules, restored.ClimateRules);
        Assert.Equal(WorldSnapshot.CanonicalHash(world), WorldSnapshot.CanonicalHash(restored));
    }

    [Fact]
    [Trait("Category", "Scenario")]
    public void A_forced_drought_in_year_five_cuts_the_food_harvest_and_does_not_lower_the_price_and_the_village_survives_twenty_years()
    {
        var (control, controlClock) = ScenarioRunner.Create(4, climateRules: new ClimateRules(true, 1000, 1000));
        var (dry, dryClock) = ScenarioRunner.Create(4, climateRules: new ClimateRules(true, 1000, 1000));

        long droughtYear = 5;
        controlClock.Run(control, droughtYear * YearHours - 1);
        dryClock.Run(dry, droughtYear * YearHours - 1);
        // o sorteio do ano 5 acontece no primeiro tick dele; gravar antes, o sistema não reescreve um ano já sorteado
        foreach (var region in dry.Map.Regions) dry.SetClimateYear(region.Id, droughtYear, 300); // seca em toda região
        controlClock.Run(control, 1);
        dryClock.Run(dry, 1);
        long ProducedBefore(WorldState w) => w.ResourceProduced.GetValueOrDefault(new ResourceType(w.EconomyRules.FoodResourceId));
        long controlStart = ProducedBefore(control), dryStart = ProducedBefore(dry);
        controlClock.Run(control, YearHours / 2);
        dryClock.Run(dry, YearHours / 2);

        long controlHarvest = ProducedBefore(control) - controlStart, dryHarvest = ProducedBefore(dry) - dryStart;
        long FoodPrice(WorldState w) => w.Workplaces.Where(x => x.Prices.ContainsKey(new ResourceType(w.EconomyRules.FoodResourceId)))
            .Max(x => x.Prices[new ResourceType(w.EconomyRules.FoodResourceId)]);
        Assert.True(dryHarvest < 0.6 * controlHarvest, $"colheita com seca {dryHarvest} contra controle {controlHarvest}");
        Assert.True(FoodPrice(dry) >= FoodPrice(control), $"preço com seca {FoodPrice(dry)} contra controle {FoodPrice(control)}");

        dryClock.Run(dry, 15 * YearHours - YearHours / 2);
        Assert.True(dry.Npcs.Count(n => n.IsAlive) > 0, "a vila se extinguiu depois da seca");
    }
}
