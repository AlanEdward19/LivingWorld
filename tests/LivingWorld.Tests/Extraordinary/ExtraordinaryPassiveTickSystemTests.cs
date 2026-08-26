using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Extraordinary;

public sealed class ExtraordinaryPassiveTickSystemTests
{
    [Fact]
    public void Passive_area_mind_alter_trait_applies_each_tick_without_manual_invoke()
    {
        var (world, _, target, _) = WorldWithPassiveAura();
        var system = new ExtraordinaryPassiveTickSystem();
        var ctx = new TickContext(world, world.Rng, world.Scheduler);

        system.Tick(world, ctx);
        Assert.Equal(60, target.Personality.Agreeableness);

        system.Tick(world, ctx);
        Assert.Equal(70, target.Personality.Agreeableness);
    }

    [Fact]
    public void Missing_cost_skips_tick_without_revoking()
    {
        var (world, carrier, target, home) = WorldWithPassiveAura(stock: 2);
        var sink = new RecordingSink();
        var system = new ExtraordinaryPassiveTickSystem();
        var ctx = new TickContext(world, world.Rng, world.Scheduler, sink);

        system.Tick(world, ctx);
        Assert.Equal((60, 0L), (target.Personality.Agreeableness, home.Stock[new ResourceType(9)]));

        system.Tick(world, ctx);

        var state = Assert.Single(world.ExtraordinaryCarriers);
        Assert.Equal(60, target.Personality.Agreeableness);
        Assert.Equal(0L, home.Stock[new ResourceType(9)]);
        Assert.Equal((true, true, "passive-aura"),
            (state.IsManifested, state.PowerIds.Contains("passive-aura"), Assert.Single(state.PowerIds)));
        Assert.Contains(sink.Events, evt =>
            evt.Kind == WorldEventKind.ExtraordinaryUseFailed
            && evt.Payload.Contains("Costs[", StringComparison.Ordinal)
            && evt.Payload.Contains("insuficiente", StringComparison.Ordinal));
        Assert.Equal(carrier.Id, state.CarrierId);
    }

    [Fact]
    public void Unmanifest_stops_reinvoke_immediately()
    {
        var (world, carrier, target, _) = WorldWithPassiveAura(manifestationCondition: "carrier:action:Work");
        carrier.SetCurrentAction(ActionType.Work, 0);
        var sink = new RecordingSink();
        var ctx = new TickContext(world, world.Rng, world.Scheduler, sink);
        var state = new ExtraordinaryStateSystem();
        var passive = new ExtraordinaryPassiveTickSystem();

        state.Tick(world, ctx);
        passive.Tick(world, ctx);
        Assert.Equal(60, target.Personality.Agreeableness);

        carrier.SetCurrentAction(ActionType.Idle, 0);
        int effectsBefore = sink.Events.Count(evt => evt.Kind == WorldEventKind.ExtraordinaryEffectApplied);
        state.Tick(world, ctx);
        passive.Tick(world, ctx);

        Assert.Equal(50, target.Personality.Agreeableness);
        Assert.False(Assert.Single(world.ExtraordinaryCarriers).IsManifested);
        Assert.Equal(effectsBefore, sink.Events.Count(evt => evt.Kind == WorldEventKind.ExtraordinaryEffectApplied));
    }

    [Fact]
    public void Disabled_extraordinary_does_no_work()
    {
        var (world, _, target, home) = WorldWithPassiveAura(enabled: false);
        var sink = new RecordingSink();

        new ExtraordinaryPassiveTickSystem().Tick(
            world, new TickContext(world, world.Rng, world.Scheduler, sink));

        Assert.Equal((50, 5L, 0),
            (target.Personality.Agreeableness, home.Stock[new ResourceType(9)], sink.Events.Count));
        Assert.DoesNotContain(
            ScenarioRunner.DefaultSystems(extraordinary: ExtraordinaryScenarioData.Disabled),
            system => system is ExtraordinaryPassiveTickSystem);
    }

    [Fact]
    public void Enabled_runtime_plan_and_default_systems_include_passive_tick()
    {
        var plan = ExtraordinaryRuntimePlan.Create(new ExtraordinaryScenarioData(true, []));
        var enabled = ScenarioRunner.DefaultSystems(extraordinary: new ExtraordinaryScenarioData(true, []));

        Assert.Equal(ExtraordinaryStateSystem.SystemName, plan.Value!.SystemNames[0]);
        Assert.Equal(ExtraordinaryPowerStageSystem.SystemName, plan.Value.SystemNames[1]);
        Assert.Equal(ExtraordinaryPassiveTickSystem.SystemName, plan.Value.SystemNames[2]);
        Assert.Equal(
            [
                ExtraordinaryStateSystem.SystemName,
                ExtraordinaryPowerStageSystem.SystemName,
                ExtraordinaryPassiveTickSystem.SystemName,
            ],
            enabled.Where(system => system is ExtraordinaryStateSystem
                    or ExtraordinaryPowerStageSystem
                    or ExtraordinaryPassiveTickSystem)
                .Select(system => system.Name));
    }

    private static (WorldState World, Npc Carrier, Npc Target, Household Home) WorldWithPassiveAura(
        long stock = 5, string? manifestationCondition = null, bool enabled = true)
    {
        var descriptor = new PowerDescriptor(
            "passive-aura", "test-source",
            ["area:radius:3", "mind.alter-trait:agreeableness:+10"],
            "Passive", ["household.resource.9:2"], "Guaranteed",
            [], [], [], [], ManifestationCondition: manifestationCondition);
        var state = new ExtraordinaryCarrierState(
            new NpcId(1), [descriptor.Id], true, "manifested",
            new ExtraordinaryAppearanceState(1, "", ""), null, 1);
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, 42, ScenarioRunner.DefaultMap(42),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules,
            extraordinary: new ExtraordinaryScenarioData(enabled, [descriptor]),
            extraordinaryCarriers: [state]);
        var carrier = Npc(new NpcId(1), "carrier", 100);
        var target = Npc(new NpcId(2), "target", 50);
        var home = new Household(new HouseholdId(1), new CellCoord(0, 0), carrier.Id, [carrier.Id, target.Id],
            new Dictionary<ResourceType, long> { [new ResourceType(9)] = stock });
        world.AddNpc(carrier);
        world.AddNpc(target);
        world.AddHousehold(home);
        return (world, carrier, target, home);
    }

    private static Npc Npc(NpcId id, string name, int health) => new(
        id, name, Sex.Male, WorldDate.Epoch(ScenarioRunner.DefaultCalendar),
        ScenarioRunner.DefaultCulture, new CellCoord(0, 0), motherId: null, fatherId: null,
        household: new HouseholdId(1), health: health,
        personality: Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!,
        profession: ProfessionType.None, currentLocation: new CellCoord(0, 0));

    private sealed class RecordingSink : IWorldEventSink
    {
        public List<WorldEvent> Events { get; } = [];
        public void Record(WorldEvent evt) => Events.Add(evt);
    }
}
