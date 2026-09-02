using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Extraordinary;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Extraordinary.Engine;
using LivingWorld.Simulation.Extraordinary.Systems;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Extraordinary.Mechanics;

public sealed class MindMechanicTests
{
    [Fact]
    public void Alter_trait_agreeableness_plus_thirty_changes_target_personality()
    {
        var (world, carrier, target, _) = WorldWithPower(["mind.alter-trait:agreeableness:+30"], []);

        var result = ExtraordinaryInvocationEngine.Invoke(
            world, new TickContext(world, world.Rng, world.Scheduler),
            new ExtraordinaryInvocation(90, carrier.Id, "test-power", target.Id));

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(80, target.Personality.Agreeableness);
    }

    [Fact]
    public void Alter_trait_applies_through_authoring_command_log()
    {
        var (world, carrier, target, _) = WorldWithPower(["mind.alter-trait:agreeableness:30"], []);
        var sink = new RecordingSink();

        ExtraordinaryInvocationEngine.Invoke(
            world, new TickContext(world, world.Rng, world.Scheduler, sink),
            new ExtraordinaryInvocation(91, carrier.Id, "test-power", target.Id));

        Assert.Contains(sink.Events, evt =>
            evt.Kind == WorldEventKind.AuthoringCommandApplied
            && evt.Payload.Contains("personality", StringComparison.Ordinal)
            && evt.Payload.Contains(target.Id.Value.ToString(), StringComparison.Ordinal));
    }

    [Fact]
    public void Alter_trait_reverts_when_manifestation_condition_fails()
    {
        var (world, carrier, _, _) = WorldWithPower(
            ["mind.alter-trait:agreeableness:+30"], [], "carrier:action:Work");
        carrier.SetCurrentAction(ActionType.Work, 0);

        ExtraordinaryInvocationEngine.Invoke(
            world, new TickContext(world, world.Rng, world.Scheduler),
            new ExtraordinaryInvocation(92, carrier.Id, "test-power", carrier.Id));
        Assert.Equal(80, carrier.Personality.Agreeableness);

        carrier.SetCurrentAction(ActionType.Idle, 0);
        new ExtraordinaryStateSystem().Tick(world, new TickContext(world, world.Rng, world.Scheduler));

        Assert.Equal(50, carrier.Personality.Agreeableness);
        Assert.Null(Assert.Single(world.ExtraordinaryCarriers).PreAlterationTraits);
    }

    [Fact]
    public void Alter_trait_on_another_npc_reverts_only_when_the_casters_manifestation_ends()
    {
        var (world, carrier, target, _) = WorldWithPower(
            ["mind.alter-trait:agreeableness:+30"], [], "carrier:action:Work");
        carrier.SetCurrentAction(ActionType.Work, 0);

        ExtraordinaryInvocationEngine.Invoke(
            world, new TickContext(world, world.Rng, world.Scheduler),
            new ExtraordinaryInvocation(94, carrier.Id, "test-power", target.Id));
        Assert.Equal(80, target.Personality.Agreeableness);

        new ExtraordinaryStateSystem().Tick(world, new TickContext(world, world.Rng, world.Scheduler));
        Assert.Equal(80, target.Personality.Agreeableness);

        carrier.SetCurrentAction(ActionType.Idle, 0);
        new ExtraordinaryStateSystem().Tick(world, new TickContext(world, world.Rng, world.Scheduler));

        Assert.Equal(50, target.Personality.Agreeableness);
        Assert.Null(Assert.Single(world.ExtraordinaryCarriers).PreAlterationTraits);
    }

    [Fact]
    public void Two_competing_alters_last_invocation_wins()
    {
        var first = Descriptor("first-power", ["mind.alter-trait:agreeableness:+30"]);
        var second = Descriptor("second-power", ["mind.alter-trait:agreeableness:+10"]);
        var (world, carrier, target, _) = WorldWith(
            [first, second],
            new ExtraordinaryCarrierState(
                new NpcId(1), ["first-power", "second-power"], true, "active",
                new ExtraordinaryAppearanceState(1, "", ""), null, 1));

        ExtraordinaryInvocationEngine.Invoke(
            world, new TickContext(world, world.Rng, world.Scheduler),
            new ExtraordinaryInvocation(1, carrier.Id, "first-power", target.Id));
        ExtraordinaryInvocationEngine.Invoke(
            world, new TickContext(world, world.Rng, world.Scheduler),
            new ExtraordinaryInvocation(2, carrier.Id, "second-power", target.Id));

        Assert.Equal(90, target.Personality.Agreeableness);
    }

