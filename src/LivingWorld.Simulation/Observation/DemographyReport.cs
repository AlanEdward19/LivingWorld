using System.Globalization;
using System.Text;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Simulation.Observation;

/// <summary>Medida demográfica de uma rodada (AD-130): o sink só lê o mundo no instante do evento (nascimento e morte de
/// NPC) e a amostra mensal dá a exposição por idade. Nada é escrito no mundo: o hash é o mesmo com ou sem ela.</summary>
public sealed class DemographyTally(WorldState world) : IWorldEventSink
{
    public const int MaxAge = 120;

    public long[] DeathsByAge { get; } = new long[MaxAge + 1];
    public double[] ExposureByAge { get; } = new double[MaxAge + 1];
    public long[] BirthsByMotherAge { get; } = new long[MaxAge + 1];
    public double[] FemaleExposureByAge { get; } = new double[MaxAge + 1];
    public long[] BirthsByMonth { get; } = new long[12];
    public Dictionary<int, long> BirthsByDayOfYear { get; } = [];
    public long Births, Deaths, MaleBirths, DeathsOnBirthday, StillBirths, MaternalDeaths;
    public double PersonYears;
    public long MeasureFromTick;

    /// <summary>Por bebê: tick de nascimento e (se morreu) idade em horas na morte.</summary>
    public Dictionary<long, (long BirthTick, long? DeathAgeHours)> Babies { get; } = [];
    public Dictionary<long, List<long>> BirthTicksByMother { get; } = [];

    /// <summary>Corpo, saúde e causas de morte (AD-134).</summary>
    public BodyHealthTally Body { get; } = new(world);

    public void Record(WorldEvent evt)
    {
        Body.OnEvent(evt);
        if (evt.Tick < MeasureFromTick) return;
        switch (evt.Kind)
        {
            case WorldEventKind.StillBirth when evt.SourceSystem == YearEventTally.BirthSource:
                StillBirths++;
                return;
            case WorldEventKind.Birth when evt.SourceSystem == YearEventTally.BirthSource:
                var parts = evt.Payload.Split('|');
                var baby = world.FindNpc(new NpcId(long.Parse(parts[0], CultureInfo.InvariantCulture)));
                var mother = world.FindNpc(new NpcId(long.Parse(parts[1], CultureInfo.InvariantCulture)));
                if (baby is null) return;
                Births++;
                if (baby.Sex == Sex.Male) MaleBirths++;
                BirthsByMonth[baby.BirthDate.Month]++;
                int day = baby.BirthDate.Month * baby.BirthDate.Calendar.DaysPerMonth + baby.BirthDate.Day;
                BirthsByDayOfYear[day] = BirthsByDayOfYear.GetValueOrDefault(day) + 1;
                Babies[baby.Id.Value] = (evt.Tick, null);
                if (mother is not null)
                {
                    BirthsByMotherAge[Math.Min(MaxAge, mother.AgeYears(world.CurrentDate))]++;
                    if (!BirthTicksByMother.TryGetValue(mother.Id.Value, out var list))
                        BirthTicksByMother[mother.Id.Value] = list = [];
                    list.Add(evt.Tick);
                }

                return;
            case WorldEventKind.Death or WorldEventKind.Starvation or WorldEventKind.Dehydration
                or WorldEventKind.Exhaustion or WorldEventKind.Disease or WorldEventKind.MaternalDeath
                when evt.SourceSystem == YearEventTally.DeathSource:
                int cut = evt.Payload.IndexOf('|');
                string first = cut < 0 ? evt.Payload : evt.Payload[..cut];
                if (!long.TryParse(first, CultureInfo.InvariantCulture, out long id)) return;
                var dead = world.FindNpc(new NpcId(id));
                if (dead is null) return;
                Deaths++;
                if (evt.Kind == WorldEventKind.MaternalDeath) MaternalDeaths++;
                long ageHours = evt.Tick - dead.BirthDate.TotalHours;
                DeathsByAge[Math.Min(MaxAge, (int)(ageHours / world.Calendar.HoursPerYear))]++;
                if (ageHours % world.Calendar.HoursPerYear < world.Calendar.HoursPerDay) DeathsOnBirthday++;
                if (Babies.TryGetValue(id, out var born)) Babies[id] = (born.BirthTick, ageHours);
                return;
        }
    }

