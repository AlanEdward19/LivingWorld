using LivingWorld.Domain.History;
using LivingWorld.Domain.History.Distortion;
using LivingWorld.Domain.Narrative;
using LivingWorld.Domain.Shared;
using LivingWorld.Infrastructure.EventLog;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.History.Books;
using LivingWorld.Simulation.History.Distortion;
using LivingWorld.Simulation.History.Queries;
using LivingWorld.Simulation.Narrative;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.History;

/// <summary>Plano history-narrative-close: S1 (fato vira relato), S2 (verdade não vaza, crônica ancorada).</summary>
public class HistoryNarrativeCloseTests
{
    private static HistoryRules Rules(int canon = 10) => HistoryRules.Create(
        enabled: true, skeletonSignificanceThreshold: 0.5, canonSizePerCommunity: canon,
        mediumFidelityByType: HistoryRules.Default.MediumFidelityByType,
        operatorProbability: HistoryRules.Default.OperatorProbability,
        importanceWeight: 1, transmissibilityWeight: 1, recencyWeight: 1).Value!;

    private static void Kill(WorldState world, LivingWorld.Domain.Population.Npc npc, TickContext ctx)
    {
        npc.Die(world.CurrentDate);
        world.AliveNpcIndex.OnDied(npc);
        FactToReportConversionScheduler.OnWitnessDied(npc.Id, world, ctx);
    }

    [Fact]
    public void Only_the_last_of_three_witnesses_dying_schedules_the_conversion_and_a_200_year_hop_is_deterministic()
    {
        string Run(ulong seed, out int convertedBefore)
        {
            var (world, _) = ScenarioRunner.Create(seed, historyRules: Rules());
            var w = world.Npcs.Take(3).OrderBy(n => n.Id.Value).ToList();
            var fact = new Fact(new FactId(1), 5, WorldEventKind.Death, w.Select(n => n.Id).ToList(), w[0].City, 0.9, "x");
            world.AddFact(fact);
            var ctx = new TickContext(world, world.Rng, world.Scheduler, new BufferingWorldEventSink());

            Kill(world, w[0], ctx);
            Kill(world, w[1], ctx);
            convertedBefore = world.Scheduler.HasDue(ctx.CurrentTick) ? 1 : 0;
            Kill(world, w[2], ctx);
            Assert.True(world.Scheduler.HasDue(ctx.CurrentTick));

            FactToReportConversionScheduler.Convert(world, ctx, fact.Id, w[0].City);
            var report = world.FindCity(w[0].City)!.CanonSlots.Single();
            var after200Years = report with { HopCount = 40 }; // ~5 anos por salto oral
            var belief = DistortionEngine.Materialize(after200Years, fact, world.HistoryRules, world.Rng, world);
            var first = DistortionEngine.Materialize(report with { HopCount = 1 }, fact, world.HistoryRules, world.Rng, world);
            Assert.True(belief.DistanceFromFact >= first.DistanceFromFact);
            return $"{belief.MoralizedNarrativeSeed}|{belief.DistortedMagnitude}|{belief.DistanceFromFact}";
        }

        string a = Run(11, out int beforeLast);
        string b = Run(11, out _);

        Assert.Equal(0, beforeLast);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Canon_over_capacity_evicts_the_lowest_weight_report()
    {
        var (world, _) = ScenarioRunner.Create(3, historyRules: Rules(canon: 2));
        var city = world.ActiveCities().First();
        ReportState R(long id, double weight) => new(new ReportId(id), new FactId(id), city.Id,
            TransmissionMediumType.OralTradition, 0, weight, 0, 0);

        CanonSlotManager.Admit(city, R(1, 0.9), world.HistoryRules, 0);
        CanonSlotManager.Admit(city, R(2, 0.1), world.HistoryRules, 0);
        CanonSlotManager.Admit(city, R(3, 0.8), world.HistoryRules, 0);

        Assert.Equal(2, city.CanonSlots.Count);
        Assert.DoesNotContain(city.CanonSlots, r => r.Id.Value == 2);
    }

    [Fact]
    public void Truth_skeleton_requires_engine_scope_and_hides_nothing_from_it()
    {
        var (world, _) = ScenarioRunner.Create(4, historyRules: Rules());
        world.AddFact(new Fact(world.NextFactIdAndAdvance(), 7, WorldEventKind.Death, [], null, 0.9, "p"));

        Assert.False(HistoryTruthGateway.Skeleton(world, null, 0).IsSuccess);
        Assert.Equal(HistoryTruthGateway.ForbiddenError, HistoryTruthGateway.Skeleton(world, null, 0).Error);
        Assert.Null(EngineScope.TryGrant("a", null));
        Assert.Null(EngineScope.TryGrant("a", "b"));
        var granted = EngineScope.TryGrant("tok", "tok");
        Assert.NotNull(granted);
        Assert.Single(HistoryTruthGateway.Skeleton(world, granted, 0).Value!);
        Assert.Empty(HistoryTruthGateway.Skeleton(world, granted, 8).Value!);
    }

    [Fact]
    public void Chronicle_discards_and_logs_orphan_claims_and_is_deterministic_for_same_window()
    {
        NarrativeDocument Publish(out ChronicleGenerationSystem system)
        {
            var (world, _) = ScenarioRunner.Create(5, initialPopulation: 0, historyRules: Rules());
            var city = world.ActiveCities().Single().Id;
            world.AddFact(new Fact(world.NextFactIdAndAdvance(), 10, WorldEventKind.Death, [], city, 0.9, "real"));
            system = new ChronicleGenerationSystem();
            var real = system.GenerateChronicle(world, city, 0, 100);
            var orphan = new NarrativeClaim("o rei Zorg tinha 900 soldados", []);
            var draft = new NarrativeDraft(city, 200, 300, [.. real.Claims, orphan]);
            var doc = system.Publish(draft);
            Assert.DoesNotContain(orphan, doc.Claims);
            Assert.Equal(orphan, Assert.Single(system.DiscardedClaims).Claim);
            Assert.DoesNotContain("Zorg", doc.Prose);
            return real;
        }

        var a = Publish(out _);
        var b = Publish(out _);

        Assert.Equal(a.Claims.Select(c => c.Text), b.Claims.Select(c => c.Text));
        Assert.Equal(a.Prose, b.Prose);
    }
}
