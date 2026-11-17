using LivingWorld.Domain.Climate;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Health;
using LivingWorld.Simulation.Climate;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Emergence;
using LivingWorld.Simulation.Extraordinary.Mechanics;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Climate;

/// <summary>AD-180: o tempo do dia por região (estação suave, temperatura com dia e noite, chuva em frentes, neve, lama, seca),
/// determinístico, no snapshot, e as consequências lidas por quem já existia.</summary>
public sealed class WeatherTests
{
    private const int DayHours = 24;

    private static (WorldState World, WorldClock Clock) NewWorld(ulong seed = 1, ClimateRules? climate = null) =>
        ScenarioRunner.Create(seed, initialPopulation: 5, climateRules: climate);

    private static WeatherRegionRow Village(WorldState world) =>
        WeatherNow.RowAt(world, world.ActiveCities().First().Location)!;

    [Fact]
    public void Weather_same_seed_gives_the_same_world_and_another_seed_does_not()
    {
        var (a, ca) = NewWorld(7);
        var (b, cb) = NewWorld(7);
        var (c, cc) = NewWorld(8);
        ca.Run(a, 60 * DayHours);
        cb.Run(b, 60 * DayHours);
        cc.Run(c, 60 * DayHours);

        Assert.Equal(WorldSnapshot.CanonicalHash(a), WorldSnapshot.CanonicalHash(b));
        Assert.NotEqual(
            string.Join(",", a.Weather!.Regions.Select(r => r.PrecipTenths)), string.Join(",", c.Weather!.Regions.Select(r => r.PrecipTenths)));
    }

    [Fact]
    public void Weather_round_trips_through_the_snapshot_and_the_reopened_world_continues_identically()
    {
        var (world, clock) = NewWorld(3);
        clock.Run(world, 45 * DayHours);
        var reopened = WorldSnapshot.Deserialize(WorldSnapshot.Serialize(world));
        Assert.Equal(WorldSnapshot.CanonicalHash(world), WorldSnapshot.CanonicalHash(reopened));

        clock.Run(world, 40 * DayHours);
        NewWorld(3).Clock.Run(reopened, 40 * DayHours);

        Assert.Equal(WorldSnapshot.CanonicalHash(world), WorldSnapshot.CanonicalHash(reopened));
    }

    [Fact]
    public void Weather_turned_off_changes_the_world_hash()
    {
        var (on, c1) = NewWorld(2);
        var (off, c2) = NewWorld(2, ClimateRules.Default with { Weather = null });
        c1.Run(on, 10 * DayHours);
        c2.Run(off, 10 * DayHours);

        Assert.Null(off.Weather);
        Assert.NotEqual(WorldSnapshot.CanonicalHash(on), WorldSnapshot.CanonicalHash(off));
    }

    [Fact]
    public void Seasons_change_gradually_day_by_day_instead_of_stepping_at_each_quarter()
    {
        var r = WeatherRules.Default;
        double largest = 0;
        for (int d = 1; d < 360; d++)
            largest = Math.Max(largest, Math.Abs(r.Interpolate(r.MeanTempC, d, 30, 12) - r.Interpolate(r.MeanTempC, d - 1, 30, 12)));

        Assert.True(largest < 0.15, $"maior salto da climatologia entre dois dias: {largest:F2} °C");
    }

    [Fact]
    public void Daylight_follows_latitude_and_season_and_flips_in_the_southern_hemisphere()
    {
        var north = WeatherRules.Default;
        var south = north with { LatitudeDeg = -north.LatitudeDeg };

        Assert.InRange(north.DaylightHours(5 * 30 + 21, 360), 16.0, 16.9); // fim de junho em Oxford: ~16,5 h
        Assert.InRange(north.DaylightHours(11 * 30 + 21, 360), 7.4, 8.2); // fim de dezembro: ~7,8 h
        Assert.InRange(south.DaylightHours(5 * 30 + 21, 360), 7.4, 8.2);
    }

