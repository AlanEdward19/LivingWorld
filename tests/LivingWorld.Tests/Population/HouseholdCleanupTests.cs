using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Population;

/// <summary>Fase 7, T12: <see cref="HouseholdCleanup.DissolveIfEmpty"/> — refactor puro da
/// dissolução que já vivia em <see cref="NpcDeath.Apply"/>.</summary>
public class HouseholdCleanupTests
{
    private static readonly WorldCalendar Calendar = new(24, 30, 12);

    private sealed class EventSink : IWorldEventSink
    {
        public List<(WorldEventKind Kind, string Payload)> Events { get; } = [];

        public void Record(WorldEvent evt) => Events.Add((evt.Kind, evt.Payload!));
    }

    private static WorldState BuildWorld() => new(
        Calendar, seed: 1, ScenarioRunner.DefaultMap(1), ScenarioRunner.DefaultPopulationCatalog,
        ScenarioRunner.DefaultPopulationRules, ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
        ScenarioRunner.DefaultLifeStageRules, familyRules: FamilyRules.Disabled);

    [Fact]
    public void DissolveIfEmpty_on_non_empty_household_is_no_op()
    {
        var world = BuildWorld();
        var sink = new EventSink();
        var ctx = new TickContext(world, world.Rng, world.Scheduler, sink);
        var head = new NpcId(1);
        var household = new Household(new HouseholdId(10), new CellCoord(0, 0), head, [head, new NpcId(2)]);
        world.AddHousehold(household);

        HouseholdCleanup.DissolveIfEmpty(world, ctx, household);

        Assert.Contains(household, world.Households);
        Assert.Empty(sink.Events);
    }

    [Fact]
    public void DissolveIfEmpty_removes_household_logs_stock_and_clears_stale_references()
    {
        var world = BuildWorld();
        world.CurrentDate = WorldDate.Epoch(Calendar).AddHours(100);
        var sink = new EventSink();
        var ctx = new TickContext(world, world.Rng, world.Scheduler, sink);
        var resource = new ResourceType(1);
        var headId = world.NextNpcIdAndAdvance();
        var npc = new Npc(
            headId, "head", Sex.Male, WorldDate.Epoch(Calendar), new CultureId(1), new CellCoord(0, 0),
            motherId: null, fatherId: null, household: null, health: 100,
            Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!,
            profession: new ProfessionType(1), currentLocation: new CellCoord(0, 0));
        world.AddNpc(npc);

        var householdId = world.NextHouseholdIdAndAdvance();
        var household = new Household(householdId, new CellCoord(0, 0), headId, [headId], stock: new Dictionary<ResourceType, long> { [resource] = 42 });
        world.AddHousehold(household);
        npc.JoinHousehold(householdId);
        household.RemoveMember(headId);

        HouseholdCleanup.DissolveIfEmpty(world, ctx, household);

        Assert.DoesNotContain(world.Households, h => h.Id == householdId);
        Assert.Null(npc.Household);
        Assert.Equal(world.CurrentDate, npc.HomelessSince);
        Assert.Contains(
            (WorldEventKind.ResourceLost, $"{householdId.Value}|{resource.Id}|42"),
            sink.Events);
    }
}