    /// <summary>Exposição de um mês: cada vivo soma 1/12 de ano à própria idade.</summary>
    public void SampleMonth()
    {
        double share = 1.0 / world.Calendar.MonthsPerYear;
        foreach (var npc in world.AliveNpcIndex.Alive)
        {
            int age = Math.Min(MaxAge, npc.AgeYears(world.CurrentDate));
            ExposureByAge[age] += share;
            if (npc.Sex == Sex.Female) FemaleExposureByAge[age] += share;
            PersonYears += share;
        }
    }
}

/// <summary>Resumo de N seeds × A anos do cenário padrão de código (<see cref="ScenarioRunner.Create"/>).</summary>
public static class DemographyReport
{
    public sealed record SeedResult(ulong Seed, int FinalAlive, int MinAlive, DemographyTally Tally);

    /// <summary>Roda uma seed; as taxas contam a partir de <paramref name="warmupYears"/> (a pirâmide inicial é sintética).</summary>
    public static SeedResult Run(ulong seed, int years, int warmupYears)
    {
        var (world, baseClock) = ScenarioRunner.Create(seed);
        var tally = new DemographyTally(world) { MeasureFromTick = warmupYears * world.Calendar.HoursPerYear };
        tally.Body.MeasureFromTick = tally.MeasureFromTick;
        var clock = new WorldClock(baseClock.Systems, baseClock.MaxIterationsPerTick, tally);
        int months = years * world.Calendar.MonthsPerYear;
        int minAlive = int.MaxValue;
        for (int m = 0; m < months; m++)
        {
            clock.Run(world, world.Calendar.HoursPerMonth);
            if (m >= warmupYears * world.Calendar.MonthsPerYear) tally.SampleMonth();
            if ((m + 1) % world.Calendar.MonthsPerYear == 0) tally.Body.SampleYear();
            minAlive = Math.Min(minAlive, world.AliveNpcIndex.Alive.Count);
            if (world.AliveNpcIndex.Alive.Count == 0) break;
        }

        return new SeedResult(seed, world.AliveNpcIndex.Alive.Count, minAlive, tally);
    }

