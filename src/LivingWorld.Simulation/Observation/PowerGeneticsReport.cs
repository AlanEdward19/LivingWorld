using System.Globalization;
using System.Text;
using LivingWorld.Domain.Extraordinary;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Extraordinary.Inheritance;

namespace LivingWorld.Simulation.Observation;

/// <summary>Medida da herança de poderes (AD-156): o cenário de referência que liga os poderes no cenário padrão de código
/// (quatro poderes de efeito brando, só invocáveis por autoria) e a amostra, a cada 25 anos (~uma geração), de quantos vivos
/// manifestam cada poder, de quantos poderes tem cada portador e da frequência do alelo de cada poder entre os vivos (média das
/// seeds). Modos: dominante, recessivo, ligado ao X recessivo e poligênico, com a genética padrão de <see cref="PowerGenetics"/>.</summary>
public static class PowerGeneticsReport
{
    public const int SampleEveryYears = 25;

    /// <summary>Prevalência inicial de 8% repartida entre os quatro poderes; cada um com um modo de herança.</summary>
    public static ExtraordinaryScenarioData ReferenceScenario() =>
        new(true, [Power("dom"), Power("rec"), Power("xlr"), Power("poly")], prevalence: 0.08,
            inheritanceRules: new PowerInheritanceRules(new PowerGenetics(), new Dictionary<string, PowerGenetics>
            {
                ["rec"] = new(PowerInheritanceMode.Recessive),
                ["xlr"] = new(PowerInheritanceMode.XLinkedRecessive),
                ["poly"] = new(PowerInheritanceMode.Polygenic),
            }));

    private static PowerDescriptor Power(string id) =>
        new(id, "genetica", ["npc.health:5"], "Active", [], "Guaranteed", [], [], [], []);

    /// <summary>Amostras de uma seed: (ano, vivos, manifestos por poder, portadores, poderes por portador, máximo).</summary>
    public sealed class Tally(WorldState world)
    {
        public List<Sample> Samples { get; } = [];

        public void SampleMonth(int monthIndex)
        {
            int monthsPerYear = world.Calendar.MonthsPerYear;
            if ((monthIndex + 1) % (SampleEveryYears * monthsPerYear) != 0 && monthIndex != 0) return;
            var ids = world.Extraordinary.Descriptors.Select(d => d.Id).ToList();
            var manifest = new int[ids.Count];
            var layout = PowerGenome.Layout(world.Extraordinary);
            var alleles = new double[ids.Count];
            var slots = new double[ids.Count];
            int carriers = 0, powers = 0, max = 0, alive = 0;
            foreach (var npc in world.AliveNpcIndex.Alive.OrderBy(n => n.Id.Value))
            {
                alive++;
                ulong genome = PowerGenome.Of(world, npc);
                foreach (var locus in layout)
                {
                    int i = ids.IndexOf(locus.PowerId);
                    alleles[i] += PowerGenome.Dose(locus, genome);
                    slots[i] += locus.Genetics.LociCount * (locus.Genetics.IsXLinked && npc.Sex == Domain.Population.Sex.Male ? 1 : 2);
                }
                if (world.CarrierOf(npc.Id) is not { PowerIds.Count: > 0 } c) continue;
                carriers++;
                powers += c.PowerIds.Count;
                max = Math.Max(max, c.PowerIds.Count);
                for (int i = 0; i < ids.Count; i++)
                    if (c.PowerIds.Contains(ids[i])) manifest[i]++;
            }
            Samples.Add(new Sample((monthIndex + 1) / monthsPerYear, alive, manifest, carriers, powers, max,
                alleles.Select((a, i) => slots[i] == 0 ? 0 : a / slots[i]).ToArray()));
        }
    }

    public sealed record Sample(int Year, int Alive, int[] Manifest, int Carriers, int Powers, int MaxPowers, double[] AlleleFrequency);

    /// <summary>Soma as seeds por ano de amostra: manifestos por mil vivos de cada poder e poderes médios por portador.</summary>
    public static string Summarize(IReadOnlyList<Tally> runs, IReadOnlyList<string> powerIds)
    {
        var sb = new StringBuilder("\nPoderes por geração (soma das seeds; manifestos por mil vivos):\n");
        sb.Append("ano  vivos  ").Append(string.Join("  ", powerIds.Select(id => id.PadLeft(6)))).Append("  portad  pod/port  máx\n");
        foreach (var year in runs.SelectMany(r => r.Samples).Select(s => s.Year).Distinct().Order())
        {
            var at = runs.SelectMany(r => r.Samples).Where(s => s.Year == year).ToList();
            int alive = at.Sum(s => s.Alive), carriers = at.Sum(s => s.Carriers);
            sb.Append(CultureInfo.InvariantCulture, $"{year,3}  {alive,5}  ");
            for (int i = 0; i < powerIds.Count; i++)
                sb.Append(CultureInfo.InvariantCulture, $"{(alive == 0 ? 0 : 1000.0 * at.Sum(s => s.Manifest[i]) / alive),6:F1}  ");
            double perCarrier = carriers == 0 ? 0 : at.Sum(s => s.Powers) / (double)carriers;
            sb.Append(CultureInfo.InvariantCulture, $"{carriers,6}  {perCarrier,8:F2}  {at.Max(s => s.MaxPowers),3}  q:");
            for (int i = 0; i < powerIds.Count; i++)
                sb.Append(CultureInfo.InvariantCulture, $" {at.Average(s => s.AlleleFrequency[i]):F4}");
            sb.Append('\n');
        }
        return sb.ToString();
    }
}
