using System.Globalization;
using System.Text;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Observation;

/// <summary>Medida de corpo e saúde de uma rodada (AD-134), só leitura: altura e peso por idade e sexo, variância de vitalidade,
/// gene de taxa e estatura adulta por geração, distribuição da saúde dos vivos, mortes por causa e sobrevivência no 1º ano
/// pela mãe viva ou morta antes dos 6 meses do bebê.</summary>
public sealed class BodyHealthTally(WorldState world)
{
    public static readonly int[] Ages = [0, 1, 5, 10, 15, 20, 40, 70];
    public const int MaxGeneration = 8;

    // [sexo, idade] → soma e contagem
    public double[,] HeightSum { get; } = new double[2, Ages.Length];
    public double[,] WeightSum { get; } = new double[2, Ages.Length];
    public long[,] BodyCount { get; } = new long[2, Ages.Length];
    public long[] HealthBuckets { get; } = new long[5];
    public Dictionary<WorldEventKind, long> DeathsByCause { get; } = [];
    public Dictionary<long, int> Generation { get; } = [];
    public Dictionary<long, (double Vitality, double RateGene)> Genes { get; } = [];
    public Dictionary<long, (Sex Sex, double Height)> AdultHeight { get; } = [];
    public Dictionary<long, (long Mother, long BirthTick)> Births { get; } = [];
    public Dictionary<long, long> DeathTicks { get; } = [];
    public long MeasureFromTick;
    public double UndernutritionSum;
    public long UndernutritionCount, Nurslings, UnnursedNurslings, InfantDeaths;
    public Dictionary<WorldEventKind, long> InfantDeathsByCause { get; } = [];

    public void OnEvent(WorldEvent evt)
    {
        if (evt.Kind == WorldEventKind.Birth && evt.SourceSystem == YearEventTally.BirthSource)
        {
            var parts = evt.Payload.Split('|');
            long baby = long.Parse(parts[0], CultureInfo.InvariantCulture);
            long mother = long.Parse(parts[1], CultureInfo.InvariantCulture);
            long father = long.Parse(parts[2], CultureInfo.InvariantCulture);
            Generation[baby] = 1 + Math.Max(Generation.GetValueOrDefault(mother), Generation.GetValueOrDefault(father));
            if (world.FindNpc(new NpcId(baby)) is { } npc) Genes[baby] = (npc.Vitality, npc.RateGene.Value);
            if (evt.Tick >= MeasureFromTick) Births[baby] = (mother, evt.Tick);
            return;
        }

        if (evt.SourceSystem != YearEventTally.DeathSource) return;
        int cut = evt.Payload.IndexOf('|');
        if (long.TryParse(cut < 0 ? evt.Payload : evt.Payload[..cut], CultureInfo.InvariantCulture, out long deadId))
            DeathTicks.TryAdd(deadId, evt.Tick);
        if (evt.Tick < MeasureFromTick) return;
        if (world.FindNpc(new NpcId(deadId)) is { } deceased && evt.Tick - deceased.BirthDate.TotalHours < world.Calendar.HoursPerYear)
            InfantDeathsByCause[evt.Kind] = InfantDeathsByCause.GetValueOrDefault(evt.Kind) + 1;
        if (evt.Kind is WorldEventKind.Death or WorldEventKind.Starvation or WorldEventKind.Dehydration
            or WorldEventKind.Exhaustion or WorldEventKind.Disease or WorldEventKind.MaternalDeath
                or WorldEventKind.Accident or WorldEventKind.OldAge)
            DeathsByCause[evt.Kind] = DeathsByCause.GetValueOrDefault(evt.Kind) + 1;
        else if (evt.Kind.ToString().Contains("Death", StringComparison.Ordinal) || evt.Kind.ToString().Contains("Kill", StringComparison.Ordinal))
            DeathsByCause[evt.Kind] = DeathsByCause.GetValueOrDefault(evt.Kind) + 1;
    }

    /// <summary>Amostra anual dos vivos: corpo por idade e sexo, saúde, genes dos fundadores, estatura adulta (25–45 anos).</summary>
    public void SampleYear()
    {
        var now = world.CurrentDate;
        foreach (var npc in world.AliveNpcIndex.Alive)
        {
            if (!Genes.ContainsKey(npc.Id.Value)) Genes[npc.Id.Value] = (npc.Vitality, npc.RateGene.Value);
            int age = npc.AgeYears(now);
            if (age is >= 25 and <= 45) AdultHeight[npc.Id.Value] = (npc.Sex, npc.Height);
            if (now.TotalHours < MeasureFromTick) continue;
            HealthBuckets[Math.Clamp(npc.Health / 20, 0, 4)]++;
            UndernutritionSum += npc.Undernutrition;
            UndernutritionCount++;
            if (npc.GestationAtBirthDays is not null && now.TotalHours - npc.BirthDate.TotalHours < 2 * world.Calendar.HoursPerYear)
            {
                Nurslings++;
                if (npc.LastNursedTick is not { } nursed || now.TotalHours - nursed > 3 * world.Calendar.HoursPerDay) UnnursedNurslings++;
            }
            int slot = Array.IndexOf(Ages, age);
            if (slot < 0) continue;
            int s = npc.Sex == Sex.Male ? 0 : 1;
            HeightSum[s, slot] += npc.Height;
            WeightSum[s, slot] += npc.Weight;
            BodyCount[s, slot]++;
        }
    }

