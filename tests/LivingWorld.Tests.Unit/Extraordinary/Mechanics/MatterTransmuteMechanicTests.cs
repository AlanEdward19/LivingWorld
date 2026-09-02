using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Extraordinary;

public sealed class MatterTransmuteMechanicTests
{
    [Fact]
    public void Transmute_debits_origin_and_credits_dest_at_the_declared_rate()
    {
        var (world, carrier, target, home) = WorldWithPower(["matter.transmute:1:2:1"]);
        var sink = new RecordingSink();

        var result = ExtraordinaryInvocationEngine.Invoke(
            world, new TickContext(world, world.Rng, world.Scheduler, sink),
            new ExtraordinaryInvocation(101, carrier.Id, "test-power", target.Id));

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal((9L, 1L), (home.Stock[new ResourceType(1)], home.Stock[new ResourceType(2)]));
        Assert.Contains(sink.Events, evt => evt.Kind == WorldEventKind.Destroyed);
        Assert.Contains(sink.Events, evt => evt.Kind == WorldEventKind.Minted);
    }

    [Fact]
    public void Transmute_with_insufficient_origin_applies_no_credit()
    {
        var (world, carrier, target, home) = WorldWithPower(["matter.transmute:1:2:1"]);
        home.Withdraw(new ResourceType(1), 10);

        var result = ExtraordinaryInvocationEngine.Invoke(
            world, new TickContext(world, world.Rng, world.Scheduler),
            new ExtraordinaryInvocation(102, carrier.Id, "test-power", target.Id));

        Assert.False(result.IsSuccess);
        Assert.Contains("insuficiente", result.Error, StringComparison.Ordinal);
        Assert.Equal(0L, home.Stock.GetValueOrDefault(new ResourceType(1)));
        Assert.Equal(0L, home.Stock.GetValueOrDefault(new ResourceType(2)));
    }

    [Fact]
    public void Transmute_applies_the_declared_rate_without_an_engine_cap()
    {
        var (world, carrier, target, home) = WorldWithPower(["matter.transmute:1:2:3"]);

        var result = ExtraordinaryInvocationEngine.Invoke(
            world, new TickContext(world, world.Rng, world.Scheduler),
            new ExtraordinaryInvocation(103, carrier.Id, "test-power", target.Id));

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal((9L, 3L), (home.Stock[new ResourceType(1)], home.Stock[new ResourceType(2)]));
    }

    [Fact]
    public void Transmute_Destroyed_and_Minted_events_explain_the_stock_change()
    {
        var (world, carrier, target, home) = WorldWithPower(["matter.transmute:1:2:1"]);
        var sink = new RecordingSink();
        long originBefore = home.Stock[new ResourceType(1)];
        long destBefore = home.Stock.GetValueOrDefault(new ResourceType(2));

        ExtraordinaryInvocationEngine.Invoke(
            world, new TickContext(world, world.Rng, world.Scheduler, sink),
            new ExtraordinaryInvocation(104, carrier.Id, "test-power", target.Id));

        var destroyed = Assert.Single(sink.Events, evt => evt.Kind == WorldEventKind.Destroyed);
        var minted = Assert.Single(sink.Events, evt => evt.Kind == WorldEventKind.Minted);
        var destroyedParts = destroyed.Payload.Split('|');
        var mintedParts = minted.Payload.Split('|');
        Assert.Equal(("1", "1"), (destroyedParts[0], destroyedParts[1]));
        Assert.Equal(("2", "1"), (mintedParts[0], mintedParts[1]));
        Assert.Equal(originBefore - long.Parse(destroyedParts[1]), home.Stock[new ResourceType(1)]);
        Assert.Equal(destBefore + long.Parse(mintedParts[1]), home.Stock[new ResourceType(2)]);
    }

    private static (WorldState World, Npc Carrier, Npc Target, Household Home) WorldWithPower(
        IReadOnlyList<string> effects)
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
            extraordinary: new ExtraordinaryScenarioData(true, [descriptor]), extraordinaryCarriers: [state]);
        var carrier = Npc(new NpcId(1), "carrier", 100);
        var target = Npc(new NpcId(2), "target", 50);
        var home = new Household(new HouseholdId(1), new CellCoord(0, 0), carrier.Id, [carrier.Id],
            new Dictionary<ResourceType, long> { [new ResourceType(1)] = 10 });
        world.AddNpc(carrier);
        world.AddNpc(target);
        world.AddHousehold(home);
        return (world, carrier, target, home);
    }

    private static Npc Npc(NpcId id, string name, int health) => new(
        id, name, Sex.Male, WorldDate.Epoch(ScenarioRunner.DefaultCalendar),
        ScenarioRunner.DefaultCulture, new CellCoord(0, 0), motherId: null, fatherId: null,
        household: id == new NpcId(1) ? new HouseholdId(1) : null, health: health,
        personality: Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!,
        profession: ProfessionType.None, currentLocation: new CellCoord(0, 0));

    private sealed class RecordingSink : IWorldEventSink
    {
        public List<WorldEvent> Events { get; } = [];
        public void Record(WorldEvent evt) => Events.Add(evt);
    }
}
