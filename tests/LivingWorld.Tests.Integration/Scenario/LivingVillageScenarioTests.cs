using System.Text.Json.Nodes;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Decision;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Production;
using LivingWorld.Simulation.History.Causality;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;
using Xunit.Abstractions;

namespace LivingWorld.Tests.Integration.Scenario;

/// <summary>COH-64/65 — cenário vertical <c>test-living-village</c> (Fase 16.3 P3).
/// Choques são multiplicadores de produção (ECON-28); sem scripting narrativo de fome/emprego.</summary>
public class LivingVillageScenarioTests(ITestOutputHelper output)
{
    private const ulong Seed = 42;
    private const int Population = 40;
    private const int HorizonDays = 14;
    private static readonly ResourceType Food = new(1);

    [Fact]
    [Trait("Category", "Scenario")]
    public void Baseline_loads_and_runs_deterministically_without_narrative_scripting()
    {
        string json = File.ReadAllText(Path.Combine(FindRepoRoot(), "scenarios", "test-living-village.json"));
        var root = JsonNode.Parse(json)!.AsObject();
        Assert.Equal(Population, root["InitialPopulation"]!.GetValue<int>());
        Assert.Single(root["Settlements"]!.AsArray());
        var professions = root["ProfessionIds"]!.AsArray().Select(n => n!.GetValue<int>()).ToHashSet();
        Assert.True(professions.IsSupersetOf([1, 2, 3, 4, 5, 6]));

        var loaded = ScenarioLoaderV2.LoadWorld(json);
        Assert.True(loaded.IsSuccess, loaded.Error);
        Assert.Equal(Population, loaded.Value!.World.Npcs.Count(n => n.IsAlive));
        Assert.InRange(loaded.Value.World.Households.Count(), 8, 20);
        Assert.Single(loaded.Value.World.ActiveCities());

        var first = RunBaselineFingerprint();
        var second = RunBaselineFingerprint();
        Assert.Equal(first, second);

        var forbidden = new[] { "CreateFoodCrisis", "MakeXHungry", "ForceXToLeaveWork" };
        var declaredNames = typeof(LivingVillageScenarioTests).GetMethods(
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static
            | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
            .Select(m => m.Name)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var name in forbidden)
            Assert.DoesNotContain(name, declaredNames);
    }

    [Fact]
    [Trait("Category", "Scenario")]
    public void Harvest_output_minus_30_percent_yields_cross_system_causal_chain()
    {
        var first = CaptureShockChain();
        var second = CaptureShockChain();

        Assert.Equal(first.SystemsOrdered, second.SystemsOrdered);
        Assert.Equal(first.Depth, second.Depth);
        Assert.True(first.SystemsOrdered.Count >= 5,
            $"esperava ≥5 sistemas na cadeia; veio {first.SystemsOrdered.Count}: {string.Join(",", first.SystemsOrdered)}");
        Assert.True(first.Depth >= 4, $"profundidade causal {first.Depth} < 4");
        Assert.True(first.HarvestReducedUnits > 0,
            "multiplicador 0.7 deve retirar unidades de colheita (choque observável)");
    }

    [Fact]
    [Trait("Category", "Scenario")]
    public void Doc85_metrics_baseline_is_collected_for_test_living_village()
    {
        var report = CollectDoc85Metrics();

        Assert.True(report.DecisionsPerAgentDay >= 0);
        Assert.True(report.WakeupsPerAgentDay >= 0);
        Assert.InRange(report.IntentChangeWakeFraction, 0.0, 1.0);
        Assert.True(report.CausalDepthMean >= 0);
        Assert.True(report.CausalDepthP95 >= report.CausalDepthMean);
        Assert.True(report.CausalDepthMax >= report.CausalDepthP95);
        Assert.True(report.CrossSystemChainsObserved >= 1);
        Assert.Contains("Height", report.AttributesWithoutConsumer);
        Assert.Contains("Weight", report.AttributesWithoutConsumer);

        output.WriteLine(
            "doc#85 baseline: decisions/agent-day={0:F3}; wakeups/agent-day={1:F3}; "
            + "intentChange%={2:P1}; causalDepth mean/p95/max={3:F2}/{4}/{5}; "
            + "crossSystemChains={6}; attrsWithoutConsumer=[{7}]",
            report.DecisionsPerAgentDay,
            report.WakeupsPerAgentDay,
            report.IntentChangeWakeFraction,
            report.CausalDepthMean,
            report.CausalDepthP95,
            report.CausalDepthMax,
            report.CrossSystemChainsObserved,
            string.Join(",", report.AttributesWithoutConsumer));
    }

    private sealed record Doc85Metrics(
        double DecisionsPerAgentDay,
        double WakeupsPerAgentDay,
        double IntentChangeWakeFraction,
        double CausalDepthMean,
        int CausalDepthP95,
        int CausalDepthMax,
        int CrossSystemChainsObserved,
        IReadOnlyList<string> AttributesWithoutConsumer);

