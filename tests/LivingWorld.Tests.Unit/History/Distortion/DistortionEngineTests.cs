using LivingWorld.Domain.History;
using LivingWorld.Domain.History.Distortion;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.History.Distortion;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Tests.Shared;

namespace LivingWorld.Tests.Unit.History.Distortion;

/// <summary>Fase 10, T8/T9: <see cref="DistortionEngine"/> (HIST-05/HIST-06).</summary>
public class DistortionEngineTests
{
    private static readonly HistoryRules Rules = HistoryRules.Default;

    private static (WorldState world, Fact fact, ReportState report) Sample()
    {
        var (world, _) = ScenarioRunner.Create(3, historyRules: Rules);
        var npc = world.Npcs[0];
        var fact = new Fact(new FactId(1), 5, WorldEventKind.Marriage, [npc.Id, new NpcId(2)], npc.City, 0.8, "1|2");
        world.AddFact(fact);
        var report = new ReportState(
            world.NextReportIdAndAdvance(),
            fact.Id,
            npc.City,
            TransmissionMediumType.OralTradition,
            HopCount: 0,
            Weight: fact.Significance,
            CreatedAtTick: 10,
            LastHopTick: 10);
        return (world, fact, report);
    }

    private static WorldRng FixedRng(int seed) => new WorldRng((ulong)seed);

    [Theory]
    [InlineData(DistortionOperator.AttributionSwap)]
    [InlineData(DistortionOperator.MagnitudeInflation)]
    [InlineData(DistortionOperator.TemporalCompression)]
    [InlineData(DistortionOperator.CausalLoss)]
    [InlineData(DistortionOperator.Moralization)]
    [InlineData(DistortionOperator.Anachronism)]
    [InlineData(DistortionOperator.ConvenientOmission)]
    [InlineData(DistortionOperator.CharacterMerge)]
    public void Apply_is_deterministic_for_each_operator(DistortionOperator op)
    {
        var (world, fact, _) = Sample();
        var input = DistortionEngine.FromFact(fact);
        var rngA = FixedRng(99);
        var rngB = FixedRng(99);

        var a = DistortionEngine.Apply(op, input, rngA, world);
        var b = DistortionEngine.Apply(op, input, rngB, world);

        Assert.Equal(a.Participants.Select(p => p.Value), b.Participants.Select(p => p.Value));
        Assert.Equal(a.Magnitude, b.Magnitude);
        Assert.Equal(a.Tick, b.Tick);
        Assert.Equal(a.Payload, b.Payload);
        Assert.Equal(a.MoralSeed, b.MoralSeed);
        Assert.Equal(a.DistanceFromFact, b.DistanceFromFact);
        Assert.True(a.DistanceFromFact > input.DistanceFromFact);
    }

    [Fact]
    public void DistortionEngine_does_not_reference_llm_assembly()
    {
        var referenced = typeof(DistortionEngine).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name);
        Assert.DoesNotContain("LivingWorld.AI", referenced);
    }

    [Fact]
    public void Five_hop_chain_maintains_non_decreasing_distance()
    {
        var (world, fact, report) = Sample();
        report = report with { Medium = TransmissionMediumType.Song };
        double previous = 0;

        for (int i = 0; i < 5; i++)
        {
            report = DistortionEngine.AdvanceHop(report, fact, Rules, world.Rng, world, nowTick: 20 + i);
            double distance = DistortionEngine.DistanceFromFact(report, fact, Rules, world.Rng, world);
            Assert.True(distance + 1e-9 >= previous);
            previous = distance;
        }
    }

    [Fact]
    public void Distortion_digest_is_identical_across_two_separate_processes()
    {
        var a = RunDigestInSeparateProcess(42);
        var b = RunDigestInSeparateProcess(42);
        Assert.Equal(a, b);
    }

    private static string RunDigestInSeparateProcess(ulong seed) =>
        WorkersProcess.RunDigest($"history-distortion-digest {seed}");
}
