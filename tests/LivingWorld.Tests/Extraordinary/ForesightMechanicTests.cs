using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Extraordinary;

public sealed class ForesightMechanicTests
{
    [Fact]
    public void Preview_reports_the_resolver_result_for_that_tick_and_seed_with_no_new_fact()
    {
        var treated = WorldWithPreview("check");
        var expectedWorld = WorldWithPreview("check");
        var sink = new RecordingSink();
        const long invocationId = 280;
        const string evento = "check";
        int factsBefore = treated.World.Facts.Count;
        int targetHealthBefore = treated.Target.Health;
        var expected = Resolver.Resolve(
            Difficulty(treated.Target, magnitude: 1),
            BaseCapacity(treated.Carrier),
            VarianceProfile.Dramatico("extraordinary"),
            expectedWorld.World.Rng.Stream(
                $"extraordinary-resolution-{treated.Carrier.Id.Value}-{evento}-{invocationId}"));

        var result = ExtraordinaryInvocationEngine.Invoke(
            treated.World, new TickContext(treated.World, treated.World.Rng, treated.World.Scheduler, sink),
            new ExtraordinaryInvocation(invocationId, treated.Carrier.Id, "test-power", treated.Target.Id));

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(expected, PreviewedResolution(sink, evento));
        Assert.Equal(factsBefore, treated.World.Facts.Count);
        Assert.DoesNotContain(sink.Events, evt => evt.Kind == WorldEventKind.FactRecorded);
        Assert.Equal(targetHealthBefore, treated.Target.Health);
    }

    [Fact]
    public void Preview_does_not_consume_the_live_resolution_stream()
    {
        var treated = WorldWithPreview("check");
        const long invocationId = 281;
        const string evento = "check";
        string streamKey = $"extraordinary-resolution-{treated.Carrier.Id.Value}-{evento}-{invocationId}";
        ulong stateBefore = treated.World.Rng.Stream(streamKey).State;

        var result = ExtraordinaryInvocationEngine.Invoke(
            treated.World, new TickContext(treated.World, treated.World.Rng, treated.World.Scheduler),
            new ExtraordinaryInvocation(invocationId, treated.Carrier.Id, "test-power", treated.Target.Id));

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(stateBefore, treated.World.Rng.Stream(streamKey).State);
    }

    [Fact]
    public void After_the_world_changes_a_later_resolve_follows_current_inputs_not_the_preview()
    {
        var world = WorldWithPreview("check");
        var sink = new RecordingSink();
        const long invocationId = 282;
        const string evento = "check";
        string streamKey = $"extraordinary-resolution-{world.Carrier.Id.Value}-{evento}-{invocationId}";
        int previewDifficulty = Difficulty(world.Target, magnitude: 1);
        int capacity = BaseCapacity(world.Carrier);

        Assert.True(ExtraordinaryInvocationEngine.Invoke(
            world.World, new TickContext(world.World, world.World.Rng, world.World.Scheduler, sink),
            new ExtraordinaryInvocation(invocationId, world.Carrier.Id, "test-power", world.Target.Id)).IsSuccess);
        var preview = PreviewedResolution(sink, evento);
        ulong streamState = world.World.Rng.Stream(streamKey).State;

        world.Target.SetHealth(0);
        int laterDifficulty = Difficulty(world.Target, magnitude: 1);
        var later = Resolver.Resolve(
            laterDifficulty, capacity, VarianceProfile.Dramatico("extraordinary"),
            new WorldRng(streamState));
        var replayPreview = Resolver.Resolve(
            previewDifficulty, capacity, VarianceProfile.Dramatico("extraordinary"),
            new WorldRng(streamState));

        Assert.NotEqual(previewDifficulty, laterDifficulty);
        Assert.Equal(preview, replayPreview);
        Assert.Equal(
            Resolver.Resolve(
                laterDifficulty, capacity, VarianceProfile.Dramatico("extraordinary"),
                new WorldRng(streamState)),
            later);
        Assert.Empty(world.World.Facts);
    }

    [Fact]
    public void Default_registry_resolves_the_foresight_prefix()
    {
        Assert.IsType<ForesightMechanic>(
            ExtraordinaryMechanicRegistry.Default.Resolve("foresight.preview:check"));
    }

    private static ResolutionResult PreviewedResolution(RecordingSink sink, string evento)
    {
        string marker = $"{ForesightMechanic.PreviewPrefix}{evento}|";
        var preview = Assert.Single(
            sink.Events,
            evt => evt.Kind == WorldEventKind.ExtraordinaryEffectApplied
                && evt.Payload.StartsWith(marker, StringComparison.Ordinal));
        return Enum.Parse<ResolutionResult>(preview.Payload[marker.Length..]);
    }

    private static (WorldState World, Npc Carrier, Npc Target) WorldWithPreview(string evento)
    {
        var descriptor = new PowerDescriptor(
            "test-power", "test-source", [$"{ForesightMechanic.PreviewPrefix}{evento}"], "Active", [], "Guaranteed",
            [], [], [], []);
        var state = new ExtraordinaryCarrierState(
            new NpcId(1), [descriptor.Id], true, "active",
            new ExtraordinaryAppearanceState(1, "", ""), null, 1);
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, 42, ScenarioRunner.DefaultMap(42),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules,
            extraordinary: new ExtraordinaryScenarioData(true, [descriptor]), extraordinaryCarriers: [state]);
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

    private static int Difficulty(Npc target, int magnitude) =>
        10 + (int)Math.Ceiling(magnitude / 10d) + Math.Clamp((100 - target.Health) / 20, 0, 5);

    private static int BaseCapacity(Npc carrier) =>
        (int)Math.Clamp(Math.Round(carrier.Vitality / 10d + carrier.RateGene.Value * 5d), 0, 20);

    private sealed class RecordingSink : IWorldEventSink
    {
        public List<WorldEvent> Events { get; } = [];
        public void Record(WorldEvent evt) => Events.Add(evt);
    }
}