    [Fact]
    public void Rain_comes_in_fronts_wet_days_follow_wet_days_more_often_than_dry_days()
    {
        var rules = WeatherRules.Default;
        var rng = new LivingWorld.Domain.Shared.WorldRng(42);
        var row = new WeatherRegionRow();
        int wetWet = 0, wet = 0, dryWet = 0, dry = 0;
        bool previous = false;
        for (int d = 0; d < 360 * 30; d++)
        {
            ulong s1 = rng.DeriveState(d), s2 = rng.DeriveState(1_000_000 + d);
            WeatherGenerator.Step(rules, row, WeatherGenerator.Read(ref s1), WeatherGenerator.Read(ref s2), d % 360 + 0.5, 30, 12, d == 0);
            if (d > 0)
            {
                if (previous) { wet++; if (row.Wet) wetWet++; }
                else { dry++; if (row.Wet) dryWet++; }
            }
            previous = row.Wet;
        }

        Assert.True((double)wetWet / wet > 1.5 * dryWet / dry, $"P(chuva|chuva)={(double)wetWet / wet:F2} P(chuva|seco)={(double)dryWet / dry:F2}");
    }

    [Fact]
    public void Daily_temperature_follows_the_day_with_cold_nights_and_warm_afternoons()
    {
        var (world, clock) = NewWorld(5);
        clock.Run(world, 200 * DayHours);
        var cell = world.ActiveCities().First().Location;
        long day = world.CurrentDate.TotalHours;

        Assert.True(WeatherNow.TemperatureAt(world, cell, day + 15) > WeatherNow.TemperatureAt(world, cell, day + 5) + 3);
    }

    [Fact]
    public void The_village_cell_reads_the_climatology_of_the_place()
    {
        var (world, clock) = NewWorld(4);
        clock.Run(world, 100 * DayHours);
        var cell = world.ActiveCities().First().Location;

        Assert.Equal(Village(world).MeanCenti / 100f, EnvironmentTemperatureMechanic.EffectiveTemperature(world, cell, world.CurrentDate.TotalHours), 2);
    }

    [Fact]
    public void Temperature_field_reads_the_daily_weather_instead_of_the_quarter_step()
    {
        var (world, clock) = NewWorld(4);
        clock.Run(world, 100 * DayHours);
        var cell = world.ActiveCities().First().Location;
        world.Map.TryGetBaseClimate(cell, out float baseC, out _);
        var row = Village(world);

        Assert.Equal(baseC + row.MeanCenti / 100f - world.Weather!.ReferenceCenti / 100f,
            EnvironmentTemperatureMechanic.EffectiveTemperature(world, cell, world.CurrentDate.TotalHours), 3);
    }

    [Fact]
    public void The_crop_year_factor_comes_from_the_weather_of_the_season_not_from_a_parallel_draw()
    {
        var (world, clock) = NewWorld(6);
        clock.Run(world, 30 * DayHours);
        Assert.Empty(world.ClimateYears); // em janeiro nada sorteado: o fator sai da estação

        clock.Run(world, (6 * 30 - 30) * DayHours + 1);
        var row = Village(world);
        int expected = WeatherGenerator.CropFactor(world.ClimateRules, world.ClimateRules.Weather!, WeatherGenerator.CropIndex(world.ClimateRules.Weather!, row));
        var region = world.Map.RegionOf(world.ActiveCities().First().Location);

        Assert.Equal(expected, world.ClimateFactorMilli(region, 0));
    }

