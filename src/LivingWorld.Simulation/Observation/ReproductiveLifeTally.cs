using System.Globalization;
using System.Text;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Observation;

/// <summary>Medida da vida reprodutiva de uma rodada (AD-139): nascimentos fora de união, primeiros filhos concebidos antes
/// da união, prematuros, idade da mãe no primeiro parto e nascimentos de mães adolescentes. Só lê o mundo no instante do
/// nascimento; nada é escrito, o hash é o mesmo com ou sem ela.</summary>
public sealed class ReproductiveLifeTally(WorldState world)
{
    public long MeasureFromTick { get; set; }

    public long Births, OutOfUnionBirths, PretermBirths, TeenBirths, FirstBirths, FirstBirthsConceivedOutsideUnion;
    public List<double> MotherAgeAtFirstBirth { get; } = [];
    private readonly HashSet<long> _firstSeen = [];

    /// <summary>Desfecho da gravidez fora da união quando ela apareceu (casamento, abandono, sem_pai).</summary>
    public Dictionary<string, long> UnwedOutcomes { get; } = [];

    /// <summary>Idade da menarca das nascidas no mundo (AD-139).</summary>
    public List<double> MenarcheAges { get; } = [];
    private readonly HashSet<long> _menarcheSeen = [];

    /// <summary>Nascidos de pais aparentados (r ≥ 1/16) e não aparentados, e quantos deles afetados por recessiva.</summary>
    public long KinBirths, KinAffected, OtherBirths, OtherAffected;

    /// <summary>Por locus, na última amostra: vivos, portadores (uma cópia) e afetados.</summary>
    public long[] LocusCarriers { get; private set; } = [];
    public long[] LocusAffected { get; private set; } = [];
    public long GenotypedAlive;

    public void Record(WorldEvent evt)
    {
        if (evt.Kind == WorldEventKind.UnwedPregnancy && evt.Tick >= MeasureFromTick)
        {
            string outcome = evt.Payload[(evt.Payload.LastIndexOf('|') + 1)..];
            UnwedOutcomes[outcome] = UnwedOutcomes.GetValueOrDefault(outcome) + 1;
            return;
        }
        if (evt.Tick < MeasureFromTick || evt.Kind != WorldEventKind.Birth || evt.SourceSystem != YearEventTally.BirthSource) return;
        var p = evt.Payload.Split('|');
        var baby = world.FindNpc(new NpcId(long.Parse(p[0], CultureInfo.InvariantCulture)));
        var mother = world.FindNpc(new NpcId(long.Parse(p[1], CultureInfo.InvariantCulture)));
        if (baby is null || mother is null) return;
        Births++;
        long hpd = world.Calendar.HoursPerDay;
        int gestation = baby.GestationAtBirthDays ?? world.PopulationRules.Fertility.TermMeanDays;
        long conceived = evt.Tick - gestation * hpd;
        bool inUnion = InUnionAt(mother, conceived);
        bool married = InUnionAt(mother, evt.Tick);
        if (!married && !inUnion) OutOfUnionBirths++;
        if (gestation < world.PopulationRules.Fertility.TermMinDays) PretermBirths++;
        double age = (evt.Tick - mother.BirthDate.TotalHours) / (double)world.Calendar.HoursPerYear;
        if (age < 20) TeenBirths++;
        CountKinship(baby, mother, world.FindNpc(new NpcId(long.Parse(p[2], CultureInfo.InvariantCulture))));

        // primeiro parto de mãe nascida no mundo (a população inicial não tem o histórico de antes)
        if (mother.GestationAtBirthDays is null || mother.Reproduction?.Parity != 1 || !_firstSeen.Add(mother.Id.Value)) return;
        FirstBirths++;
        MotherAgeAtFirstBirth.Add(age);
        if (!inUnion) FirstBirthsConceivedOutsideUnion++;
    }

    private void CountKinship(Npc baby, Npc mother, Npc? father)
    {
        var rules = world.BodyRules.Disorders;
        if (!rules.Enabled || father is null) return;
        bool kin = LivingWorld.Simulation.Population.Family.Kinship.Relatedness(world, mother, father, 4) >= 1.0 / 16 - 1e-9;
        uint g = LivingWorld.Simulation.Population.Body.Genome.Of(world, baby);
        bool affected = false;
        for (int i = 0; i < rules.Loci.Count; i++)
            if (!rules.Loci[i].Dominant && LivingWorld.Simulation.Population.Body.Genome.Copies(g, i) == 2) affected = true;
        if (kin) { KinBirths++; if (affected) KinAffected++; }
        else { OtherBirths++; if (affected) OtherAffected++; }
    }

