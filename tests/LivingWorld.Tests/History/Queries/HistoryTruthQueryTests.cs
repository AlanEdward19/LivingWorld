using LivingWorld.Domain;
using LivingWorld.Simulation;
using LivingWorld.Simulation.History;

namespace LivingWorld.Tests.History;

/// <summary>Fase 10, T14: <see cref="HistoryTruthQuery"/> (HIST-15 AC1).</summary>
public class HistoryTruthQueryTests
{
    private static Fact SampleFact(NpcId participant, CityId city) =>
        new(new FactId(1), 5, WorldEventKind.Marriage, [participant, new NpcId(2)], city, 0.8, "1|2");

    [Fact]
    public void GetFact_returns_complete_fact_without_distortion()
    {
        var (world, _) = ScenarioRunner.Create(3, historyRules: HistoryRules.Default);
        var npc = world.Npcs[0];
        var fact = SampleFact(npc.Id, npc.City);
        world.AddFact(fact);

        var result = HistoryTruthQuery.GetFact(world, fact.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(fact.Id, result.Value!.Id);
        Assert.Equal(fact.Tick, result.Value.Tick);
        Assert.Equal(fact.Kind, result.Value.Kind);
        Assert.Equal(fact.Participants.Select(p => p.Value), result.Value.Participants.Select(p => p.Value));
        Assert.Equal(fact.Location, result.Value.Location);
        Assert.Equal(fact.Significance, result.Value.Significance);
        Assert.Equal(fact.Payload, result.Value.Payload);
    }

    [Fact]
    public void GetFact_fails_for_missing_fact_id()
    {
        var (world, _) = ScenarioRunner.Create(1, historyRules: HistoryRules.Default);

        var result = HistoryTruthQuery.GetFact(world, new FactId(999));

        Assert.False(result.IsSuccess);
        Assert.Equal("Fact: não existe", result.Error);
    }
}
