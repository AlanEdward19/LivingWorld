using System.Text.Json.Nodes;
using LivingWorld.Domain;
using LivingWorld.Simulation;
using LivingWorld.Simulation.Periods;

namespace LivingWorld.Tests.Extraordinary;

public sealed class ExtraordinaryIntegrationTests
{
    [Fact]
    public void Optional_state_contract_parses_appearance_need_senescence_and_manifestation_condition()
    {
        var root = JsonNode.Parse(ScenarioJson())!.AsObject();

        var result = ExtraordinaryScenarioLoader.Load(root.ToJsonString());

        var power = Assert.Single(result.Value!.Descriptors);
        Assert.Equal(
            (1.35, "pallor", "mist", "hunger", 9, 2L, 0.0, "world:is-night"),
            (power.Appearance!.ScaleMultiplier, power.Appearance.SkinTint, power.Appearance.MovementTrail,
                power.NeedSubstitution!.ReplacesNeed, power.NeedSubstitution.Resource.Id,
                power.NeedSubstitution.UnitsPerUse, power.SenescenceRateMultiplier,
                power.ManifestationCondition));
    }

    [Theory]
    [InlineData("\"ScaleMultiplier\": 1.35", "\"ScaleMultiplier\": 0", "Appearance.ScaleMultiplier")]
    [InlineData("\"UnitsPerUse\": 2", "\"UnitsPerUse\": 0", "NeedSubstitution.UnitsPerUse")]
    [InlineData("\"SenescenceRateMultiplier\": 0.0", "\"SenescenceRateMultiplier\": -1", "SenescenceRateMultiplier")]
    public void Invalid_optional_state_value_fails_at_the_boundary(
        string original, string replacement, string expectedField)
    {
        var result = ExtraordinaryScenarioLoader.Load(ScenarioJson().Replace(original, replacement));

        Assert.Equal((false, true), (result.IsSuccess, result.Error!.Contains(expectedField, StringComparison.Ordinal)));
    }

    [Fact]
    public void Period_definition_aggregates_extraordinary_scenario_data()
    {
        string json = File.ReadAllText(Path.Combine(FindRepoRoot(), "scenarios", "periods", "medieval.json"));
        var root = JsonNode.Parse(json)!.AsObject();
        root["Extraordinary"] = JsonNode.Parse(ScenarioJson())!["Extraordinary"]!.DeepClone();

        var result = PeriodDefinitionValidator.Validate(root.ToJsonString());

        Assert.Equal((true, "conditional-metabolism"),
            (result.Value!.Extraordinary.Enabled, Assert.Single(result.Value.Extraordinary.Descriptors).Id));
    }

    [Fact]
    public void Disabled_configuration_registers_no_system_enabled_registers_exactly_one()
    {
        var enabled = ScenarioRunner.DefaultSystems(extraordinary: ScenarioData());
        var disabled = ScenarioRunner.DefaultSystems(extraordinary: ExtraordinaryScenarioData.Disabled);

        Assert.Equal(
            (false, 1, ExtraordinaryStateSystem.SystemName),
            (disabled.Any(system => system.Name == ExtraordinaryStateSystem.SystemName),
                enabled.Count(system => system.Name == ExtraordinaryStateSystem.SystemName),
                Assert.Single(enabled, system => system.Name == ExtraordinaryStateSystem.SystemName).Name));
    }

    [Fact]
    public void Configuration_and_resolved_carrier_state_round_trip_through_snapshot()
    {
        var carrier = new ExtraordinaryCarrierState(
            new NpcId(7), ["conditional-metabolism"], true, "night-active",
            new ExtraordinaryAppearanceState(1.35, "pallor", "mist"),
            new NeedSubstitutionDescriptor("hunger", new ResourceType(9), 2), 0);
        var world = World(ScenarioData(), [carrier]);

        var restored = WorldSnapshot.Deserialize(WorldSnapshot.Serialize(world));
        var actual = Assert.Single(restored.ExtraordinaryCarriers);

        Assert.Equal(
            (true, "conditional-metabolism", 7L, true, "night-active", 1.35, "pallor", "mist",
                "hunger", 9, 2L, 0.0),
            (restored.Extraordinary.Enabled, Assert.Single(restored.Extraordinary.Descriptors).Id,
                actual.CarrierId.Value, actual.IsManifested, actual.ManifestationState,
                actual.Appearance.ScaleMultiplier, actual.Appearance.SkinTint, actual.Appearance.MovementTrail,
                actual.NeedSubstitution!.ReplacesNeed, actual.NeedSubstitution.Resource.Id,
                actual.NeedSubstitution.UnitsPerUse, actual.SenescenceRateMultiplier));
    }

