using System.Reflection;
using LivingWorld.Domain;
using LivingWorld.Simulation;
using LivingWorld.Simulation.History;

namespace LivingWorld.Tests.History;

/// <summary>Fase 10, T15: <see cref="HistoryBeliefQuery"/> + <see cref="DistortionEngine.Materialize"/>
/// (HIST-16, HIST-19).</summary>
public class HistoryBeliefQueryTests
{
    private static HistoryRules DistortingRules => HistoryRules.Create(
        enabled: true,
        skeletonSignificanceThreshold: 0.5,
        canonSizePerCommunity: 10,
        mediumFidelityByType: new Dictionary<TransmissionMediumType, MediumFidelity>
        {
            [TransmissionMediumType.OralTradition] = new(1.0, 10, DeathConditionType.Decay),
            [TransmissionMediumType.Song] = new(1.0, 10, DeathConditionType.Decay),
            [TransmissionMediumType.Book] = new(1.0, 10, DeathConditionType.Decay),
            [TransmissionMediumType.Monument] = new(1.0, 10, DeathConditionType.Decay),
        },
        operatorProbability: HistoryRules.Default.OperatorProbability,
        importanceWeight: 1,
        transmissibilityWeight: 0,
        recencyWeight: 0).Value!;

    private static City EnsureCity(WorldState world, CityId cityId, AggregatePopulationPool? pool = null)
    {
        if (world.FindCity(cityId) is { } existing)
            return existing;

        var city = new City(
            cityId,
            ScenarioRunner.DefaultVillageLocation,
            0,
            null,
            pool ?? AggregatePopulationPool.Empty);
        world.AddCity(city);
        return city;
    }

    private static (WorldState world, Fact fact, ReportState report, City city) SeedCanonReport(
        int hopCount = 0,
        CityId? cityId = null)
    {
        var (world, _) = ScenarioRunner.Create(3, historyRules: DistortingRules);
        var npc = world.Npcs[0];
        var community = cityId ?? npc.City;
        var city = EnsureCity(world, community);

        var fact = new Fact(
            new FactId(1),
            5,
            WorldEventKind.Marriage,
            [npc.Id, new NpcId(2)],
            community,
            0.8,
            "1|2|cause");
        world.AddFact(fact);

        var report = new ReportState(
            world.NextReportIdAndAdvance(),
            fact.Id,
            city.Id,
            TransmissionMediumType.Song,
            HopCount: hopCount,
            Weight: fact.Significance,
            CreatedAtTick: 10,
            LastHopTick: 10);
        world.RegisterReport(report);
        CanonSlotManager.Admit(city, report, DistortingRules, nowTick: 20);

        return (world, fact, report, city);
    }

