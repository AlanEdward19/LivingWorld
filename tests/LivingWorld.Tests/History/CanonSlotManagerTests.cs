using LivingWorld.Domain;
using LivingWorld.Simulation;
using LivingWorld.Simulation.History;

namespace LivingWorld.Tests.History;

/// <summary>Fase 10, T11: <see cref="CanonSlotManager"/> (HIST-08 AC2).</summary>
public class CanonSlotManagerTests
{
    private static HistoryRules Rules => HistoryRules.Create(
        enabled: true,
        skeletonSignificanceThreshold: 0.5,
        canonSizePerCommunity: 2,
        mediumFidelityByType: HistoryRules.Default.MediumFidelityByType,
        operatorProbability: HistoryRules.Default.OperatorProbability,
        importanceWeight: 1,
        transmissibilityWeight: 0,
        recencyWeight: 0).Value!;

    private static ReportState MakeReport(long id, double weight, long tick = 100) =>
        new(
            new ReportId(id),
            new FactId(id),
            new CityId(Guid.Parse("11111111-1111-1111-1111-111111111111")),
            TransmissionMediumType.OralTradition,
            HopCount: 0,
            Weight: weight,
            CreatedAtTick: tick,
            LastHopTick: tick);

    private static City EnsureCity(WorldState world, CityId cityId)
    {
        if (world.FindCity(cityId) is { } existing)
            return existing;

        var city = new City(cityId, ScenarioRunner.DefaultVillageLocation, 0, null, AggregatePopulationPool.Empty);
        world.AddCity(city);
        return city;
    }

    [Fact]
    public void Admit_without_eviction_when_canon_not_full()
    {
        var (world, _) = ScenarioRunner.Create(1, historyRules: Rules);
        var npc = world.Npcs[0];
        var city = EnsureCity(world, npc.City);
        var report = MakeReport(1, 0.5) with { CommunityId = city.Id };

        var result = CanonSlotManager.Admit(city, report, Rules, nowTick: 200);

        Assert.True(result.IsSuccess);
        Assert.Single(city.CanonSlots);
        Assert.Equal(new ReportId(1), city.CanonSlots[0].Id);
    }

    [Fact]
    public void Admit_evicts_lowest_weight_when_canon_full()
    {
        var (world, _) = ScenarioRunner.Create(1, historyRules: Rules);
        var npc = world.Npcs[0];
        var city = EnsureCity(world, npc.City);
        var low = MakeReport(1, 0.2) with { CommunityId = city.Id };
        var high = MakeReport(2, 0.9) with { CommunityId = city.Id };
        CanonSlotManager.Admit(city, low, Rules, nowTick: 200);
        CanonSlotManager.Admit(city, high, Rules, nowTick: 200);

        var incoming = MakeReport(3, 0.95) with { CommunityId = city.Id };
        CanonSlotManager.Admit(city, incoming, Rules, nowTick: 200);

        Assert.Equal(2, city.CanonSlots.Count);
        Assert.DoesNotContain(city.CanonSlots, r => r.Id == low.Id);
        Assert.Contains(city.CanonSlots, r => r.Id == incoming.Id);
    }

    [Fact]
    public void Admit_tie_breaks_by_report_id_when_weights_equal()
    {
        var rules = Rules with { ImportanceWeight = 0, TransmissibilityWeight = 0, RecencyWeight = 0 };
        var (world, _) = ScenarioRunner.Create(1, historyRules: rules);
        var npc = world.Npcs[0];
        var city = EnsureCity(world, npc.City);
        var first = MakeReport(1, 0.5) with { CommunityId = city.Id };
        var second = MakeReport(2, 0.5) with { CommunityId = city.Id };
        CanonSlotManager.Admit(city, first, rules, nowTick: 100);
        CanonSlotManager.Admit(city, second, rules, nowTick: 100);

        var incoming = MakeReport(3, 0.5) with { CommunityId = city.Id };
        CanonSlotManager.Admit(city, incoming, rules, nowTick: 100);

        Assert.Equal(2, city.CanonSlots.Count);
        Assert.DoesNotContain(city.CanonSlots, r => r.Id == first.Id);
    }
}
