using LivingWorld.Domain.Extraordinary;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Extraordinary.Engine;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Extraordinary.Mechanics;

public sealed class LuckMechanicTests
{
    [Fact]
    public void Capacity_bonus_adds_n_to_resolver_capacity_without_replacing_the_rng_stream()
    {
        var treated = WorldWithBonusAndCheck();
        var treatedExpected = WorldWithBonusAndCheck();
        var control = WorldWithCheckOnly();
        var controlExpected = WorldWithCheckOnly();
        const long invocationId = 210;
        int difficulty = Difficulty(treated.Target, magnitude: 15);
        int baseCapacity = BaseCapacity(treated.Carrier);
        var expectedTreated = Resolver.Resolve(
            difficulty, baseCapacity + 50, VarianceProfile.Dramatico("extraordinary"),
            treatedExpected.World.Rng.Stream(
                $"extraordinary-resolution-{treated.Carrier.Id.Value}-check-power-{invocationId}"));
        var expectedControl = Resolver.Resolve(
            difficulty, baseCapacity, VarianceProfile.Dramatico("extraordinary"),
            controlExpected.World.Rng.Stream(
                $"extraordinary-resolution-{control.Carrier.Id.Value}-check-power-{invocationId}"));

        var treatedResult = ExtraordinaryInvocationEngine.Invoke(
            treated.World, new TickContext(treated.World, treated.World.Rng, treated.World.Scheduler),
            new ExtraordinaryInvocation(invocationId, treated.Carrier.Id, "check-power", treated.Target.Id));
        var controlResult = ExtraordinaryInvocationEngine.Invoke(
            control.World, new TickContext(control.World, control.World.Rng, control.World.Scheduler),
            new ExtraordinaryInvocation(invocationId, control.Carrier.Id, "check-power", control.Target.Id));

        Assert.Equal(expectedTreated, treatedResult.Value?.Resolution ?? ParseFailed(treatedResult.Error));
        Assert.Equal(expectedControl, controlResult.Value?.Resolution ?? ParseFailed(controlResult.Error));
        Assert.Equal(
            new TickContext(treated.World, treated.World.Rng, treated.World.Scheduler)
                .Rng("unrelated-stream").NextDouble(),
            new TickContext(control.World, control.World.Rng, control.World.Scheduler)
                .Rng("unrelated-stream").NextDouble());
    }

    [Fact]
    public void Curse_subtracts_n_from_the_targets_capacity_for_the_declared_tick_window()
    {
        var setup = WorldWithCurseAndCheck("luck.curse:10:100");
        var expectedWorld = WorldWithCurseAndCheck("luck.curse:10:100");
        var ctx = new TickContext(setup.World, setup.World.Rng, setup.World.Scheduler);

        var curse = ExtraordinaryInvocationEngine.Invoke(
            setup.World, ctx,
            new ExtraordinaryInvocation(211, setup.Carrier.Id, "curse-power", setup.Target.Id));

        var cursed = setup.World.ExtraordinaryCarriers.Single(item => item.CarrierId == setup.Target.Id);
        Assert.True(curse.IsSuccess, curse.Error);
        Assert.Equal((10, ctx.CurrentTick + 100), (cursed.LuckCurseAmount, cursed.LuckCurseUntilTick));

        const long invocationId = 212;
        int difficulty = Difficulty(setup.Carrier, magnitude: 15);
        int expectedCapacity = Math.Max(0, BaseCapacity(setup.Target) - 10);
        var expected = Resolver.Resolve(
            difficulty, expectedCapacity, VarianceProfile.Dramatico("extraordinary"),
            expectedWorld.World.Rng.Stream(
                $"extraordinary-resolution-{setup.Target.Id.Value}-check-power-{invocationId}"));

        var check = ExtraordinaryInvocationEngine.Invoke(
            setup.World, ctx,
            new ExtraordinaryInvocation(invocationId, setup.Target.Id, "check-power", setup.Carrier.Id));

        Assert.Equal(expected, check.Value?.Resolution ?? ParseFailed(check.Error));
    }

