using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Extraordinary;

public sealed class CarryCapacityTests
{
    [Fact]
    public void Pickup_above_base_capacity_is_rejected()
    {
        var npc = Npc(new NpcId(1), 100);
        Assert.False(npc.PickUp(new ResourceType(1), npc.CarryCapacity + 1).IsSuccess);
        Assert.False(npc.IsCarrying);
        Assert.True(npc.PickUp(new ResourceType(1), npc.CarryCapacity).IsSuccess);
        Assert.Equal(npc.CarryCapacity, npc.CarriedQuantity);
    }

    [Fact]
    public void Strength_multiplier_raises_effective_capacity_only_while_manifested()
    {
        var (world, carrier, _) = WorldWithPower(["attribute.strength:3"]);
        long effective = AttributeMechanic.EffectiveCarryCapacity(world, carrier);
        Assert.Equal(carrier.CarryCapacity * 3, effective);
        Assert.True(carrier.PickUp(new ResourceType(1), effective, effective).IsSuccess);

        var dormant = WorldWithPower(["attribute.strength:3"], manifested: false);
        Assert.Equal(dormant.Carrier.CarryCapacity, AttributeMechanic.EffectiveCarryCapacity(dormant.World, dormant.Carrier));
    }

    private static (WorldState World, Npc Carrier, Npc Target) WorldWithPower(
        IReadOnlyList<string> effects, bool manifested = true)
    {
        var descriptor = new PowerDescriptor(
            "test-power", "test-source", effects, "Active", [], "Guaranteed",
            [], [], [], []);
        var state = new ExtraordinaryCarrierState(
            new NpcId(1), [descriptor.Id], manifested, manifested ? "active" : "dormant",
            new ExtraordinaryAppearanceState(1, "", ""), null, 1);
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, 42, ScenarioRunner.DefaultMap(42),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules,
            extraordinary: new ExtraordinaryScenarioData(true, [descriptor]), extraordinaryCarriers: [state]);
        var carrier = Npc(new NpcId(1), 100);
        var target = Npc(new NpcId(2), 50);
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
}
