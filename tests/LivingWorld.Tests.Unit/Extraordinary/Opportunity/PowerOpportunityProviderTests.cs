using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Extraordinary;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Extraordinary.Engine;
using LivingWorld.Simulation.Extraordinary.Opportunity;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Extraordinary.Opportunity;

/// <summary>Fase 16.3, T20 (COH-31/32): filtro Mode + estágio e cobertura do registry.</summary>
public sealed class PowerOpportunityProviderTests
{
    [Fact]
    public void Npc_without_carrier_returns_empty()
    {
        var (world, npc) = WorldWithNpc(carrier: null, descriptor: null);

        var opps = PowerOpportunityProvider.ApplicableTo(world, npc, tick: 0);

        Assert.Empty(opps);
    }

    [Fact]
    public void Applicable_Active_power_at_stage_zero_appears()
    {
        var descriptor = Descriptor(
            "teleport-power",
            mode: "Active",
            effects: ["npc.teleport:elsewhere"]);
        var (world, npc) = WorldWithNpc(
            carrier: Carrier(npcId: 1, powerIds: [descriptor.Id], stageIndex: 0, manifested: true),
            descriptor: descriptor);

        var opps = PowerOpportunityProvider.ApplicableTo(world, npc, tick: 0);

        Assert.Single(opps);
        Assert.Equal("teleport-power", opps[0].PowerId);
        Assert.Equal("npc.teleport:elsewhere", opps[0].MechanicToken);
    }

    [Fact]
    public void Passive_mode_is_excluded_from_authored_decision()
    {
        var descriptor = Descriptor(
            "passive-power",
            mode: "Passive",
            effects: ["attribute.strength:2"]);
        var (world, npc) = WorldWithNpc(
            carrier: Carrier(npcId: 1, powerIds: [descriptor.Id], stageIndex: 0, manifested: true),
            descriptor: descriptor);

        Assert.Empty(PowerOpportunityProvider.ApplicableTo(world, npc, tick: 0));
    }

    [Fact]
    public void Stage_locked_effects_do_not_appear_at_stage_zero()
    {
        var descriptor = Descriptor(
            "staged-power",
            mode: "Active",
            effects: [],
            stages:
            [
                new PowerEvolutionStage(
                    AgeThreshold: null,
                    UseCountThreshold: 5,
                    EffectTokens: ["npc.teleport:elsewhere"]),
            ]);
        var (world, npc) = WorldWithNpc(
            carrier: Carrier(npcId: 1, powerIds: [descriptor.Id], stageIndex: 0, manifested: true),
            descriptor: descriptor);

        Assert.Empty(PowerOpportunityProvider.ApplicableTo(world, npc, tick: 0));
    }

    [Fact]
    public void Unlocked_stage_effects_appear_when_CurrentStageIndex_matches()
    {
        var descriptor = Descriptor(
            "staged-power",
            mode: "Active",
            effects: ["attribute.strength:1"],
            stages:
            [
                new PowerEvolutionStage(
                    AgeThreshold: null,
                    UseCountThreshold: 5,
                    EffectTokens: ["npc.teleport:elsewhere"]),
            ]);
        var (world, npc) = WorldWithNpc(
            carrier: Carrier(npcId: 1, powerIds: [descriptor.Id], stageIndex: 1, manifested: true),
            descriptor: descriptor);

        var opps = PowerOpportunityProvider.ApplicableTo(world, npc, tick: 0);

        Assert.Single(opps);
        Assert.Equal("npc.teleport:elsewhere", opps[0].MechanicToken);
    }

    [Fact]
    public void Conditional_unmanifested_is_excluded()
    {
        var descriptor = Descriptor(
            "cond-power",
            mode: "Conditional",
            effects: ["gravity.self:0.5"]);
        var (world, npc) = WorldWithNpc(
            carrier: Carrier(npcId: 1, powerIds: [descriptor.Id], stageIndex: 0, manifested: false),
            descriptor: descriptor);

        Assert.Empty(PowerOpportunityProvider.ApplicableTo(world, npc, tick: 0));
    }

