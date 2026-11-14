using System.Diagnostics;
using LivingWorld.Domain.Cognition;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Decision;
using LivingWorld.Tests.Shared.Performance;

namespace LivingWorld.Tests.LongRunning.Performance;

/// <summary>Custo por decisão (ADR-0046, AD-142), não é gate: com os contextos reais de 1.000 NPCs, mede a utilidade sem cérebro,
/// com ganhos (opção c, com e sem guardar a participação dos grupos) e a rede 28-16-10 do sandbox (opção a). Alterna os braços
/// em 5 rodadas e fica com a mediana. Rode com <c>LW_BRAIN_COST_OUT=arquivo</c>.</summary>
[Collection(ScalePerformanceCollection.Name)]
[Trait("Category", "Scenario")]
[Trait("Category", "Measure")]
[Trait("Category", "Timing")] // mede tempo de relógio: fase quieta do verify.sh
public class BrainDecisionCostHarness
{
    [Fact]
    public void Measure()
    {
        var (world, clock) = ScaleScenarioFixture.CreateWorld(42, 1000);
        clock.Tick(world);
        var contexts = world.Npcs.Where(n => n.IsAlive)
            .Select(n => DecisionContextBuilder.Build(world, n, world.CurrentDate.TotalHours)).ToArray();
        var rng = new WorldRng(9);
        var gains = contexts.Select(_ =>
        {
            var g = new short[BrainState.Actions * BrainState.Groups];
            for (int i = 0; i < g.Length; i++) g[i] = (short)((rng.NextDouble() * 2 - 1) * 300);
            return g;
        }).ToArray();
        var groups = new double[2 * BrainState.Groups];
        var net = NeuralPolicySandbox.Random(new WorldRng(3));
        var rules = world.NeedsRules;
        var economy = world.EconomyRules;
        const int reps = 400;
        double sink = 0;

        double Time(Action<int> body)
        {
            var sw = Stopwatch.StartNew();
            for (int r = 0; r < reps; r++)
                for (int i = 0; i < contexts.Length; i++) body(i);
            return sw.Elapsed.TotalNanoseconds / (reps * (double)contexts.Length);
        }

        var samples = new List<double[]>();
        for (int round = 0; round < 5; round++)
        {
            double off = Time(i => sink += (int)BehaviorDecisionSystem.SelectByUtility(contexts[i], rules, economy, null, captureTrace: false).Action);
            double on = Time(i => sink += (int)BehaviorDecisionSystem.SelectByUtility(contexts[i], rules, economy, null, captureTrace: false, brainGains: gains[i]).Action);
            double onGroups = Time(i => sink += (int)BehaviorDecisionSystem.SelectByUtility(
                contexts[i], rules, economy, null, captureTrace: false, brainGains: gains[i], brainGroups: groups).Action);
            double neural = Time(i => sink += (int)net.Decide(contexts[i]));
            samples.Add([off, on, onGroups, neural]);
        }

        double Median(int k) => samples.Select(s => s[k]).OrderBy(v => v).ElementAt(samples.Count / 2);
        string text = $"contexts={contexts.Length} nsPerDecision off={Median(0):F1} gains={Median(1):F1} gainsAndShares={Median(2):F1} "
            + $"neural28x16x10={Median(3):F1} checksum={sink:F0}";
        File.WriteAllText(Environment.GetEnvironmentVariable("LW_BRAIN_COST_OUT") ?? Path.Combine(Path.GetTempPath(), "lw-brain-cost.txt"), text);
    }
}
