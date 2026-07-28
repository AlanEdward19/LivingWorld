using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Population;

/// <summary>Fase 7, T11 (FAM-01..05): <see cref="RelationshipSystem"/> — convivência em
/// household/workplace cria/atualiza pares ordenados; ausência prolongada decai em direção ao
/// neutro.</summary>
public class RelationshipSystemTests
{
    private static readonly WorldCalendar Calendar = new(24, 30, 12);

    private static readonly Personality SomePersonality =
        Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    private static FamilyRules RulesWith(
        double cohabitationTrustDelta = 3,
        double decayPerDay = 2,
        int contactLossThresholdDays = 1,
        double neutralAxisValue = 50) =>
        FamilyRules.Create(
            relationshipDeltas: CohabitationTrustDelta(cohabitationTrustDelta),
            decayPerDay: decayPerDay,
            contactLossThresholdDays: contactLossThresholdDays,
            neutralAxisValue: neutralAxisValue,
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
            vitalityMortalityWeight: 0.3,
            upbringingWealthWeight: 0.3,
            environmentalWealthChannelEnabled: false,
            neutralDriftEnabled: false).Value!;

    private static Dictionary<(RelationshipEventType, RelationshipAxis), double> CohabitationTrustDelta(double delta)
    {
        var deltas = new Dictionary<(RelationshipEventType, RelationshipAxis), double>();
        foreach (var type in Enum.GetValues<RelationshipEventType>())
        foreach (var axis in Enum.GetValues<RelationshipAxis>())
            deltas[(type, axis)] = 0.0;
        deltas[(RelationshipEventType.Cohabitation, RelationshipAxis.Trust)] = delta;
        return deltas;
    }

    private static WorldState BuildWorld(FamilyRules rules, ulong seed = 1) =>
        new(
            Calendar, seed, ScenarioRunner.DefaultMap(1), ScenarioRunner.DefaultPopulationCatalog,
            ScenarioRunner.DefaultPopulationRules, ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules, familyRules: rules);

    private static Npc MakeNpc(WorldState world, CellCoord location, HouseholdId? household = null) =>
        MakeNpc(world, location, household, npcId: world.NextNpcIdAndAdvance());

    private static Npc MakeNpc(WorldState world, CellCoord location, HouseholdId? household, NpcId npcId)
    {
        var npc = new Npc(
            npcId, $"npc-{npcId.Value}", Sex.Male, WorldDate.Epoch(Calendar), new CultureId(1),
            location, motherId: null, fatherId: null, household: household, health: 100,
            personality: SomePersonality, profession: new ProfessionType(1), currentLocation: location);
        world.AddNpc(npc);
        return npc;
    }

    private static void AddPairToHousehold(WorldState world, Npc a, Npc b)
    {
        var household = new Household(world.NextHouseholdIdAndAdvance(), a.CurrentLocation, a.Id, [a.Id, b.Id]);
        world.AddHousehold(household);
        a.JoinHousehold(household.Id);
        b.JoinHousehold(household.Id);
    }

    private static void TickRelationships(WorldState world) =>
        new RelationshipSystem().Tick(world, new TickContext(world, world.Rng, world.Scheduler));

    [Fact]
    public void Cohabiting_household_members_gain_bidirectional_relationship_entries()
    {
        var rules = RulesWith(cohabitationTrustDelta: 4);
        var world = BuildWorld(rules);
        var a = MakeNpc(world, new CellCoord(1, 1));
        var b = MakeNpc(world, new CellCoord(1, 1));
        AddPairToHousehold(world, a, b);

        TickRelationships(world);

        var ab = world.Relationships[new RelationshipKey(a.Id, b.Id)];
        var ba = world.Relationships[new RelationshipKey(b.Id, a.Id)];
        Assert.Equal(4, ab.Get(RelationshipAxis.Trust));
        Assert.Equal(4, ba.Get(RelationshipAxis.Trust));
        Assert.NotSame(ab, ba);
    }

    [Fact]
    public void Npcs_without_shared_household_or_workplace_get_no_relationship_entries()
    {
        var rules = RulesWith();
        var world = BuildWorld(rules);
        MakeNpc(world, new CellCoord(1, 1));
        MakeNpc(world, new CellCoord(9, 9));

        TickRelationships(world);

        Assert.Empty(world.Relationships);
    }

    [Fact]
    public void Workplace_employees_at_the_same_location_gain_relationships()
    {
        var rules = RulesWith(cohabitationTrustDelta: 2);
        var world = BuildWorld(rules);
        var loc = new CellCoord(3, 3);
        var e1 = MakeNpc(world, loc);
        var e2 = MakeNpc(world, loc);
        var workplace = new Workplace(
            world.NextWorkplaceIdAndAdvance(), new LocationType(1), loc, maxVacancies: 2,
            employees: [e1.Id, e2.Id], stock: new Dictionary<ResourceType, long>(), treasury: Money.Zero,
            prices: new Dictionary<ResourceType, long>());
        world.AddWorkplace(workplace);
        e1.Hire(workplace.Id);
        e2.Hire(workplace.Id);

        TickRelationships(world);

        Assert.True(world.Relationships.ContainsKey(new RelationshipKey(e1.Id, e2.Id)));
    }

