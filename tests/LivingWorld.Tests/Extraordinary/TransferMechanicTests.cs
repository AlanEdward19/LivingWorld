using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Extraordinary;

public sealed class TransferMechanicTests
{
    [Fact]
    public void Transfer_pays_household_cost_before_moving_health()
    {
        var (world, carrier, target, home) = WorldWithPower(
            ["transfer.health:20"], ["household.resource.9:2"]);
        var sink = new RecordingSink();

        var result = ExtraordinaryInvocationEngine.Invoke(
            world, new TickContext(world, world.Rng, world.Scheduler, sink),
            new ExtraordinaryInvocation(89, carrier.Id, "test-power", target.Id));

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal((80, 70, 3L), (carrier.Health, target.Health, home.Stock[new ResourceType(9)]));
        Assert.Equal(
            [WorldEventKind.ExtraordinaryUseAttempted, WorldEventKind.ExtraordinaryCostPaid,
                WorldEventKind.ExtraordinaryEffectApplied],
            sink.Events.Select(evt => evt.Kind));
    }

    [Fact]
    public void Transfer_health_debits_the_donor_and_credits_the_recipient_atomically()
    {
        var (world, carrier, target, _) = WorldWithPower(["transfer.health:20"], []);

        var result = ExtraordinaryInvocationEngine.Invoke(
            world, new TickContext(world, world.Rng, world.Scheduler),
            new ExtraordinaryInvocation(90, carrier.Id, "test-power", target.Id));

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal((80, 70), (carrier.Health, target.Health));
    }

    [Fact]
    public void Transfer_with_insufficient_donor_balance_applies_no_credit()
    {
        var (world, carrier, target, _) = WorldWithPower(["transfer.health:20"], []);
        carrier.SetHealth(10);

        var result = ExtraordinaryInvocationEngine.Invoke(
            world, new TickContext(world, world.Rng, world.Scheduler),
            new ExtraordinaryInvocation(91, carrier.Id, "test-power", target.Id));

        Assert.False(result.IsSuccess);
        Assert.Contains("insuficiente", result.Error, StringComparison.Ordinal);
        Assert.Equal((10, 50), (carrier.Health, target.Health));
    }

    [Fact]
    public void Transfer_discards_recipient_overflow_instead_of_failing()
    {
        var (world, carrier, target, _) = WorldWithPower(["transfer.health:20"], []);
        target.SetHealth(95);

        var result = ExtraordinaryInvocationEngine.Invoke(
            world, new TickContext(world, world.Rng, world.Scheduler),
            new ExtraordinaryInvocation(92, carrier.Id, "test-power", target.Id));

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal((80, 100), (carrier.Health, target.Health));
    }

    [Fact]
    public void Paired_control_accounts_for_every_transferred_health_unit_when_neither_side_clamps()
    {
        var (treated, carrier, target, _) = WorldWithPower(["transfer.health:20"], []);
        var (_, controlCarrier, controlTarget, _) = WorldWithPower(["transfer.health:20"], []);

        ExtraordinaryInvocationEngine.Invoke(
            treated, new TickContext(treated, treated.Rng, treated.Scheduler),
            new ExtraordinaryInvocation(93, carrier.Id, "test-power", target.Id));

        Assert.Equal(
            controlCarrier.Health + controlTarget.Health,
            carrier.Health + target.Health);
        Assert.Equal(controlCarrier.Health - 20, carrier.Health);
        Assert.Equal(controlTarget.Health + 20, target.Health);
    }

    private static (WorldState World, Npc Carrier, Npc Target, Household Home) WorldWithPower(
        IReadOnlyList<string> effects, IReadOnlyList<string> costs)
    {
        var descriptor = new PowerDescriptor(
            "test-power", "test-source", effects, "Active", costs, "Guaranteed",
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
            new Dictionary<ResourceType, long> { [new ResourceType(9)] = 5 });
        world.AddNpc(carrier);
        world.AddNpc(target);
        world.AddHousehold(home);
        return (world, carrier, target, home);
    }

    private sealed class RecordingSink : IWorldEventSink
    {
        public List<WorldEvent> Events { get; } = [];
        public void Record(WorldEvent evt) => Events.Add(evt);
    }

    private static Npc Npc(NpcId id, string name, int health) => new(
        id, name, Sex.Male, WorldDate.Epoch(ScenarioRunner.DefaultCalendar),
        ScenarioRunner.DefaultCulture, new CellCoord(0, 0), motherId: null, fatherId: null,
        household: id == new NpcId(1) ? new HouseholdId(1) : null, health: health,
        personality: Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!,
        profession: ProfessionType.None, currentLocation: new CellCoord(0, 0));
}
