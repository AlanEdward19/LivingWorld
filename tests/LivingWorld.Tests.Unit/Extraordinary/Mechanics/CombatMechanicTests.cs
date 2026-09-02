using LivingWorld.Domain.Extraordinary;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Extraordinary.Engine;
using LivingWorld.Simulation.Extraordinary.Mechanics;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Extraordinary.Mechanics;

public sealed class CombatMechanicTests
{
    [Fact]
    public void Strike_resolves_via_resolver_applies_health_damage_and_logs_combat_resolved()
    {
        var (world, carrier, target) = WorldWithPower(["combat.strike:20"]);
        var expected = WorldWithPower(["combat.strike:20"]);
        var sink = new RecordingSink();
        const long invocationId = 301;
        int difficulty = 10 + Math.Clamp((100 - target.Health) / 20, 0, 5);
        int capacity = (int)Math.Clamp(Math.Round(carrier.Vitality / 10d + carrier.RateGene.Value * 5d), 0, 20);
        var resolution = Resolver.Resolve(
            difficulty, capacity, VarianceProfile.Dramatico("extraordinary"),
            expected.World.Rng.Stream($"combat-strike-{carrier.Id.Value}-{target.Id.Value}-{invocationId}"));
        int expectedHealth = ExtraordinaryMechanicSupport.ClampNeed(
            target.Health - CombatMechanic.DamageOf(20, resolution));

        var result = ExtraordinaryInvocationEngine.Invoke(
            world, new TickContext(world, world.Rng, world.Scheduler, sink),
            new ExtraordinaryInvocation(invocationId, carrier.Id, "test-power", target.Id));

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(expectedHealth, target.Health);
        var combat = Assert.Single(sink.Events, evt => evt.Kind == WorldEventKind.CombatResolved);
        Assert.Equal($"{carrier.Id.Value}|{target.Id.Value}|{resolution}", combat.Payload);
    }

    [Fact]
    public void Strike_never_uses_extraordinary_effect_applied_as_the_combat_record()
    {
        var (world, carrier, target) = WorldWithPower(["combat.strike:15"]);
        var sink = new RecordingSink();

        ExtraordinaryInvocationEngine.Invoke(
            world, new TickContext(world, world.Rng, world.Scheduler, sink),
            new ExtraordinaryInvocation(302, carrier.Id, "test-power", target.Id));

        Assert.Contains(sink.Events, evt => evt.Kind == WorldEventKind.CombatResolved);
        Assert.DoesNotContain(sink.Events.Where(evt => evt.Kind == WorldEventKind.ExtraordinaryEffectApplied),
            evt => evt.Payload.Contains("CombatResolved", StringComparison.Ordinal));
    }

    [Fact]
    public void Same_seed_reproduces_strike_health_and_combat_payload()
    {
        var first = WorldWithPower(["combat.strike:20"]);
        var second = WorldWithPower(["combat.strike:20"]);
        var firstSink = new RecordingSink();
        var secondSink = new RecordingSink();

        ExtraordinaryInvocationEngine.Invoke(
            first.World, new TickContext(first.World, first.World.Rng, first.World.Scheduler, firstSink),
            new ExtraordinaryInvocation(303, first.Carrier.Id, "test-power", first.Target.Id));
        ExtraordinaryInvocationEngine.Invoke(
            second.World, new TickContext(second.World, second.World.Rng, second.World.Scheduler, secondSink),
            new ExtraordinaryInvocation(303, second.Carrier.Id, "test-power", second.Target.Id));

        Assert.Equal(first.Target.Health, second.Target.Health);
        Assert.Equal(
            firstSink.Events.Single(evt => evt.Kind == WorldEventKind.CombatResolved).Payload,
            secondSink.Events.Single(evt => evt.Kind == WorldEventKind.CombatResolved).Payload);
    }

    [Fact]
    public void Strike_is_unreachable_when_extraordinary_is_disabled()
    {
        var (world, carrier, target) = WorldWithPower(["combat.strike:20"], enabled: false);

        var result = ExtraordinaryInvocationEngine.Invoke(
            world, new TickContext(world, world.Rng, world.Scheduler),
            new ExtraordinaryInvocation(304, carrier.Id, "test-power", target.Id));

        Assert.False(result.IsSuccess);
        Assert.Equal("Extraordinary.Enabled: false", result.Error);
        Assert.Equal(80, target.Health);
    }

