using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Extraordinary;

public sealed class ExtraordinaryPowerStageSystemTests
{
    private static readonly WorldCalendar Calendar = ScenarioRunner.DefaultCalendar;

    [Fact]
    public void Before_first_threshold_invocation_uses_stage_zero_baseline_effects()
    {
        var (world, carrier, target) = StagedWorld(
            baseline: ["npc.health:5"],
            stages:
            [
                new PowerEvolutionStage(18, null, ["npc.health:20"]),
                new PowerEvolutionStage(null, 5, ["npc.health:30"]),
                new PowerEvolutionStage(18, 5, ["npc.health:40"]),
            ],
            carrierAgeYears: 10,
            useCount: 0);

        var result = Invoke(world, carrier, target);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(55, target.Health);
    }

    [Fact]
    public void Age_only_stage_applies_when_age_threshold_is_met()
    {
        var (world, carrier, target) = StagedWorld(
            baseline: ["npc.health:5"],
            stages: [new PowerEvolutionStage(18, null, ["npc.health:20"])],
            carrierAgeYears: 18,
            useCount: 0);

        var result = Invoke(world, carrier, target);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(70, target.Health);
    }

    [Fact]
    public void Use_only_stage_applies_after_enough_successful_uses()
    {
        var (world, carrier, target) = StagedWorld(
            baseline: ["npc.health:5"],
            stages: [new PowerEvolutionStage(null, 5, ["npc.health:30"])],
            carrierAgeYears: 10,
            useCount: 5);

        var result = Invoke(world, carrier, target);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(80, target.Health);
    }

    [Fact]
    public void Combined_age_and_use_stage_requires_both_thresholds_strictly()
    {
        var (world, carrier, target) = StagedWorld(
            baseline: ["npc.health:5"],
            stages:
            [
                new PowerEvolutionStage(18, null, ["npc.health:20"]),
                new PowerEvolutionStage(18, 5, ["npc.health:40"]),
            ],
            carrierAgeYears: 18,
            useCount: 4);

        var tooFewUses = Invoke(world, carrier, target);
        Assert.True(tooFewUses.IsSuccess, tooFewUses.Error);
        Assert.Equal(70, target.Health);

        target.SetHealth(50);
        world.UpsertExtraordinaryCarrier(
            world.ExtraordinaryCarriers.Single(item => item.CarrierId == carrier.Id) with { UseCount = 5 });

        var bothMet = Invoke(world, carrier, target);
        Assert.True(bothMet.IsSuccess, bothMet.Error);
        Assert.Equal(90, target.Health);
    }

    [Fact]
    public void Highest_reached_stage_wins_and_never_applies_a_future_stage()
    {
        var (world, carrier, target) = StagedWorld(
            baseline: ["npc.health:5"],
            stages:
            [
                new PowerEvolutionStage(10, null, ["npc.health:15"]),
                new PowerEvolutionStage(20, null, ["npc.health:25"]),
                new PowerEvolutionStage(30, null, ["npc.health:35"]),
            ],
            carrierAgeYears: 22,
            useCount: 0);

        var result = Invoke(world, carrier, target);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(75, target.Health);
    }

    [Fact]
    public void Same_seed_history_and_age_produce_identical_current_stage_index()
    {
        var first = RunDeterministicStageScenario();
        var second = RunDeterministicStageScenario();

        Assert.Equal(first.StageIndex, second.StageIndex);
        Assert.Equal(first.TargetHealth, second.TargetHealth);
    }

    [Fact]
    public void Hourly_tick_updates_current_stage_index_cache()
    {
        var (world, carrier, target) = StagedWorld(
            baseline: ["npc.health:5"],
            stages: [new PowerEvolutionStage(18, null, ["npc.health:20"])],
            carrierAgeYears: 10,
            useCount: 0);

        new WorldClock([new ExtraordinaryPowerStageSystem()]).Tick(world);
        Assert.Equal(0, Carrier(world).CurrentStageIndex);

        var agedCarrier = CarrierNpc(new NpcId(1), 18);
        world.RemoveNpc(carrier.Id);
        world.AddNpc(agedCarrier);

        new WorldClock([new ExtraordinaryPowerStageSystem()]).Tick(world);
        Assert.Equal(1, Carrier(world).CurrentStageIndex);
        _ = target;
    }

    private static (int StageIndex, int TargetHealth) RunDeterministicStageScenario()
    {
        var (world, carrier, target) = StagedWorld(
            baseline: ["npc.health:5"],
            stages:
            [
                new PowerEvolutionStage(null, 3, ["npc.health:15"]),
                new PowerEvolutionStage(18, 5, ["npc.health:25"]),
            ],
            carrierAgeYears: 18,
            useCount: 5);

        Invoke(world, carrier, target);
        new WorldClock([new ExtraordinaryPowerStageSystem()]).Tick(world);
        return (Carrier(world).CurrentStageIndex, target.Health);
    }

    private static Result<ExtraordinaryInvocationResult> Invoke(WorldState world, Npc carrier, Npc target) =>
        ExtraordinaryInvocationEngine.Invoke(
            world,
            new TickContext(world, world.Rng, world.Scheduler),
            new ExtraordinaryInvocation(1, carrier.Id, "test-power", target.Id));

    private static ExtraordinaryCarrierState Carrier(WorldState world) =>
        world.ExtraordinaryCarriers.Single(item => item.CarrierId == new NpcId(1));

    private static (WorldState World, Npc Carrier, Npc Target) StagedWorld(
        IReadOnlyList<string> baseline,
        IReadOnlyList<PowerEvolutionStage> stages,
        int carrierAgeYears,
        int useCount)
    {
        var descriptor = new PowerDescriptor(
            "test-power", "test-source", baseline, "Active", [], "Guaranteed", [], [], [], [],
            Stages: stages);
        var carrier = CarrierNpc(new NpcId(1), carrierAgeYears);
        var target = TargetNpc();
        var state = new ExtraordinaryCarrierState(
            carrier.Id, [descriptor.Id], true, "active",
            new ExtraordinaryAppearanceState(1, "", ""), null, 1,
            UseCount: useCount);
        var world = new WorldState(
            Calendar, 42, ScenarioRunner.DefaultMap(42),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules,
            extraordinary: new ExtraordinaryScenarioData(true, [descriptor]),
            extraordinaryCarriers: [state]);
        world.AddNpc(carrier);
        world.AddNpc(target);
        return (world, carrier, target);
    }

    private static Npc CarrierNpc(NpcId id, int ageYears)
    {
        var birth = WorldDate.Epoch(Calendar).AddYears(-ageYears);
        return new Npc(
            id, "carrier", Sex.Male, birth, ScenarioRunner.DefaultCulture, new CellCoord(0, 0),
            motherId: null, fatherId: null, household: new HouseholdId(1), health: 100,
            personality: Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!,
            profession: ProfessionType.None, currentLocation: new CellCoord(0, 0));
    }

    private static Npc TargetNpc() => new(
        new NpcId(2), "target", Sex.Male, WorldDate.Epoch(Calendar),
        ScenarioRunner.DefaultCulture, new CellCoord(0, 0), motherId: null, fatherId: null,
        household: null, health: 50,
        personality: Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!,
        profession: ProfessionType.None, currentLocation: new CellCoord(0, 0));
}
