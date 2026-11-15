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
    // AD-154: crimes contra a pessoa, denúncias e condenações por crime, desfechos de sequestro
    public SortedDictionary<string, long> DomesticByBond { get; } = new(StringComparer.Ordinal);
    public SortedDictionary<string, long> DomesticByMotive { get; } = new(StringComparer.Ordinal);
    public SortedDictionary<string, long> KidnapsByMotive { get; } = new(StringComparer.Ordinal);
    public SortedDictionary<string, long> KidnapOutcomes { get; } = new(StringComparer.Ordinal);
    public SortedDictionary<string, long> SexualByCategory { get; } = new(StringComparer.Ordinal);
    public SortedDictionary<string, long> ReportsByKind { get; } = new(StringComparer.Ordinal);
    public SortedDictionary<string, long> ConvictedByKind { get; } = new(StringComparer.Ordinal);
    public SortedDictionary<string, long> InnocentByKind { get; } = new(StringComparer.Ordinal);
    public long Domestic, DomesticDeaths, Kidnaps, KidnapChildren, RansomPaid, Sexual, SexualConceptions;
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
            case WorldEventKind.DomesticViolence:
                Domestic++;
                if (At(5) == "morto") DomesticDeaths++;
                DomesticByBond[At(2)] = DomesticByBond.GetValueOrDefault(At(2)) + 1;
                DomesticByMotive[At(3)] = DomesticByMotive.GetValueOrDefault(At(3)) + 1;
                return;
            case WorldEventKind.Kidnapped:
                Kidnaps++;
                if (At(3) == "crianca") KidnapChildren++;
                KidnapsByMotive[At(2)] = KidnapsByMotive.GetValueOrDefault(At(2)) + 1;
                return;
            case WorldEventKind.KidnappingEnded:
                KidnapOutcomes[At(2)] = KidnapOutcomes.GetValueOrDefault(At(2)) + 1;
                if (long.TryParse(At(4), System.Globalization.NumberStyles.Integer, CultureInfo.InvariantCulture, out long paid)) RansomPaid += paid;
                return;
            case WorldEventKind.SexualAssault:
                Sexual++;
                if (At(5) == "1") SexualConceptions++;
                SexualByCategory[At(2)] = SexualByCategory.GetValueOrDefault(At(2)) + 1;
                return;
            case WorldEventKind.CrimeReported:
                Reports++;
                if (At(4) == "0") FalseReports++;
                ReportsByKind[At(3)] = ReportsByKind.GetValueOrDefault(At(3)) + 1;
                return;
            case WorldEventKind.Verdict:
                Verdicts++;
                if (At(4) != "culpado") return;
                Convicted++;
                ConvictedByKind[At(3)] = ConvictedByKind.GetValueOrDefault(At(3)) + 1;
                if (At(7) == "0") { InnocentConvicted++; InnocentByKind[At(3)] = InnocentByKind.GetValueOrDefault(At(3)) + 1; }
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
        sb.AppendLine(inv, $"pessoa_por_seed_ano domestica={PerYear(t => t.Domestic)} (mortes={PerYear(t => t.DomesticDeaths)}) vinculo[{Dict(t => t.DomesticByBond)}] motivo[{Dict(t => t.DomesticByMotive)}] sequestros={PerYear(t => t.Kidnaps)} (criancas={PerYear(t => t.KidnapChildren)}) motivo[{Dict(t => t.KidnapsByMotive)}] desfecho[{Dict(t => t.KidnapOutcomes)}] resgate_pago={PerYear(t => t.RansomPaid)} sexual={PerYear(t => t.Sexual)} ({1e5 * Sum(t => t.Sexual) / Math.Max(1e-9, py):F1}/100mil/ano) categoria[{Dict(t => t.SexualByCategory)}] concepcoes={PerYear(t => t.SexualConceptions)}");
        sb.AppendLine(inv, $"justica_por_crime denuncias[{Dict(t => t.ReportsByKind)}] condenados[{Dict(t => t.ConvictedByKind)}] inocentes[{Dict(t => t.InnocentByKind)}]");
        sb.AppendLine(inv, $"justica_por_seed_ano denuncias={PerYear(t => t.Reports)} (falsas={PerYear(t => t.FalseReports)}) julgamentos={PerYear(t => t.Verdicts)} condenados={PerYear(t => t.Convicted)} (inocentes={PerYear(t => t.InnocentConvicted)}) punicoes[{Dict(t => t.Punishments)}] forcas={PerYear(t => t.Executions)} banidos={PerYear(t => t.Banished)} fugas={PerYear(t => t.Fled)} disputas_heranca={PerYear(t => t.Disputes)}");
        return sb.ToString();
    }
}
