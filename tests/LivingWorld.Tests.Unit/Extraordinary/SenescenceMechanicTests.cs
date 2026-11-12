using LivingWorld.Domain.Extraordinary;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Extraordinary.Systems;
using LivingWorld.Simulation.Population.Lifecycle;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Extraordinary;

public sealed class SenescenceMechanicTests
{
    // AD-130: a morte por idade é risco mensal; o NPC tem 99 anos e saúde 0, então sem poder o risco do mês é 1.
    private static void RollMonth(WorldState world, TickContext ctx, Npc npc) =>
        MortalitySystem.RollWindow(world, ctx, npc, world.CurrentDate.TotalHours, world.Calendar.HoursPerMonth);

    [Fact]
    public void Unit_manifested_zero_senescence_does_not_schedule_age_death()
    {
        var (world, npc, ctx) = WorldWithCarrier(seed: 42, npcId: 1, manifested: true, senescence: 0);

        RollMonth(world, ctx, npc);

        Assert.DoesNotContain(world.PendingEvents, evt => evt.SystemName == MortalitySystem.SystemName);
    }

    [Fact]
    public void Unit_npc_without_power_still_schedules_age_death()
    {
        var (world, npc, ctx) = WorldWithCarrier(seed: 42, npcId: 1, manifested: false, senescence: 1, includeCarrier: false);

        RollMonth(world, ctx, npc);

        Assert.Contains(world.PendingEvents, evt => evt.SystemName == MortalitySystem.SystemName);
    }

    [Fact]
    public void Unit_half_senescence_gives_lower_age_death_risk_than_full_rate()
    {
        // Mesma idade de calendário: com metade da senescência a idade biológica é metade, e o risco do mês cai (PWR-20).
        var (fullWorld, fullNpc, _) = WorldWithCarrier(seed: 1, npcId: 1, manifested: true, senescence: 1.0);
        var (halfWorld, halfNpc, _) = WorldWithCarrier(seed: 1, npcId: 1, manifested: true, senescence: 0.5);

        double full = MortalitySystem.WindowRisk(fullWorld, fullNpc, fullWorld.CurrentDate.TotalHours, fullWorld.Calendar.HoursPerMonth);
        double half = MortalitySystem.WindowRisk(halfWorld, halfNpc, halfWorld.CurrentDate.TotalHours, halfWorld.Calendar.HoursPerMonth);

        Assert.True(half < full);
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
        RollMonth(world, ctx, npc);
        var scheduled = Assert.Single(world.PendingEvents, evt => evt.SystemName == MortalitySystem.SystemName);

        world.UpsertExtraordinaryCarrier(Carrier(npc.Id, manifested: true, senescence: 0));
        RollMonth(world, ctx, npc);

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
        npc.SetHealth(0);
        // AD-134: a saúde entra relativa ao teto da idade, então aos 99 com saúde 0 o risco do mês não é mais 1; no teto da
        // tábua (100 anos) continua certo, que é o que estes testes precisam
        world.CurrentDate = WorldDate.Epoch(ScenarioRunner.DefaultCalendar).AddYears(100);
        return (world, npc, new TickContext(world, world.Rng, world.Scheduler));
    }

    private static ExtraordinaryCarrierState Carrier(NpcId id, bool manifested, double senescence) => new(
        id, ["longevity"], manifested, manifested ? "manifested" : "dormant",
        new ExtraordinaryAppearanceState(1, "", ""), null, senescence);

    private static PowerDescriptor Descriptor(string id, double senescence) => new(
        id, "test-source", ["npc.health:0"], "Passive", [], "Guaranteed",
        [], [], [], [], SenescenceRateMultiplier: senescence);
}
