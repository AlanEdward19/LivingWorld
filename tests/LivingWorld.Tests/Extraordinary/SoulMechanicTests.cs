using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Extraordinary;

public sealed class SoulMechanicTests
{
    [Fact]
    public void Ghost_carrier_remains_queryable_after_death()
    {
        var (world, carrier, _) = WorldWithPower(["soul.persist-as-ghost"]);
        var lastCell = new CellCoord(4, 7);
        var personality = carrier.Personality;
        carrier.MoveTo(lastCell, 0);
        carrier.GainSkill(new SkillType(3), 40, 100);
        double skill = carrier.Skills.Get(new SkillType(3));

        NpcDeath.Apply(world, new TickContext(world, world.Rng, world.Scheduler), carrier, WorldEventKind.Death);

        Assert.False(carrier.IsAlive);
        Assert.True(carrier.IsGhost);
        var query = SoulMechanic.TryQuery(world, carrier.Id);
        Assert.NotNull(query);
        Assert.Equal(
            (carrier.Name, lastCell, personality.Agreeableness, skill),
            (query.Value.Name, query.Value.LastPosition, query.Value.Personality.Agreeableness, query.Value.Skills.Get(new SkillType(3))));
    }

    [Fact]
    public void Control_npc_without_ghost_power_is_not_queryable_as_ghost()
    {
        var treated = WorldWithPower(["soul.persist-as-ghost"]);
        var control = WorldWithPower(["mind.read"]);

        NpcDeath.Apply(
            treated.World, new TickContext(treated.World, treated.World.Rng, treated.World.Scheduler),
            treated.Carrier, WorldEventKind.Death);
        NpcDeath.Apply(
            control.World, new TickContext(control.World, control.World.Rng, control.World.Scheduler),
            control.Carrier, WorldEventKind.Death);

        Assert.False(treated.Carrier.IsAlive);
        Assert.False(control.Carrier.IsAlive);
        Assert.True(treated.Carrier.IsGhost);
        Assert.False(control.Carrier.IsGhost);
        Assert.NotNull(SoulMechanic.TryQuery(treated.World, treated.Carrier.Id));
        Assert.Null(SoulMechanic.TryQuery(control.World, control.Carrier.Id));
        Assert.NotNull(control.World.FindNpc(control.Carrier.Id));
    }

    [Fact]
    public void Commune_on_ghost_reuses_read_memory_fact_ids()
    {
        var (world, carrier, living) = WorldWithPowers(
            Descriptor("ghost-power", ["soul.persist-as-ghost"]),
            Descriptor("commune-power", ["mind.commune"]),
            Descriptor("read-power", ["mind.read-memory"]));
        world.AddFact(new Fact(new FactId(1), 0, WorldEventKind.Birth, [carrier.Id], null, 0.9, "1"));
        world.AddFact(new Fact(new FactId(2), 1, WorldEventKind.Marriage, [living.Id], null, 0.8, "2"));
        NpcDeath.Apply(world, new TickContext(world, world.Rng, world.Scheduler), carrier, WorldEventKind.Death);
        var invocation = new ExtraordinaryInvocation(1, living.Id, "commune-power", carrier.Id);
        var ctx = MechanicContext(world, invocation, living, carrier);
        var communeSink = new RecordingSink();
        var readSink = new RecordingSink();

        var commune = new MindMechanic().PrepareEffect(ctx with { Tick = Tick(world, communeSink) }, "mind.commune");
        var read = new MindMechanic().PrepareEffect(ctx with { Tick = Tick(world, readSink) }, "mind.read-memory");
        Assert.True(commune.IsSuccess, commune.Error);
        Assert.True(read.IsSuccess, read.Error);
        commune.Value!.Apply(ResolutionResult.Success);
        read.Value!.Apply(ResolutionResult.Success);

        Assert.Equal(ListedFactIds(readSink), ListedFactIds(communeSink));
        Assert.Equal([1L], ListedFactIds(communeSink));
    }

    [Fact]
    public void Ghost_and_commune_are_unreachable_when_extraordinary_is_disabled()
    {
        var (world, carrier, living) = WorldWithPower(["soul.persist-as-ghost"], enabled: false);

        var invoke = ExtraordinaryInvocationEngine.Invoke(
            world, new TickContext(world, world.Rng, world.Scheduler),
            new ExtraordinaryInvocation(1, carrier.Id, "test-power", living.Id));
        NpcDeath.Apply(world, new TickContext(world, world.Rng, world.Scheduler), carrier, WorldEventKind.Death);

        Assert.Equal("Extraordinary.Enabled: false", invoke.Error);
        Assert.False(carrier.IsAlive);
        Assert.False(carrier.IsGhost);
        Assert.Null(SoulMechanic.TryQuery(world, carrier.Id));
    }

