using LivingWorld.Domain.Climate;
using LivingWorld.Domain.Geography;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Extraordinary.Mechanics;

namespace LivingWorld.Simulation.Climate;

/// <summary>Estado do chão de terra pelo tempo (AD-180).</summary>
public enum GroundWeather : byte { Dry = 0, Wet = 1, Mud = 2, HeavyMud = 3, Snow = 4, Frozen = 5 }

/// <summary>Leitura do tempo (AD-180): o que os outros sistemas perguntam ao tempo do dia. Uma consulta à linha da região; nada por
/// NPC além disso. Com o tempo desligado, todas as respostas são as neutras (sem efeito).</summary>
public static class WeatherNow
{
    /// <summary>A linha do tempo da região da célula; nula com o tempo desligado ou fora do mapa.</summary>
    public static WeatherRegionRow? RowAt(WorldState world, CellCoord cell) =>
        world.Weather is { } ledger && world.Map.TryGetBaseClimate(cell, out _, out var region) ? ledger.Find(region.Value) : null;

    /// <summary>Temperatura na célula e na hora: o campo do dia (base da célula + tempo da região + poderes) com a curva do dia
    /// (mínima na alvorada, máxima às 15 h).</summary>
    public static double TemperatureAt(WorldState world, CellCoord cell, long tick)
    {
        double daily = EnvironmentTemperatureMechanic.EffectiveTemperature(world, cell, tick);
        if (RowAt(world, cell) is not { } row) return daily;
        int hour = (int)(tick % world.Calendar.HoursPerDay);
        return daily + DiurnalShape(hour, world.Calendar.HoursPerDay) * (row.MaxCenti - row.MinCenti) / 200.0;
    }

    /// <summary>Forma do dia em [−1, 1]: −1 às 5 h, +1 às 15 h, linear entre elas (média zero no dia).</summary>
    public static double DiurnalShape(int hour, int hoursPerDay)
    {
        double h = hour * 24.0 / hoursPerDay;
        if (h >= 5 && h <= 15) return -1 + 2 * (h - 5) / 10;
        double since = h > 15 ? h - 15 : h + 9;
        return 1 - 2 * since / 14;
    }

    /// <summary>Sensação: o vento tira calor abaixo de 10 °C e a chuva molha a roupa (números escolhidos, ver doc de domínio).</summary>
    public static double ApparentC(double temperature, WeatherRegionRow row)
    {
        double wind = row.WindDecims / 10.0;
        double chill = temperature < 10 ? 0.7 * Math.Max(0, wind - 1.3) * (10 - temperature) / 10 : 0;
        return temperature - chill - (row.Kind == Precipitation.Rain ? 2 : 0);
    }

    public static GroundWeather Ground(WeatherRules r, WeatherRegionRow row) =>
        row.SnowMm >= 1 ? GroundWeather.Snow
        : row.MeanCenti <= r.FrozenGroundMeanC * 100 ? GroundWeather.Frozen
        : row.SoilPm >= r.HeavyMudAbovePm ? GroundWeather.HeavyMud
        : row.SoilPm >= r.MudAbovePm ? GroundWeather.Mud
        : row.SoilPm >= r.WetAbovePm ? GroundWeather.Wet
        : GroundWeather.Dry;

    /// <summary>Quanto o tempo alonga a viagem: a lama na parte da rota sem calçamento (<paramref name="softPm"/> ‰) e a neve na
    /// rota toda. 1 sem efeito.</summary>
    public static double TravelFactor(WorldState world, WeatherRegionRow row, int softPm)
    {
        var r = world.ClimateRules.Weather!;
        double mud = Ground(r, row) switch
        {
            GroundWeather.HeavyMud => r.HeavyMudTravelFactor,
            GroundWeather.Mud => r.MudTravelFactor,
            GroundWeather.Wet => r.WetTravelFactor,
            _ => 1.0,
        };
        double snow = Math.Min(r.MaxSnowTravelFactor, 1 + r.SnowTravelPerCm * row.SnowMm);
        return snow * (1 + softPm / 1000.0 * (mud - 1));
    }

    /// <summary>A viagem de <paramref name="origin"/> a <paramref name="destination"/> muda com o tempo (lama ou neve no lugar de saída).</summary>
    public static bool SlowsTravel(WorldState world, CellCoord origin, out WeatherRegionRow row)
    {
        row = null!;
        if (RowAt(world, origin) is not { } found) return false;
        var g = Ground(world.ClimateRules.Weather!, found);
        if (g is GroundWeather.Dry or GroundWeather.Frozen) return false;
        row = found;
        return true;
    }

    /// <summary>Lavoura, obra e coleta andam: sem chuva forte, sem neve no chão, sem chão congelado.</summary>
    public static bool OutdoorWorkable(WeatherRules r, WeatherRegionRow row) =>
        !(row.Kind == Precipitation.Rain && row.PrecipTenths >= r.HeavyRainMm * 10)
        && row.SnowMm < r.FieldSnowCm && row.MeanCenti > r.FrozenGroundMeanC * 100;

    public static bool OutdoorWorkable(WorldState world, CellCoord cell) =>
        RowAt(world, cell) is not { } row || OutdoorWorkable(world.ClimateRules.Weather!, row);

    /// <summary>AD-194: por que o trabalho ao ar livre não anda hoje: <c>neve</c>, <c>gelo</c> (chão congelado) ou <c>chuva</c>.</summary>
    public static string LostDayReason(WorldState world, CellCoord cell)
    {
        if (RowAt(world, cell) is not { } row) return "chuva";
        var r = world.ClimateRules.Weather!;
        return row.SnowMm >= r.FieldSnowCm ? "neve" : row.MeanCenti <= r.FrozenGroundMeanC * 100 ? "gelo" : "chuva";
    }

