using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Population.Lifecycle;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Population.Lifecycle;

/// <summary>Fase 7, T9 (FAM-21) e AD-130: a morte por idade é risco contínuo pelo estado atual. <c>Vitality</c>
/// (<see cref="FamilyRules.EffectiveVitalityMultiplier"/>), saúde e prematuridade mudam o risco do mês; nenhuma data de morte
/// é decidida no nascimento.</summary>
public class MortalitySystemTests
{
    private static readonly WorldCalendar Calendar = new(24, 30, 12);

    private static readonly Personality SomePersonality =
        Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    private static IReadOnlyDictionary<(RelationshipEventType, RelationshipAxis), double> FullDeltas()
    {
        var deltas = new Dictionary<(RelationshipEventType, RelationshipAxis), double>();
        foreach (var type in Enum.GetValues<RelationshipEventType>())
            foreach (var axis in Enum.GetValues<RelationshipAxis>())
                deltas[(type, axis)] = 0.0;
        return deltas;
    }

    // FamilyRules com VitalityMortalityWeight != 0 — o único parâmetro que este teste exercita;
    // o resto são valores mínimos válidos (mesmo espírito de CreateValid em FamilyRulesTests).
    private static readonly FamilyRules Rules = FamilyRules.Create(
        relationshipDeltas: FullDeltas(),
        decayPerDay: 0.5,
        contactLossThresholdDays: 30,
        neutralAxisValue: 50,
        attractionWeights: Enum.GetValues<AttractionFactor>().ToDictionary(f => f, _ => 1.0),
        courtshipThreshold: 0.6,
        courtshipDurationDays: 90,
        marriageInitialStock: new Dictionary<int, long> { [1] = 100 },
        conceptionHealthFloor: 40,
        conceptionRelationshipFloor: 40,
        conceptionResourceFloor: new Dictionary<int, long> { [1] = 10 },
        maternalDeathRisk: 0.02,
        infantDeathRisk: 0.05,
        vitalityMotherWeight: 0.5,
        vitalityFatherWeight: 0.5,
        vitalityMutationStdDev: 5,
        vitalityMortalityWeight: 0.6,
        upbringingWealthWeight: 0.3,
        environmentalWealthChannelEnabled: false,
        neutralDriftEnabled: false,
        vitalityMortalitySelectionEnabled: true).Value!;

    private static WorldState BuildWorld(ulong seed) => new(
        Calendar, seed, ScenarioRunner.DefaultMap(1), ScenarioRunner.DefaultPopulationCatalog,
        ScenarioRunner.DefaultPopulationRules, ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
        ScenarioRunner.DefaultLifeStageRules, familyRules: Rules);

    private static Npc MakeNpc(WorldState world, double vitality) => new(
        world.NextNpcIdAndAdvance(), "npc", Sex.Male, WorldDate.Epoch(Calendar), new CultureId(1),
        new CellCoord(0, 0), motherId: null, fatherId: null, household: null, health: 100,
        personality: SomePersonality, profession: new ProfessionType(1), currentLocation: new CellCoord(0, 0),
        vitality: vitality);

    private static double MonthRisk(WorldState world, Npc npc) =>
        MortalitySystem.WindowRisk(world, npc, world.CurrentDate.TotalHours, world.Calendar.HoursPerMonth);

    [Fact]
    public void Higher_vitality_gives_lower_monthly_death_risk_than_lower_vitality_at_the_same_age()
    {
        var world = BuildWorld(1);
        world.CurrentDate = WorldDate.Epoch(Calendar).AddYears(50);

        Assert.True(MonthRisk(world, MakeNpc(world, vitality: 100)) < MonthRisk(world, MakeNpc(world, vitality: 0)));
    }

    [Fact]
    public void Poor_current_health_raises_the_monthly_death_risk()
    {
        var world = BuildWorld(2);
        world.CurrentDate = WorldDate.Epoch(Calendar).AddYears(30);
        var healthy = MakeNpc(world, vitality: 50);
        var sick = MakeNpc(world, vitality: 50);
        sick.SetHealth(20);

        Assert.True(MonthRisk(world, sick) > MonthRisk(world, healthy));
    }

    [Fact]
    public void Twelve_monthly_risks_compound_to_the_annual_table_value()
    {
        var table = ScenarioRunner.DefaultPopulationRules.LifeTable;
        double monthly = table.PeriodMortality(40, 100, 1.0, 1.0 / 12);
        double survival = 1.0;
        for (int m = 0; m < 12; m++) survival *= 1 - monthly;

        Assert.Equal(table.AnnualMortality(40, 100), 1 - survival, 12);
    }

    [Fact]
    public void Creating_the_village_does_not_plan_a_death_date_for_every_npc()
    {
        var (world, _) = ScenarioRunner.Create(seed: 42);
        int planned = world.PendingEvents.Count(e => e.SystemName == MortalitySystem.SystemName);

        // antes (AD-130) cada NPC nascia com um evento de morte marcado: planned == população
        Assert.True(planned * 10 < world.AliveNpcIndex.Alive.Count, $"{planned} mortes marcadas para {world.AliveNpcIndex.Alive.Count} vivos");
    }

    [Fact]
    public void Preterm_newborn_has_higher_first_year_risk_than_a_term_newborn()
    {
        var world = BuildWorld(3);
        var term = MakeNpc(world, vitality: 50);
        var preterm = MakeNpc(world, vitality: 50);
        term.SetGestationAtBirth(268);
        preterm.SetGestationAtBirth(200);

        Assert.True(MonthRisk(world, preterm) > MonthRisk(world, term));
    }

    [Fact]
    public void A_death_drawn_for_the_month_is_scheduled_at_an_hour_inside_that_month()
    {
        var world = BuildWorld(4);
        world.CurrentDate = WorldDate.Epoch(Calendar).AddYears(99).AddHours(5 * 24);
        var ancient = MakeNpc(world, vitality: 0); // aos 99 e sem saúde o risco do mês é 1
        ancient.SetHealth(0);
        world.AddNpc(ancient);
        var ctx = new TickContext(world, world.Rng, world.Scheduler);

        MortalitySystem.RollWindow(world, ctx, ancient, world.CurrentDate.TotalHours, world.Calendar.HoursPerMonth);

        var death = Assert.Single(world.PendingEvents, e => e.SystemName == MortalitySystem.SystemName);
        Assert.InRange(death.TargetTick, world.CurrentDate.TotalHours + 1, world.CurrentDate.TotalHours + world.Calendar.HoursPerMonth);
    }
}
