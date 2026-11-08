using System.Text.Json;
using LivingWorld.Domain.Extraordinary;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Extraordinary.Culture;
using LivingWorld.Simulation.Extraordinary.Engine;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Extraordinary.Engine;

/// <summary>Issue #64, critérios 2 e 6: poder inédito só por dados e base sem poder intacta.</summary>
public sealed class DataOnlyPowerTests
{
    // Poder que nenhum C# conhece: seletor de área + sede alheia + custo de estoque doméstico.
    private const string ScenarioJson = """
    {
      "Extraordinary": {
        "Enabled": true,
        "Descriptors": [{
          "Id": "fonte-seca",
          "Source": "dom-escalar",
          "Effects": ["area:radius:3", "npc.thirst:-20"],
          "Mode": "Active",
          "Costs": ["household.resource.9:2", "carrier.sleep:5"],
          "Reliability": "Guaranteed"
        }]
      }
    }
    """;

    [Fact]
    public void Novel_power_made_only_of_existing_tokens_is_granted_invoked_and_recorded()
    {
        var loaded = ExtraordinaryScenarioLoader.Load(ScenarioJson);
        Assert.True(loaded.IsSuccess, loaded.Error);
        var (world, carrier, near, far, home) = WorldFrom(loaded.Value!);
        near.SetThirst(60, 0);
        far.SetThirst(60, 0);
        carrier.SetThirst(60, 0);
        int sleepBefore = carrier.SleepAt(0);
        var sink = new RecordingSink();

        var result = ExtraordinaryInvocationEngine.Invoke(
            world, new TickContext(world, world.Rng, world.Scheduler, sink),
            new ExtraordinaryInvocation(1, carrier.Id, "fonte-seca", carrier.Id));

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal((40, 40, 60, 3L, Math.Max(0, sleepBefore - 5)),
            (near.ThirstAt(0), carrier.ThirstAt(0), far.ThirstAt(0),
                home.Stock[new ResourceType(9)], carrier.SleepAt(0)));
        Assert.Equal(2, sink.Events.Count(evt => evt.Kind == WorldEventKind.ExtraordinaryCostPaid));
        Assert.True(sink.Events.Count(evt => evt.Kind == WorldEventKind.ExtraordinaryEffectApplied) >= 2);
    }

    [Fact]
    public void Default_scenario_hash_with_the_power_system_off_matches_the_committed_golden()
    {
        // Critério 6: sem bloco Extraordinary (Enabled = false implícito) o hash canônico não muda.
        using var golden = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(FindRepoRoot(), "tests", "golden", "world-hashes.json")));
        var entry = golden.RootElement.EnumerateArray()
            .Single(item => item.GetProperty("Seed").GetUInt64() == 42 && item.GetProperty("Ticks").GetInt64() == 100);

        var (hash, _) = ScenarioRunner.RunAndHash(42, 100);

        Assert.Equal(entry.GetProperty("CanonicalHash").GetString(), hash);
    }

    private static (WorldState World, Npc Carrier, Npc Near, Npc Far, Household Home) WorldFrom(
        ExtraordinaryScenarioData data)
    {
        var state = new ExtraordinaryCarrierState(
            new NpcId(1), ["fonte-seca"], true, "active",
            new ExtraordinaryAppearanceState(1, "", ""), null, 1);
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, 42, ScenarioRunner.DefaultMap(42),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules,
            extraordinary: data, extraordinaryCarriers: [state]);
        var carrier = Npc(new NpcId(1));
        var near = Npc(new NpcId(2));
        near.MoveTo(new CellCoord(1, 1), 0);
        var far = Npc(new NpcId(3));
        far.MoveTo(new CellCoord(9, 9), 0);
        var home = new Household(new HouseholdId(1), new CellCoord(0, 0), carrier.Id, [carrier.Id],
            new Dictionary<ResourceType, long> { [new ResourceType(9)] = 5 });
        world.AddNpc(carrier);
        world.AddNpc(near);
        world.AddNpc(far);
        world.AddHousehold(home);
        return (world, carrier, near, far, home);
    }

    private static Npc Npc(NpcId id) => new(
        id, "npc" + id.Value, Sex.Male, WorldDate.Epoch(ScenarioRunner.DefaultCalendar),
        ScenarioRunner.DefaultCulture, new CellCoord(0, 0), motherId: null, fatherId: null,
        household: id == new NpcId(1) ? new HouseholdId(1) : null, health: 50,
        personality: Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!,
        profession: ProfessionType.None, currentLocation: new CellCoord(0, 0));

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "LivingWorld.sln")))
            dir = Directory.GetParent(dir)?.FullName;
        return dir ?? throw new InvalidOperationException("LivingWorld.sln não encontrado");
    }

    private sealed class RecordingSink : IWorldEventSink
    {
        public List<WorldEvent> Events { get; } = [];
        public void Record(WorldEvent evt) => Events.Add(evt);
    }
}