    /// <summary>Amostra mensal: a menarca de quem acabou de tê-la.</summary>
    public void SampleMonth()
    {
        foreach (var npc in world.AliveNpcIndex.Alive)
            if (npc.Sex == Sex.Female && npc.PubertyTick is { } t && npc.GestationAtBirthDays is not null && _menarcheSeen.Add(npc.Id.Value))
                MenarcheAges.Add((t - npc.BirthDate.TotalHours) / (double)world.Calendar.HoursPerYear);
    }

    /// <summary>Portadores e afetados por locus entre os vivos (fim da rodada).</summary>
    public void SampleGenetics()
    {
        var rules = world.BodyRules.Disorders;
        LocusCarriers = new long[rules.Loci.Count];
        LocusAffected = new long[rules.Loci.Count];
        GenotypedAlive = 0;
        foreach (var npc in world.AliveNpcIndex.Alive)
        {
            GenotypedAlive++;
            uint g = LivingWorld.Simulation.Population.Body.Genome.Of(world, npc);
            for (int i = 0; i < rules.Loci.Count; i++)
            {
                int c = LivingWorld.Simulation.Population.Body.Genome.Copies(g, i);
                if (c == 1) LocusCarriers[i]++;
                if (LivingWorld.Simulation.Population.Body.Genome.IsAffected(rules.Loci[i], c)) LocusAffected[i]++;
            }
        }
    }

    /// <summary>Casada com cônjuge vivo no tick (a união começou antes dele e o cônjuge ainda não tinha morrido).</summary>
    private bool InUnionAt(Npc mother, long tick)
    {
        if (mother.Spouse is not { } spouseId || world.FindNpc(spouseId) is not { } spouse) return false;
        if (spouse.DeathDate is { } died && died.TotalHours <= tick) return false;
        return mother.Union is not { } union || union.SinceTick <= tick;
    }

    public static string Summarize(IReadOnlyList<ReproductiveLifeTally> tallies)
    {
        var inv = CultureInfo.InvariantCulture;
        double Sum(Func<ReproductiveLifeTally, double> f) => tallies.Sum(f);
        string Pct(double n, double d) => (d > 0 ? 100 * n / d : 0).ToString("F1", inv) + "%";
        var ages = tallies.SelectMany(t => t.MotherAgeAtFirstBirth).OrderBy(a => a).ToList();
        double births = Sum(t => t.Births), first = Sum(t => t.FirstBirths);
        var sb = new StringBuilder();
        sb.AppendLine(inv, $"reproducao fora_de_uniao={Pct(Sum(t => t.OutOfUnionBirths), births)} prematuros={Pct(Sum(t => t.PretermBirths), births)} maes_adolescentes={Pct(Sum(t => t.TeenBirths), births)} primeiros_concebidos_fora_da_uniao={Pct(Sum(t => t.FirstBirthsConceivedOutsideUnion), first)} (n={first})");
        sb.AppendLine(inv, $"idade_mae_1o_parto media={(ages.Count > 0 ? ages.Average() : 0):F1} mediana={(ages.Count > 0 ? ages[ages.Count / 2] : 0):F1} p10={(ages.Count > 0 ? ages[ages.Count / 10] : 0):F1} n={ages.Count}");
        var unwed = tallies.SelectMany(t => t.UnwedOutcomes).GroupBy(kv => kv.Key).OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => $"{g.Key}={g.Sum(kv => kv.Value)}");
        sb.AppendLine(inv, $"gravidez_sem_uniao_aparente: {string.Join(' ', unwed)}");
        var menarche = tallies.SelectMany(t => t.MenarcheAges).OrderBy(a => a).ToList();
        if (menarche.Count > 0)
            sb.AppendLine(inv, $"menarca media={menarche.Average():F2} p10={menarche[menarche.Count / 10]:F1} mediana={menarche[menarche.Count / 2]:F1} p90={menarche[menarche.Count * 9 / 10]:F1} n={menarche.Count}");
        int loci = tallies.Max(t => t.LocusCarriers.Length);
        if (loci > 0)
        {
            double alive = Sum(t => t.GenotypedAlive);
            var parts = Enumerable.Range(0, loci).Select(i =>
                $"L{i}:portadores={Pct(tallies.Sum(t => t.LocusCarriers.Length > i ? t.LocusCarriers[i] : 0), alive)},afetados={Pct(tallies.Sum(t => t.LocusAffected.Length > i ? t.LocusAffected[i] : 0), alive)}");
            sb.AppendLine(inv, $"genetica_fim vivos={alive} {string.Join(' ', parts)}");
            sb.AppendLine(inv, $"recessiva_homozigota filhos_de_parentes={Pct(Sum(t => t.KinAffected), Sum(t => t.KinBirths))} (n={Sum(t => t.KinBirths)}) outros={Pct(Sum(t => t.OtherAffected), Sum(t => t.OtherBirths))} (n={Sum(t => t.OtherBirths)})");
        }
        return sb.ToString();
    }
}