    /// <summary>A ceifa anda: grão molhado não se corta nem se guarda.</summary>
    public static bool CanReap(WorldState world, CellCoord cell) =>
        RowAt(world, cell) is not { } row
        || row.PrecipTenths < world.ClimateRules.Weather!.ReapStopsAtOrAboveMm * 10 && row.SnowMm < 1;

    /// <summary>Fração da jornada do campo que a luz do dia permite (do nascer ao pôr do sol). 1 com o tempo desligado.</summary>
    public static double DaylightFactor(WorldState world) =>
        world.Weather is { Day: >= 0 } ledger && world.WeatherOn
            ? Math.Min(1.0, ledger.DaylightMinutes / 60.0 / Math.Max(1, world.ClimateRules.Weather!.FieldDayHours))
            : 1.0;

    /// <summary>Granizo hoje na célula.</summary>
    public static bool Hail(WorldState world, CellCoord cell) => RowAt(world, cell) is { Hail: true };

    /// <summary>Razão da chuva da janela sobre a normal (1 = normal; abaixo do limiar é seca).</summary>
    public static double RainRatio(WeatherRegionRow row) =>
        row.NormalAvgTenths > 0 ? row.RainAvgTenths / (double)row.NormalAvgTenths : 1.0;

    public static bool InDrought(WeatherRules r, WeatherRegionRow row) => RainRatio(row) < r.DroughtBelowRatio;

    /// <summary>Fração da água que a fonte natural dá: 1 fora da seca; na seca cai com a chuva até o piso.</summary>
    public static double SpringYield(WorldState world, CellCoord cell)
    {
        if (RowAt(world, cell) is not { } row) return 1.0;
        var r = world.ClimateRules.Weather!;
        double ratio = RainRatio(row);
        return ratio >= r.DroughtBelowRatio ? 1.0 : Math.Max(r.DroughtSpringFloor, ratio / r.DroughtBelowRatio);
    }

    /// <summary>Anomalia de temperatura do dia (°C, com o regime do ano) na célula; 0 sem tempo.</summary>
    public static double AnomalyC(WorldState world, CellCoord cell) =>
        RowAt(world, cell) is { } row ? (row.AnomalyCenti + row.AdvectionCenti) / 100.0 + row.RegimeMilli / 1000.0 * world.ClimateRules.Weather!.RegimeTempC : 0;

    /// <summary>Mau tempo para sair sem precisar: tempestade, chuva forte, nevasca ou frio de geada de dia. 0 bom, até 1.</summary>
    public static double OutingDiscomfort(WorldState world, CellCoord cell)
    {
        if (RowAt(world, cell) is not { } row) return 0;
        var r = world.ClimateRules.Weather!;
        if (row.Storm) return 1;
        if (row.Kind == Precipitation.Snow) return 0.8;
        if (row.Kind == Precipitation.Rain && row.PrecipTenths >= r.HeavyRainMm * 10) return 0.7;
        if (row.Kind == Precipitation.Rain) return 0.3;
        return row.MaxCenti < 0 ? 0.5 : 0;
    }

    /// <summary>Pasto que o animal alcança sob a neve (fração).</summary>
    public static double GrazeAccess(WorldState world, CellCoord cell)
    {
        if (RowAt(world, cell) is not { } row) return 1;
        var r = world.ClimateRules.Weather!;
        return row.SnowMm >= r.GrazeQuarterSnowCm ? 0.25 : row.SnowMm >= r.GrazeHalfSnowCm ? 0.5 : 1;
    }

    /// <summary>AD-189: parte do pasto que a vegetação dá hoje pela folhagem (erva e broto): cheia 1, nua
    /// <see cref="WeatherRules.BareForageShare"/>. 1 com o tempo ou a folhagem desligados.</summary>
    public static double ForageShare(WorldState world, CellCoord cell)
    {
        if (RowAt(world, cell) is not { } row) return 1;
        var r = world.ClimateRules.Weather!;
        if (!r.FoliageEnabled) return 1;
        double bare = Math.Clamp(r.BareForageShare, 0, 1);
        return bare + (1 - bare) * Math.Clamp(row.LeafPm / 1000.0, 0, 1);
    }

    /// <summary>Carroça passa: sem neve funda e sem lamaçal na saída.</summary>
    public static bool CartPasses(WorldState world, CellCoord cell)
    {
        if (RowAt(world, cell) is not { } row) return true;
        var r = world.ClimateRules.Weather!;
        return row.SnowMm < r.CartStopSnowCm && Ground(r, row) != GroundWeather.HeavyMud;
    }

    /// <summary>Excesso de risco de morte do mês que passou (frio abaixo do normal na estação fria; calor das máximas para criança
    /// pequena e velho), e a parte dele que é do calor. 1 sem efeito.</summary>
    public static (double Multiplier, double HeatShare) Hazard(WorldState world, CellCoord cell, double ageYears)
    {
        if (RowAt(world, cell) is not { } row) return (1, 0);
        var r = world.ClimateRules.Weather!;
        bool vulnerable = ageYears < 1 || ageYears >= 60;
        double cold = row.LastColdCenti / 100.0 * (vulnerable ? r.ColdHazardPerC : r.ColdHazardPerCOthers);
        double heat = vulnerable ? row.LastHeatCenti / 100.0 * r.HeatHazardPerC : 0;
        double excess = cold + heat;
        return excess <= 0 ? (1, 0) : (1 + excess, heat / excess);
    }
}
