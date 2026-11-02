using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.History.Distortion;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Decision;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.History.Distortion;
using LivingWorld.Simulation.History.Queries;
using LivingWorld.Simulation.Population.Lifecycle;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.History;

public class RuntimeHistoryTests
{
    private static (WorldState World, WorldClock Clock) Create(bool enabled = true, double threshold = 0.4)
    {
        var (world, clock) = ScenarioRunner.Create(91, initialPopulation: 4,
            historyRules: enabled ? HistoryRules.Default with { SkeletonSignificanceThreshold = threshold } : HistoryRules.Disabled);
        return (world, clock);
    }

    [Fact]
    public void Significant_event_records_one_intact_fact_before_the_sink_observes_it()
    {
        var (world, _) = Create();
        var npc = world.Npcs[0];
        string payload = npc.Id.Value.ToString();
        var sink = new InspectingSink(evt =>
        {
            var fact = Assert.Single(world.Facts);
            Assert.Equal((evt.Tick, evt.Kind, evt.Payload, npc.City, npc.Id),
                (fact.Tick, fact.Kind, fact.Payload, fact.Location!.Value, Assert.Single(fact.Participants)));
            Assert.True(fact.Significance >= world.HistoryRules.SkeletonSignificanceThreshold);
            Assert.Equal(("runtime-test", 7L), (evt.SourceSystem, evt.CauseEventId));
        });
        var ctx = new TickContext(world, world.Rng, world.Scheduler, sink);
        ctx.LogEvent(WorldEventKind.Death, payload, "runtime-test", 7);
        Assert.Equal(1, sink.Count);
    }

    [Theory]
    [InlineData(false, WorldEventKind.Death)]
    [InlineData(true, WorldEventKind.Trade)]
    public void Disabled_history_or_insignificant_event_does_not_record_facts(bool enabled, WorldEventKind kind)
    {
        var (world, _) = Create(enabled);
        new TickContext(world, world.Rng, world.Scheduler).LogEvent(kind, "1");
        Assert.Empty(world.Facts);
    }

    [Theory]
    [InlineData(WorldEventKind.FactRecorded)]
    [InlineData(WorldEventKind.ReportConverted)]
    [InlineData(WorldEventKind.CompensatingCorrection)]
    [InlineData(WorldEventKind.PossessionResisted)]
    [InlineData(WorldEventKind.NpcInstantiated)]
    public void Metadata_and_explicit_fact_producers_do_not_record_a_second_fact(WorldEventKind kind)
    {
        var (world, _) = Create(threshold: 0);
        new TickContext(world, world.Rng, world.Scheduler).LogEvent(kind, "1");
        Assert.Empty(world.Facts);
    }

    [Fact]
    public void Starvation_creates_oral_history_that_changes_the_survivors_buy_utility_without_a_sink()
    {
        var (world, _) = Create();
        var victim = world.Npcs[0];
        var listener = world.Npcs[1];
        var runtime = new WorldClock([new StarvationSystem(victim), new FactToReportConversionScheduler()]);
        runtime.Tick(world);
        var fact = Assert.Single(world.Facts, f => f.Kind == WorldEventKind.Starvation);
        Assert.Equal(victim.Id.Value.ToString(), fact.Payload);
        var report = Assert.Single(world.Reports, r => r.OriginFactId == fact.Id);
        Assert.Contains(world.FindCity(listener.City)!.CanonSlots, r => r.Id == report.Id);
        var ctx = DecisionContextBuilder.Build(world, listener, world.CurrentDate.TotalHours);
        Assert.Contains("hunger", ctx.RelevantBeliefs);
        var onlyBuy = new ActionEligibility(uint.MaxValue ^ (1u << (int)ActionType.Buy), false, 1);
        double Score(DecisionContext context) => BehaviorDecisionSystem.SelectByUtility(
            context, world.NeedsRules, world.EconomyRules, null, eligibility: onlyBuy).Trace.WinningUtility;
        var withMoney = ctx with
        {
            WalletAmount = 100,
            Household = ctx.Household! with { Stock = new Dictionary<ResourceType, long>() },
        };
        Assert.Equal(40, Score(withMoney) - Score(withMoney with { RelevantBeliefs = [] }), precision: 8);

        // Sem com que pagar, ou com a despensa já acima da meta, a crença não tira ninguém do trabalho.
        var broke = ctx with { WalletAmount = 0 };
        Assert.Equal(0, Score(broke) - Score(broke with { RelevantBeliefs = [] }), precision: 8);
        var stocked = withMoney with
        {
            Household = ctx.Household! with
            {
                Stock = new Dictionary<ResourceType, long>
                {
                    [new ResourceType(world.EconomyRules.FoodResourceId)] = 10_000,
                    [new ResourceType(world.EconomyRules.WaterResourceId)] = 10_000,
                },
            },
        };
        Assert.Equal(0, Score(stocked) - Score(stocked with { RelevantBeliefs = [] }), precision: 8);
    }

    [Fact]
    public void Replacing_a_full_canon_refreshes_beliefs_without_leaking_into_an_identical_world()
    {
        var (world, _) = Create();
        var npc = world.Npcs[0];
        var city = world.FindCity(npc.City)!;
        var ctx = new TickContext(world, world.Rng, world.Scheduler);
        ctx.LogEvent(WorldEventKind.Death, $"{npc.Id.Value}|food");
        ctx.LogEvent(WorldEventKind.Death, $"{npc.Id.Value}|hunger");
        var facts = world.Facts.ToArray();
        ReportState Report(int index) => new(new ReportId(index + 1), facts[index].Id, city.Id,
            TransmissionMediumType.OralTradition, 0, 1, 0, 0);
        city.SetCanonSlots([Report(0)]);
        Assert.Equal(new[] { $"{npc.Id.Value}|food" }, NpcBeliefQuery.BeliefsOf(world, npc.Id));
        city.SetCanonSlots([Report(1)]);
        string beforeQuery = WorldSnapshot.CanonicalHash(world);
        Assert.Equal(new[] { $"{npc.Id.Value}|hunger" }, NpcBeliefQuery.BeliefsOf(world, npc.Id));
        Assert.Equal(beforeQuery, WorldSnapshot.CanonicalHash(world));
        var restored = WorldSnapshot.Deserialize(WorldSnapshot.Serialize(world));
        Assert.Equal(new[] { $"{npc.Id.Value}|hunger" }, NpcBeliefQuery.BeliefsOf(restored, npc.Id));
        var (other, _) = Create();
        Assert.Empty(NpcBeliefQuery.BeliefsOf(other, other.Npcs[0].Id));
    }

    private sealed class StarvationSystem(Npc victim) : ISimulationSystem
    {
        public string Name => "starvation-test";
        public TickFrequency Frequency => TickFrequency.Hourly;
        public void Tick(WorldState world, TickContext ctx) => NpcDeath.Apply(world, ctx, victim, WorldEventKind.Starvation);
    }

    private sealed class InspectingSink(Action<WorldEvent> inspect) : IWorldEventSink
    {
        public int Count { get; private set; }
        public void Record(WorldEvent evt) { inspect(evt); Count++; }
    }
}