    [Fact]
    public void Strength_multiplier_feeds_resolver_capacity_as_extra_modifiers()
    {
        var treated = WorldWithPowers(["attribute.strength:3"], ["combat.strike:10"]);
        var expected = WorldWithPowers(["attribute.strength:3"], ["combat.strike:10"]);
        const long invocationId = 305;
        int difficulty = 10 + Math.Clamp((100 - treated.Target.Health) / 20, 0, 5);
        int baseCapacity = (int)Math.Clamp(
            Math.Round(treated.Carrier.Vitality / 10d + treated.Carrier.RateGene.Value * 5d), 0, 20);
        int capacity = baseCapacity + (int)Math.Round((3d - 1) * 10);
        var resolution = Resolver.Resolve(
            difficulty, capacity, VarianceProfile.Dramatico("extraordinary"),
            expected.World.Rng.Stream(
                $"combat-strike-{treated.Carrier.Id.Value}-{treated.Target.Id.Value}-{invocationId}"));

        var result = ExtraordinaryInvocationEngine.Invoke(
            treated.World, new TickContext(treated.World, treated.World.Rng, treated.World.Scheduler),
            new ExtraordinaryInvocation(invocationId, treated.Carrier.Id, "strike-power", treated.Target.Id));

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(
            ExtraordinaryMechanicSupport.ClampNeed(80 - CombatMechanic.DamageOf(10, resolution)),
            treated.Target.Health);
    }

    [Fact]
    public void Default_registry_resolves_combat_strike()
    {
        Assert.IsType<CombatMechanic>(ExtraordinaryMechanicRegistry.Default.Resolve("combat.strike:12"));
    }

    private static (WorldState World, Npc Carrier, Npc Target) WorldWithPower(
        IReadOnlyList<string> effects, bool enabled = true)
    {
        var descriptor = new PowerDescriptor(
            "test-power", "test-source", effects, "Active", [], "Guaranteed",
            [], [], [], []);
        var state = new ExtraordinaryCarrierState(
            new NpcId(1), [descriptor.Id], true, "active",
            new ExtraordinaryAppearanceState(1, "", ""), null, 1);
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, 42, ScenarioRunner.DefaultMap(42),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules,
            extraordinary: new ExtraordinaryScenarioData(enabled, [descriptor]), extraordinaryCarriers: [state]);
        var carrier = Npc(new NpcId(1), 100);
        var target = Npc(new NpcId(2), 80);
        world.AddNpc(carrier);
        world.AddNpc(target);
        return (world, carrier, target);
    }

    private static (WorldState World, Npc Carrier, Npc Target) WorldWithPowers(
        IReadOnlyList<string> strengthEffects, IReadOnlyList<string> strikeEffects)
    {
        var strength = new PowerDescriptor(
            "strength-power", "test-source", strengthEffects, "Active", [], "Guaranteed", [], [], [], []);
        var strike = new PowerDescriptor(
            "strike-power", "test-source", strikeEffects, "Active", [], "Guaranteed", [], [], [], []);
        var state = new ExtraordinaryCarrierState(
            new NpcId(1), [strength.Id, strike.Id], true, "active",
            new ExtraordinaryAppearanceState(1, "", ""), null, 1);
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, 42, ScenarioRunner.DefaultMap(42),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules,
            extraordinary: new ExtraordinaryScenarioData(true, [strength, strike]), extraordinaryCarriers: [state]);
        var carrier = Npc(new NpcId(1), 100);
        var target = Npc(new NpcId(2), 80);
        world.AddNpc(carrier);
        world.AddNpc(target);
        return (world, carrier, target);
    }

    private static Npc Npc(NpcId id, int health) => new(
        id, "n", Sex.Male, WorldDate.Epoch(ScenarioRunner.DefaultCalendar),
        ScenarioRunner.DefaultCulture, new CellCoord(0, 0), motherId: null, fatherId: null,
        household: null, health: health,
        personality: Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!,
        profession: ProfessionType.None, currentLocation: new CellCoord(0, 0));

    private sealed class RecordingSink : IWorldEventSink
    {
        public List<WorldEvent> Events { get; } = [];
        public void Record(WorldEvent evt) => Events.Add(evt);
    }
}
