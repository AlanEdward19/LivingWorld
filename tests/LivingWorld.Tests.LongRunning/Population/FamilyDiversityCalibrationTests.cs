using System.Text.Json;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Tests.Shared;
using LivingWorld.Tests.Shared.Population;

namespace LivingWorld.Tests.LongRunning.Population;

/// <summary>FAM-32: faixa observada, controle negativo e piso empírico, ADR-0036.</summary>
[Trait("Category", "Century")] // 20 seeds × 100 anos: fora do gate de lote, roda no --full diário
public class FamilyDiversityCalibrationTests
{
    public sealed record Sample(int Seed, double VitalityDifference, double OpennessDifference, double MinimumVitalityCv, int MinimumAlive, int FinalAlive);
    public sealed record Calibration(int Seeds, int GateYears, int CenturyYears, double ConfidenceZ, Sample[] Samples);
    private static string Root => FindRoot();
    private static string BaselinePath => Path.Combine(Root, "tests/baselines/family-diversity.json");
    // O braço real dos 10 anos é o prefixo do século: os dois saem da mesma corrida compartilhada (ADR-0044).
    private static readonly Lazy<Sample[]> Current = new(() => DefaultVillageCentury.Runs.Select(r => new Sample((int)r.Seed,
        r.GateVitalityDifference, r.GateOpennessDifference, DefaultVillageCentury.MinimumCv(r, 10), DefaultVillageCentury.MinimumAlive(r, 10), r.Alive[10])).ToArray());
    private static readonly Lazy<Sample[]> Century = new(() => DefaultVillageCentury.Runs.Select(r => new Sample((int)r.Seed,
        r.CenturySelfDifference, r.CenturyOpennessSelfDifference, DefaultVillageCentury.MinimumCv(r, 100), DefaultVillageCentury.MinimumAlive(r, 100), r.Alive[100])).ToArray());

    /// <summary>Com <c>LW_RECORD_FAMILY_DIVERSITY=1</c> a baseline é regravada antes da primeira leitura, no mesmo processo
    /// que a verifica: o século compartilhado roda uma vez só para gravar e para conferir.</summary>
    [Fact, Trait("Category", "Scenario")]
    public void Record_explicit_family_diversity_baseline() => Assert.Equal(20, Baseline().Seeds);

    private static Calibration Record()
    {
        var samples = Measure(10, 20);
        var century = Century.Value;
        samples = samples.Select((s, i) => s with { MinimumVitalityCv = century[i].MinimumVitalityCv, MinimumAlive = century[i].MinimumAlive, FinalAlive = century[i].FinalAlive }).ToArray();
        Assert.All(samples, s => Assert.True(s.MinimumVitalityCv > 0 && s.MinimumAlive > 1, $"seed={s.Seed}: CV mínimo={s.MinimumVitalityCv}, população mínima={s.MinimumAlive}"));
        var recorded = new Calibration(20, 10, 100, 1.959963984540054, samples);
        File.WriteAllText(BaselinePath, JsonSerializer.Serialize(recorded, new JsonSerializerOptions { WriteIndented = true }));
        return recorded;
    }

    [Fact, Trait("Category", "Scenario")]
    public void Real_vitality_difference_stays_in_the_calibrated_band() => Assert.True(InBand(Current.Value.Select(s => s.VitalityDifference),
        Baseline().Samples.Select(s => s.VitalityDifference), out string detail), detail);

    [Fact, Trait("Category", "Scenario")]
    public void Unselected_trait_stays_in_the_neutral_band()
    {
        var differences = Current.Value.Select(s => s.OpennessDifference).ToArray();
        // Openness não entra na mortalidade nem na atração: a faixa calibrada é o controle. Até o AD-134 havia também a
        // exigência de diferença média zero no IC pareado; com a saúde dinâmica (que pesa na atração), o braço real escolhe
        // parceiro pela saúde e o neutro ao acaso, o sucesso reprodutivo fica mais desigual e um traço neutro perde um pouco
        // mais de variância (−0,009 de CV em 10 anos). Os braços deixaram de diferir só pela seleção de vitalidade.
        Assert.True(InBand(differences, Baseline().Samples.Select(s => s.OpennessDifference), out string detail), detail);
    }

    /// <summary>Classe própria: a seleção forte (20 seeds × 10 anos, pareada) roda em paralelo com o século em vez de esperar por ele.</summary>
    [Trait("Category", "Century")] // 20 seeds × 100 anos: fora do gate de lote, roda no --full diário
    public class StrongSelection
    {
        [Fact, Trait("Category", "Scenario")]
        public void Strong_selection_is_rejected_by_the_same_band()
        {
            var baseline = Baseline();
            var strong = Measure(baseline.GateYears, baseline.Seeds, mortalityWeight: 20);
            Assert.False(InBand(strong.Select(s => s.VitalityDifference), baseline.Samples.Select(s => s.VitalityDifference), out string detail), detail);
            Assert.True(InBand(Current.Value.Select(s => s.VitalityDifference), baseline.Samples.Select(s => s.VitalityDifference), out detail), detail);
        }
    }