    [Fact]
    public void Employees_not_at_workplace_location_do_not_cohabit_for_relationships()
    {
        var rules = RulesWith();
        var world = BuildWorld(rules);
        var workplaceLoc = new CellCoord(1, 1);
        var e1 = MakeNpc(world, workplaceLoc);
        var e2 = MakeNpc(world, new CellCoord(5, 5));
        var workplace = new Workplace(
            world.NextWorkplaceIdAndAdvance(), new LocationType(1), workplaceLoc, maxVacancies: 2,
            employees: [e1.Id, e2.Id], stock: new Dictionary<ResourceType, long>(), treasury: Money.Zero,
            prices: new Dictionary<ResourceType, long>());
        world.AddWorkplace(workplace);
        e1.Hire(workplace.Id);
        e2.Hire(workplace.Id);

        TickRelationships(world);

        Assert.Empty(world.Relationships);
    }

    [Fact]
    public void Relationship_without_recent_contact_decays_toward_neutral_without_overshooting()
    {
        var rules = RulesWith(cohabitationTrustDelta: 20, decayPerDay: 3, contactLossThresholdDays: 1, neutralAxisValue: 50);
        var world = BuildWorld(rules);
        var a = MakeNpc(world, new CellCoord(0, 0));
        var b = MakeNpc(world, new CellCoord(0, 0));
        AddPairToHousehold(world, a, b);
        TickRelationships(world);
        double trustAfterCohabitation = world.Relationships[new RelationshipKey(a.Id, b.Id)].Get(RelationshipAxis.Trust);

        b.LeaveHousehold(world.CurrentDate);
        world.FindHousehold(a.Household!.Value)!.RemoveMember(b.Id);
        var soloHousehold = new Household(world.NextHouseholdIdAndAdvance(), b.CurrentLocation, b.Id, [b.Id]);
        world.AddHousehold(soloHousehold);
        b.JoinHousehold(soloHousehold.Id);

        world.CurrentDate = world.CurrentDate.AddDays(2);
        TickRelationships(world);

        double trustAfterDecay = world.Relationships[new RelationshipKey(a.Id, b.Id)].Get(RelationshipAxis.Trust);
        Assert.True(trustAfterDecay > trustAfterCohabitation);
        Assert.True(trustAfterDecay <= 50);
    }

    [Fact]
    public void Same_seed_produces_identical_relationship_state_after_daily_tick()
    {
        var rules = RulesWith(cohabitationTrustDelta: 2);

        static string SnapshotTrust(WorldState world, NpcId from, NpcId to) =>
            world.Relationships[new RelationshipKey(from, to)].Get(RelationshipAxis.Trust).ToString("F6");

        var worldA = BuildWorld(rules, seed: 99);
        var a1 = MakeNpc(worldA, new CellCoord(1, 1));
        var b1 = MakeNpc(worldA, new CellCoord(1, 1));
        AddPairToHousehold(worldA, a1, b1);
        TickRelationships(worldA);

        var worldB = BuildWorld(rules, seed: 99);
        var a2 = MakeNpc(worldB, new CellCoord(1, 1));
        var b2 = MakeNpc(worldB, new CellCoord(1, 1));
        AddPairToHousehold(worldB, a2, b2);
        TickRelationships(worldB);

        Assert.Equal(SnapshotTrust(worldA, a1.Id, b1.Id), SnapshotTrust(worldB, a2.Id, b2.Id));
        Assert.Equal(worldA.Relationships.Count, worldB.Relationships.Count);
    }

    [Fact]
    public void Decay_from_above_neutral_never_drops_below_neutral_in_one_step()
    {
        var rules = RulesWith(cohabitationTrustDelta: 60, decayPerDay: 5, contactLossThresholdDays: 1, neutralAxisValue: 50);
        var world = BuildWorld(rules);
        var a = MakeNpc(world, new CellCoord(0, 0));
        var b = MakeNpc(world, new CellCoord(0, 0));
        AddPairToHousehold(world, a, b);
        TickRelationships(world);
        var key = new RelationshipKey(a.Id, b.Id);
        Assert.Equal(60, world.Relationships[key].Get(RelationshipAxis.Trust));

        b.LeaveHousehold(world.CurrentDate);
        world.FindHousehold(a.Household!.Value)!.RemoveMember(b.Id);
        var solo = new Household(world.NextHouseholdIdAndAdvance(), b.CurrentLocation, b.Id, [b.Id]);
        world.AddHousehold(solo);
        b.JoinHousehold(solo.Id);
        world.CurrentDate = world.CurrentDate.AddDays(2);

        TickRelationships(world);

        Assert.Equal(55, world.Relationships[key].Get(RelationshipAxis.Trust));
    }
}