    [Fact]
    public void A_wet_spring_gives_a_poorer_crop_factor_than_a_normal_one()
    {
        var rules = WeatherRules.Default;
        var normal = new WeatherRegionRow { SpringRainTenths = 1000, SpringNormalTenths = 1000, SpringDays = 120 };
        var wet = new WeatherRegionRow { SpringRainTenths = 1600, SpringNormalTenths = 1000, SpringDays = 120 };

        Assert.True(WeatherGenerator.CropFactor(ClimateRules.Default, rules, WeatherGenerator.CropIndex(rules, wet))
            < WeatherGenerator.CropFactor(ClimateRules.Default, rules, WeatherGenerator.CropIndex(rules, normal)) - 200);
    }

    [Fact]
    public void Mud_slows_walking_and_paved_streets_lose_less_time()
    {
        var (world, _) = NewWorld(1);
        var rules = WeatherRules.Default;
        var row = new WeatherRegionRow { SoilPm = 900, MeanCenti = 800 };

        double dirt = WeatherNow.TravelFactor(world, row, 1000);
        double paved = WeatherNow.TravelFactor(world, row, 100);

        Assert.Equal(rules.HeavyMudTravelFactor, dirt, 6);
        Assert.True(paved < dirt && paved > 1);
    }

    [Fact]
    public void A_muddy_day_makes_the_same_trip_take_longer()
    {
        var (world, clock) = NewWorld(1);
        clock.Run(world, DayHours);
        var npc = world.AliveNpcIndex.Alive[0];
        var from = world.ActiveCities().First().Location;
        var to = new CellCoord(Math.Max(0, from.X - 6), Math.Max(0, from.Y - 6));
        foreach (var r in world.Weather!.Regions) { r.SoilPm = 0; r.SnowMm = 0; r.MeanCenti = 1000; }
        long dry = Mobility.TravelTicks(world, npc, from, to, 1.0);
        foreach (var r in world.Weather!.Regions) r.SoilPm = 950;
        long muddy = Mobility.TravelTicks(world, npc, from, to, 1.0);

        Assert.True(muddy > dry, $"seco {dry} h, lama {muddy} h");
    }

    [Fact]
    public void Heavy_rain_snow_and_frozen_ground_stop_outdoor_work_and_any_rain_stops_reaping()
    {
        var r = WeatherRules.Default;

        Assert.False(WeatherNow.OutdoorWorkable(r, new WeatherRegionRow { Kind = Precipitation.Rain, PrecipTenths = 120, MeanCenti = 1000 }));
        Assert.False(WeatherNow.OutdoorWorkable(r, new WeatherRegionRow { SnowMm = 5, MeanCenti = 0 }));
        Assert.False(WeatherNow.OutdoorWorkable(r, new WeatherRegionRow { MeanCenti = -300 }));
        Assert.True(WeatherNow.OutdoorWorkable(r, new WeatherRegionRow { Kind = Precipitation.Rain, PrecipTenths = 20, MeanCenti = 1000 }));
    }

    [Fact]
    public void Construction_does_not_advance_on_a_day_of_heavy_rain()
    {
        var (world, clock) = NewWorld(1);
        clock.Run(world, DayHours);
        var city = world.ActiveCities().First();
        foreach (var r in world.Weather!.Regions) { r.Kind = Precipitation.Rain; r.PrecipTenths = 200; }

        Assert.False(WeatherNow.OutdoorWorkable(world, city.Location));
    }

    [Fact]
    public void A_rough_sleeper_on_a_freezing_night_dies_of_cold_and_a_housed_neighbour_does_not()
    {
        // o lugar mais frio que a regra admite (todo mês −8 °C) e um risco por grau-noite que mata na primeira geada
        var cold = ClimateRules.Default with
        {
            Weather = WeatherRules.Default with { RoughSleepDeathPerDegNight = 10, MeanTempC = Enumerable.Repeat(-8.0, 12).ToList() },
        };
        var (world, clock) = NewWorld(1, cold);
        var rough = world.AliveNpcIndex.Alive[0];
        world.FindHousehold(rough.Household!.Value)!.RemoveMember(rough.Id);
        rough.LeaveHousehold(world.CurrentDate);
        var neighbour = world.AliveNpcIndex.Alive.First(n => n.Id != rough.Id && n.Household is not null);

        clock.Run(world, 2 * DayHours);

        Assert.False(rough.IsAlive);
        Assert.True(neighbour.IsAlive);
    }