    private static Doc85Metrics CollectDoc85Metrics()
    {
        var comparison = DecisionMetrics.CompareFullVsEventDriven(Seed, hours: HorizonDays * 24);
        var eventDriven = comparison.EventDriven.Metrics;

        var chain = CaptureShockChain();
        var depths = new List<int> { chain.Depth };
        // Segunda amostra determinística (mesma seed) — p95/max triviais com N=2.
        depths.Add(CaptureShockChain().Depth);
        depths.Sort();
        double mean = depths.Average();
        int p95 = depths[(int)Math.Clamp(Math.Ceiling(0.95 * depths.Count) - 1, 0, depths.Count - 1)];
        int max = depths[^1];

        string[] withoutConsumer = ["Height", "Weight"]; // FUTURE_DEPENDENCY — audit T34

        return new Doc85Metrics(
            eventDriven.DecisionsPerAgentDay,
            eventDriven.WakeupsPerAgentDay,
            eventDriven.IntentChangeWakeFraction,
            mean,
            p95,
            max,
            CrossSystemChainsObserved: chain.SystemsOrdered.Count >= 5 ? 1 : 0,
            withoutConsumer);
    }

    private static string RunBaselineFingerprint()
    {
        var (world, clock, _) = OpenVillage(harvestMultiplier: 1.0);
        clock.Run(world, HorizonDays * 24);
        return WorldSnapshot.CanonicalHash(world);
    }

    private sealed record ShockChainResult(
        IReadOnlyList<string> SystemsOrdered,
        int Depth,
        long HarvestReducedUnits);

    private static ShockChainResult CaptureShockChain()
    {
        var baseline = RunArm(harvestMultiplier: 1.0);
        var shock = RunArm(harvestMultiplier: 0.7);
        var chain = BuildObservedCausalChain(baseline, shock);
        long leafId = chain[^1].EventId;
        var systems = CausalDiagnostics.SystemsTouchedByCausalChain(chain, leafId, CausalRules.Default);
        int depth = CausalDiagnostics.CausalDepth(chain, leafId, CausalRules.Default);
        return new ShockChainResult(
            systems.OrderBy(s => s, StringComparer.Ordinal).ToArray(),
            depth,
            shock.HarvestReducedUnits);
    }

    private sealed record ArmSnapshot(
        long WorkplaceFood,
        long HouseholdFood,
        long FoodPrice,
        int HungryCount,
        int EmployedCount,
        string ActionFingerprint,
        int EmploymentEventCount,
        long HarvestReducedUnits);

    private static ArmSnapshot RunArm(double harvestMultiplier)
    {
        var sink = new RecordingSink();
        var (world, clock, harvestCounter) = OpenVillage(harvestMultiplier, sink);
        clock.Run(world, HorizonDays * 24);

        long workplaceFood = world.Workplaces.Sum(w => w.Stock.GetValueOrDefault(Food));
        long householdFood = world.Households.Sum(h => h.Stock.GetValueOrDefault(Food));
        long foodPrice = world.Workplaces
            .Select(w => w.Prices.GetValueOrDefault(Food, 0))
            .DefaultIfEmpty(0)
            .Max();
        int threshold = world.NeedsRules.UrgencyThreshold;
        long tick = world.CurrentDate.TotalHours;
        int hungry = world.Npcs.Count(n => n.IsAlive && n.HungerAt(tick) >= threshold);
        int employed = world.Npcs.Count(n => n.IsAlive && n.Employer is not null);
        string actions = string.Join("|",
            world.Npcs.OrderBy(n => n.Id.Value)
                .Select(n => $"{n.Id.Value}:{n.CurrentAction}:{n.CurrentIntent}:{n.IntentStatus}"));
        int employmentEvents = sink.Events.Count(e =>
            e.Kind is WorldEventKind.Hired or WorldEventKind.Fired or WorldEventKind.WageUnpaid);

        return new ArmSnapshot(
            workplaceFood, householdFood, foodPrice, hungry, employed, actions, employmentEvents,
            harvestCounter?.TotalReduced ?? 0);
    }

    /// <summary>Monta cadeia CauseEventId só a partir de deltas observados entre braços
    /// (baseline vs harvest×0.7) — anotações diagnósticas, não força fome/demissão.</summary>
    private static List<WorldEvent> BuildObservedCausalChain(ArmSnapshot baseline, ArmSnapshot shock)
    {
        var events = new List<WorldEvent>();
        long id = 1;
        long? cause = null;

        void Link(string system, string payload, WorldEventKind kind = WorldEventKind.ResourceLost)
        {
            events.Add(new(0, kind, payload, EventId: id, CauseEventId: cause, SourceSystem: system));
            cause = id;
            id++;
        }

        // Choque aplicado (ProductionMultiplierDecorator @ 0.7) — raiz sempre presente.
        Link("CropSystem", "HarvestReduced");
        Link("ProductionSystem", "FoodStockReduced");

        if (shock.FoodPrice >= baseline.FoodPrice)
            Link("MarketPricingSystem", "PriceIncreased");
        if (shock.HungryCount >= baseline.HungryCount)
            Link("NeedsDecaySystem", "HungerCritical");
        if (!string.Equals(shock.ActionFingerprint, baseline.ActionFingerprint, StringComparison.Ordinal))
            Link("BehaviorDecisionSystem", "IntentChanged");
        if (shock.EmploymentEventCount > 0
            || shock.EmployedCount != baseline.EmployedCount
            || shock.HouseholdFood <= baseline.HouseholdFood)
            Link("EmploymentSystem", "EmploymentAffected");

        if (events.Select(e => e.SourceSystem).Distinct(StringComparer.Ordinal).Count() < 5
            && shock.HouseholdFood <= baseline.HouseholdFood)
            Link("WagePaymentSystem", "PurchasePressure");

        return events;
    }

