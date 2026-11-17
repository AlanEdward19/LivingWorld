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
    public long Births, Deaths, MaleBirths, DeathsOnBirthday, StillBirths, Miscarriages, MaternalDeaths;
    public double PersonYears;
    public long MeasureFromTick;

    /// <summary>Por bebê: tick de nascimento e (se morreu) idade em horas na morte.</summary>
    public Dictionary<long, (long BirthTick, long? DeathAgeHours)> Babies { get; } = [];
    public Dictionary<long, List<long>> BirthTicksByMother { get; } = [];

    /// <summary>Corpo, saúde e causas de morte (AD-134).</summary>
    public BodyHealthTally Body { get; } = new(world);

    /// <summary>Vida familiar da mesma rodada (AD-136), medida desde o início.</summary>
    public FamilyLifeTally Family { get; } = new(world);

    /// <summary>Causas de morte, anos de surto e dias perdidos por doença (AD-137).</summary>
    public DiseaseTally Disease { get; } = new(world);
    /// <summary>Vida reprodutiva (AD-139): fora de união, prematuros, idade da mãe no 1º parto.</summary>
    public ReproductiveLifeTally Reproduction { get; } = new(world);
    /// <summary>Crime, violência e justiça (AD-150).</summary>
    public CrimeTally Crime { get; } = new();

    public void Record(WorldEvent evt)
    {
        Crime.Record(evt);
        Body.OnEvent(evt);
        Family.Record(evt);
        Disease.OnEvent(evt);
        Reproduction.Record(evt);
        if (evt.Tick < MeasureFromTick) return;
        switch (evt.Kind)
        {
            case WorldEventKind.StillBirth when evt.SourceSystem == YearEventTally.BirthSource:
                // perda gestacional (aborto espontâneo) e natimorto a termo saem no mesmo evento; o marcador separa (AD-140)
                if (evt.Payload.EndsWith(LivingWorld.Simulation.Population.Lifecycle.NatalitySystem.MiscarriageMarker, StringComparison.Ordinal))
                    Miscarriages++;
                else StillBirths++;
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
                or WorldEventKind.Accident or WorldEventKind.OldAge or WorldEventKind.Homicide or WorldEventKind.Executed
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
    public sealed record SeedResult(ulong Seed, int FinalAlive, int MinAlive, DemographyTally Tally, FoodTally? Food = null, InventionSummary? Invention = null, EconomyTally? Economy = null, WorkdayTally? Workday = null, DecadeTally? Decades = null, VentureTally? Ventures = null, CreditTally? Credit = null, NeedDistressTally? Distress = null, ReferenceTally? Reference = null, DomesticHungerProbe? DomesticHunger = null);

    private sealed class BothSinks(IWorldEventSink a, IWorldEventSink b) : IWorldEventSink
    {
        public void Record(WorldEvent evt) { a.Record(evt); b.Record(evt); }
    }

    /// <summary>Roda uma seed; as taxas contam a partir de <paramref name="warmupYears"/> (a pirâmide inicial é sintética).
    /// <paramref name="observe"/> e <paramref name="afterMonth"/> só observam (corrida compartilhada dos testes, ADR-0044):
    /// o primeiro recebe o mundo recém-criado e devolve um sink extra; o segundo roda no fim de cada mês (índice 0-based).</summary>
    public static SeedResult Run(ulong seed, int years, int warmupYears, LivingWorld.Domain.Cognition.BrainRules? brain = null,
        LivingWorld.Domain.Population.Family.FamilyRules? familyRules = null,
        Func<WorldState, IWorldEventSink?>? observe = null, Action<WorldState, int>? afterMonth = null,
        LivingWorld.Domain.Behavior.NeedsRules? needsRules = null, LivingWorld.Domain.Invention.InventionRules? inventionRules = null,
        LivingWorld.Domain.Economy.AgricultureRules? agricultureRules = null, LivingWorld.Domain.Economy.EconomyRules? economyRules = null,
        LivingWorld.Domain.Crime.CrimeRules? crimeRules = null, LivingWorld.Domain.Extraordinary.ExtraordinaryScenarioData? extraordinary = null,
        Func<LivingWorld.Simulation.Scheduling.ISimulationSystem, LivingWorld.Simulation.Scheduling.ISimulationSystem>? swapSystem = null,
        LivingWorld.Domain.Ontogeny.OntogenyRules? ontogenyRules = null, LivingWorld.Domain.Cities.CityLayoutRules? cityLayout = null)
    {
        var (world, baseClock) = ScenarioRunner.Create(seed, familyRules: familyRules, needsRules: needsRules, inventionRules: inventionRules,
            agricultureRules: agricultureRules, economyRules: economyRules, crimeRules: crimeRules, extraordinary: extraordinary, ontogenyRules: ontogenyRules,
            cityLayout: cityLayout);
        if (brain is not null) world.UseBrain(brain); // AD-160b: nulo = o do cenário (ligado); BrainRules.Off desliga
        var extra = observe?.Invoke(world);
        var tally = new DemographyTally(world) { MeasureFromTick = warmupYears * world.Calendar.HoursPerYear };
        tally.Body.MeasureFromTick = tally.MeasureFromTick;
        tally.Family.MeasureFromTick = tally.MeasureFromTick;
        tally.Disease.MeasureFromTick = tally.MeasureFromTick;
        tally.Reproduction.MeasureFromTick = tally.MeasureFromTick;
        tally.Crime.MeasureFromTick = tally.MeasureFromTick;
        var food = new FoodTally(world);
        var economy = new EconomyTally(world);
        var workday = new WorkdayTally(world) { MeasureFromTick = tally.MeasureFromTick };
        var decades = new DecadeTally(world);
        var ventures = new VentureTally(world); // AD-152
        world.VentureProbe = ventures.Decisions; // AD-159: só observa
        var credit = new CreditTally(world); // AD-155
        var distress = new NeedDistressTally(world) { MeasureFromTick = tally.MeasureFromTick }; // AD-157
        var reference = new ReferenceTally(world) { MeasureFromTick = tally.MeasureFromTick }; // AD-163
        reference.Attach();
        var domestic = new DomesticHungerProbe(world) { MeasureFromTick = tally.MeasureFromTick }; // AD-164
        IWorldEventSink sinks = new BothSinks(new BothSinks(new BothSinks(new BothSinks(new BothSinks(new BothSinks(tally, food), decades), ventures), credit), distress), new BothSinks(reference, domestic));
        if (extra is not null) sinks = new BothSinks(sinks, extra);
        // AD-163: só para medir (item 8): troca um sistema do relógio por outro com regras diferentes
        var systems = swapSystem is null ? baseClock.Systems : baseClock.Systems.Select(swapSystem).ToList();
        var clock = new WorldClock(systems, baseClock.MaxIterationsPerTick, sinks);
        int months = years * world.Calendar.MonthsPerYear;
        int minAlive = int.MaxValue;
        for (int m = 0; m < months; m++)
        {
            if (m % world.Calendar.MonthsPerYear == 0) economy.SampleYear(m / world.Calendar.MonthsPerYear);
            for (int d = 0; d < world.Calendar.DaysPerMonth; d++)
            {
                for (int h = 0; h < world.Calendar.HoursPerDay; h++)
                {
                    clock.Run(world, 1);
                    workday.SampleHour(); // AD-148: o dia do adulto
                    distress.SampleHour(); // AD-157: horas com fome ou sede crítica
                    domestic.SampleHour(); // AD-164: de onde vem a fome depois da agressão
                }
                tally.Disease.SampleDay();
            }

            tally.Family.SampleMonth();
            tally.Disease.SampleMonth();
            tally.Reproduction.SampleMonth();
            if (m >= warmupYears * world.Calendar.MonthsPerYear) tally.SampleMonth();
            if ((m + 1) % world.Calendar.MonthsPerYear == 0) tally.Body.SampleYear();
            food.SampleMonth();
            decades.SampleMonth(world.CurrentDate.TotalHours);
            ventures.SampleMonth();
            credit.SampleMonth();
            reference.SampleMonth();
            domestic.SampleMonth();
            if ((m + 1) % world.Calendar.MonthsPerYear == 0) reference.SampleYear();
            afterMonth?.Invoke(world, m);
            minAlive = Math.Min(minAlive, world.AliveNpcIndex.Alive.Count);
            if (world.AliveNpcIndex.Alive.Count == 0) break;
        }

        tally.Reproduction.SampleGenetics();
        economy.SampleYear(years);
        reference.Finish();
        domestic.Finish();
        return new SeedResult(seed, world.AliveNpcIndex.Alive.Count, minAlive, tally, food, InventionSummary.Of(world), economy, workday, decades, ventures, credit, distress, reference, domestic);
    }

    public static string Summarize(IReadOnlyList<SeedResult> runs, int years, int warmupYears, long hoursPerYear)
    {
        var deaths = new double[DemographyTally.MaxAge + 1];
        var exposure = new double[DemographyTally.MaxAge + 1];
        var asfrBirths = new double[DemographyTally.MaxAge + 1];
        var femaleExposure = new double[DemographyTally.MaxAge + 1];
        var months = new long[12];
        double births = 0, dead = 0, py = 0, males = 0, onBirthday = 0, still = 0, lost = 0, maternal = 0;
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
            onBirthday += t.DeathsOnBirthday; still += t.StillBirths; lost += t.Miscarriages; maternal += t.MaternalDeaths;
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
        sb.AppendLine(inv, $"pop_final media={runs.Average(r => r.FinalAlive):F1} min={runs.Min(r => r.FinalAlive)} max={runs.Max(r => r.FinalAlive)} extintas={runs.Count(r => r.FinalAlive == 0)} pop_min_media={runs.Average(r => r.MinAlive):F1} pop_min_min={runs.Min(r => r.MinAlive)}");
        sb.AppendLine(inv, $"natalidade_bruta={1000 * births / py:F1}/mil mortalidade_bruta={1000 * dead / py:F1}/mil nascimentos={births} mortes={dead} natimortos={still} ({1000 * still / Math.Max(1, births + still):F0}/mil partos) perdas_gestacionais={lost} mortes_maternas={maternal} ({1000 * maternal / Math.Max(1, births + still):F1}/mil partos)");
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
        sb.Append(FamilyLifeTally.Summarize(runs.Select(r => r.Tally.Family).ToList()));
        sb.Append(ReproductiveLifeTally.Summarize(runs.Select(r => r.Tally.Reproduction).ToList()));
        sb.Append("seeds(final)=").AppendLine(string.Join(",", runs.Select(r => r.FinalAlive)));
        sb.Append(BodyHealthTally.Summarize(runs.Select(r => (r.Tally.Body, followUpEnd)).ToList()));
        sb.Append(DiseaseTally.Summarize(runs.Select(r => r.Tally.Disease).ToList()));
        sb.Append(CrimeTally.Summarize(runs.Select(r => (r.Tally.Crime, r.Tally.PersonYears, r.Tally.PersonYears > 0 ? (double)(years - warmupYears) : 0.0)).ToList()));
        var foods = runs.Where(r => r.Food is not null).Select(r => r.Food!).ToList();
        if (foods.Count > 0) sb.Append(FoodTally.Summarize(foods, warmupYears, 12));
        var economies = runs.Where(r => r.Economy is not null).Select(r => r.Economy!).ToList();
        if (economies.Count > 0) sb.Append(EconomyTally.Summarize(economies));
        var workdays = runs.Where(r => r.Workday is not null).Select(r => r.Workday!).ToList();
        if (workdays.Count > 0) sb.Append(WorkdayTally.Summarize(workdays));
        var decadeRuns = runs.Where(r => r.Decades is not null).Select(r => r.Decades!).ToList();
        if (decadeRuns.Count > 0) sb.Append(DecadeTally.Summarize(decadeRuns));
        var ventureRuns = runs.Where(r => r.Ventures is not null).Select(r => r.Ventures!).ToList();
        if (ventureRuns.Count > 0) sb.Append(VentureTally.Summarize(ventureRuns, hoursPerYear));
        var creditRuns = runs.Where(r => r.Credit is not null).Select(r => r.Credit!).ToList();
        if (creditRuns.Count > 0) sb.Append(CreditTally.Summarize(creditRuns));
        sb.Append(InventionSummary.Summarize(runs.Where(r => r.Invention is not null).Select(r => r.Invention!).ToList()));
        sb.Append(NeedDistressTally.Summarize(runs.Where(r => r.Distress is not null).Select(r => (r.Seed, r.Distress!)).ToList(), hoursPerYear));
        foreach (var r in runs) sb.AppendLine(MeasureLine(r, hoursPerYear, followUpEnd));
        return sb.ToString();
    }

    /// <summary>AD-163: uma linha `medida seed=...` por seed, só `chave=valor`, para o script que pareia dois braços
    /// (`scripts/paired-rates.py`): pessoas-ano e mortes por causa nas faixas de <see cref="DiseaseTally.BandNames"/> (desde o
    /// aquecimento), mortes, fome e pessoas-ano por década (desde o ano 0), nascidos por sexo, natimortos, 1º ano da coorte,
    /// construções, ofício seguido e os contadores de <see cref="ReferenceTally"/>.</summary>
    public static string MeasureLine(SeedResult r, long hoursPerYear, long followUpEnd)
    {
        var inv = CultureInfo.InvariantCulture;
        var t = r.Tally;
        var bands = DiseaseTally.BandStarts;
        var pa = new double[bands.Length];
        for (int a = 0; a <= DemographyTally.MaxAge; a++)
        {
            int b = bands.Length - 1;
            while (b > 0 && a < bands[b]) b--;
            pa[b] += t.ExposureByAge[a];
        }
        var byCause = new SortedDictionary<string, long[]>(StringComparer.Ordinal);
        foreach (var (cause, row) in t.Disease.DeathsByCauseAndBand)
        {
            string key = cause.StartsWith("Disease:", StringComparison.Ordinal) ? "Disease" : cause.Replace(':', '.'); // Death.nome (AD-164)
            if (!byCause.TryGetValue(key, out var acc)) byCause[key] = acc = new long[bands.Length];
            for (int i = 0; i < row.Length; i++) acc[i] += row[i];
        }
        long cohort = 0, infant = 0;
        foreach (var (_, (birthTick, deathAge)) in t.Babies)
            if (birthTick + hoursPerYear <= followUpEnd)
            {
                cohort++;
                if (deathAge is { } d && d < hoursPerYear) infant++;
            }
        var sb = new StringBuilder();
        sb.Append(inv, $"medida seed={r.Seed} pop_final={r.FinalAlive} pop_min={r.MinAlive} pa={string.Join('/', pa.Select(x => x.ToString("F1", inv)))}");
        foreach (var (cause, row) in byCause) sb.Append(inv, $" d.{cause}={string.Join('/', row)}");
        sb.Append(inv, $" nasc={t.Births} nasc_m={t.MaleBirths} nasc_f={t.Births - t.MaleBirths} natimortos={t.StillBirths} perdas={t.Miscarriages} coorte1={cohort} mortes1={infant}");
        if (r.Decades is { } dec)
        {
            sb.Append(" pa_dec=").Append(string.Join('/', dec.Data.Select(d => d.PersonYears.ToString("F1", inv))));
            sb.Append(" mortes_dec=").Append(string.Join('/', dec.Data.Select(d => d.Deaths)));
            sb.Append(" fome_dec=").Append(string.Join('/', dec.Data.Select(d => d.Starved)));
            sb.Append(" nasc_dec=").Append(string.Join('/', dec.Data.Select(d => d.Births)));
        }
        if (r.Invention is { } invention) sb.Append(inv, $" constr_boas={invention.Tally.Built} constr_falhas={invention.Tally.BuildFailures}");
        var fam = t.Family;
        sb.Append(inv, $" oficio_n={fam.FollowCompared} oficio_segue={fam.Followed} oficio_acaso={fam.FollowByChance:F2} oficio_alta_n={fam.HighPressureCompared} oficio_alta={fam.HighPressureFollowed} oficio_baixa_n={fam.LowPressureCompared} oficio_baixa={fam.LowPressureFollowed}");
        r.Reference?.Append(sb);
        r.DomesticHunger?.Append(sb);
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