    [Fact]
    public void Invoke_commune_on_ghost_is_not_rejected_as_dead_target()
    {
        var (world, carrier, living) = WorldWithPowers(
            Descriptor("ghost-power", ["soul.persist-as-ghost"]),
            Descriptor("commune-power", ["mind.commune"]));
        world.AddFact(new Fact(new FactId(1), 0, WorldEventKind.Birth, [carrier.Id], null, 0.9, "1"));
        NpcDeath.Apply(world, new TickContext(world, world.Rng, world.Scheduler), carrier, WorldEventKind.Death);
        var sink = new RecordingSink();

        var result = ExtraordinaryInvocationEngine.Invoke(
            world, Tick(world, sink),
            new ExtraordinaryInvocation(1, living.Id, "commune-power", carrier.Id));

        Assert.True(result.IsSuccess, result.Error);
        Assert.Contains(
            sink.Events,
            evt => evt.Kind == WorldEventKind.ExtraordinaryEffectApplied
                && evt.Payload.StartsWith("mind.read-memory", StringComparison.Ordinal));
    }

    private static IReadOnlyList<long> ListedFactIds(RecordingSink sink) =>
        sink.Events
            .Where(evt => evt.Kind == WorldEventKind.ExtraordinaryEffectApplied
                && evt.Payload.StartsWith("mind.read-memory", StringComparison.Ordinal))
            .SelectMany(evt => evt.Payload.Split('|').Skip(1))
            .Where(part => part.Length > 0)
            .Select(long.Parse)
            .ToList();

    private static TickContext Tick(WorldState world, RecordingSink sink) =>
        new(world, world.Rng, world.Scheduler, sink);

    private static ExtraordinaryMechanicContext MechanicContext(
        WorldState world, ExtraordinaryInvocation invocation, Npc living, Npc ghost) =>
        new(world, new TickContext(world, world.Rng, world.Scheduler), invocation, living, ghost,
            ExtraordinaryMechanicKind.Effect);

    private static PowerDescriptor Descriptor(string id, IReadOnlyList<string> effects) => new(
        id, "test-source", effects, "Active", [], "Guaranteed",
        [], [], [], []);

    private static (WorldState World, Npc Carrier, Npc Living) WorldWithPower(
        IReadOnlyList<string> effects, bool enabled = true)
    {
        var descriptor = Descriptor("test-power", effects);
        var state = new ExtraordinaryCarrierState(
            new NpcId(1), [descriptor.Id], true, "active",
            new ExtraordinaryAppearanceState(1, "", ""), null, 1);
        return WorldWith([descriptor], [state], enabled);
    }

    private static (WorldState World, Npc Carrier, Npc Living) WorldWithPowers(params PowerDescriptor[] descriptors)
    {
        var ghost = new ExtraordinaryCarrierState(
            new NpcId(1), ["ghost-power"], true, "active",
            new ExtraordinaryAppearanceState(1, "", ""), null, 1);
        var living = new ExtraordinaryCarrierState(
            new NpcId(2), ["commune-power", "read-power"], true, "active",
            new ExtraordinaryAppearanceState(1, "", ""), null, 1);
        return WorldWith(descriptors, [ghost, living]);
    }

    private static (WorldState World, Npc Carrier, Npc Living) WorldWith(
        IReadOnlyList<PowerDescriptor> descriptors,
        IReadOnlyList<ExtraordinaryCarrierState> carriers,
        bool enabled = true)
    {
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, 42, ScenarioRunner.DefaultMap(42),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules,
            extraordinary: new ExtraordinaryScenarioData(enabled, descriptors), extraordinaryCarriers: carriers);
        var carrier = Npc(new NpcId(1), "carrier", 100);
        var living = Npc(new NpcId(2), "living", 50);
        world.AddNpc(carrier);
        world.AddNpc(living);
        return (world, carrier, living);
    }

    private static Npc Npc(NpcId id, string name, int health) => new(
        id, name, Sex.Male, WorldDate.Epoch(ScenarioRunner.DefaultCalendar),
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
