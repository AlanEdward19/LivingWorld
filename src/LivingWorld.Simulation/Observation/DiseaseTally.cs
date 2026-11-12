using System.Globalization;
using System.Text;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Observation;

/// <summary>Causas de morte e doença de uma rodada (AD-137), só leitura: mortes por causa (doença nomeada pelo perfil) e faixa
/// de idade, mortes e pessoas-ano por ano (para ver os anos de surto), surtos abertos e dias de trabalho perdidos por doença.</summary>
public sealed class DiseaseTally(WorldState world)
{
    public static readonly int[] BandStarts = [0, 1, 5, 15, 40, 60];
    public static readonly string[] BandNames = ["0", "1-4", "5-14", "15-39", "40-59", "60+"];

    public long MeasureFromTick;
    public SortedDictionary<string, long[]> DeathsByCauseAndBand { get; } = new(StringComparer.Ordinal);
    public SortedDictionary<int, long> DeathsByYear { get; } = [];
    public SortedDictionary<int, double> PersonYearsByYear { get; } = [];
    public SortedDictionary<int, SortedDictionary<string, long>> CausesByYear { get; } = [];
    public SortedDictionary<int, List<string>> OutbreaksByYear { get; } = [];
    public double LostWorkDays, CareDays, WorkerYears;
    private readonly Dictionary<long, int> _diseaseOf = [];

    public void OnEvent(WorldEvent evt)
    {
        if (evt.Kind == WorldEventKind.Disease && evt.SourceSystem != YearEventTally.DeathSource)
        {
            var parts = evt.Payload.Split('|');
            if (parts.Length >= 2 && long.TryParse(parts[0], CultureInfo.InvariantCulture, out long sick)
                && int.TryParse(parts[1], CultureInfo.InvariantCulture, out int disease))
                _diseaseOf[sick] = disease;
            return;
        }

        int year = (int)(evt.Tick / world.Calendar.HoursPerYear);
        if (evt.Kind == WorldEventKind.OutbreakStarted && evt.Tick >= MeasureFromTick)
        {
            if (!OutbreaksByYear.TryGetValue(year, out var list)) OutbreaksByYear[year] = list = [];
            list.Add(NameOf(int.Parse(evt.Payload.Split('|')[0], CultureInfo.InvariantCulture)));
            return;
        }

        if (evt.SourceSystem != YearEventTally.DeathSource || evt.Tick < MeasureFromTick) return;
        if (!DemographyDeath.IsNpcDeath(evt.Kind)) return;
        int cut = evt.Payload.IndexOf('|');
        if (!long.TryParse(cut < 0 ? evt.Payload : evt.Payload[..cut], CultureInfo.InvariantCulture, out long id)) return;
        if (world.FindNpc(new NpcId(id)) is not { } dead) return;
        string cause = evt.Kind == WorldEventKind.Disease && _diseaseOf.TryGetValue(id, out int d) ? "Disease:" + NameOf(d) : evt.Kind.ToString();
        int age = (int)((evt.Tick - dead.BirthDate.TotalHours) / world.Calendar.HoursPerYear);
        int band = BandStarts.Length - 1;
        while (band > 0 && age < BandStarts[band]) band--;
        if (!DeathsByCauseAndBand.TryGetValue(cause, out var row)) DeathsByCauseAndBand[cause] = row = new long[BandStarts.Length];
        row[band]++;
        DeathsByYear[year] = DeathsByYear.GetValueOrDefault(year) + 1;
        if (!CausesByYear.TryGetValue(year, out var causes)) CausesByYear[year] = causes = new(StringComparer.Ordinal);
        causes[cause] = causes.GetValueOrDefault(cause) + 1;
    }

    /// <summary>Exposição do mês por ano de calendário.</summary>
    public void SampleMonth()
    {
        if (world.CurrentDate.TotalHours < MeasureFromTick) return;
        int year = (int)(world.CurrentDate.TotalHours / world.Calendar.HoursPerYear);
        PersonYearsByYear[year] = PersonYearsByYear.GetValueOrDefault(year) + world.AliveNpcIndex.Alive.Count / (double)world.Calendar.MonthsPerYear;
    }

    /// <summary>Um dia: quem tem emprego e está de cama perde o dia; quem cuida perde a parte do dia do cuidado.</summary>
    public void SampleDay()
    {
        if (world.CurrentDate.TotalHours < MeasureFromTick) return;
        var (lost, care, workers) = LivingWorld.Simulation.Health.SickBehavior.WorkDaysLostToday(world);
        LostWorkDays += lost;
        CareDays += care;
        WorkerYears += workers / (double)(world.Calendar.DaysPerMonth * world.Calendar.MonthsPerYear);
    }

    private string NameOf(int disease) =>
        world.DiseaseRules.Find(disease) is { Name: { Length: > 0 } name } ? name : disease.ToString(CultureInfo.InvariantCulture);

