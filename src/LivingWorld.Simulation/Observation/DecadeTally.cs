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
    }

    public Decade[] Data { get; } = Enumerable.Range(0, Decades).Select(_ => new Decade()).ToArray();
    private readonly HashSet<long> _everMarried = [];
    private long _lastFood;

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
            case WorldEventKind.Marriage:
                var p = evt.Payload.Split('|');
                foreach (var raw in p.Take(2))
                {
                    if (!long.TryParse(raw, CultureInfo.InvariantCulture, out long id) || !_everMarried.Add(id)) continue;
                    if (world.FindNpc(new NpcId(id)) is { Sex: Sex.Female } w && w.BirthDate.TotalHours >= 0 && (w.Union?.Unions ?? 0) <= 1)
                        At(evt.Tick).FirstMarriageWomen.Add((evt.Tick - w.BirthDate.TotalHours) / (double)world.Calendar.HoursPerYear);
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

        return sb.ToString();
    }
}
