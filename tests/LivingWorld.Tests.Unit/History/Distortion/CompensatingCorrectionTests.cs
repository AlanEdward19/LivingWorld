using LivingWorld.Domain.History;
using LivingWorld.Domain.History.Distortion;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.History.Distortion;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Tests.Shared;

namespace LivingWorld.Tests.Unit.History.Distortion;

/// <summary>Fase 10, T19: <see cref="CompensatingCorrection"/> (HIST-24/25).</summary>
public class CompensatingCorrectionTests
{
    [Fact]
    public void Apply_does_not_mutate_original_fact()
    {
        var (world, original) = SampleOriginalFact();
        var originalSnapshot = SnapshotFact(original);

        var result = CompensatingCorrectionOperations.Apply(
            world,
            original.Id,
            tick: 200,
            WorldEventKind.Birth,
            [new NpcId(99)],
            location: null,
            significance: 0.9,
            correctedPayload: "99|1|2",
            reason: "paternity_corrected");

        Assert.True(result.IsSuccess);
        var stillOriginal = world.FindFact(original.Id)!;
        Assert.Equal(originalSnapshot, SnapshotFact(stillOriginal));
    }

    [Fact]
    public void GetFactLine_exposes_original_and_correction_marked()
    {
        var (world, original) = SampleOriginalFact();

        CompensatingCorrectionOperations.Apply(
            world,
            original.Id,
            tick: 200,
            WorldEventKind.Birth,
            [new NpcId(99)],
            location: null,
            significance: 0.9,
            correctedPayload: "99|1|2",
            reason: "paternity_corrected");

        var line = CompensatingCorrectionOperations.GetFactLine(world, original.Id);

        Assert.Equal(2, line.Count);
        Assert.Contains(line, e => e.Role == FactLineRole.Original && e.Fact.Id == original.Id);
        Assert.Contains(line, e => e.Role == FactLineRole.Correction && e.Fact.Kind == WorldEventKind.CompensatingCorrection);
    }

    [Fact]
    public void Compensating_correction_digest_is_identical_across_two_separate_processes()
    {
        var a = RunCorrectionDigestInSeparateProcess(42);
        var b = RunCorrectionDigestInSeparateProcess(42);
        Assert.Equal(a, b);
    }

    private static (WorldState World, Fact Original) SampleOriginalFact()
    {
        var (world, _) = ScenarioRunner.Create(1, historyRules: HistoryRules.Default);
        var npc = world.Npcs[0];
        var original = new Fact(new FactId(1), 10, WorldEventKind.Birth, [npc.Id], npc.City, 0.9, "wrong|payload");
        world.AddFact(original);
        return (world, original);
    }

    private static string SnapshotFact(Fact fact) =>
        $"{fact.Id.Value}|{fact.Tick}|{fact.Kind}|{string.Join(',', fact.Participants.Select(p => p.Value))}|{fact.Significance}|{fact.Payload}";

    private static string RunCorrectionDigestInSeparateProcess(ulong seed) =>
        WorkersProcess.RunDigest($"history-correction-digest {seed}");
}
