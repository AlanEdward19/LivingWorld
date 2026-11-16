using System.Globalization;
using System.Text;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Observation;

/// <summary>Demografia por década (AD-149, backlog 08 item 49): natalidade, mortalidade, TFR, idade da mulher ao 1º
/// casamento, crescimento anual, comida colhida por pessoa, braços e vagas da fazenda, e quem poderia emigrar e por que não
/// vai (cidade com vaga paga aberta). Só lê eventos e o mundo no fim de cada mês.</summary>
public sealed class DecadeTally(WorldState world) : IWorldEventSink
{
    public const int Decades = 10;
    public const int FarmType = 1;

    public sealed class Decade
    {
        public double PersonYears, AdultMonths, EmployedAdultMonths, FarmWorkerMonths, FarmSlotMonths, OpenMonths, Months;
        public double YoungIdleMonths, YoungIdleSingleMonths, ArableSeedMonths;
        public long Births, Deaths, Starved, Emigrants, FoodProduced, PopStart = -1, PopEnd;
        public readonly double[] AsfrBirths = new double[50];
        public readonly double[] FemaleExposure = new double[50];
        public readonly List<double> FirstMarriageWomen = [];
        /// <summary>AD-158: idade do homem ao 1º casamento, intervalos entre partos (meses), idade da mãe no último parto
        /// (contada quando ela faz 50), quem chega aos 45 sem nunca ter casado e partos de mãe sem cônjuge.</summary>
        public readonly List<double> FirstMarriageMen = [];
        public readonly List<double> BirthIntervalMonths = [];
        public readonly List<double> LastBirthAge = [];
        public long WomenAt45, WomenNeverMarried45, MenAt45, MenNeverMarried45, BirthsWithoutSpouse, MaritalBirths;
        public double MarriedWomen20to49Years;
        /// <summary>Diagnóstico do freio (AD-158): recusas por falta de meios, casamentos com o noivo empregado e os meios do
        /// casal na data.</summary>
        public long RejectedNoMeans, FirstMarriages, GroomEmployed;
        public double MeansAtMarriage, DwellingMonths, VacantMonths;
    }

    public Decade[] Data { get; } = Enumerable.Range(0, Decades).Select(_ => new Decade()).ToArray();
    private readonly HashSet<long> _everMarried = [];
    private readonly HashSet<long> _counted45 = [], _counted50 = [];
    private readonly Dictionary<long, long> _lastBirth = [];
    private long _lastFood;
    public const int NeverMarriedAge = 45, LastBirthAgeCheck = 50;

    private Decade At(long tick) => Data[Math.Clamp((int)(tick / world.Calendar.HoursPerYear / 10), 0, Decades - 1)];