    public static string Summarize(IReadOnlyList<DiseaseTally> runs)
    {
        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        var table = new SortedDictionary<string, long[]>(StringComparer.Ordinal);
        foreach (var t in runs)
            foreach (var (cause, row) in t.DeathsByCauseAndBand)
            {
                if (!table.TryGetValue(cause, out var acc)) table[cause] = acc = new long[BandStarts.Length];
                for (int b = 0; b < row.Length; b++) acc[b] += row[b];
            }

        long total = Math.Max(1, table.Values.Sum(r => r.Sum()));
        long unknown = table.TryGetValue(nameof(WorldEventKind.Death), out var u) ? u.Sum() : 0;
        sb.AppendLine(inv, $"causa_explicita={100.0 * (total - unknown) / total:F1}% (mortes={total}; Death = sem causa)");
        sb.AppendLine("mortes_causa_por_idade " + string.Join(" ", BandNames));
        foreach (var (cause, row) in table)
            sb.AppendLine(inv, $"  {cause}: {string.Join(" ", row)} ({100.0 * row.Sum() / total:F1}%)");
        var bandTotals = new long[BandStarts.Length];
        foreach (var row in table.Values) for (int b = 0; b < row.Length; b++) bandTotals[b] += row[b];
        sb.AppendLine("  total: " + string.Join(" ", bandTotals));

        // variação de ano a ano: taxa bruta anual por seed; ano de surto = mortalidade >= 1,5 × a mediana da própria seed
        var cvs = new List<double>();
        var dispersions = new List<double>();
        long surgeYears = 0, years = 0;
        var surgeCauses = new SortedDictionary<string, long>(StringComparer.Ordinal);
        var outbreakNames = new SortedDictionary<string, long>(StringComparer.Ordinal);
        foreach (var t in runs)
        {
            var rates = new List<(int Year, double Rate)>();
            foreach (var (year, py) in t.PersonYearsByYear)
                if (py >= 10) rates.Add((year, 1000.0 * t.DeathsByYear.GetValueOrDefault(year) / py));
            if (rates.Count < 3) continue;
            years += rates.Count;
            double mean = rates.Average(r => r.Rate);
            double sd = Math.Sqrt(rates.Sum(r => (r.Rate - mean) * (r.Rate - mean)) / (rates.Count - 1));
            if (mean > 0) cvs.Add(sd / mean);
            // dispersão das mortes anuais (variância / média): ~1 é ruído de Poisson de uma taxa lisa, acima disso há anos de surto
            var counts = rates.Select(r => (double)t.DeathsByYear.GetValueOrDefault(r.Year)).ToList();
            double countMean = counts.Average();
            if (countMean > 0) dispersions.Add(counts.Sum(c => (c - countMean) * (c - countMean)) / (counts.Count - 1) / countMean);
            var sorted = rates.Select(r => r.Rate).OrderBy(r => r).ToList();
            double median = sorted[sorted.Count / 2];
            foreach (var (year, rate) in rates)
            {
                if (rate < 1.5 * median || t.DeathsByYear.GetValueOrDefault(year) < 5) continue;
                surgeYears++;
                if (t.CausesByYear.TryGetValue(year, out var causes))
                    foreach (var (cause, n) in causes) surgeCauses[cause] = surgeCauses.GetValueOrDefault(cause) + n;
                if (t.OutbreaksByYear.TryGetValue(year, out var names))
                    foreach (var name in names) outbreakNames[name] = outbreakNames.GetValueOrDefault(name) + 1;
            }
        }

        long surgeDeaths = Math.Max(1, surgeCauses.Values.Sum());
        long outbreaks = runs.Sum(t => t.OutbreaksByYear.Values.Sum(l => l.Count));
        sb.AppendLine(inv, $"mortalidade_anual cv_medio_por_seed={(cvs.Count > 0 ? cvs.Average() : 0):F2} dispersao_mortes_anuais={(dispersions.Count > 0 ? dispersions.Average() : 0):F2} anos_de_surto={surgeYears} de {years} ({100.0 * surgeYears / Math.Max(1, years):F1}%) surtos_abertos={outbreaks}");
        sb.AppendLine("  mortes_nos_anos_de_surto " + string.Join(" ", surgeCauses.OrderByDescending(c => c.Value)
            .Select(c => string.Create(inv, $"{c.Key}:{100.0 * c.Value / surgeDeaths:F0}%"))));
        sb.AppendLine("  surtos_abertos_nos_anos_de_surto " + string.Join(" ", outbreakNames.Select(c => $"{c.Key}:{c.Value}")));
        double workerYears = Math.Max(1e-9, runs.Sum(t => t.WorkerYears));
        sb.AppendLine(inv, $"dias_trabalho_perdidos_por_trabalhador_ano doente={runs.Sum(t => t.LostWorkDays) / workerYears:F2} cuidando={runs.Sum(t => t.CareDays) / workerYears:F2}");
        return sb.ToString();
    }
}

/// <summary>Quais tipos de evento são morte de NPC (fonte <see cref="YearEventTally.DeathSource"/>).</summary>
public static class DemographyDeath
{
    public static bool IsNpcDeath(WorldEventKind kind) => kind is WorldEventKind.Death or WorldEventKind.Starvation
        or WorldEventKind.Dehydration or WorldEventKind.Exhaustion or WorldEventKind.Disease or WorldEventKind.MaternalDeath
        or WorldEventKind.Accident or WorldEventKind.OldAge;
}
