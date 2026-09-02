using LivingWorld.Simulation.Extraordinary.Engine;
using LivingWorld.Simulation.Extraordinary.Inheritance;
using LivingWorld.Simulation.Extraordinary.Mechanics;

namespace LivingWorld.Tests.Unit.Extraordinary.Engine;

public sealed class ExtraordinaryMechanicRegistryTests
{
    [Fact]
    public void Default_registry_resolves_each_migrated_effect_and_cost_prefix()
    {
        var registry = ExtraordinaryMechanicRegistry.Default;
        Assert.IsType<NpcStatMechanic>(registry.Resolve("npc.health:15"));
        Assert.IsType<TeleportMechanic>(registry.Resolve("npc.teleport:5"));
        Assert.IsType<ForceActionMechanic>(registry.Resolve("npc.force-action:1"));
        Assert.IsType<ConstructMechanic>(registry.Resolve("construct.create:1x1:1:1:stone"));
        Assert.IsType<MovementEffectMechanic>(registry.Resolve("movement.flight:1"));
        Assert.IsType<AttributeMechanic>(registry.Resolve("attribute.fertility:0"));
        Assert.IsType<CarrierCostMechanic>(registry.Resolve("carrier.health:1"));
        Assert.IsType<HouseholdResourceCostMechanic>(registry.Resolve("household.resource.9:2"));
        Assert.IsType<AreaSelectorMechanic>(registry.Resolve("area:radius:3"));
        Assert.IsType<TransferMechanic>(registry.Resolve("transfer.health:20"));
        Assert.IsType<SkillMechanic>(registry.Resolve("skill.copy:1"));
        Assert.IsType<SkillMechanic>(registry.Resolve("skill.learn-rate:5"));
        Assert.IsType<EnvironmentTemperatureMechanic>(
            registry.Resolve("environment.temperature:0:-5:10"));
        Assert.IsType<DimensionMechanic>(registry.Resolve("dimension.pocket-store"));
        Assert.IsType<FaunaMechanic>(registry.Resolve("fauna.dominate:3"));
        Assert.IsType<FloraMechanic>(registry.Resolve("flora.growth-rate:5"));
        Assert.Null(registry.Resolve("unknown.token:1"));
    }

    [Fact]
    public void Duplicate_prefix_fails_at_composition_not_at_invocation()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            new ExtraordinaryMechanicRegistry([new NpcStatMechanic(), new NpcStatMechanic()]));
        Assert.Contains("prefixo duplicado", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Longest_registered_prefix_wins_over_a_shorter_npc_namespace()
    {
        Assert.IsType<TeleportMechanic>(ExtraordinaryMechanicRegistry.Default.Resolve("npc.teleport:4"));
        Assert.IsType<NpcStatMechanic>(ExtraordinaryMechanicRegistry.Default.Resolve("npc.health:4"));
        Assert.IsType<NpcCloneMechanic>(ExtraordinaryMechanicRegistry.Default.Resolve("npc.clone:1"));
        Assert.IsType<NpcSplitOnDeathMechanic>(
            ExtraordinaryMechanicRegistry.Default.Resolve("npc.split-on-death:2"));
        Assert.IsType<NpcReincarnateMechanic>(
            ExtraordinaryMechanicRegistry.Default.Resolve("npc.reincarnate:50"));
    }
}