    /// <summary>Sobrevivência ao 1º ano (mãe morta antes dos 6 meses do bebê / demais), só bebês com o ano inteiro observado.</summary>
    public (long DeadN, long DeadSurvived, long AliveN, long AliveSurvived) InfantSurvival(long followUpEnd)
    {
        long hoursPerYear = world.Calendar.HoursPerYear;
        long deadN = 0, deadS = 0, aliveN = 0, aliveS = 0;
        foreach (var (babyId, (motherId, birth)) in Births.OrderBy(kv => kv.Key))
        {
            if (birth + hoursPerYear > followUpEnd) continue;
            // o morto pode ter ido para o arquivo frio: a data vem do evento de morte
            bool survived = !DeathTicks.TryGetValue(babyId, out long d) || d - birth >= hoursPerYear;
            bool motherLost = DeathTicks.TryGetValue(motherId, out long md) && md - birth < hoursPerYear / 2;
            if (motherLost) { deadN++; if (survived) deadS++; }
            else { aliveN++; if (survived) aliveS++; }
        }

        return (deadN, deadS, aliveN, aliveS);
    }

    public static string Summarize(IReadOnlyList<(BodyHealthTally Tally, long FollowUpEnd)> runs)
    {
        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        for (int s = 0; s < 2; s++)
        {
            sb.Append(s == 0 ? "corpo_homens " : "corpo_mulheres ");
            for (int a = 0; a < Ages.Length; a++)
            {
                double h = 0, w = 0; long n = 0;
                foreach (var (t, _) in runs) { h += t.HeightSum[s, a]; w += t.WeightSum[s, a]; n += t.BodyCount[s, a]; }
                sb.Append(inv, $"{Ages[a]}a:{(n > 0 ? h / n : 0):F2}m/{(n > 0 ? w / n : 0):F1}kg ");
            }

            sb.AppendLine();
        }

        long[] buckets = new long[5];
        foreach (var (t, _) in runs) for (int i = 0; i < 5; i++) buckets[i] += t.HealthBuckets[i];
        long total = Math.Max(1, buckets.Sum());
        sb.AppendLine(inv, $"saude_vivos 0-19:{100.0 * buckets[0] / total:F1}% 20-39:{100.0 * buckets[1] / total:F1}% 40-59:{100.0 * buckets[2] / total:F1}% 60-79:{100.0 * buckets[3] / total:F1}% 80-100:{100.0 * buckets[4] / total:F1}%");

        var causes = new SortedDictionary<string, long>(StringComparer.Ordinal);
        foreach (var (t, _) in runs)
            foreach (var (kind, n) in t.DeathsByCause) causes[kind.ToString()] = causes.GetValueOrDefault(kind.ToString()) + n;
        long deaths = Math.Max(1, causes.Values.Sum());
        double un = runs.Sum(r => r.Tally.UndernutritionSum) / Math.Max(1, runs.Sum(r => r.Tally.UndernutritionCount));
        long nurs = runs.Sum(r => r.Tally.Nurslings), unn = runs.Sum(r => r.Tally.UnnursedNurslings);
        sb.AppendLine(inv, $"desnutricao_media_vivos={un:F3} lactentes_sem_leite={100.0 * unn / Math.Max(1, nurs):F1}% (n={nurs})");
        var infant = new SortedDictionary<string, long>(StringComparer.Ordinal);
        foreach (var (t, _) in runs)
            foreach (var (kind, n) in t.InfantDeathsByCause) infant[kind.ToString()] = infant.GetValueOrDefault(kind.ToString()) + n;
        sb.AppendLine("mortes_1o_ano_por_causa " + string.Join(" ", infant.Select(c => $"{c.Key}:{c.Value}")));
        sb.AppendLine("mortes_por_causa " + string.Join(" ", causes.Select(c => string.Create(inv, $"{c.Key}:{c.Value}({100.0 * c.Value / deaths:F1}%)"))));

        sb.Append("variancia_por_geracao (vitalidade | gene_taxa | estatura_adulta_dentro_do_sexo, n) ");
        for (int g = 0; g <= MaxGeneration; g++)
        {
            var vit = new List<double>(); var rate = new List<double>(); var male = new List<double>(); var female = new List<double>();
            foreach (var (t, _) in runs)
                foreach (var (id, genes) in t.Genes.OrderBy(kv => kv.Key))
                {
                    if (Math.Min(MaxGeneration, t.Generation.GetValueOrDefault(id)) != g) continue;
                    vit.Add(genes.Vitality); rate.Add(genes.RateGene);
                    if (t.AdultHeight.TryGetValue(id, out var adult)) (adult.Sex == Sex.Male ? male : female).Add(adult.Height * 100);
                }

            if (vit.Count < 10) continue;
            double stature = (Variance(male) * male.Count + Variance(female) * female.Count) / Math.Max(1, male.Count + female.Count);
            sb.Append(inv, $"g{g}:{Variance(vit):F0}|{Variance(rate):F4}|{stature:F1}cm²(n={vit.Count}) ");
        }

        sb.AppendLine();
        long dn = 0, ds = 0, an = 0, asv = 0;
        foreach (var (t, end) in runs) { var r = t.InfantSurvival(end); dn += r.DeadN; ds += r.DeadSurvived; an += r.AliveN; asv += r.AliveSurvived; }
        sb.AppendLine(inv, $"sobrevivencia_1o_ano mae_morta_antes_6m={(double)ds / Math.Max(1, dn):F3} (n={dn}) demais={(double)asv / Math.Max(1, an):F3} (n={an})");
        return sb.ToString();
    }

    private static double Variance(List<double> values)
    {
        if (values.Count < 2) return 0;
        double mean = values.Average();
        return values.Sum(v => (v - mean) * (v - mean)) / (values.Count - 1);
    }
}