    [Fact]
    public void Every_default_registry_mechanic_is_reachable_via_ApplicableTo()
    {
        var registry = ExtraordinaryMechanicRegistry.Default;
        Assert.True(registry.All.Count >= 27, $"expected ≥27 mechanics, got {registry.All.Count}");

        foreach (var mechanic in registry.All)
        {
            string token = SampleTokenFor(mechanic.Prefix);
            var descriptor = Descriptor(
                $"cov-{mechanic.Prefix.Replace('.', '-').Replace(':', '-')}",
                mode: "Active",
                effects: [token]);
            var (world, npc) = WorldWithNpc(
                carrier: Carrier(npcId: 1, powerIds: [descriptor.Id], stageIndex: 0, manifested: true),
                descriptor: descriptor,
                registryWorld: true);

            var opps = PowerOpportunityProvider.ApplicableTo(world, npc, tick: 0, registry);

            Assert.True(
                opps.Any(o => o.MechanicToken == token),
                $"mechanic '{mechanic.Prefix}' not exposed for token '{token}'");
        }
    }

    [Fact]
    public void ApplicableTo_is_deterministic()
    {
        var descriptor = Descriptor(
            "det-power",
            mode: "Active",
            effects: ["combat.strike:10", "attribute.strength:2"]);
        var (world, npc) = WorldWithNpc(
            carrier: Carrier(npcId: 1, powerIds: [descriptor.Id], stageIndex: 0, manifested: true),
            descriptor: descriptor);

        var a = PowerOpportunityProvider.ApplicableTo(world, npc, tick: 10);
        var b = PowerOpportunityProvider.ApplicableTo(world, npc, tick: 10);

        Assert.Equal(a.Select(o => o.MechanicToken), b.Select(o => o.MechanicToken));
    }

    private static string SampleTokenFor(string prefix) =>
        prefix.EndsWith('.') || prefix.EndsWith(':') ? prefix + "sample" : prefix + ":1";

    private static PowerDescriptor Descriptor(
        string id,
        string mode,
        IReadOnlyList<string> effects,
        IReadOnlyList<PowerEvolutionStage>? stages = null) =>
        new(
            id, "test-source", effects, mode, [], "Guaranteed",
            [], [], [], [], Stages: stages);

    private static ExtraordinaryCarrierState Carrier(
        int npcId, IReadOnlyList<string> powerIds, int stageIndex, bool manifested) =>
        new(
            new NpcId(npcId), powerIds, manifested, manifested ? "active" : "latent",
            new ExtraordinaryAppearanceState(1, "", ""), null, 1,
            CurrentStageIndex: stageIndex);

    private static (WorldState World, Npc Npc) WorldWithNpc(
        ExtraordinaryCarrierState? carrier,
        PowerDescriptor? descriptor,
        bool registryWorld = false)
    {
        _ = registryWorld;
        var needs = NeedsRules.Create(
            hungerDecayPerHour: 0, thirstDecayPerHour: 0, sleepDecayPerHour: 0, socialDecayPerHour: 0,
            urgencyThreshold: 70, maxActionSelectionSteps: 10, hysteresisEnabled: false,
            continuityBonus: 0, homelessSleepEfficiency: 0.5).Value!;
        var catalog = ActionCatalog.Create(
            maxDurationHours: new Dictionary<ActionType, int>
            {
                [ActionType.Eat] = 2,
                [ActionType.Sleep] = 8,
                [ActionType.Work] = 8,
                [ActionType.Socialize] = 3,
                [ActionType.Travel] = 4,
                [ActionType.Idle] = 2,
                [ActionType.Buy] = 2,
                [ActionType.UsePower] = 1,
            },
            routineSlots:
            [
                new RoutineSlot(ProfessionId: null, Stage: LifeStage.Adult, HourStart: 0, HourEnd: 23,
                    Action: ActionType.Work),
            ],
            defaultAction: ActionType.Idle).Value!;

        IReadOnlyList<PowerDescriptor> descriptors = descriptor is null ? [] : [descriptor];
        IReadOnlyList<ExtraordinaryCarrierState> carriers = carrier is null ? [] : [carrier];
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, 42, ScenarioRunner.DefaultMap(42),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            needs, catalog, ScenarioRunner.DefaultLifeStageRules,
            extraordinary: new ExtraordinaryScenarioData(enabled: true, descriptors),
            extraordinaryCarriers: carriers);

        var npc = new Npc(
            new NpcId(1), "npc", Sex.Male,
            WorldDate.Epoch(ScenarioRunner.DefaultCalendar).AddYears(-30),
            ScenarioRunner.DefaultCulture, new CellCoord(0, 0),
            motherId: null, fatherId: null, household: new HouseholdId(1),
            health: 100,
            personality: Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!,
            profession: ProfessionType.None,
            currentLocation: new CellCoord(0, 0),
            currentAction: ActionType.Idle);
        world.AddNpc(npc);
        return (world, npc);
    }
}
