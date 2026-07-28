using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests;

/// <summary>Fase 7, T8 (FAM-01, FAM-02, FAM-05): coleção canônica de <c>Relationship</c> +
/// <c>FamilyRules</c> canônico em <c>WorldState</c>.</summary>
public class WorldStateTests
{
    private static FamilyRules CustomFamilyRules(double courtshipThreshold)
    {
        var deltas = new Dictionary<(RelationshipEventType, RelationshipAxis), double>();
        foreach (var type in Enum.GetValues<RelationshipEventType>())
            foreach (var axis in Enum.GetValues<RelationshipAxis>())
                deltas[(type, axis)] = 1.0;

        return FamilyRules.Create(
            relationshipDeltas: deltas,
            decayPerDay: 0.5,
            contactLossThresholdDays: 30,
            neutralAxisValue: 50,
            attractionWeights: Enum.GetValues<AttractionFactor>().ToDictionary(f => f, _ => 1.0),
            courtshipThreshold: courtshipThreshold,
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
            vitalityMortalityWeight: 0.3,
            upbringingWealthWeight: 0.5,
            environmentalWealthChannelEnabled: true,
            neutralDriftEnabled: false).Value!;
    }

    private static WorldState BuildWorld(FamilyRules? familyRules = null) => new(
        ScenarioRunner.DefaultCalendar, seed: 1, ScenarioRunner.DefaultMap(1), ScenarioRunner.DefaultPopulationCatalog,
        ScenarioRunner.DefaultPopulationRules, ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
        ScenarioRunner.DefaultLifeStageRules, familyRules: familyRules);

    // FAM-02: "quem nunca se encontra nunca se conhece" — nenhum par pré-populado.
    [Fact]
    public void Relationships_starts_empty_for_a_new_world()
    {
        var world = BuildWorld();

        Assert.Empty(world.Relationships);
    }

    // AD-052: único ponto de criação — primeira chamada cria, chamadas seguintes devolvem a
    // mesma instância (nunca uma nova a cada tick).
    [Fact]
    public void GetOrCreateRelationship_creates_once_and_reuses_the_same_instance_on_later_calls()
    {
        var world = BuildWorld();
        var key = new RelationshipKey(new NpcId(1), new NpcId(2));

        var first = world.GetOrCreateRelationship(key, now: 100);
        var second = world.GetOrCreateRelationship(key, now: 200);

        Assert.Same(first, second);
        Assert.Single(world.Relationships);
        Assert.Equal(100, first.LastContactTick); // segunda chamada não reseta o contato inicial
    }

    // FAM-05: A->B e B->A são entradas distintas por construção (mesmo par de NPCs, chave
    // trocada) — GetOrCreateRelationship nunca as confunde.
    [Fact]
    public void GetOrCreateRelationship_treats_reversed_pairs_as_distinct_entries()
    {
        var world = BuildWorld();
        var forward = new RelationshipKey(new NpcId(1), new NpcId(2));
        var backward = new RelationshipKey(new NpcId(2), new NpcId(1));

        var a = world.GetOrCreateRelationship(forward, now: 10);
        var b = world.GetOrCreateRelationship(backward, now: 10);

        Assert.NotSame(a, b);
        Assert.Equal(2, world.Relationships.Count);
    }

    // Round-trip de snapshot preserva Relationships e FamilyRules (Done-when de T8).
    [Fact]
    public void Snapshot_round_trip_preserves_relationships_and_family_rules()
    {
        var rules = CustomFamilyRules(courtshipThreshold: 0.73);
        var world = BuildWorld(rules);
        var key = new RelationshipKey(new NpcId(5), new NpcId(6));
        var relationship = world.GetOrCreateRelationship(key, now: 42);
        relationship.ApplyEvent(RelationshipEventType.Cohabitation, rules);

        var rehydrated = WorldSnapshot.Deserialize(WorldSnapshot.Serialize(world));

        Assert.Equal(rules.CourtshipThreshold, rehydrated.FamilyRules.CourtshipThreshold);
        Assert.Equal(rules.VitalityMutationStdDev, rehydrated.FamilyRules.VitalityMutationStdDev);
        Assert.Single(rehydrated.Relationships);
        var rehydratedRelationship = rehydrated.Relationships[key];
        Assert.Equal(relationship.Trust, rehydratedRelationship.Trust);
        Assert.Equal(relationship.LastContactTick, rehydratedRelationship.LastContactTick);
    }
}
