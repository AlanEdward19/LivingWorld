using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Extraordinary;

public sealed class BondMechanicTests
{
    [Fact]
    public void Share_health_reflects_damage_on_the_partner_each_passive_tick()
    {
        var (world, carrier, partner) = WorldWithShare("bond.share:health");
        carrier.SetHealth(40);
        var system = new ExtraordinaryPassiveTickSystem();
        var ctx = new TickContext(world, world.Rng, world.Scheduler);

        system.Tick(world, ctx);
        Assert.Equal((40, 40), (carrier.Health, partner.Health));

        carrier.SetHealth(10);
        system.Tick(world, ctx);
        Assert.Equal((10, 10), (carrier.Health, partner.Health));
    }

    [Fact]
    public void Share_health_applies_declared_proportion_of_the_gap_each_tick()
    {
        var (world, carrier, partner) = WorldWithShare("bond.share:health:50");
        carrier.SetHealth(40);

        new ExtraordinaryPassiveTickSystem().Tick(
            world, new TickContext(world, world.Rng, world.Scheduler));

        Assert.Equal((40, 70), (carrier.Health, partner.Health));
    }

    [Fact]
    public void Share_bond_is_undone_when_the_partner_dies_and_stops_reflecting()
    {
        var (world, carrier, partner) = WorldWithShare("bond.share:health");
        carrier.SetHealth(40);
        var system = new ExtraordinaryPassiveTickSystem();
        var ctx = new TickContext(world, world.Rng, world.Scheduler);
        system.Tick(world, ctx);
        Assert.Equal(40, partner.Health);

        partner.Die(world.CurrentDate);
        system.Tick(world, ctx);

        var state = Assert.Single(world.ExtraordinaryCarriers);
        Assert.Null(state.BondPartnerId);

        carrier.SetHealth(5);
        system.Tick(world, ctx);
        Assert.Equal(40, partner.Health);
        Assert.Equal(5, carrier.Health);
    }

    [Fact]
    public void Oath_applies_declared_consequence_to_the_violator()
    {
        var (world, carrier, partner) = WorldWithOath();
        carrier.SetCurrentAction(ActionType.Idle, 0);
        partner.SetCurrentAction(ActionType.Work, 0);

        new ExtraordinaryPassiveTickSystem().Tick(
            world, new TickContext(world, world.Rng, world.Scheduler));

        Assert.Equal((80, 100), (carrier.Health, partner.Health));
    }

    [Fact]
    public void Same_seed_reproduces_bond_share()
    {
        var first = WorldWithShare("bond.share:health:50");
        var second = WorldWithShare("bond.share:health:50");
        first.Carrier.SetHealth(40);
        second.Carrier.SetHealth(40);

        new ExtraordinaryPassiveTickSystem().Tick(
            first.World, new TickContext(first.World, first.World.Rng, first.World.Scheduler));
        new ExtraordinaryPassiveTickSystem().Tick(
            second.World, new TickContext(second.World, second.World.Rng, second.World.Scheduler));

        Assert.Equal(
            (first.Carrier.Health, first.Partner.Health),
            (second.Carrier.Health, second.Partner.Health));
    }

    [Fact]
    public void Disabled_extraordinary_does_not_apply_bond()
    {
        var (world, carrier, partner) = WorldWithShare("bond.share:health", enabled: false);
        carrier.SetHealth(40);
        var sink = new RecordingSink();

        new ExtraordinaryPassiveTickSystem().Tick(
            world, new TickContext(world, world.Rng, world.Scheduler, sink));

        Assert.Equal((40, 100, 0), (carrier.Health, partner.Health, sink.Events.Count));
        Assert.Equal(
            "Extraordinary.Enabled: false",
            ExtraordinaryInvocationEngine.Invoke(
                world, new TickContext(world, world.Rng, world.Scheduler),
                new ExtraordinaryInvocation(1, carrier.Id, "bond-share", partner.Id)).Error);
    }

    private static (WorldState World, Npc Carrier, Npc Partner) WorldWithShare(
        string effect, bool enabled = true)
    {
        var descriptor = new PowerDescriptor(
            "bond-share", "test-source", [effect], "Passive", [], "Guaranteed",
            [], [], [], []);
        return World(descriptor, enabled);
    }

    private static (WorldState World, Npc Carrier, Npc Partner) WorldWithOath()
    {
        var descriptor = new PowerDescriptor(
            "bond-oath", "test-source", ["bond.oath:npc.health:-20"], "Passive", [], "Guaranteed",
            [], [], [], [], ManifestationCondition: "carrier:action:Work");
        var setup = World(descriptor, enabled: true);
        setup.Carrier.SetCurrentAction(ActionType.Work, 0);
        setup.Partner.SetCurrentAction(ActionType.Work, 0);
        setup.Carrier.SetHealth(100);
        setup.Partner.SetHealth(100);
        return setup;
    }

    private static (WorldState World, Npc Carrier, Npc Partner) World(
        PowerDescriptor descriptor, bool enabled)
    {
        var carrierId = new NpcId(1);
        var partnerId = new NpcId(2);
        var state = new ExtraordinaryCarrierState(
            carrierId, [descriptor.Id], true, "manifested",
            new ExtraordinaryAppearanceState(1, "", ""), null, 1,
            BondPartnerId: partnerId);
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, 42, ScenarioRunner.DefaultMap(42),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules,
            extraordinary: new ExtraordinaryScenarioData(enabled, [descriptor]),
            extraordinaryCarriers: [state]);
        var carrier = Npc(carrierId, "carrier", 100);
        var partner = Npc(partnerId, "partner", 100);
        var home = new Household(new HouseholdId(1), new CellCoord(0, 0), carrier.Id, [carrier.Id, partner.Id]);
        world.AddNpc(carrier);
        world.AddNpc(partner);
        world.AddHousehold(home);
        return (world, carrier, partner);
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