    [Fact]
    public void Huge_curse_clamps_capacity_at_zero_and_still_resolves()
    {
        var setup = WorldWithCurseAndCheck("luck.curse:9999:100");
        var expectedWorld = WorldWithCurseAndCheck("luck.curse:9999:100");
        var ctx = new TickContext(setup.World, setup.World.Rng, setup.World.Scheduler);
        ExtraordinaryInvocationEngine.Invoke(
            setup.World, ctx,
            new ExtraordinaryInvocation(213, setup.Carrier.Id, "curse-power", setup.Target.Id));

        const long invocationId = 214;
        int difficulty = Difficulty(setup.Carrier, magnitude: 15);
        var expected = Resolver.Resolve(
            difficulty, 0, VarianceProfile.Dramatico("extraordinary"),
            expectedWorld.World.Rng.Stream(
                $"extraordinary-resolution-{setup.Target.Id.Value}-check-power-{invocationId}"));

        var check = ExtraordinaryInvocationEngine.Invoke(
            setup.World, ctx,
            new ExtraordinaryInvocation(invocationId, setup.Target.Id, "check-power", setup.Carrier.Id));

        Assert.Equal(expected, check.Value?.Resolution ?? ParseFailed(check.Error));
    }

    [Fact]
    public void Capacity_bonus_on_the_resolution_target_also_feeds_capacity()
    {
        var luck = Descriptor("luck-power", ["luck.capacity-bonus:10"], "Guaranteed");
        var check = Descriptor("check-power", ["npc.health:15"], "ResolutionCheck");
        var roller = new ExtraordinaryCarrierState(
            new NpcId(1), [check.Id], true, "active",
            new ExtraordinaryAppearanceState(1, "", ""), null, 1);
        var luckyTarget = new ExtraordinaryCarrierState(
            new NpcId(2), [luck.Id], true, "active",
            new ExtraordinaryAppearanceState(1, "", ""), null, 1);
        var treated = World([luck, check], [roller, luckyTarget]);
        var expectedWorld = World([luck, check], [roller, luckyTarget]);
        const long invocationId = 216;
        int difficulty = Difficulty(treated.Target, magnitude: 15);
        var expected = Resolver.Resolve(
            difficulty, BaseCapacity(treated.Carrier) + 10, VarianceProfile.Dramatico("extraordinary"),
            expectedWorld.World.Rng.Stream(
                $"extraordinary-resolution-{treated.Carrier.Id.Value}-check-power-{invocationId}"));

        var result = ExtraordinaryInvocationEngine.Invoke(
            treated.World, new TickContext(treated.World, treated.World.Rng, treated.World.Scheduler),
            new ExtraordinaryInvocation(invocationId, treated.Carrier.Id, "check-power", treated.Target.Id));

        Assert.Equal(expected, result.Value?.Resolution ?? ParseFailed(result.Error));
    }

    [Fact]
    public void Same_seed_reproduces_luck_modified_resolution()
    {
        var first = WorldWithBonusAndCheck();
        var second = WorldWithBonusAndCheck();
        const long invocationId = 215;

        var firstResult = ExtraordinaryInvocationEngine.Invoke(
            first.World, new TickContext(first.World, first.World.Rng, first.World.Scheduler),
            new ExtraordinaryInvocation(invocationId, first.Carrier.Id, "check-power", first.Target.Id));
        var secondResult = ExtraordinaryInvocationEngine.Invoke(
            second.World, new TickContext(second.World, second.World.Rng, second.World.Scheduler),
            new ExtraordinaryInvocation(invocationId, second.Carrier.Id, "check-power", second.Target.Id));

        Assert.Equal(
            (firstResult.IsSuccess, firstResult.Value?.Resolution, firstResult.Error, first.Target.Health),
            (secondResult.IsSuccess, secondResult.Value?.Resolution, secondResult.Error, second.Target.Health));
    }

