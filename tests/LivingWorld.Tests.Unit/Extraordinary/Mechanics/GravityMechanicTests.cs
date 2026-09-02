using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Extraordinary;

public sealed class GravityMechanicTests
{
    [Fact]
    public void Gravity_self_zero_enables_flight_like_legacy_movement_flight()
    {
        var map = Map(coord => coord.X == 1 && coord.Y < 2 ? 2 : 1);
        var (world, npc) = WorldWith(["gravity.self:0"], new CellCoord(0, 0), map);
        var profile = ExtraordinaryLocomotion.Resolve(world, npc);

        var result = ExtraordinaryLocomotion.Advance(
            world, npc, new CellCoord(2, 0), tick: 1, [], profile);

        Assert.True(profile.CanFly);
        Assert.Equal((true, false, 1, new CellCoord(1, 0)),
            (result.Moved, result.Reached, result.Steps, npc.CurrentLocation));
    }

    [Fact]
    public void Gravity_self_above_one_reduces_cell_budget_versus_a_speedster()
    {
        var light = WorldWith(["gravity.self:0.5"], new CellCoord(0, 0));
        var heavy = WorldWith(["gravity.self:2"], new CellCoord(0, 0));

        var lightMove = ExtraordinaryLocomotion.Advance(
            light.World, light.Npc, new CellCoord(5, 0), 1, [],
            ExtraordinaryLocomotion.Resolve(light.World, light.Npc));
        var heavyMove = ExtraordinaryLocomotion.Advance(
            heavy.World, heavy.Npc, new CellCoord(5, 0), 1, [],
            ExtraordinaryLocomotion.Resolve(heavy.World, heavy.Npc));

        Assert.Equal(2, lightMove.Steps);
        Assert.True(heavyMove.Steps < lightMove.Steps);
        Assert.Equal(0, heavyMove.Steps);
    }

    [Fact]
    public void Gravity_target_reduces_the_targets_movement_budget()
    {
        var speed = Descriptor("speed", ["movement.speed-multiplier:3"]);
        var crush = Descriptor("crush", ["gravity.target:2"]);
        var control = TwoNpcs([speed], speedOwner: true, crushOwner: false, crush);
        var treated = TwoNpcs([speed], speedOwner: true, crushOwner: true, crush);

        ExtraordinaryInvocationEngine.Invoke(
            treated.World,
            new TickContext(treated.World, treated.World.Rng, treated.World.Scheduler),
            new ExtraordinaryInvocation(300, treated.Caster.Id, crush.Id, treated.Target.Id));

        var controlMove = ExtraordinaryLocomotion.Advance(
            control.World, control.Target, new CellCoord(5, 0), 1, [],
            ExtraordinaryLocomotion.Resolve(control.World, control.Target));
        var treatedMove = ExtraordinaryLocomotion.Advance(
            treated.World, treated.Target, new CellCoord(5, 0), 1, [],
            ExtraordinaryLocomotion.Resolve(treated.World, treated.Target));

        Assert.Equal(3, controlMove.Steps);
        Assert.Equal(1, treatedMove.Steps);
        Assert.Equal(2, treated.World.ExtraordinaryCarriers
            .Single(item => item.CarrierId == treated.Target.Id).GravityTargetMultiplier);
    }

    [Fact]
    public void Gravity_self_and_target_compose_multiplicatively_in_descriptor_id_order()
    {
        var selfA = Descriptor("a-self", ["gravity.self:2"]);
        var selfB = Descriptor("b-self", ["gravity.self:0.5"]);
        var crush = Descriptor("crush", ["gravity.target:3"]);
        var world = TwoNpcs([selfA, selfB], speedOwner: true, crushOwner: true, crush);

        ExtraordinaryInvocationEngine.Invoke(
            world.World,
            new TickContext(world.World, world.World.Rng, world.World.Scheduler),
            new ExtraordinaryInvocation(301, world.Caster.Id, crush.Id, world.Target.Id));

        var profile = ExtraordinaryLocomotion.Resolve(world.World, world.Target);
        double expectedGravity = 2d * 0.5d * 3d;
        var moved = ExtraordinaryLocomotion.Advance(
            world.World, world.Target, new CellCoord(5, 0), 1, [], profile);

        Assert.False(profile.CanFly);
        Assert.Equal(1d / expectedGravity, profile.SpeedMultiplier);
        Assert.Equal((false, 0), (moved.Moved, moved.Steps));
    }