    [Fact]
    public void BeliefOf_npc_resolves_canon_report_not_raw_fact()
    {
        var (world, fact, _, city) = SeedCanonReport(hopCount: 2);
        var npc = world.Npcs[0];

        var result = HistoryBeliefQuery.BeliefOf(world, npc.Id, fact.Id);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.DistanceFromFact > 0);
        Assert.Equal(city.CanonSlots[0].Id, result.Value.ReportId);
    }

    [Fact]
    public void BeliefOf_city_resolves_canon_report_not_raw_fact()
    {
        var (world, fact, _, city) = SeedCanonReport(hopCount: 1);

        var result = HistoryBeliefQuery.BeliefOf(world, city.Id, fact.Id);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.DistanceFromFact > 0);
    }

    [Fact]
    public void BeliefOf_fails_explicitly_when_community_never_heard_fact()
    {
        var (world, fact, _, city) = SeedCanonReport();
        var otherCity = EnsureCity(world, new CityId(Guid.Parse("22222222-2222-2222-2222-222222222222")));

        var result = HistoryBeliefQuery.BeliefOf(world, otherCity.Id, fact.Id);

        Assert.False(result.IsSuccess);
        Assert.Equal(HistoryBeliefQuery.NeverHeardError, result.Error);
    }

    [Fact]
    public void BeliefOf_diverges_between_two_communities_on_same_fact()
    {
        var cityAId = new CityId(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
        var cityBId = new CityId(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
        var (world, fact, _, _) = SeedCanonReport(hopCount: 4, cityId: cityAId);
        var cityA = world.FindCity(cityAId)!;
        var cityB = EnsureCity(world, cityBId);

        var reportB = new ReportState(
            world.NextReportIdAndAdvance(),
            fact.Id,
            cityB.Id,
            TransmissionMediumType.OralTradition,
            HopCount: 0,
            Weight: fact.Significance,
            CreatedAtTick: 10,
            LastHopTick: 10);
        world.RegisterReport(reportB);
        CanonSlotManager.Admit(cityB, reportB, DistortingRules, nowTick: 20);

        var beliefA = HistoryBeliefQuery.BeliefOf(world, cityA.Id, fact.Id);
        var beliefB = HistoryBeliefQuery.BeliefOf(world, cityB.Id, fact.Id);

        Assert.True(beliefA.IsSuccess);
        Assert.True(beliefB.IsSuccess);
        Assert.NotEqual(beliefA.Value!.DistortedMagnitude, beliefB.Value!.DistortedMagnitude);
        Assert.NotEqual(beliefA.Value.DistanceFromFact, beliefB.Value.DistanceFromFact);
    }

    [Fact]
    public void BeliefOf_truth_and_belief_diverge_when_hops_distort()
    {
        var (world, fact, _, city) = SeedCanonReport(hopCount: 3);

        var truth = HistoryTruthQuery.GetFact(world, fact.Id);
        var belief = HistoryBeliefQuery.BeliefOf(world, city.Id, fact.Id);

        Assert.True(truth.IsSuccess);
        Assert.True(belief.IsSuccess);
        Assert.Equal(fact.Significance, truth.Value!.Significance);
        Assert.True(belief.Value!.DistanceFromFact > 0);
    }

    [Fact]
    public void BeliefOf_aggregate_pool_npc_resolves_community_canon_without_individual_state()
    {
        var poolCityId = new CityId(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"));
        var (world, _) = ScenarioRunner.Create(3, historyRules: DistortingRules);
        var city = new City(
            poolCityId,
            ScenarioRunner.DefaultVillageLocation,
            0,
            null,
            new AggregatePopulationPool(5, 500, 250));
        world.AddCity(city);

        var npc = world.Npcs[0];
        var fact = new Fact(
            new FactId(1),
            5,
            WorldEventKind.Marriage,
            [npc.Id, new NpcId(2)],
            poolCityId,
            0.8,
            "1|2|cause");
        world.AddFact(fact);

        var report = new ReportState(
            world.NextReportIdAndAdvance(),
            fact.Id,
            city.Id,
            TransmissionMediumType.OralTradition,
            HopCount: 1,
            Weight: fact.Significance,
            CreatedAtTick: 10,
            LastHopTick: 10);
        world.RegisterReport(report);
        CanonSlotManager.Admit(city, report, DistortingRules, nowTick: 20);

        var poolNpcId = new NpcId(world.NextNpcId);
        int npcCountBefore = world.Npcs.Count;

        var result = HistoryBeliefQuery.BeliefOf(world, poolNpcId, fact.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(npcCountBefore, world.Npcs.Count);
        Assert.Equal(city.CanonSlots[0].Id, result.Value!.ReportId);
    }

    [Fact]
    public void Materialize_output_is_not_part_of_world_snapshot_canonical_state()
    {
        Assert.DoesNotContain(
            WorldSnapshot.ReflectedProperties,
            p => p.PropertyType == typeof(DistortedReport)
                 || p.PropertyType == typeof(IReadOnlyList<DistortedReport>));

        Assert.Null(typeof(DistortedReport).GetCustomAttribute<CanonicalAttribute>());
    }
}