    private static (WorldState World, Npc Carrier, Npc Target, Household Home) WorldWithBonusAndCheck()
    {
        var luck = Descriptor("luck-power", ["luck.capacity-bonus:50"], "Guaranteed");
        var check = Descriptor("check-power", ["npc.health:15"], "ResolutionCheck");
        var state = new ExtraordinaryCarrierState(
            new NpcId(1), [luck.Id, check.Id], true, "active",
            new ExtraordinaryAppearanceState(1, "", ""), null, 1);
        return World([luck, check], [state]);
    }

    private static (WorldState World, Npc Carrier, Npc Target, Household Home) WorldWithCheckOnly()
    {
        var check = Descriptor("check-power", ["npc.health:15"], "ResolutionCheck");
        var state = new ExtraordinaryCarrierState(
            new NpcId(1), [check.Id], true, "active",
            new ExtraordinaryAppearanceState(1, "", ""), null, 1);
        return World([check], [state]);
    }

    private static (WorldState World, Npc Carrier, Npc Target, Household Home) WorldWithCurseAndCheck(
        string curseToken)
    {
        var curse = Descriptor("curse-power", [curseToken], "Guaranteed");
        var check = Descriptor("check-power", ["npc.health:15"], "ResolutionCheck");
        var curser = new ExtraordinaryCarrierState(
            new NpcId(1), [curse.Id], true, "active",
            new ExtraordinaryAppearanceState(1, "", ""), null, 1);
        var cursed = new ExtraordinaryCarrierState(
            new NpcId(2), [check.Id], true, "active",
            new ExtraordinaryAppearanceState(1, "", ""), null, 1);
        return World([curse, check], [curser, cursed]);
    }

    private static (WorldState World, Npc Carrier, Npc Target, Household Home) World(
        IReadOnlyList<PowerDescriptor> descriptors, IReadOnlyList<ExtraordinaryCarrierState> carriers)
    {
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, 42, ScenarioRunner.DefaultMap(42),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules,
            extraordinary: new ExtraordinaryScenarioData(true, descriptors), extraordinaryCarriers: carriers);
        var carrier = Npc(new NpcId(1), "carrier", 100);
        var target = Npc(new NpcId(2), "target", 50);
        var home = new Household(new HouseholdId(1), new CellCoord(0, 0), carrier.Id, [carrier.Id],
            new Dictionary<ResourceType, long> { [new ResourceType(9)] = 5 });
        world.AddNpc(carrier);
        world.AddNpc(target);
        world.AddHousehold(home);
        return (world, carrier, target, home);
    }

    private static PowerDescriptor Descriptor(string id, IReadOnlyList<string> effects, string reliability) =>
        new(id, "test-source", effects, "Active", [], reliability,
            reliability == "ResolutionCheck" ? ["no-effect"] : [], [], [], []);

    private static Npc Npc(NpcId id, string name, int health) => new(
        id, name, Sex.Male, WorldDate.Epoch(ScenarioRunner.DefaultCalendar),
        ScenarioRunner.DefaultCulture, new CellCoord(0, 0), motherId: null, fatherId: null,
        household: id == new NpcId(1) ? new HouseholdId(1) : null, health: health,
        personality: Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!,
        profession: ProfessionType.None, currentLocation: new CellCoord(0, 0));

    private static int Difficulty(Npc target, int magnitude) =>
        10 + (int)Math.Ceiling(magnitude / 10d) + Math.Clamp((100 - target.Health) / 20, 0, 5);

    private static int BaseCapacity(Npc carrier) =>
        (int)Math.Clamp(Math.Round(carrier.Vitality / 10d + carrier.RateGene.Value * 5d), 0, 20);

    private static ResolutionResult ParseFailed(string? error) => error switch
    {
        "resolution:Failure" => ResolutionResult.Failure,
        "resolution:CriticalFailure" => ResolutionResult.CriticalFailure,
        _ => throw new Xunit.Sdk.XunitException($"Resultado inesperado: {error}"),
    };
}
