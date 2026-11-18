using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Population.Lifecycle;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Climate;

/// <summary>Tempo do dia (AD-180), <c>Daily</c> na primeira hora: gera o tempo de cada região (temperatura do dia com a anomalia que
/// persiste, chuva em frentes, neve, geada, granizo, vento, neve e lama no chão, seca pela chuva da janela), fecha o mês do corpo, abre
/// o regime do ano em janeiro e, no mês do fator, grava o fator de safra de cada região a partir da estação que passou. Quem dorme
/// ao relento numa noite de geada arrisca morrer de frio. Sem tempo ligado, não faz nada.</summary>
[LivingCapability("WEATHER", Consumers = ["climate.temperature", "economy.crop", "streets.travel",
    "health.outbreak", "population.mortality", "cities.construction", "ecology.fauna", "economy.natural-water", "inspector.climate"])]
public sealed class WeatherSystem : ISimulationSystem
{
    public const string SystemName = "weather";

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Daily;

    public void Tick(WorldState world, TickContext ctx)
    {
        if (!world.WeatherOn) return;
        var climate = world.ClimateRules;
        var rules = climate.Weather!;
        var cal = world.Calendar;
        long day = ctx.CurrentTick / cal.HoursPerDay;
        var ledger = world.Weather;
        if (ledger is null)
        {
            ledger = new WeatherLedger();
            world.UseWeather(ledger);
        }
        if (ledger.Day == day) return;

        bool first = ledger.Day < 0;
        EnsureRegions(world, ledger);
        if (ledger.ReferenceCenti == int.MinValue)
            ledger.ReferenceCenti = world.Cities.OrderBy(c => c.FoundedAtTick).ThenBy(c => c.Id.Value).FirstOrDefault() is { } oldest
                && world.Map.TryGetBaseClimate(oldest.Location, out float baseC, out _)
                    ? (int)Math.Round(baseC * 100) : (int)Math.Round(rules.ReferenceBaseC * 100);
        int month = world.CurrentDate.Month, dom = world.CurrentDate.Day;
        long year = world.ClimateYearOf(ctx.CurrentTick);
        double doy = month * cal.DaysPerMonth + dom + 0.5;
        bool newYear = first || (month == 0 && dom == 0);
        bool newMonth = !first && dom == 0;

        if (newMonth)
        {
            int previous = (month + cal.MonthsPerYear - 1) % cal.MonthsPerYear;
            double normal = rules.Interpolate(rules.MeanTempC, previous * cal.DaysPerMonth + cal.DaysPerMonth / 2.0, cal.DaysPerMonth, cal.MonthsPerYear);
            foreach (var row in ledger.Regions) WeatherGenerator.CloseMonth(rules, row, normal);
        }

        if (newYear)
        {
            ulong sharedYear = world.Rng.StreamStateFor("weather-regime-shared", year);
            double sharedNormal = WeatherGenerator.Read(ref sharedYear).TempNormal;
            foreach (var row in ledger.Regions)
            {
                ulong local = world.Rng.StreamStateFor("weather-regime", unchecked(year * 1_000_003L + row.Region));
                WeatherGenerator.NewYear(climate, row, sharedNormal, WeatherGenerator.Read(ref local).TempNormal, first);
            }
        }

        // o fator de safra do ano sai da estação que passou (uma fonte só: o tempo), antes de a espiga poder amadurecer
        if (!first && dom == 0 && month == rules.CropFactorMonth)
        {
            foreach (var row in ledger.Regions)
                world.SetClimateYear(new LivingWorld.Domain.Geography.RegionId(row.Region), year,
                    WeatherGenerator.CropFactor(climate, rules, WeatherGenerator.CropIndex(rules, row)));
            world.PurgeClimateYearsBefore(year - WorldState.ClimateYearsKept);
        }

        ulong sharedState = world.Rng.StreamStateFor("weather-shared", day);
        var shared = WeatherGenerator.Read(ref sharedState);
        foreach (var row in ledger.Regions)
        {
            ulong localState = world.Rng.StreamStateFor("weather", unchecked(day * 1_000_003L + row.Region));
            WeatherGenerator.Step(rules, row, shared, WeatherGenerator.Read(ref localState), doy, cal.DaysPerMonth, cal.MonthsPerYear, first);
            if (!WeatherNow.OutdoorWorkable(rules, row)) row.OutdoorLostDays++;
        }

        ledger.Day = day;
        ledger.DaylightMinutes = (int)Math.Round(rules.DaylightHours(doy, cal.DaysPerMonth * cal.MonthsPerYear) * 60);
        world.TouchWeather();
        Exposure(world, ctx, rules, ledger, day);
    }

    /// <summary>Uma linha por região do mapa, em ordem de id (região nova entra com o regime do ano em zero).</summary>
    private static void EnsureRegions(WorldState world, WeatherLedger ledger)
    {
        if (ledger.Regions.Count == world.Map.Regions.Count) return;
        var known = new HashSet<int>(ledger.Regions.Select(r => r.Region));
        foreach (var region in world.Map.Regions)
            if (known.Add(region.Id.Value)) ledger.Regions.Add(new WeatherRegionRow { Region = region.Id.Value });
        ledger.Regions.Sort((a, b) => a.Region.CompareTo(b.Region));
        ledger.Reindex();
    }

    /// <summary>A noite ao relento (AD-180): quem não tem casa dorme onde está; numa noite abaixo de zero (sensação, com o vento) o
    /// risco de morrer de frio cresce com os graus abaixo de zero, maior para criança pequena e velho. Só nas noites de geada, e só
    /// quem não tem teto: a casa (sem lenha nem roupa no motor) é o abrigo.</summary>
    private static void Exposure(WorldState world, TickContext ctx, Domain.Climate.WeatherRules rules, WeatherLedger ledger, long day)
    {
        if (rules.RoughSleepDeathPerDegNight <= 0) return;
        bool anyFrost = false;
        foreach (var row in ledger.Regions) anyFrost |= row.Frost;
        if (!anyFrost) return;

        var alive = world.AliveNpcIndex.Alive;
        List<Npc>? dying = null;
        for (int i = 0; i < alive.Count; i++)
        {
            var npc = alive[i];
            if (npc.Household is { } h && world.FindHousehold(h) is { IsHomeless: false }) continue;
            if (WeatherNow.RowAt(world, npc.CurrentLocation) is not { } row) continue;
            double night = WeatherNow.ApparentC(row.MinCenti / 100.0, row);
            if (night >= 0) continue;
            double age = Math.Max(0, ctx.CurrentTick - npc.BirthDate.TotalHours) / (double)world.Calendar.HoursPerYear;
            double p = rules.RoughSleepDeathPerDegNight * -night * (age < 5 || age >= 60 ? rules.RoughSleepVulnerableFactor : 1);
            if (world.Rng.DrawFor("weather-exposure", unchecked(npc.Id.Value * 1_000_003L + day)) < p) (dying ??= []).Add(npc);
            // quem passa a noite gelada e não morre acorda mais fraco (a saúde dinâmica do AD-134 recupera com comida e sono)
            else if (rules.RoughSleepHealthPerDegNight > 0)
                npc.SetHealth(npc.Health - (int)Math.Ceiling(rules.RoughSleepHealthPerDegNight * -night));
        }

        if (dying is null) return;
        foreach (var npc in dying)
            if (npc.IsAlive) NpcDeath.Apply(world, ctx, npc, WorldEventKind.Death, "frio");
    }
}