    public void Record(WorldEvent evt)
    {
        switch (evt.Kind)
        {
            case WorldEventKind.Birth when evt.SourceSystem == YearEventTally.BirthSource:
                var d = At(evt.Tick);
                d.Births++;
                var parts = evt.Payload.Split('|');
                if (parts.Length > 1 && world.FindNpc(new NpcId(long.Parse(parts[1], CultureInfo.InvariantCulture))) is { } mother)
                {
                    int age = mother.AgeYears(world.CurrentDate);
                    if (age is >= 0 and < 50) d.AsfrBirths[age]++;
                    if (mother.Spouse is null || world.FindNpc(mother.Spouse.Value) is not { IsAlive: true }) d.BirthsWithoutSpouse++;
                    else d.MaritalBirths++;
                    if (_lastBirth.TryGetValue(mother.Id.Value, out long prev))
                        d.BirthIntervalMonths.Add((evt.Tick - prev) / (double)world.Calendar.HoursPerMonth);
                    _lastBirth[mother.Id.Value] = evt.Tick;
                }
                return;
            case WorldEventKind.Death or WorldEventKind.Starvation or WorldEventKind.Dehydration
                or WorldEventKind.Exhaustion or WorldEventKind.Disease or WorldEventKind.MaternalDeath
                or WorldEventKind.Accident or WorldEventKind.OldAge
                when evt.SourceSystem == YearEventTally.DeathSource:
                At(evt.Tick).Deaths++;
                if (evt.Kind == WorldEventKind.Starvation) At(evt.Tick).Starved++;
                return;
            case WorldEventKind.Emigrated:
                At(evt.Tick).Emigrants++;
                return;
            case WorldEventKind.CourtshipRejected when evt.Payload.StartsWith("SemMeios|", StringComparison.Ordinal):
                At(evt.Tick).RejectedNoMeans++;
                return;
            case WorldEventKind.Marriage:
                var p = evt.Payload.Split('|');
                if (p.Length > 1 && world.FindNpc(new NpcId(long.Parse(p[0], CultureInfo.InvariantCulture))) is { } sa
                    && world.FindNpc(new NpcId(long.Parse(p[1], CultureInfo.InvariantCulture))) is { } sb && (sa.Union?.Unions ?? 0) <= 1)
                {
                    var md = At(evt.Tick);
                    md.FirstMarriages++;
                    var groom = sa.Sex == Sex.Male ? sa : sb;
                    if (groom.Employer is not null) md.GroomEmployed++;
                    md.MeansAtMarriage += LivingWorld.Simulation.Population.Family.HouseholdMeans.OfCouple(world, sa, sb);
                }
                foreach (var raw in p.Take(2))
                {
                    if (!long.TryParse(raw, CultureInfo.InvariantCulture, out long id) || !_everMarried.Add(id)) continue;
                    if (world.FindNpc(new NpcId(id)) is { } w && w.BirthDate.TotalHours >= 0 && (w.Union?.Unions ?? 0) <= 1)
                        (w.Sex == Sex.Female ? At(evt.Tick).FirstMarriageWomen : At(evt.Tick).FirstMarriageMen)
                            .Add((evt.Tick - w.BirthDate.TotalHours) / (double)world.Calendar.HoursPerYear);
                }
                return;
        }
    }

    /// <summary>Fim do mês: exposição, braços e vagas da fazenda, jovens sem trabalho e se a cidade tem vaga paga aberta.</summary>
    public void SampleMonth(long tick)
    {
        var d = At(Math.Max(0, tick - 1));
        int alive = world.AliveNpcIndex.Alive.Count;
        if (d.PopStart < 0) d.PopStart = alive;
        d.PopEnd = alive;
        d.Months++;
        double share = 1.0 / world.Calendar.MonthsPerYear;
        var now = world.CurrentDate;
        foreach (var npc in world.AliveNpcIndex.Alive)
        {
            d.PersonYears += share;
            int age = npc.AgeYears(now);
            if (npc.Sex == Sex.Female && age is >= 0 and < 50) d.FemaleExposure[age] += share;
            if (npc.Sex == Sex.Female && age is >= 20 and < 50 && npc.Spouse is { } sp && world.FindNpc(sp) is { IsAlive: true })
                d.MarriedWomen20to49Years += share;
            if (age == NeverMarriedAge && npc.BirthDate.TotalHours >= 0 && _counted45.Add(npc.Id.Value))
            {
                bool never = npc.Spouse is null && !_everMarried.Contains(npc.Id.Value) && (npc.Union?.Unions ?? 0) == 0;
                if (npc.Sex == Sex.Female) { d.WomenAt45++; if (never) d.WomenNeverMarried45++; }
                else { d.MenAt45++; if (never) d.MenNeverMarried45++; }
            }
            if (npc.Sex == Sex.Female && age == LastBirthAgeCheck && npc.BirthDate.TotalHours >= 0 && npc.Reproduction?.LastBirthTick is { } last && _counted50.Add(npc.Id.Value))
                d.LastBirthAge.Add((last - npc.BirthDate.TotalHours) / (double)world.Calendar.HoursPerYear);
            if (age < 16) continue;
            d.AdultMonths++;
            if (npc.Employer is not null) d.EmployedAdultMonths++;
            else if (age <= 35)
            {
                d.YoungIdleMonths++;
                if (npc.Spouse is null) d.YoungIdleSingleMonths++;
            }
        }

        bool open = false;
        foreach (var w in world.Workplaces)
        {
            if (w.Employees.Count < w.MaxVacancies && !w.PayrollFailed) open = true;
            if (w.LocationType.Id != FarmType) continue;
            d.FarmWorkerMonths += w.Employees.Count;
            d.FarmSlotMonths += w.MaxVacancies;
            if (world.FindCropAt(w.Location) is { ArableSeed: > 0 } field) d.ArableSeedMonths += field.ArableSeed;
        }

        if (open) d.OpenMonths++;
        if (LivingWorld.Simulation.Population.Family.Holdings.Rules(world) is { } holdings)
            foreach (var city in world.Cities)
                if (LivingWorld.Simulation.Population.Family.Holdings.Arable(world, city.Id) is { } arable)
                {
                    d.DwellingMonths += arable / LivingWorld.Simulation.Population.Family.Holdings.SeedPerHolding(world, holdings);
                    d.VacantMonths += LivingWorld.Simulation.Population.Family.Holdings.Vacant(world, city.Id);
                }
        long food = world.ResourceProduced.GetValueOrDefault(new ResourceType(world.EconomyRules.FoodResourceId));
        d.FoodProduced += food - _lastFood;
        _lastFood = food;
    }

