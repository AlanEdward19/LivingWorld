using System.Globalization;
using System.Text;
using LivingWorld.Domain.History;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Observation;

/// <summary>Crime, violência e justiça de uma rodada (AD-150), só lendo o log: furtos por motivo e bem, agressões por motivo,
/// homicídios, denúncias, vereditos (e condenações de inocentes), punições, banimentos e disputas de herança.</summary>
public sealed class CrimeTally
{
    public long MeasureFromTick;
    public SortedDictionary<string, long> TheftsByMotive { get; } = new(StringComparer.Ordinal);
    public SortedDictionary<string, long> TheftsByGood { get; } = new(StringComparer.Ordinal);
    public SortedDictionary<string, long> AssaultsByMotive { get; } = new(StringComparer.Ordinal);
    public SortedDictionary<string, long> Punishments { get; } = new(StringComparer.Ordinal);
    public long Thefts, TheftsFailed, TheftsUnseen, Assaults, Injuries, Homicides, Reports, FalseReports, Verdicts, Convicted,
        InnocentConvicted, Executions, Banished, Fled, Disputes;

    public void Record(WorldEvent evt)
    {
        if (evt.Tick < MeasureFromTick) return;
        var f = evt.Payload.Split('|');
        string At(int i) => i < f.Length ? f[i] : "";
        switch (evt.Kind)
        {
            case WorldEventKind.TheftCommitted:
                Thefts++;
                if (At(7) != "levou") TheftsFailed++;
                if (At(8) == "0") TheftsUnseen++;
                TheftsByMotive[At(3)] = TheftsByMotive.GetValueOrDefault(At(3)) + 1;
                TheftsByGood[At(4)] = TheftsByGood.GetValueOrDefault(At(4)) + 1;
                return;
            case WorldEventKind.Assault:
                Assaults++;
                if (At(4) == "ferido") Injuries++;
                AssaultsByMotive[At(2)] = AssaultsByMotive.GetValueOrDefault(At(2)) + 1;
                return;
            case WorldEventKind.Homicide: Homicides++; return;
            case WorldEventKind.CrimeReported:
                Reports++;
                if (At(4) == "0") FalseReports++;
                return;
            case WorldEventKind.Verdict:
                Verdicts++;
                if (At(4) != "culpado") return;
                Convicted++;
                if (At(7) == "0") InnocentConvicted++;
                Punishments[At(5)] = Punishments.GetValueOrDefault(At(5)) + 1;
                return;
            case WorldEventKind.Executed: Executions++; return;
            case WorldEventKind.Banished:
                if (At(3) == "fuga") Fled++; else Banished++;
                return;
            case WorldEventKind.InheritanceDisputed: Disputes++; return;
        }
    }

    public static string Summarize(IReadOnlyList<(CrimeTally Tally, double PersonYears, double SeedYears)> runs)
    {
        var inv = CultureInfo.InvariantCulture;
        double py = runs.Sum(r => r.PersonYears), years = Math.Max(1e-9, runs.Sum(r => r.SeedYears));
        long Sum(Func<CrimeTally, long> f) => runs.Sum(r => f(r.Tally));
        string PerYear(Func<CrimeTally, long> f) => (Sum(f) / years).ToString("F2", inv);
        string Dict(Func<CrimeTally, SortedDictionary<string, long>> f)
        {
            var all = new SortedDictionary<string, long>(StringComparer.Ordinal);
            foreach (var r in runs) foreach (var (k, v) in f(r.Tally)) all[k] = all.GetValueOrDefault(k) + v;
            return string.Join(" ", all.Select(kv => $"{kv.Key}:{(kv.Value / years).ToString("F2", inv)}"));
        }
        var sb = new StringBuilder();
        sb.AppendLine(inv, $"crime_por_seed_ano furtos={PerYear(t => t.Thefts)} (falhos={PerYear(t => t.TheftsFailed)} sem_testemunha={PerYear(t => t.TheftsUnseen)}) por_motivo[{Dict(t => t.TheftsByMotive)}] por_bem[{Dict(t => t.TheftsByGood)}]");
        sb.AppendLine(inv, $"violencia_por_seed_ano agressoes={PerYear(t => t.Assaults)} ferimentos={PerYear(t => t.Injuries)} homicidios={PerYear(t => t.Homicides)} ({1e5 * Sum(t => t.Homicides) / Math.Max(1e-9, py):F1}/100mil/ano) por_motivo[{Dict(t => t.AssaultsByMotive)}] vingancas={(runs.Sum(r => r.Tally.AssaultsByMotive.GetValueOrDefault("vinganca")) / years).ToString("F2", inv)}");
        sb.AppendLine(inv, $"justica_por_seed_ano denuncias={PerYear(t => t.Reports)} (falsas={PerYear(t => t.FalseReports)}) julgamentos={PerYear(t => t.Verdicts)} condenados={PerYear(t => t.Convicted)} (inocentes={PerYear(t => t.InnocentConvicted)}) punicoes[{Dict(t => t.Punishments)}] forcas={PerYear(t => t.Executions)} banidos={PerYear(t => t.Banished)} fugas={PerYear(t => t.Fled)} disputas_heranca={PerYear(t => t.Disputes)}");
        return sb.ToString();
    }
}
