using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Population;

/// <summary>Fase 7, T9 (FAM-21): <see cref="MortalitySystem.SchedulePlannedDeath"/> passa o
/// multiplicador de <c>Vitality</c> (<see cref="FamilyRules.EffectiveVitalityMultiplier"/>) para
/// <see cref="MortalityPlanner.RollDeathAge"/> — NPCs com <c>Vitality</c> diferente (mesma
/// idade/saúde) devem ter distribuição de idade de morte diferente.</summary>
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
        neutralDriftEnabled: false).Value!;

    private static WorldState BuildWorld(ulong seed) => new(
        Calendar, seed, ScenarioRunner.DefaultMap(1), ScenarioRunner.DefaultPopulationCatalog,
        ScenarioRunner.DefaultPopulationRules, ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
        ScenarioRunner.DefaultLifeStageRules, familyRules: Rules);

    private static Npc MakeNpc(WorldState world, double vitality) => new(
        world.NextNpcIdAndAdvance(), "npc", Sex.Male, WorldDate.Epoch(Calendar), new CultureId(1),
        new CellCoord(0, 0), motherId: null, fatherId: null, household: null, health: 100,
        personality: SomePersonality, profession: new ProfessionType(1), currentLocation: new CellCoord(0, 0),
        vitality: vitality);

    private static long ScheduledDeathTick(WorldState world, TickContext ctx, Npc npc)
    {
        MortalitySystem.SchedulePlannedDeath(world, ctx, npc);
        return world.PendingEvents.Single(e => e.Payload == npc.Id.Value.ToString()).TargetTick;
    }

    [Fact]
    public void Higher_vitality_produces_later_average_scheduled_death_than_lower_vitality_across_seeds()
    {
        long highVitalitySum = 0, lowVitalitySum = 0;

        for (ulong seed = 1; seed <= 200; seed++)
        {
            var world = BuildWorld(seed);
            var ctx = new TickContext(world, world.Rng, world.Scheduler);

            var highVitality = MakeNpc(world, vitality: 100);
            var lowVitality = MakeNpc(world, vitality: 0);

            highVitalitySum += ScheduledDeathTick(world, ctx, highVitality);
            lowVitalitySum += ScheduledDeathTick(world, ctx, lowVitality);
        }

        Assert.True(highVitalitySum >= lowVitalitySum);
    }
}