    public static string Summarize(IReadOnlyList<SeedResult> runs, int years, int warmupYears, long hoursPerYear)
    {
        var deaths = new double[DemographyTally.MaxAge + 1];
        var exposure = new double[DemographyTally.MaxAge + 1];
        var asfrBirths = new double[DemographyTally.MaxAge + 1];
        var femaleExposure = new double[DemographyTally.MaxAge + 1];
        var months = new long[12];
        double births = 0, dead = 0, py = 0, males = 0, onBirthday = 0, still = 0, maternal = 0;
        long cohort = 0, infantDeaths = 0, cohort15 = 0, survived15 = 0, maxDayBirths = 0;
        var intervals = new List<double>();
        long followUpEnd = years * hoursPerYear;
        foreach (var r in runs)
        {
            var t = r.Tally;
            for (int a = 0; a <= DemographyTally.MaxAge; a++)
            {
                deaths[a] += t.DeathsByAge[a]; exposure[a] += t.ExposureByAge[a];
                asfrBirths[a] += t.BirthsByMotherAge[a]; femaleExposure[a] += t.FemaleExposureByAge[a];
            }

            for (int m = 0; m < 12; m++) months[m] += t.BirthsByMonth[m];
            births += t.Births; dead += t.Deaths; py += t.PersonYears; males += t.MaleBirths;
            onBirthday += t.DeathsOnBirthday; still += t.StillBirths; maternal += t.MaternalDeaths;
            if (t.BirthsByDayOfYear.Count > 0) maxDayBirths += t.BirthsByDayOfYear.Values.Max();
            foreach (var (_, (birthTick, deathAge)) in t.Babies.OrderBy(kv => kv.Key))
            {
                if (birthTick + hoursPerYear <= followUpEnd)
                {
                    cohort++;
                    if (deathAge is { } d && d < hoursPerYear) infantDeaths++;
                }

                if (birthTick + 15 * hoursPerYear <= followUpEnd)
                {
                    cohort15++;
                    if (deathAge is not { } d15 || d15 >= 15 * hoursPerYear) survived15++;
                }
            }

            foreach (var (_, ticks) in t.BirthTicksByMother.OrderBy(kv => kv.Key))
                for (int i = 1; i < ticks.Count; i++)
                    intervals.Add((ticks[i] - ticks[i - 1]) / (double)(hoursPerYear / 12));
        }

        var (e0, q0Period) = LifeExpectancy(deaths, exposure);
        double tfr = 0;
        for (int a = 0; a <= DemographyTally.MaxAge; a++)
            if (femaleExposure[a] > 0) tfr += asfrBirths[a] / femaleExposure[a];
        intervals.Sort();
        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.AppendLine(inv, $"seeds={runs.Count} anos={years} aquecimento={warmupYears}");
        sb.AppendLine(inv, $"pop_final media={runs.Average(r => r.FinalAlive):F1} min={runs.Min(r => r.FinalAlive)} max={runs.Max(r => r.FinalAlive)} extintas={runs.Count(r => r.FinalAlive == 0)} pop_min_media={runs.Average(r => r.MinAlive):F1}");
        sb.AppendLine(inv, $"natalidade_bruta={1000 * births / py:F1}/mil mortalidade_bruta={1000 * dead / py:F1}/mil nascimentos={births} mortes={dead} natimortos={still} mortes_maternas={maternal}");
        sb.AppendLine(inv, $"1q0_coorte={1000.0 * infantDeaths / Math.Max(1, cohort):F0}/mil (n={cohort}) 1q0_periodo={1000 * q0Period:F0}/mil l15_coorte={(double)survived15 / Math.Max(1, cohort15):F3} (n={cohort15}) e0={e0:F1} TFR={tfr:F2}");
        sb.AppendLine(inv, $"intervalo_partos_meses media={(intervals.Count > 0 ? intervals.Average() : 0):F1} mediana={(intervals.Count > 0 ? intervals[intervals.Count / 2] : 0):F1} n={intervals.Count}");
        sb.AppendLine(inv, $"razao_sexo_nascer={100 * males / Math.Max(1, births - males):F1} meninos/100 meninas");
        sb.AppendLine(inv, $"nascimentos_por_mes={string.Join(",", months.Select(m => (100.0 * m / Math.Max(1, births)).ToString("F1", inv)))} (%)");
        sb.AppendLine(inv, $"maior_dia_do_ano_por_seed={100.0 * maxDayBirths / Math.Max(1, births):F1}% dos nascimentos  mortes_no_aniversario={100 * onBirthday / Math.Max(1, dead):F1}%");
        sb.Append("ASFR(5a)=");
        for (int a0 = 15; a0 < 50; a0 += 5)
        {
            double b = 0, e = 0;
            for (int a = a0; a < a0 + 5; a++) { b += asfrBirths[a]; e += femaleExposure[a]; }
            sb.Append(inv, $"{a0}-{a0 + 4}:{(e > 0 ? b / e : 0):F3} ");
        }

        sb.AppendLine();
        sb.Append("seeds(final)=").AppendLine(string.Join(",", runs.Select(r => r.FinalAlive)));
        sb.Append(BodyHealthTally.Summarize(runs.Select(r => (r.Tally.Body, followUpEnd)).ToList()));
        return sb.ToString();
    }

    /// <summary>Tábua de período: q = m/(1+(1-a)m), a = 0,3 no 1º ano e 0,5 depois; último grupo aberto.</summary>
    public static (double E0, double Q0) LifeExpectancy(double[] deaths, double[] exposure)
    {
        double l = 1.0, e0 = 0, q0 = 0;
        for (int a = 0; a < deaths.Length; a++)
        {
            double m = exposure[a] > 0 ? deaths[a] / exposure[a] : (a == 0 ? 0 : 1);
            double sep = a == 0 ? 0.3 : 0.5;
            double q = a == deaths.Length - 1 ? 1.0 : Math.Min(1.0, m / (1 + (1 - sep) * m));
            if (a == 0) q0 = q;
            double d = l * q;
            e0 += a == deaths.Length - 1 ? (m > 0 ? l / m : 0) : l - (1 - sep) * d;
            l -= d;
            if (l <= 0) break;
        }

        return (e0, q0);
    }
}
