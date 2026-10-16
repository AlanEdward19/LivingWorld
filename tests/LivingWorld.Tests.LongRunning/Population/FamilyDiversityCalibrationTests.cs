using System.Text.Json;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Tests.Shared.Population;

namespace LivingWorld.Tests.LongRunning.Population;

/// <summary>FAM-32: faixa observada, controle negativo e piso empírico, ADR-0034.</summary>
public class FamilyDiversityCalibrationTests
{
    public sealed record Sample(int Seed, double VitalityDifference, double OpennessDifference, double MinimumVitalityCv, int MinimumAlive, int FinalAlive);
    public sealed record Calibration(int Seeds, int GateYears, int CenturyYears, double ConfidenceZ, Sample[] Samples);
    private static string Root => FindRoot();
    private static string BaselinePath => Path.Combine(Root, "tests/baselines/family-diversity.json");
    private static readonly Lazy<Sample[]> Current = new(() => Measure(10, 20));
    private static readonly Lazy<Sample[]> Century = new(() => Measure(100, 20, paired: false));

    [Fact, Trait("Category", "Scenario")]
    public void Record_explicit_family_diversity_baseline()
    {
        if (Environment.GetEnvironmentVariable("LW_RECORD_FAMILY_DIVERSITY") != "1") return;
        var samples = Measure(10, 20);
        var century = Century.Value;
        samples = samples.Select((s, i) => s with { MinimumVitalityCv = century[i].MinimumVitalityCv, MinimumAlive = century[i].MinimumAlive, FinalAlive = century[i].FinalAlive }).ToArray();
        Assert.All(samples, s => Assert.True(s.MinimumVitalityCv > 0 && s.MinimumAlive > 1, $"seed={s.Seed}: CV mínimo={s.MinimumVitalityCv}, população mínima={s.MinimumAlive}"));
        File.WriteAllText(BaselinePath, JsonSerializer.Serialize(new Calibration(20, 10, 100, 1.959963984540054, samples),
            new JsonSerializerOptions { WriteIndented = true }));
    }

    [Fact, Trait("Category", "Scenario")]
    public void Real_vitality_difference_stays_in_the_calibrated_band() => Assert.True(InBand(Current.Value.Select(s => s.VitalityDifference),
        Baseline().Samples.Select(s => s.VitalityDifference), out string detail), detail);

    [Fact, Trait("Category", "Scenario")]
    public void Unselected_trait_stays_in_the_neutral_band()
    {
        var differences = Current.Value.Select(s => s.OpennessDifference).ToArray();
        Assert.True(InBand(differences, Baseline().Samples.Select(s => s.OpennessDifference), out string detail), detail);
        // Openness não entra na função de mortalidade. Ausência de seleção direta admite zero no IC pareado.
        Assert.True(Math.Abs(differences.Average()) <= Baseline().ConfidenceZ * StandardError(differences),
            $"Controle negativo: diferença média {differences.Average()}, EP {StandardError(differences)}");
    }

    [Fact, Trait("Category", "Scenario")]
    public void Strong_selection_is_rejected_by_the_same_band()
    {
        var baseline = Baseline();
        var strong = Measure(baseline.GateYears, baseline.Seeds, mortalityWeight: 20);
        Assert.False(InBand(strong.Select(s => s.VitalityDifference), baseline.Samples.Select(s => s.VitalityDifference), out string detail), detail);
        Assert.True(InBand(Current.Value.Select(s => s.VitalityDifference), baseline.Samples.Select(s => s.VitalityDifference), out detail), detail);
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

    private static Calibration Baseline() => JsonSerializer.Deserialize<Calibration>(File.ReadAllText(BaselinePath))!;
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
    private static Sample[] Measure(int years, int count, double? mortalityWeight = null, bool paired = true)
    {
        var samples = new Sample[count];
        Parallel.For(1, count + 1, new ParallelOptions { MaxDegreeOfParallelism = 4 }, seed =>
        {
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
