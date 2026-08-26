using LivingWorld.Domain;
using LivingWorld.Domain.Llm;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Extraordinary;

/// <summary>Fase 16.3 T24 (COH-34..36): divergência com/sem capacidade + possessão intocada.</summary>
public sealed class PowerUtilityMigrationTests
{
    private static readonly WorldCalendar Calendar = new(24, 30, 12);
    private static readonly Personality Neutral =
        Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    [Fact]
    public void Agent_with_capability_sees_opportunities_agent_without_does_not()
    {
        var descriptor = new PowerDescriptor(
            "teleport-power", "test", ["npc.teleport:elsewhere"], "Active", [], "Guaranteed",
            [], [], [], []);
        var withCarrier = Build(seed: 7, descriptor, hasCarrier: true);
        var without = Build(seed: 7, descriptor, hasCarrier: false);

        var oppsWith = PowerOpportunityProvider.ApplicableTo(withCarrier.World, withCarrier.Npc, 0);
        var oppsWithout = PowerOpportunityProvider.ApplicableTo(without.World, without.Npc, 0);

        Assert.NotEmpty(oppsWith);
        Assert.Empty(oppsWithout);
        Assert.All(oppsWith, o => Assert.Equal("teleport-power", o.PowerId));
    }

    [Fact]
    public void ControlMechanic_TryDelegatedAction_still_works_after_power_utility_migration()
    {
        var effects = new[] { "control.possess:Sleep" };
        var descriptor = new PowerDescriptor(
            "test-power", "test-source", effects, "Active", [], "Guaranteed",
            [], [], [], []);
        var carrierState = new ExtraordinaryCarrierState(
            new NpcId(1), [descriptor.Id], true, "active",
            new ExtraordinaryAppearanceState(1, "", ""), null, 1);
        var needs = NeedsRules.Create(
            0, 0, 0, 0, 70, 10, false, 0, 0.5).Value!;
        var catalog = ScenarioRunner.DefaultActionCatalog;
        var world = new WorldState(
            Calendar, 9, ScenarioRunner.DefaultMap(9),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            needs, catalog, ScenarioRunner.DefaultLifeStageRules,
            extraordinary: new ExtraordinaryScenarioData(true, [descriptor]),
            extraordinaryCarriers: [carrierState]);

        var carrier = MakeNpc(1, "carrier", ActionType.Work);
        var target = MakeNpc(2, "target", ActionType.Idle);
        world.AddNpc(carrier);
        world.AddNpc(target);

        Assert.True(ExtraordinaryInvocationEngine.Invoke(
            world, new TickContext(world, world.Rng, world.Scheduler),
            new ExtraordinaryInvocation(401, carrier.Id, "test-power", target.Id)).IsSuccess);

        Assert.True(ControlMechanic.IsPossessed(world, target));
        Assert.True(ControlMechanic.TryDelegatedAction(world, target, justCompleted: true, out var delegated));
        Assert.Equal(ActionType.Sleep, delegated);
    }

    private static (WorldState World, Npc Npc) Build(ulong seed, PowerDescriptor descriptor, bool hasCarrier)
    {
        var needs = NeedsRules.Create(0, 0, 0, 0, 70, 10, false, 0, 0.5).Value!;
        ExtraordinaryCarrierState[] carriers = hasCarrier
            ?
            [
                new ExtraordinaryCarrierState(
                    new NpcId(1), [descriptor.Id], true, "active",
                    new ExtraordinaryAppearanceState(1, "", ""), null, 1),
            ]
            : [];
        var world = new WorldState(
            Calendar, seed, ScenarioRunner.DefaultMap(seed),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            needs, ScenarioRunner.DefaultActionCatalog, ScenarioRunner.DefaultLifeStageRules,
            extraordinary: new ExtraordinaryScenarioData(true, [descriptor]),
            extraordinaryCarriers: carriers);
        var npc = MakeNpc(1, "npc", ActionType.Idle);
        world.AddNpc(npc);
        return (world, npc);
    }

    private static Npc MakeNpc(int id, string name, ActionType action) =>
        new(
            new NpcId(id), name, Sex.Male, WorldDate.Epoch(Calendar).AddYears(-30),
            ScenarioRunner.DefaultCulture, new CellCoord(0, 0), null, null,
            new HouseholdId(id), 100, Neutral, ProfessionType.None, new CellCoord(0, 0),
            currentAction: action);
}