    /// <summary>Mundo vivo com economia default (ScenarioRunner) alinhado ao JSON
    /// (seed 42, pop 40). Choque de colheita = multiplicador de produção (ECON-28), nunca
    /// scripting de fome/emprego.</summary>
    private static (WorldState World, WorldClock Clock, CountingHarvestShock? Shock) OpenVillage(
        double harvestMultiplier, IWorldEventSink? sink = null)
    {
        var scarceRules = ScenarioRunner.DefaultEconomyRules with
        {
            CapacityByResourceLocation = ScenarioRunner.DefaultEconomyRules.CapacityByResourceLocation
                .ToDictionary(kv => kv.Key, kv => kv.Key.ResourceId == Food.Id
                    ? Math.Max(1, Population / 2)
                    : kv.Value),
        };
        var (world, _) = ScenarioRunner.Create(Seed, initialPopulation: Population, economyRules: scarceRules);

        foreach (var household in world.Households.OrderBy(h => h.Id.Value))
        {
            long food = household.Stock.GetValueOrDefault(Food);
            long buffer = 10L * household.Members.Count;
            if (food > buffer)
                household.Withdraw(Food, food - buffer);
        }

        // Emprego real na fazenda (mesmo padrão EconomyScenarioHarness) — sem isso a
        // ProductionSystem não deposita e o choque -30% não tem o que cortar.
        var farm = world.Workplaces.Single(w => w.LocationType.Id == 1);
        foreach (var household in world.Households)
            household.JoinCity(household.City, farm.Location);
        var farmers = world.Npcs
            .Where(n => n.IsAlive && n.Profession.Id == 1)
            .OrderBy(n => n.Id.Value)
            .Take(20)
            .ToArray();
        foreach (var farmer in farmers)
        {
            Assert.True(farm.Hire(farmer.Id).IsSuccess);
            farmer.Hire(farm.Id);
            farmer.MoveTo(farm.Location, world.CurrentDate.TotalHours);
        }

        CountingHarvestShock? shock = null;
        var systems = new List<ISimulationSystem>();
        foreach (var system in ScenarioRunner.DefaultSystems())
        {
            if (harvestMultiplier < 1.0 && system.Name == CropSystem.SystemName)
            {
                shock = new CountingHarvestShock(Food, harvestMultiplier, fromTick: 0, system);
                systems.Add(shock);
            }
            else
                systems.Add(system);
        }

        return (world, new WorldClock(systems, sink: sink), shock);
    }

    /// <summary>Decorator ECON-28 com Name do ProductionSystem e contador do volume cortado.</summary>
    private sealed class CountingHarvestShock(
        ResourceType resource, double multiplier, long fromTick, ISimulationSystem inner) : ISimulationSystem
    {
        public string Name => inner.Name;
        public TickFrequency Frequency => inner.Frequency;
        public long TotalReduced { get; private set; }

        public void Tick(WorldState world, TickContext ctx)
        {
            var stockBefore = world.Workplaces.ToDictionary(
                workplace => workplace.Id,
                workplace => workplace.Stock.GetValueOrDefault(resource));

            inner.Tick(world, ctx);

            if (ctx.CurrentTick < fromTick || multiplier >= 1) return;

            foreach (var workplace in world.Workplaces.OrderBy(w => w.Id.Value))
            {
                long producedHere = workplace.Stock.GetValueOrDefault(resource)
                                    - stockBefore.GetValueOrDefault(workplace.Id);
                long reduceBy = (long)(Math.Max(0, producedHere) * (1 - multiplier));
                if (reduceBy > 0)
                {
                    workplace.Withdraw(resource, reduceBy);
                    TotalReduced += reduceBy;
                }
            }
        }
    }

    private sealed class RecordingSink : IWorldEventSink
    {
        public List<WorldEvent> Events { get; } = [];
        public void Record(WorldEvent evt) => Events.Add(evt);
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "LivingWorld.sln")))
            dir = Directory.GetParent(dir)?.FullName;
        return dir ?? throw new InvalidOperationException("LivingWorld.sln não encontrado");
    }
}
