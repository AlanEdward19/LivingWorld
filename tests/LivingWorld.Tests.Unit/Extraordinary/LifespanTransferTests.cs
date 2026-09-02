using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Extraordinary;

public sealed class LifespanTransferTests
{
    [Fact]
    public void Transfer_lifespan_years_reschedules_donor_earlier_and_recipient_later()
    {
        var (world, carrier, target, ctx) = WorldWithScheduledDeaths(["transfer.lifespan-years:10"]);
        long yearHours = world.Calendar.HoursPerYear;
        long donorBefore = AgeDeathTick(world, carrier);
        long recipientBefore = AgeDeathTick(world, target);

        var result = ExtraordinaryInvocationEngine.Invoke(
            world, ctx, new ExtraordinaryInvocation(90, carrier.Id, "test-power", target.Id));

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(donorBefore - 10 * yearHours, AgeDeathTick(world, carrier));
        Assert.Equal(recipientBefore + 10 * yearHours, AgeDeathTick(world, target));
    }

    [Fact]
    public void Transfer_lifespan_years_fails_when_donor_lacks_remaining_years()
    {
        var (world, carrier, target, ctx) = WorldWithScheduledDeaths(["transfer.lifespan-years:10"]);
        long yearHours = world.Calendar.HoursPerYear;
        var donorDeath = AgeDeath(world, carrier);
        ctx.CancelEvent(donorDeath.Id);
        ctx.ScheduleEvent(
            ctx.CurrentTick + 5 * yearHours, MortalitySystem.SystemName, carrier.Id.Value.ToString());
        long donorBefore = AgeDeathTick(world, carrier);
        long recipientBefore = AgeDeathTick(world, target);

        var result = ExtraordinaryInvocationEngine.Invoke(
            world, ctx, new ExtraordinaryInvocation(91, carrier.Id, "test-power", target.Id));

        Assert.False(result.IsSuccess);
        Assert.Contains("insuficiente", result.Error, StringComparison.Ordinal);
        Assert.Equal(donorBefore, AgeDeathTick(world, carrier));
        Assert.Equal(recipientBefore, AgeDeathTick(world, target));
    }

    [Fact]
    public void Transfer_lifespan_years_fails_when_npc_is_dead()
    {
        var (world, carrier, target, ctx) = WorldWithScheduledDeaths(["transfer.lifespan-years:10"]);
        target.Die(world.CurrentDate);

        var result = ExtraordinaryInvocationEngine.Invoke(
            world, ctx, new ExtraordinaryInvocation(92, carrier.Id, "test-power", target.Id));

        Assert.False(result.IsSuccess);
        Assert.Contains("NPC ausente ou morto", result.Error, StringComparison.Ordinal);
    }

    private static (WorldState World, Npc Carrier, Npc Target, TickContext Ctx) WorldWithScheduledDeaths(
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
        var carrier = Npc(new NpcId(1), "carrier");
        var target = Npc(new NpcId(2), "target");
        var home = new Household(new HouseholdId(1), new CellCoord(0, 0), carrier.Id, [carrier.Id],
            new Dictionary<ResourceType, long> { [new ResourceType(9)] = 5 });
        world.AddNpc(carrier);
        world.AddNpc(target);
        world.AddHousehold(home);
        var ctx = new TickContext(world, world.Rng, world.Scheduler);
        MortalitySystem.SchedulePlannedDeath(world, ctx, carrier);
        MortalitySystem.SchedulePlannedDeath(world, ctx, target);
        return (world, carrier, target, ctx);
    }

    private static ScheduledEvent AgeDeath(WorldState world, Npc npc) =>
        world.PendingEvents.Single(e =>
            e.SystemName == MortalitySystem.SystemName && e.Payload == npc.Id.Value.ToString());

    private static long AgeDeathTick(WorldState world, Npc npc) => AgeDeath(world, npc).TargetTick;

    private static Npc Npc(NpcId id, string name) => new(
        id, name, Sex.Male, WorldDate.Epoch(ScenarioRunner.DefaultCalendar),
        ScenarioRunner.DefaultCulture, new CellCoord(0, 0), motherId: null, fatherId: null,
        household: id == new NpcId(1) ? new HouseholdId(1) : null, health: 100,
        personality: Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!,
        profession: ProfessionType.None, currentLocation: new CellCoord(0, 0));
}
