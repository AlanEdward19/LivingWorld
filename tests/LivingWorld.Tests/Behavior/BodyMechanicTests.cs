using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Behavior;

/// <summary>Fase 16.3, T8 (COH-22, COH-23): multiplicadores puros de corpo.</summary>
public class BodyMechanicTests
{
    private static readonly WorldCalendar Calendar = new(24, 30, 12);
    private static readonly Personality Personality =
        Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    private static WorldState BuildWorld(BodyRules? bodyRules = null)
    {
        var map = ScenarioRunner.DefaultMap(1);
        return new WorldState(
            Calendar, seed: 1, map, ScenarioRunner.DefaultPopulationCatalog,
            ScenarioRunner.DefaultPopulationRules, ScenarioRunner.DefaultNeedsRules,
            ScenarioRunner.DefaultActionCatalog, ScenarioRunner.DefaultLifeStageRules,
            bodyRules: bodyRules ?? BodyRules.Default);
    }

    private static Npc MakeNpc(double height, double weight, double muscleMass) => new(
        new NpcId(1), "body", Sex.Male, WorldDate.Epoch(Calendar), new CultureId(1), new CellCoord(0, 0),
        null, null, null, health: 100, personality: Personality, profession: default,
        currentLocation: new CellCoord(0, 0), height: height, weight: weight, muscleMass: muscleMass);

    [Fact]
    public void WorkCapacityMultiplier_is_neutral_when_BodyRules_disabled()
    {
        var world = BuildWorld(BodyRules.Disabled);
        var npc = MakeNpc(1.9, 90, 50);

        Assert.Equal(1.0, BodyMechanic.WorkCapacityMultiplier(world, npc));
    }

    [Fact]
    public void MovementCostMultiplier_is_neutral_when_BodyRules_disabled()
    {
        var world = BuildWorld(BodyRules.Disabled);
        var npc = MakeNpc(1.9, 90, 50);

        Assert.Equal(1.0, BodyMechanic.MovementCostMultiplier(world, npc));
    }

    [Fact]
    public void WorkCapacityMultiplier_grows_with_higher_MuscleMass()
    {
        var world = BuildWorld();
        var weak = MakeNpc(1.70, 68, muscleMass: 15);
        var strong = MakeNpc(1.70, 68, muscleMass: 45);

        double weakMult = BodyMechanic.WorkCapacityMultiplier(world, weak);
        double strongMult = BodyMechanic.WorkCapacityMultiplier(world, strong);

        Assert.True(strongMult > weakMult);
        Assert.True(strongMult > 1.0);
        Assert.True(weakMult < 1.0);
    }

    [Fact]
    public void WorkCapacityMultiplier_is_one_at_MuscleMass_mean()
    {
        var rules = BodyRules.Default;
        var world = BuildWorld(rules);
        var npc = MakeNpc(rules.HeightMean, rules.WeightMean, rules.MuscleMassMean);

        Assert.Equal(1.0, BodyMechanic.WorkCapacityMultiplier(world, npc), precision: 10);
    }

    [Fact]
    public void MovementCostMultiplier_varies_with_Weight_and_Height()
    {
        var world = BuildWorld();
        var light = MakeNpc(height: 1.55, weight: 50, muscleMass: 28);
        var heavy = MakeNpc(height: 1.90, weight: 95, muscleMass: 28);

        double lightCost = BodyMechanic.MovementCostMultiplier(world, light);
        double heavyCost = BodyMechanic.MovementCostMultiplier(world, heavy);

        Assert.True(heavyCost > lightCost);
        Assert.True(heavyCost > 1.0);
        Assert.True(lightCost < 1.0);
    }

    [Fact]
    public void Multipliers_are_deterministic_for_same_npc_state()
    {
        var world = BuildWorld();
        var npc = MakeNpc(1.75, 72, 30);

        Assert.Equal(
            BodyMechanic.WorkCapacityMultiplier(world, npc),
            BodyMechanic.WorkCapacityMultiplier(world, npc));
        Assert.Equal(
            BodyMechanic.MovementCostMultiplier(world, npc),
            BodyMechanic.MovementCostMultiplier(world, npc));
    }
}