    public static string Summarize(IReadOnlyList<DecadeTally> runs)
    {
        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.AppendLine("por_decada (soma das seeds) dec: nat‰ mort‰ TFR mulher_1o_casamento cresc%/ano pop_ini→fim fome comida/pessoa/ano lavradores/vagas adultos_empregados% jovens_sem_trabalho(solteiros)/mes meses_com_vaga% emigrantes terra_semente/pessoa");
        for (int i = 0; i < Decades; i++)
        {
            var ds = runs.Select(r => r.Data[i]).Where(d => d.Months > 0).ToList();
            if (ds.Count == 0) continue;
            double py = ds.Sum(d => d.PersonYears);
            double tfr = 0;
            for (int a = 0; a < 50; a++)
            {
                double e = ds.Sum(d => d.FemaleExposure[a]);
                if (e > 0) tfr += ds.Sum(d => d.AsfrBirths[a]) / e;
            }

            var women = ds.SelectMany(d => d.FirstMarriageWomen).ToList();
            double years = ds.Average(d => d.Months) / 12.0;
            var grown = ds.Where(d => d.PopStart > 0 && d.PopEnd > 0).ToList();
            double growth = grown.Count == 0 ? 0
                : 100 * (DeterministicMath.Exp(grown.Average(d => DeterministicMath.Log((double)d.PopEnd / d.PopStart)) / Math.Max(1e-9, years)) - 1);
            double months = ds.Sum(d => d.Months);
            sb.AppendLine(inv,
                $"  {i * 10}: {1000 * ds.Sum(d => d.Births) / py:F1} {1000 * ds.Sum(d => d.Deaths) / py:F1} {tfr:F2} {(women.Count > 0 ? women.Average() : 0):F1}(n={women.Count}) {growth:F2} {ds.Average(d => d.PopStart):F0}→{ds.Average(d => d.PopEnd):F0} {ds.Sum(d => d.Starved)} {ds.Sum(d => d.FoodProduced) / py:F0} {ds.Sum(d => d.FarmWorkerMonths) / months:F0}/{ds.Sum(d => d.FarmSlotMonths) / months:F0} {100 * ds.Sum(d => d.EmployedAdultMonths) / Math.Max(1, ds.Sum(d => d.AdultMonths)):F0}% {ds.Sum(d => d.YoungIdleMonths) / months:F1}({ds.Sum(d => d.YoungIdleSingleMonths) / months:F1}) {100 * ds.Sum(d => d.OpenMonths) / months:F0}% {ds.Sum(d => d.Emigrants)} {ds.Sum(d => d.ArableSeedMonths) / 12.0 / py:F0}");
        }

        sb.AppendLine("por_decada_nupcialidade dec: 1o_casamento_mulher(mediana) homem(mediana) nunca_casou_aos_45_mulher homem intervalo_partos_meses(mediana) idade_ultimo_parto(n) partos_sem_conjuge% TFR_conjugal_20_49 mulheres_20_49_casadas% recusas_sem_meios noivo_empregado% meios_no_casamento terra_em_posses(vagas)");
        for (int i = 0; i < Decades; i++)
        {
            var ds = runs.Select(r => r.Data[i]).Where(d => d.Months > 0).ToList();
            if (ds.Count == 0) continue;
            var w = ds.SelectMany(d => d.FirstMarriageWomen).OrderBy(x => x).ToList();
            var m = ds.SelectMany(d => d.FirstMarriageMen).OrderBy(x => x).ToList();
            var iv = ds.SelectMany(d => d.BirthIntervalMonths).OrderBy(x => x).ToList();
            var last = ds.SelectMany(d => d.LastBirthAge).ToList();
            long births = ds.Sum(d => d.Births);
            double exp2049 = 0, b2049 = 0;
            for (int a = 20; a < 50; a++) { exp2049 += ds.Sum(d => d.FemaleExposure[a]); b2049 += ds.Sum(d => d.AsfrBirths[a]); }
            double married = ds.Sum(d => d.MarriedWomen20to49Years);
            // TFR conjugal 20–49 ≈ 30 × partos de casadas 20–49 ÷ anos de casadas (partos de casadas ≈ partos 20–49 × parte conjugal)
            double maritalShare = ds.Sum(d => d.MaritalBirths) / (double)Math.Max(1, ds.Sum(d => d.MaritalBirths + d.BirthsWithoutSpouse));
            double mtfr = married > 0 ? 30 * b2049 * maritalShare / married : 0;
            static string Med(List<double> xs) => xs.Count == 0 ? "-" : xs[xs.Count / 2].ToString("F1", CultureInfo.InvariantCulture);
            static string Avg(List<double> xs) => xs.Count == 0 ? "-" : xs.Average().ToString("F1", CultureInfo.InvariantCulture);
            static string Frac(long a, long b) => b == 0 ? "-" : (100.0 * a / b).ToString("F0", CultureInfo.InvariantCulture) + "%(n=" + b + ")";
            sb.AppendLine(inv,
                $"  {i * 10}: {Avg(w)}({Med(w)}) {Avg(m)}({Med(m)}) {Frac(ds.Sum(d => d.WomenNeverMarried45), ds.Sum(d => d.WomenAt45))} {Frac(ds.Sum(d => d.MenNeverMarried45), ds.Sum(d => d.MenAt45))} {Avg(iv)}({Med(iv)}) {Avg(last)}(n={last.Count}) {100.0 * ds.Sum(d => d.BirthsWithoutSpouse) / Math.Max(1, births):F1}% {mtfr:F2} {100 * married / Math.Max(1e-9, exp2049):F0}% {ds.Sum(d => d.RejectedNoMeans)} {100.0 * ds.Sum(d => d.GroomEmployed) / Math.Max(1, ds.Sum(d => d.FirstMarriages)):F0}% {ds.Sum(d => d.MeansAtMarriage) / Math.Max(1, ds.Sum(d => d.FirstMarriages)):F2} {ds.Sum(d => d.DwellingMonths) / ds.Sum(d => d.Months):F0}({ds.Sum(d => d.VacantMonths) / ds.Sum(d => d.Months):F1})");
        }

        return sb.ToString();
    }
}
