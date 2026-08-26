using System.Text.Json.Nodes;
using LivingWorld.Domain;
using LivingWorld.Simulation;
using LivingWorld.Simulation.Economy;
using LivingWorld.Tests.Economy;

namespace LivingWorld.Tests.Scenario;

/// <summary>COH-64/65 — cenário vertical <c>test-living-village</c> (Fase 16.3 P3).
/// Choques são multiplicadores de produção (ECON-28); sem scripting narrativo de fome/emprego.</summary>
public class LivingVillageScenarioTests
{
    private const ulong Seed = 42;
    private const int Population = 40;
    private const int HorizonDays = 7;
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

    private static string RunBaselineFingerprint()
    {
        var (world, clock) = OpenVillage(harvestMultiplier: 1.0);
        clock.Run(world, HorizonDays * 24);
        return WorldSnapshot.CanonicalHash(world);
    }

    /// <summary>Mundo vivo com economia default (ScenarioRunner) alinhado ao JSON
    /// (seed 42, pop 40). Choque de colheita = multiplicador de produção (ECON-28), nunca
    /// scripting de fome/emprego.</summary>
    private static (WorldState World, WorldClock Clock) OpenVillage(
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

        var systems = new List<ISimulationSystem>();
        foreach (var system in ScenarioRunner.DefaultSystems())
        {
            if (system.Name == CropSystem.SystemName && harvestMultiplier < 1.0)
                systems.Add(new ProductionMultiplierDecorator(Food, harvestMultiplier, fromTick: 0, system));
            else
                systems.Add(system);
        }

        return (world, new WorldClock(systems, sink: sink));
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "LivingWorld.sln")))
            dir = Directory.GetParent(dir)?.FullName;
        return dir ?? throw new InvalidOperationException("LivingWorld.sln não encontrado");
    }
}