    [Fact]
    public void Extraordinary_configuration_enters_canonical_hash_and_remains_deterministic()
    {
        var first = World(ScenarioData());
        var second = World(ScenarioData());
        var disabled = World(ExtraordinaryScenarioData.Disabled);
        new WorldClock(ScenarioRunner.DefaultSystems(extraordinary: ScenarioData())).Run(first, 48);
        new WorldClock(ScenarioRunner.DefaultSystems(extraordinary: ScenarioData())).Run(second, 48);
        new WorldClock(ScenarioRunner.DefaultSystems(extraordinary: ExtraordinaryScenarioData.Disabled)).Run(disabled, 48);

        Assert.Equal(
            (WorldSnapshot.CanonicalHash(first), true),
            (WorldSnapshot.CanonicalHash(second),
                WorldSnapshot.CanonicalHash(first) != WorldSnapshot.CanonicalHash(disabled)));
    }

    [Fact]
    public void Zero_senescence_carrier_still_dies_from_sustained_starvation()
    {
        var carrier = new ExtraordinaryCarrierState(
            new NpcId(1), ["conditional-metabolism"], false, "inactive",
            new ExtraordinaryAppearanceState(1, "", ""), null, 0);
        var world = World(ScenarioData(), [carrier]);
        var npc = NewNpc(carrier.CarrierId, world);
        npc.SetHunger(0, world.CurrentDate.TotalHours);
        world.AddNpc(npc);
        var sink = new RecordingSink();

        new WorldClock([new NeedsDecaySystem()], sink: sink).Run(world, 101);

        Assert.False(npc.IsAlive);
        Assert.Contains(sink.Events, evt =>
            evt.Kind == WorldEventKind.Starvation && evt.Payload == npc.Id.Value.ToString());
    }

    private static WorldState World(
        ExtraordinaryScenarioData extraordinary, IReadOnlyList<ExtraordinaryCarrierState>? carriers = null) =>
        new(
            ScenarioRunner.DefaultCalendar, 42, ScenarioRunner.DefaultMap(42),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules,
            extraordinary: extraordinary, extraordinaryCarriers: carriers);

    private static ExtraordinaryScenarioData ScenarioData() =>
        ExtraordinaryScenarioLoader.Load(ScenarioJson()).Value!;

    private static Npc NewNpc(NpcId id, WorldState world) => new(
        id, "zero-senescence-carrier", Sex.Male, WorldDate.Epoch(ScenarioRunner.DefaultCalendar),
        ScenarioRunner.DefaultCulture, new CellCoord(0, 0), motherId: null, fatherId: null,
        household: null, health: 100,
        personality: Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!,
        profession: ProfessionType.None, currentLocation: new CellCoord(0, 0));

    private sealed class RecordingSink : IWorldEventSink
    {
        public List<WorldEvent> Events { get; } = [];
        public void Record(WorldEvent evt) => Events.Add(evt);
    }

    private static string ScenarioJson() => """
    {
      "Extraordinary": {
        "Enabled": true,
        "Descriptors": [{
          "Id": "conditional-metabolism",
          "Source": "scenario-source",
          "Effects": ["movement:speed"],
          "Mode": "Conditional",
          "Reliability": "Guaranteed",
          "Appearance": { "ScaleMultiplier": 1.35, "SkinTint": "pallor", "MovementTrail": "mist" },
          "NeedSubstitution": { "ReplacesNeed": "hunger", "ResourceId": 9, "UnitsPerUse": 2 },
          "SenescenceRateMultiplier": 0.0,
          "ManifestationCondition": "world:is-night"
        }]
      }
    }
    """;

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "LivingWorld.sln")))
            dir = Directory.GetParent(dir)?.FullName;
        return dir ?? throw new InvalidOperationException("LivingWorld.sln não encontrado");
    }
}
