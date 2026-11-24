using LivingWorld.Domain.Extraordinary;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Extraordinary.Engine;
using LivingWorld.Simulation.Population.Lifecycle;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Extraordinary;

public sealed class LifespanTransferTests
{
    // AD-130: sem data de morte marcada, transferir anos de vida desloca a idade biológica que entra no risco de morte.
    [Fact]
    public void Transfer_lifespan_years_ages_the_donor_and_rejuvenates_the_recipient()
    {
        var (world, carrier, target, ctx) = WorldWithScheduledDeaths(["transfer.lifespan-years:10"]);
        long yearHours = world.Calendar.HoursPerYear;

        var result = ExtraordinaryInvocationEngine.Invoke(
            world, ctx, new ExtraordinaryInvocation(90, carrier.Id, "test-power", target.Id));

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal((10 * yearHours, -10 * yearHours), (carrier.LifespanOffsetHours, target.LifespanOffsetHours));
    }

    [Fact]
    public void Transfer_lifespan_years_raises_the_donor_death_risk_and_lowers_the_recipient_one()
    {
        var (world, carrier, target, ctx) = WorldWithScheduledDeaths(["transfer.lifespan-years:10"]);
        world.CurrentDate = world.CurrentDate.AddYears(60);
        long now = world.CurrentDate.TotalHours, month = world.Calendar.HoursPerMonth;
        double donorBefore = MortalitySystem.WindowRisk(world, carrier, now, month);
        double recipientBefore = MortalitySystem.WindowRisk(world, target, now, month);

        ExtraordinaryInvocationEngine.Invoke(world, ctx, new ExtraordinaryInvocation(93, carrier.Id, "test-power", target.Id));

        Assert.True(MortalitySystem.WindowRisk(world, carrier, now, month) > donorBefore
            && MortalitySystem.WindowRisk(world, target, now, month) < recipientBefore);
    }

    [Fact]
    public void Transfer_lifespan_years_fails_when_donor_lacks_remaining_years()
    {
        var (world, carrier, target, ctx) = WorldWithScheduledDeaths(["transfer.lifespan-years:10"]);
        // a 5 anos do teto de longevidade da tábua, o doador não tem 10 anos para dar
        world.CurrentDate = world.CurrentDate.AddYears(world.PopulationRules.LifeTable.MaxLongevityYears - 5);

        var result = ExtraordinaryInvocationEngine.Invoke(
            world, ctx, new ExtraordinaryInvocation(91, carrier.Id, "test-power", target.Id));

        Assert.False(result.IsSuccess);
        Assert.Contains("insuficiente", result.Error, StringComparison.Ordinal);
        Assert.Equal((0L, 0L), (carrier.LifespanOffsetHours, target.LifespanOffsetHours));
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
        return (world, carrier, target, ctx);
    }

    private static Npc Npc(NpcId id, string name) => new(
        id, name, Sex.Male, WorldDate.Epoch(ScenarioRunner.DefaultCalendar),
        ScenarioRunner.DefaultCulture, new CellCoord(0, 0), motherId: null, fatherId: null,
        household: id == new NpcId(1) ? new HouseholdId(1) : null, health: 100,
        personality: Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!,
        profession: ProfessionType.None, currentLocation: new CellCoord(0, 0));
}