    [Fact]
    public void A_rough_sleeper_who_survives_a_freezing_night_wakes_weaker()
    {
        var cold = ClimateRules.Default with
        {
            Weather = WeatherRules.Default with { RoughSleepDeathPerDegNight = 0.000001, MeanTempC = Enumerable.Repeat(-8.0, 12).ToList() },
        };
        var (world, clock) = NewWorld(1, cold);
        var rough = world.AliveNpcIndex.Alive[0];
        world.FindHousehold(rough.Household!.Value)!.RemoveMember(rough.Id);
        rough.LeaveHousehold(world.CurrentDate);
        int before = rough.Health;

        new WeatherSystem().Tick(world, new TickContext(world, world.Rng, world.Scheduler)); // a noite, antes da fisiologia do dia

        Assert.True(rough.Health < before, $"saúde {before} → {rough.Health}");
    }

    [Fact]
    public void Disease_season_rises_on_a_day_colder_than_normal_for_a_cold_season_disease()
    {
        var catarrh = DiseasePresets.PreIndustrial.Diseases.First(d => d.Name == "catarro");
        var diarrhoea = DiseasePresets.PreIndustrial.Diseases.First(d => d.Name == "diarreia");

        Assert.True(catarrh.Season(0, -4) > catarrh.Season(0, 0));
        Assert.True(diarrhoea.Season(7, 4) > diarrhoea.Season(7, 0));
    }

    [Fact]
    public void A_month_colder_than_normal_raises_the_death_hazard_of_the_old_more_than_of_adults()
    {
        var (world, clock) = NewWorld(1);
        clock.Run(world, DayHours);
        var cell = world.ActiveCities().First().Location;
        foreach (var r in world.Weather!.Regions) r.LastColdCenti = 400;

        var old = WeatherNow.Hazard(world, cell, 70).Multiplier;
        var adult = WeatherNow.Hazard(world, cell, 30).Multiplier;

        Assert.True(old > adult && adult > 1);
    }

    [Fact]
    public void Drought_shrinks_what_the_natural_spring_gives()
    {
        var (world, clock) = NewWorld(1);
        clock.Run(world, DayHours);
        var cell = world.ActiveCities().First().Location;
        foreach (var r in world.Weather!.Regions) { r.RainAvgTenths = 2; r.NormalAvgTenths = 20; }

        Assert.True(WeatherNow.SpringYield(world, cell) < 0.5);
    }

    [Fact]
    public void Snow_covers_the_pasture()
    {
        var (world, clock) = NewWorld(1);
        clock.Run(world, DayHours);
        var cell = world.ActiveCities().First().Location;
        foreach (var r in world.Weather!.Regions) r.SnowMm = 20;

        Assert.Equal(0.25, WeatherNow.GrazeAccess(world, cell));
    }

    [Fact]
    public void A_year_of_weather_has_rain_frost_and_snow_in_winter_and_no_snow_in_summer()
    {
        var (world, clock) = NewWorld(2);
        int winterFrost = 0, summerSnow = 0, rain = 0;
        for (int d = 0; d < 360; d++)
        {
            clock.Run(world, DayHours);
            var row = Village(world);
            int month = (int)(world.Weather!.Day / 30 % 12);
            if (month is 0 or 1 or 11 && row.Frost) winterFrost++;
            if (month is 5 or 6 or 7 && row.Kind == Precipitation.Snow) summerSnow++;
            if (row.Wet) rain++;
        }

        Assert.True(winterFrost > 10 && summerSnow == 0 && rain is > 70 and < 200, $"geadas {winterFrost} neve no verão {summerSnow} chuva {rain}");
    }
}
