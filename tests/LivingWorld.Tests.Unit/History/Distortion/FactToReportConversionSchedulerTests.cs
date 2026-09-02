using LivingWorld.Domain.Cities;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Shared;
using LivingWorld.Infrastructure.EventLog;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.History.Distortion;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.History.Distortion;

/// <summary>Fase 10, T10: <see cref="FactToReportConversionScheduler"/> (HIST-03).</summary>
public class FactToReportConversionSchedulerTests
{
    private static HistoryRules EnabledRules => HistoryRules.Create(
        enabled: true,
        skeletonSignificanceThreshold: 0.5,
        canonSizePerCommunity: 10,
        mediumFidelityByType: HistoryRules.Default.MediumFidelityByType,
        operatorProbability: HistoryRules.Default.OperatorProbability,
        importanceWeight: 1,
        transmissibilityWeight: 1,
        recencyWeight: 1).Value!;

    private static City EnsureCity(WorldState world, CityId cityId)
    {
        if (world.FindCity(cityId) is { } existing)
            return existing;

        var city = new City(cityId, ScenarioRunner.DefaultVillageLocation, 0, null, AggregatePopulationPool.Empty);
        world.AddCity(city);
        return city;
    }

    [Fact]
    public void Last_witness_death_schedules_conversion_on_current_tick()
    {
        var sink = new BufferingWorldEventSink();
        var (world, clock) = ScenarioRunner.Create(7, historyRules: EnabledRules);
        var npc = world.Npcs[0];
        var fact = new Fact(new FactId(1), 5, WorldEventKind.Death, [npc.Id], npc.City, 0.9, npc.Id.Value.ToString());
        world.AddFact(fact);

        npc.Die(world.CurrentDate);
        world.AliveNpcIndex.OnDied(npc);
        var ctx = new TickContext(world, world.Rng, world.Scheduler, sink);
        FactToReportConversionScheduler.OnWitnessDied(npc.Id, world, ctx);

        Assert.True(world.Scheduler.HasDue(ctx.CurrentTick));
        var pending = world.Scheduler.PeekDue(ctx.CurrentTick);
        Assert.Contains(pending, e => e.SystemName == FactToReportConversionScheduler.SystemName);
    }

    [Fact]
    public void Conversion_creates_hop_zero_report_in_community()
    {
        var sink = new BufferingWorldEventSink();
        var (world, _) = ScenarioRunner.Create(8, historyRules: EnabledRules);
        var npc = world.Npcs[0];
        var city = EnsureCity(world, npc.City);
        var fact = new Fact(new FactId(1), 5, WorldEventKind.Death, [npc.Id], npc.City, 0.9, npc.Id.Value.ToString());
        world.AddFact(fact);
        var ctx = new TickContext(world, world.Rng, world.Scheduler, sink);

        FactToReportConversionScheduler.Convert(world, ctx, fact.Id, npc.City);

        Assert.Single(city.CanonSlots);
        Assert.Equal(fact.Id, city.CanonSlots[0].OriginFactId);
        Assert.Equal(npc.City, city.CanonSlots[0].CommunityId);
        Assert.Equal(0, city.CanonSlots[0].HopCount);
        Assert.Contains(sink.DrainAll(), e => e.Kind == WorldEventKind.ReportConverted);
    }

    [Fact]
    public void Two_witnesses_dying_same_tick_use_lowest_npc_id_for_scheduling_order()
    {
        var (world, _) = ScenarioRunner.Create(9, historyRules: EnabledRules);
        var witnessA = world.Npcs[0];
        var witnessB = world.Npcs[1];
        if (witnessB.Id.Value < witnessA.Id.Value)
            (witnessA, witnessB) = (witnessB, witnessA);

        var fact = new Fact(
            new FactId(1), 5, WorldEventKind.Marriage,
            [witnessA.Id, witnessB.Id], witnessA.City, 0.9, $"{witnessA.Id.Value}|{witnessB.Id.Value}");
        world.AddFact(fact);

        witnessA.Die(world.CurrentDate);
        world.AliveNpcIndex.OnDied(witnessA);
        var ctx = new TickContext(world, world.Rng, world.Scheduler, null);
        FactToReportConversionScheduler.OnWitnessDied(witnessA.Id, world, ctx);
        Assert.False(world.Scheduler.HasDue(ctx.CurrentTick));

        witnessB.Die(world.CurrentDate);
        world.AliveNpcIndex.OnDied(witnessB);
        FactToReportConversionScheduler.OnWitnessDied(witnessB.Id, world, ctx);
        Assert.True(world.Scheduler.HasDue(ctx.CurrentTick));
    }
}
