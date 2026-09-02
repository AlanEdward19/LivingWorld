using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Needs;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Extraordinary.Mechanics;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Behavior.Needs;

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
        Assert.Equal(
            BodyMechanic.CombatOffenseMultiplier(world, npc),
            BodyMechanic.WorkCapacityMultiplier(world, npc));
        Assert.Equal(
            BodyMechanic.CombatDamageTakenMultiplier(world, npc),
            BodyMechanic.CombatDamageTakenMultiplier(world, npc));
    }

    [Fact]
    public void CombatOffenseMultiplier_tracks_MuscleMass_like_WorkCapacity()
    {
        var world = BuildWorld();
        var weak = MakeNpc(1.70, 68, muscleMass: 15);
        var strong = MakeNpc(1.70, 68, muscleMass: 45);

        Assert.Equal(
            BodyMechanic.WorkCapacityMultiplier(world, strong),
            BodyMechanic.CombatOffenseMultiplier(world, strong));
        Assert.True(
            BodyMechanic.CombatOffenseMultiplier(world, strong)
            > BodyMechanic.CombatOffenseMultiplier(world, weak));
    }

    [Fact]
    public void CombatDamageTakenMultiplier_falls_as_Weight_and_Height_rise()
    {
        var world = BuildWorld();
        var light = MakeNpc(height: 1.55, weight: 50, muscleMass: 28);
        var heavy = MakeNpc(height: 1.90, weight: 95, muscleMass: 28);

        double lightTaken = BodyMechanic.CombatDamageTakenMultiplier(world, light);
        double heavyTaken = BodyMechanic.CombatDamageTakenMultiplier(world, heavy);

        Assert.True(heavyTaken < lightTaken);
        Assert.True(heavyTaken < 1.0);
        Assert.True(lightTaken > 1.0);
    }

    [Fact]
    public void CombatDamageTakenMultiplier_is_neutral_when_BodyRules_disabled()
    {
        var world = BuildWorld(BodyRules.Disabled);
        var npc = MakeNpc(1.9, 90, 50);

        Assert.Equal(1.0, BodyMechanic.CombatDamageTakenMultiplier(world, npc));
        Assert.Equal(1.0, BodyMechanic.CombatOffenseMultiplier(world, npc));
    }

    [Fact]
    public void ApplyBodyToDamage_scales_raw_damage_by_target_body()
    {
        var world = BuildWorld();
        var light = MakeNpc(height: 1.55, weight: 50, muscleMass: 28);
        var heavy = MakeNpc(height: 1.90, weight: 95, muscleMass: 28);
        const int raw = 20;

        int lightDmg = CombatMechanic.ApplyBodyToDamage(world, light, raw);
        int heavyDmg = CombatMechanic.ApplyBodyToDamage(world, heavy, raw);

        Assert.True(heavyDmg < lightDmg);
        Assert.Equal(0, CombatMechanic.ApplyBodyToDamage(world, heavy, 0));
    }

    [Fact]
    public void ApplyWorkHardening_increases_MuscleMass_from_baseline()
    {
        var world = BuildWorld();
        var npc = MakeNpc(1.70, 68, muscleMass: 20);
        double baseline = npc.MuscleMass;

        BodyMechanic.ApplyWorkHardening(world, npc);

        Assert.True(npc.MuscleMass > baseline);
        Assert.Equal(baseline + BodyMechanic.DailyWorkHardeningDelta, npc.MuscleMass, precision: 10);
    }

    [Fact]
    public void ApplyWorkHardening_clamps_at_MuscleMassMax()
    {
        var rules = BodyRules.Default;
        var world = BuildWorld(rules);
        var npc = MakeNpc(1.70, 68, muscleMass: rules.MuscleMassMax - 0.01);

        BodyMechanic.ApplyWorkHardening(world, npc);
        BodyMechanic.ApplyWorkHardening(world, npc);
        BodyMechanic.ApplyWorkHardening(world, npc);

        Assert.Equal(rules.MuscleMassMax, npc.MuscleMass);
    }

    [Fact]
    public void ApplyWorkHardening_is_noop_when_BodyRules_disabled()
    {
        var world = BuildWorld(BodyRules.Disabled);
        var npc = MakeNpc(1.70, 68, muscleMass: 20);

        BodyMechanic.ApplyWorkHardening(world, npc);

        Assert.Equal(20, npc.MuscleMass);
    }

    [Fact]
    public void WorkHardeningSystem_grows_MuscleMass_after_sustained_heavy_labor_days()
    {
        var world = BuildWorld();
        var location = new CellCoord(1, 1);
        var npc = MakeNpc(1.70, 68, muscleMass: 20);
        world.AddNpc(npc);
        var workplace = new Workplace(
            world.NextWorkplaceIdAndAdvance(), new LocationType(1), location, maxVacancies: 1,
            employees: [], stock: new Dictionary<ResourceType, long>(), treasury: Money.Zero,
            prices: new Dictionary<ResourceType, long>());
        world.AddWorkplace(workplace);
        workplace.Hire(npc.Id);
        npc.Hire(workplace.Id);
        npc.MoveTo(location, 0);
        npc.SetCurrentAction(ActionType.Work, 0);

        double baseline = npc.MuscleMass;
        var system = new WorkHardeningSystem();
        var ctx = new TickContext(world, world.Rng, world.Scheduler);

        for (int day = 0; day < 10; day++)
            system.Tick(world, ctx);

        Assert.True(npc.MuscleMass > baseline);
        Assert.Equal(baseline + 10 * BodyMechanic.DailyWorkHardeningDelta, npc.MuscleMass, precision: 10);
    }
}