    [Fact]
    public void Default_registry_resolves_gravity_prefix()
    {
        Assert.IsType<GravityMechanic>(
            ExtraordinaryMechanicRegistry.Default.Resolve("gravity.self:0"));
        Assert.IsType<GravityMechanic>(
            ExtraordinaryMechanicRegistry.Default.Resolve("gravity.target:3"));
    }

    private static (WorldState World, Npc Npc) WorldWith(
        IReadOnlyList<string> effects, CellCoord origin, WorldMap? map = null)
    {
        var descriptor = new PowerDescriptor(
            "locomotion", "test", effects, "Passive", [], "Guaranteed", [], [], [], []);
        var carrier = new ExtraordinaryCarrierState(
            new NpcId(1), [descriptor.Id], true, "manifested",
            new ExtraordinaryAppearanceState(1, "", "trail"), null, 1);
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, 42, map ?? ScenarioRunner.DefaultMap(42),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules,
            extraordinary: new ExtraordinaryScenarioData(true, [descriptor]),
            extraordinaryCarriers: [carrier]);
        var npc = MakeNpc(new NpcId(1), "npc", origin);
        world.AddNpc(npc);
        return (world, npc);
    }

    private static (
        WorldState World, Npc Caster, Npc Target) TwoNpcs(
        IReadOnlyList<PowerDescriptor> targetPowers, bool speedOwner, bool crushOwner,
        PowerDescriptor crush)
    {
        var descriptors = targetPowers.Append(crush).ToList();
        var targetState = new ExtraordinaryCarrierState(
            new NpcId(2),
            targetPowers.Select(item => item.Id).ToList(),
            speedOwner, speedOwner ? "manifested" : "dormant",
            new ExtraordinaryAppearanceState(1, "", ""), null, 1);
        var casterState = new ExtraordinaryCarrierState(
            new NpcId(1), crushOwner ? [crush.Id] : [], crushOwner,
            crushOwner ? "manifested" : "dormant",
            new ExtraordinaryAppearanceState(1, "", ""), null, 1);
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, 42, ScenarioRunner.DefaultMap(42),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules,
            extraordinary: new ExtraordinaryScenarioData(true, descriptors),
            extraordinaryCarriers: [casterState, targetState]);
        var caster = MakeNpc(new NpcId(1), "caster", new CellCoord(0, 1));
        var target = MakeNpc(new NpcId(2), "target", new CellCoord(0, 0));
        world.AddNpc(caster);
        world.AddNpc(target);
        return (world, caster, target);
    }

    private static PowerDescriptor Descriptor(string id, IReadOnlyList<string> effects) =>
        new(id, "test", effects, "Active", [], "Guaranteed", [], [], [], []);

    private static Npc MakeNpc(NpcId id, string name, CellCoord origin) => new(
        id, name, Sex.Male, WorldDate.Epoch(ScenarioRunner.DefaultCalendar),
        ScenarioRunner.DefaultCulture, origin, null, null, null, 100,
        Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!,
        ProfessionType.None, currentLocation: origin);

    private static WorldMap Map(Func<CellCoord, int> terrainOf)
    {
        var catalog = new GeographyCatalog([1, 2], [], []);
        var cost = new CostWeights(1, 0, new Dictionary<int, double> { [1] = 1, [2] = 5 });
        var cells = new List<MapCell>();
        for (int y = 0; y < 3; y++)
            for (int x = 0; x < 3; x++)
            {
                var coord = new CellCoord(x, y);
                cells.Add(MapCell.WithDerivedTemperature(
                    coord, new TerrainType(terrainOf(coord)), default, 0, false, []));
            }
        return WorldMap.Create(3, 3, 1, catalog, cost, cells, RegionGrid.Partition(3, 3, 3), []).Value!;
    }
}