    [Fact]
    public void Mind_read_logs_public_agreeableness_and_household_without_inventing_fields()
    {
        var (world, carrier, target, _) = WorldWithPower(["mind.read"], []);
        var sink = new RecordingSink();

        var result = ExtraordinaryInvocationEngine.Invoke(
            world, new TickContext(world, world.Rng, world.Scheduler, sink),
            new ExtraordinaryInvocation(93, carrier.Id, "test-power", target.Id));

        Assert.True(result.IsSuccess, result.Error);
        string payload = string.Join('\n', sink.Events
            .Where(evt => evt.Kind == WorldEventKind.ExtraordinaryEffectApplied)
            .Select(evt => evt.Payload));
        Assert.Contains("agreeableness", payload, StringComparison.Ordinal);
        Assert.Contains("household", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Read_memory_lists_only_facts_the_target_participated_in_from_the_world_log()
    {
        var (world, carrier, target, _) = WorldWithPower(["mind.read-memory"], []);
        world.AddFact(new Fact(new FactId(1), 0, WorldEventKind.Birth, [target.Id], null, 0.9, "2"));
        world.AddFact(new Fact(new FactId(2), 1, WorldEventKind.Marriage, [carrier.Id], null, 0.8, "1"));
        var sink = new RecordingSink();

        var result = ExtraordinaryInvocationEngine.Invoke(
            world, new TickContext(world, world.Rng, world.Scheduler, sink),
            new ExtraordinaryInvocation(100, carrier.Id, "test-power", target.Id));

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal([1L], ListedFactIds(sink));
    }

    [Fact]
    public void Erase_memory_hides_the_birth_fact_from_read_memory_without_mutating_the_world_log()
    {
        var birth = new Fact(new FactId(1), 0, WorldEventKind.Birth, [new NpcId(2)], null, 0.9, "2");
        var (world, carrier, target, _) = WorldWith(
            [Descriptor("erase-power", ["mind.erase-memory:1"]), Descriptor("read-power", ["mind.read-memory"])],
            new ExtraordinaryCarrierState(
                new NpcId(1), ["erase-power", "read-power"], true, "active",
                new ExtraordinaryAppearanceState(1, "", ""), null, 1));
        world.AddFact(birth);
        var ctx = new TickContext(world, world.Rng, world.Scheduler);

        var erased = ExtraordinaryInvocationEngine.Invoke(
            world, ctx, new ExtraordinaryInvocation(101, carrier.Id, "erase-power", target.Id));
        var sink = new RecordingSink();
        var read = ExtraordinaryInvocationEngine.Invoke(
            world, new TickContext(world, world.Rng, world.Scheduler, sink),
            new ExtraordinaryInvocation(102, carrier.Id, "read-power", target.Id));

        Assert.True(erased.IsSuccess, erased.Error);
        Assert.True(read.IsSuccess, read.Error);
        Assert.Empty(ListedFactIds(sink));
        Assert.Same(birth, Assert.Single(world.Facts));
        Assert.Contains(
            birth.Id,
            world.ExtraordinaryCarriers.Single(item => item.CarrierId == target.Id).ForgottenFactIds!);
    }

    [Fact]
    public void Implant_memory_exposes_an_existing_fact_the_target_did_not_participate_in()
    {
        var other = new Fact(new FactId(2), 1, WorldEventKind.Marriage, [new NpcId(1)], null, 0.8, "1");
        var (world, carrier, target, _) = WorldWith(
            [Descriptor("implant-power", ["mind.implant-memory:2"]), Descriptor("read-power", ["mind.read-memory"])],
            new ExtraordinaryCarrierState(
                new NpcId(1), ["implant-power", "read-power"], true, "active",
                new ExtraordinaryAppearanceState(1, "", ""), null, 1));
        world.AddFact(new Fact(new FactId(1), 0, WorldEventKind.Birth, [target.Id], null, 0.9, "2"));
        world.AddFact(other);

        var implanted = ExtraordinaryInvocationEngine.Invoke(
            world, new TickContext(world, world.Rng, world.Scheduler),
            new ExtraordinaryInvocation(103, carrier.Id, "implant-power", target.Id));
        var sink = new RecordingSink();
        var read = ExtraordinaryInvocationEngine.Invoke(
            world, new TickContext(world, world.Rng, world.Scheduler, sink),
            new ExtraordinaryInvocation(104, carrier.Id, "read-power", target.Id));

        Assert.True(implanted.IsSuccess, implanted.Error);
        Assert.True(read.IsSuccess, read.Error);
        Assert.Equal([1L, 2L], ListedFactIds(sink));
        Assert.Same(other, world.Facts.Single(fact => fact.Id.Value == 2));
    }

    [Fact]
    public void Implant_memory_fails_when_the_fact_id_is_not_in_the_world_log()
    {
        var (world, carrier, target, _) = WorldWithPower(["mind.implant-memory:999"], []);
        var birth = new Fact(new FactId(1), 0, WorldEventKind.Birth, [target.Id], null, 0.9, "2");
        world.AddFact(birth);

        var result = ExtraordinaryInvocationEngine.Invoke(
            world, new TickContext(world, world.Rng, world.Scheduler),
            new ExtraordinaryInvocation(105, carrier.Id, "test-power", target.Id));

        Assert.False(result.IsSuccess);
        Assert.Same(birth, Assert.Single(world.Facts));
        Assert.Null(world.ExtraordinaryCarriers.FirstOrDefault(item => item.CarrierId == target.Id)?.ImplantedFactIds);
    }

    [Fact]
    public void Memory_effects_cannot_run_when_extraordinary_is_disabled()
    {
        var (world, carrier, target, _) = WorldWithPower(["mind.erase-memory:1"], [], enabled: false);
        var birth = new Fact(new FactId(1), 0, WorldEventKind.Birth, [target.Id], null, 0.9, "2");
        world.AddFact(birth);

        var result = ExtraordinaryInvocationEngine.Invoke(
            world, new TickContext(world, world.Rng, world.Scheduler),
            new ExtraordinaryInvocation(106, carrier.Id, "test-power", target.Id));

        Assert.Equal("Extraordinary.Enabled: false", result.Error);
        Assert.Same(birth, Assert.Single(world.Facts));
        Assert.Null(world.ExtraordinaryCarriers.FirstOrDefault(item => item.CarrierId == target.Id)?.ForgottenFactIds);
    }

    private static IReadOnlyList<long> ListedFactIds(RecordingSink sink) =>
        sink.Events
            .Where(evt => evt.Kind == WorldEventKind.ExtraordinaryEffectApplied
                && evt.Payload.StartsWith("mind.read-memory", StringComparison.Ordinal))
            .SelectMany(evt => evt.Payload.Split('|').Skip(1))
            .Where(part => part.Length > 0)
            .Select(long.Parse)
            .ToList();

    private static PowerDescriptor Descriptor(
        string id, IReadOnlyList<string> effects, string? manifestationCondition = null) => new(
        id, "test-source", effects, "Active", [], "Guaranteed",
        [], [], [], [], ManifestationCondition: manifestationCondition);

    private static (WorldState World, Npc Carrier, Npc Target, Household Home) WorldWithPower(
        IReadOnlyList<string> effects, IReadOnlyList<string> costs, string? manifestationCondition = null,
        bool enabled = true)
    {
        var descriptor = new PowerDescriptor(
            "test-power", "test-source", effects, "Active", costs, "Guaranteed",
            [], [], [], [], ManifestationCondition: manifestationCondition);
        var state = new ExtraordinaryCarrierState(
            new NpcId(1), [descriptor.Id], true, "active",
            new ExtraordinaryAppearanceState(1, "", ""), null, 1);
        return WorldWith([descriptor], state, enabled);
    }

    private static (WorldState World, Npc Carrier, Npc Target, Household Home) WorldWith(
        IReadOnlyList<PowerDescriptor> descriptors, ExtraordinaryCarrierState state, bool enabled = true)
    {
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, 42, ScenarioRunner.DefaultMap(42),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules,
            extraordinary: new ExtraordinaryScenarioData(enabled, descriptors), extraordinaryCarriers: [state]);
        var carrier = Npc(new NpcId(1), "carrier", 100);
        var target = Npc(new NpcId(2), "target", 50);
        target.Marry(carrier.Id);
        var home = new Household(new HouseholdId(1), new CellCoord(0, 0), carrier.Id, [carrier.Id, target.Id],
            new Dictionary<ResourceType, long> { [new ResourceType(9)] = 5 });
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
