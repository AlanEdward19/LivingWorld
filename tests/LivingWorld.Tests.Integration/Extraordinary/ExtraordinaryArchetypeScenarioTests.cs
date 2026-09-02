using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Extraordinary;

public sealed class ExtraordinaryArchetypeScenarioTests
{
    public static TheoryData<ArchetypeCase> Cases => new()
    {
        Case("Vampiro", "predatory-exposure", "Conditional", "world:is-night", ActionType.Idle,
            "pale", "mist", 1.05, "sunlight", "night-change", 0,
            new NeedSubstitutionDescriptor("hunger", new ResourceType(9), 2), [], [], false, 1, 0),
        Case("Lobisomem", "inherited-curse", "Conditional", "world:tick-cycle:672:0:24",
            ActionType.Idle, "fur", "dust", 1.4, "silver", "lunar-change", 1, null,
            ["carrier.sleep:5"], [], false, 1, 0),
        Case("Lanterna Verde", "external-artifact", "Active", "carrier:action:Work",
            ActionType.Work, "green-glow", "green-energy", 1, "artifact-removed", "green-aura", 1,
            null, ["carrier.sleep:10"],
            ["movement.flight:1", "construct.create:2x1:40:24:green-energy"], true, 1, 1),
        Case("Kryptoniano", "stellar-origin", "Passive", null, ActionType.Idle,
            "sun-charged", "air-ripple", 1.1, "specific-radiation", "solar-aura", 0.2, null, [],
            ["movement.flight:1", "movement.speed-multiplier:2"], true, 2, 0),
        Case("Velocista", "energy-accident", "Conditional", "carrier:action:Travel",
            ActionType.Travel, "charged", "electricity", 1, "energy-drain", "speed-aura", 1,
            null, ["carrier.sleep:15"], ["movement.speed-multiplier:4"], false, 4, 0),
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Fixed_archetype_acquires_manifests_and_invokes_through_generic_engine(ArchetypeCase item)
    {
        var world = World(item.Descriptor);
        var carrier = AddNpc(world, 1, item.Action, health: 100);
        var target = AddNpc(world, 2, ActionType.Idle, health: 50);
        new TickContext(world, world.Rng, world.Scheduler).ScheduleEvent(
            1, ExtraordinaryStateSystem.SystemName,
            $"acquire|1|{item.Descriptor.Id}|{item.Trigger}");

        new WorldClock([new ExtraordinaryStateSystem()]).Tick(world);

        var state = Assert.Single(world.ExtraordinaryCarriers);
        Assert.Equal(
            (carrier.Id, item.Descriptor.Id, true, item.Scale, item.Tint, item.Trail,
                item.Need?.ReplacesNeed, item.Need?.Resource.Id, item.Need?.UnitsPerUse,
                item.Descriptor.SenescenceRateMultiplier),
            (state.CarrierId, Assert.Single(state.PowerIds), state.IsManifested,
                state.Appearance.ScaleMultiplier, state.Appearance.SkinTint, state.Appearance.MovementTrail,
                state.NeedSubstitution?.ReplacesNeed, state.NeedSubstitution?.Resource.Id,
                state.NeedSubstitution?.UnitsPerUse, state.SenescenceRateMultiplier));
        Assert.Equal(item.Source, item.Descriptor.Source);
        Assert.Equal(item.Vulnerability, Assert.Single(item.Descriptor.IntrinsicVulnerabilities));
        Assert.Equal(item.Manifestation, Assert.Single(item.Descriptor.Manifestations));
        var locomotion = ExtraordinaryLocomotion.Resolve(world, carrier);
        Assert.Equal((item.CanFly, item.SpeedMultiplier),
            (locomotion.CanFly, locomotion.SpeedMultiplier));

        int sleepBefore = carrier.SleepAt(world.CurrentDate.TotalHours);
        var result = ExtraordinaryInvocationEngine.Invoke(
            world, new TickContext(world, world.Rng, world.Scheduler),
            new ExtraordinaryInvocation(
                100, carrier.Id, item.Descriptor.Id, target.Id,
                Origin: item.Descriptor.Mode == "Passive"
                    ? ExtraordinaryInvocationOrigin.Triggered
                    : ExtraordinaryInvocationOrigin.Authored));

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(60, target.Health);
        Assert.Equal(sleepBefore - item.SleepCost, carrier.SleepAt(world.CurrentDate.TotalHours));
        Assert.Equal(item.ExpectedConstructs, world.ExtraordinaryConstructs.Count);
    }

    private static ArchetypeCase Case(
        string id, string source, string mode, string? condition, ActionType action,
        string tint, string trail, double scale, string vulnerability, string manifestation,
        double senescence, NeedSubstitutionDescriptor? need, IReadOnlyList<string> costs,
        IReadOnlyList<string> passiveEffects, bool canFly, double speedMultiplier, int expectedConstructs)
    {
        string trigger = $"awaken-{id.Replace(' ', '-').ToLowerInvariant()}";
        var descriptor = new PowerDescriptor(
            id, source, ["npc.health:10", .. passiveEffects], mode, costs, "Guaranteed", [], [vulnerability],
            [manifestation], [$"event:{trigger}"],
            new ExtraordinaryAppearanceDescriptor(scale, tint, trail), need, senescence, condition);
        int sleepCost = costs.Count == 0 ? 0 : int.Parse(costs[0].Split(':')[^1]);
        return new ArchetypeCase(
            descriptor, trigger, action, source, tint, trail, scale, vulnerability, manifestation,
            need, sleepCost, canFly, speedMultiplier, expectedConstructs);
    }

    private static WorldState World(PowerDescriptor descriptor) => new(
        ScenarioRunner.DefaultCalendar, 42, ScenarioRunner.DefaultMap(42),
        ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
        ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
        ScenarioRunner.DefaultLifeStageRules,
        extraordinary: new ExtraordinaryScenarioData(true, [descriptor]));

    private static Npc AddNpc(WorldState world, long id, ActionType action, int health)
    {
        var npc = new Npc(
            new NpcId(id), $"npc-{id}", Sex.Male, WorldDate.Epoch(ScenarioRunner.DefaultCalendar),
            ScenarioRunner.DefaultCulture, new CellCoord(0, 0), null, null, null, health,
            Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!,
            ProfessionType.None, currentLocation: new CellCoord(0, 0));
        npc.SetCurrentAction(action, 0);
        world.AddNpc(npc);
        return npc;
    }

    public sealed record ArchetypeCase(
        PowerDescriptor Descriptor,
        string Trigger,
        ActionType Action,
        string Source,
        string Tint,
        string Trail,
        double Scale,
        string Vulnerability,
        string Manifestation,
        NeedSubstitutionDescriptor? Need,
        int SleepCost,
        bool CanFly,
        double SpeedMultiplier,
        int ExpectedConstructs);
}