    [Fact, Trait("Category", "Scenario")]
    public void Vitality_does_not_collapse_at_century_checkpoints()
    {
        double floor = Baseline().Samples.Min(s => s.MinimumVitalityCv);
        Assert.True(floor > 0, "Baseline não pode tornar o piso tautológico.");
        int populationFloor = Baseline().Samples.Min(s => s.MinimumAlive);
        Assert.True(populationFloor > 1);
        Assert.All(Century.Value, s => Assert.True(s.MinimumVitalityCv >= floor && s.MinimumAlive >= populationFloor,
            $"seed={s.Seed}, mínimo CV={s.MinimumVitalityCv}, piso medido={floor}"));
    }

    [Fact]
    public void Baseline_contains_twenty_paired_seeds_and_a_positive_century_floor()
    {
        var baseline = Baseline();
        Assert.Equal(20, baseline.Seeds);
        Assert.Equal(Enumerable.Range(1, baseline.Seeds), baseline.Samples.Select(s => s.Seed));
        Assert.Equal(10, baseline.GateYears);
        Assert.Equal(100, baseline.CenturyYears);
        Assert.All(baseline.Samples, s => Assert.True(s.MinimumVitalityCv > 0 && s.MinimumAlive > 1));
    }

    [Fact]
    public void Documented_criterion_names_the_executable_proofs()
    {
        string criterion = File.ReadAllText(Path.Combine(Root, "docs/roadmap/phase-07-family.md"));
        Assert.Contains("family-diversity.json", criterion);
        Assert.Contains("controle negativo", criterion);
        Assert.Contains("100 anos", criterion);
    }

    internal static void AssertVitalityBand() => new FamilyDiversityCalibrationTests().Real_vitality_difference_stays_in_the_calibrated_band();

    private static readonly Lazy<Calibration> Loaded = new(() => Environment.GetEnvironmentVariable("LW_RECORD_FAMILY_DIVERSITY") == "1"
        ? Record() : JsonSerializer.Deserialize<Calibration>(File.ReadAllText(BaselinePath))!);
    private static Calibration Baseline() => Loaded.Value;
    private static bool InBand(IEnumerable<double> actual, IEnumerable<double> reference, out string detail)
    {
        double[] expected = reference.ToArray();
        double mean = expected.Average(), margin = Baseline().ConfidenceZ * StandardError(expected);
        double observed = actual.Average();
        detail = $"CV real-neutro médio={observed}; baseline={mean}, margem IC95={margin}";
        return observed >= mean - margin && observed <= mean + margin;
    }
    private static double StandardError(double[] values) => Math.Sqrt(values.Sum(v => Math.Pow(v - values.Average(), 2)) / (values.Length - 1) / values.Length);
    private static double Cv(IEnumerable<double> values)
    {
        double[] all = values.ToArray();
        if (all.Length == 0 || all.Average() == 0) return 0;
        double mean = all.Average();
        return Math.Sqrt(all.Sum(v => Math.Pow(v - mean, 2)) / all.Length) / mean;
    }
    internal static Sample[] Measure(int years, int count, double? mortalityWeight = null, bool paired = true)
    {
        var samples = new Sample[count];
        ParallelSeeds.ForEach(1, (ulong)count, s =>
        {
            int seed = (int)s;
            var rules = ScenarioRunner.DefaultFamilyRules;
            if (mortalityWeight is { } weight) rules = rules with { VitalityMortalityWeight = weight };
            var (real, clock) = ScenarioRunner.Create((ulong)seed, familyRules: rules);
            var controlWorld = paired ? NeutralDriftScenarioHarness.Create((ulong)seed, true) : default;
            double minimum = Cv(real.Npcs.Where(n => n.IsAlive).Select(n => n.Vitality));
            int minimumAlive = real.Npcs.Count(n => n.IsAlive);
            for (int year = 1; year <= years; year++)
            {
                clock.Run(real, real.Calendar.HoursPerYear);
                if (paired) controlWorld.Clock.Run(controlWorld.World, controlWorld.World.Calendar.HoursPerYear);
                minimum = Math.Min(minimum, Cv(real.Npcs.Where(n => n.IsAlive).Select(n => n.Vitality)));
                minimumAlive = Math.Min(minimumAlive, real.Npcs.Count(n => n.IsAlive));
            }
            var living = real.Npcs.Where(n => n.IsAlive).ToArray();
            var control = paired ? controlWorld.World.Npcs.Where(n => n.IsAlive).ToArray() : living;
            samples[seed - 1] = new(seed,
                Cv(living.Select(n => n.Vitality)) - Cv(control.Select(n => n.Vitality)),
                Cv(living.Select(n => (double)n.Personality.Openness)) - Cv(control.Select(n => (double)n.Personality.Openness)), minimum, minimumAlive, living.Length);
        });
        return samples;
    }
    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "LivingWorld.sln"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root missing");
    }
}
