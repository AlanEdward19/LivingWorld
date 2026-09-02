using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Extraordinary;

public sealed class SenescenceMechanicTests
{
    [Fact]
    public void Unit_manifested_zero_senescence_does_not_schedule_age_death()
    {
        var (world, npc, ctx) = WorldWithCarrier(seed: 42, npcId: 1, manifested: true, senescence: 0);

        MortalitySystem.SchedulePlannedDeath(world, ctx, npc);

        Assert.DoesNotContain(
            world.PendingEvents,
            evt => evt.SystemName == MortalitySystem.SystemName && evt.Payload == npc.Id.Value.ToString());
    }

    [Fact]
    public void Unit_npc_without_power_still_schedules_age_death()
    {
        var (world, npc, ctx) = WorldWithCarrier(seed: 42, npcId: 1, manifested: false, senescence: 1, includeCarrier: false);

        MortalitySystem.SchedulePlannedDeath(world, ctx, npc);

        Assert.Contains(
            world.PendingEvents,
            evt => evt.SystemName == MortalitySystem.SystemName && evt.Payload == npc.Id.Value.ToString());
    }

    [Fact]
    public void Unit_half_senescence_schedules_later_death_than_full_rate_for_same_seed()
    {
        // Infant mortality can kill both at calendar year 0 for a single unlucky seed;
        // each pair still shares seed/health, and slower biological aging must delay the
        // scheduled tick in aggregate (PWR-20).
        long fullSum = 0, halfSum = 0;
        for (ulong seed = 1; seed <= 80; seed++)
        {
            var (fullWorld, fullNpc, fullCtx) = WorldWithCarrier(seed, npcId: 1, manifested: true, senescence: 1.0);
            var (halfWorld, halfNpc, halfCtx) = WorldWithCarrier(seed, npcId: 1, manifested: true, senescence: 0.5);
            MortalitySystem.SchedulePlannedDeath(fullWorld, fullCtx, fullNpc);
            MortalitySystem.SchedulePlannedDeath(halfWorld, halfCtx, halfNpc);
            fullSum += fullWorld.PendingEvents.Single(evt => evt.SystemName == MortalitySystem.SystemName).TargetTick;
            halfSum += halfWorld.PendingEvents.Single(evt => evt.SystemName == MortalitySystem.SystemName).TargetTick;
        }

        Assert.True(halfSum > fullSum);
    }

    [Fact]
    public void Two_manifested_powers_keep_the_minimum_senescence_multiplier()
    {
        var slower = Descriptor("slower", 0.25);
        var slow = Descriptor("slow", 0.8);
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, 42, ScenarioRunner.DefaultMap(42),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules,
            extraordinary: new ExtraordinaryScenarioData(true, [slow, slower]));
        var npc = new Npc(
            new NpcId(1), "npc", Sex.Male, WorldDate.Epoch(ScenarioRunner.DefaultCalendar),
            ScenarioRunner.DefaultCulture, new CellCoord(0, 0), motherId: null, fatherId: null,
            household: null, health: 100,
            personality: Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!,
            profession: ProfessionType.None, currentLocation: new CellCoord(0, 0));
        world.AddNpc(npc);

        var resolved = ExtraordinaryStateSystem.Resolve(world, npc, [slow.Id, slower.Id]);

        Assert.Equal(0.25, resolved.SenescenceRateMultiplier);
    }

    [Fact]
    public void Unit_already_scheduled_age_death_is_not_rewritten_when_senescence_later_becomes_zero()
    {
        var (world, npc, ctx) = WorldWithCarrier(seed: 42, npcId: 1, manifested: true, senescence: 1.0);
        MortalitySystem.SchedulePlannedDeath(world, ctx, npc);
        var scheduled = Assert.Single(world.PendingEvents, evt => evt.SystemName == MortalitySystem.SystemName);

        world.UpsertExtraordinaryCarrier(Carrier(npc.Id, manifested: true, senescence: 0));
        MortalitySystem.SchedulePlannedDeath(world, ctx, npc);

        var still = Assert.Single(world.PendingEvents, evt => evt.SystemName == MortalitySystem.SystemName);
        Assert.Equal((scheduled.Id, scheduled.TargetTick), (still.Id, still.TargetTick));
    }

    private static (WorldState World, Npc Npc, TickContext Ctx) WorldWithCarrier(
        ulong seed, long npcId, bool manifested, double senescence, bool includeCarrier = true)
    {
        var descriptor = new PowerDescriptor(
            "longevity", "test-source", ["npc.health:0"], "Passive", [], "Guaranteed",
            [], [], [], [], SenescenceRateMultiplier: senescence);
        ExtraordinaryCarrierState[] carriers = includeCarrier
            ? [Carrier(new NpcId(npcId), manifested, senescence)]
            : [];
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, seed, ScenarioRunner.DefaultMap(seed),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules,
            extraordinary: new ExtraordinaryScenarioData(true, [descriptor]),
            extraordinaryCarriers: carriers);
        var npc = new Npc(
            new NpcId(npcId), "npc", Sex.Male, WorldDate.Epoch(ScenarioRunner.DefaultCalendar),
            ScenarioRunner.DefaultCulture, new CellCoord(0, 0), motherId: null, fatherId: null,
            household: null, health: 100,
            personality: Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!,
            profession: ProfessionType.None, currentLocation: new CellCoord(0, 0));
        world.AddNpc(npc);
        return (world, npc, new TickContext(world, world.Rng, world.Scheduler));
    }

    private static ExtraordinaryCarrierState Carrier(NpcId id, bool manifested, double senescence) => new(
        id, ["longevity"], manifested, manifested ? "manifested" : "dormant",
        new ExtraordinaryAppearanceState(1, "", ""), null, senescence);

    private static PowerDescriptor Descriptor(string id, double senescence) => new(
        id, "test-source", ["npc.health:0"], "Passive", [], "Guaranteed",
        [], [], [], [], SenescenceRateMultiplier: senescence);
}
