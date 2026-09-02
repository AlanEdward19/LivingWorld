using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Population.Family;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Population.Family;

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
        double neutralAxisValue = 50,
        int maxCohabitationGroupSize = int.MaxValue) =>
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
            neutralDriftEnabled: false,
            vitalityMortalitySelectionEnabled: true,
            maxCohabitationGroupSize: maxCohabitationGroupSize).Value!;

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

    private static List<Npc> MakeWorkplaceWithEmployees(WorldState world, int count)
    {
        var loc = new CellCoord(4, 4);
        var employees = new List<Npc>();
        for (int i = 0; i < count; i++)
            employees.Add(MakeNpc(world, loc));

        var workplace = new Workplace(
            world.NextWorkplaceIdAndAdvance(), new LocationType(1), loc, maxVacancies: count,
            employees: employees.Select(e => e.Id).ToList(), stock: new Dictionary<ResourceType, long>(),
            treasury: Money.Zero, prices: new Dictionary<ResourceType, long>());
        world.AddWorkplace(workplace);
        foreach (var e in employees)
            e.Hire(workplace.Id);

        return employees;
    }

    [Fact]
    public void Group_at_or_under_the_cap_still_gets_full_all_pairs_relationships()
    {
        // PERF-06 (fase 16): grupo de 4 com teto 10 (bem acima do tamanho do grupo) precisa
        // continuar formando TODO par ordenado, exatamente como antes do teto existir —
        // nenhum household/workplace normal pode perder relações por causa da otimização de
        // escala.
        var rules = RulesWith(cohabitationTrustDelta: 2, maxCohabitationGroupSize: 10);
        var world = BuildWorld(rules);
        var employees = MakeWorkplaceWithEmployees(world, count: 4);

        TickRelationships(world);

        Assert.Equal(4 * 3, world.Relationships.Count); // todo par ordenado (i,j), i != j
        foreach (var a in employees)
            foreach (var b in employees)
            {
                if (a.Id == b.Id) continue;
                Assert.True(world.Relationships.ContainsKey(new RelationshipKey(a.Id, b.Id)),
                    $"par ({a.Id.Value},{b.Id.Value}) deveria existir — grupo de {employees.Count} está sob o teto de 10");
            }
    }

    [Fact]
    public void Group_over_the_cap_forms_bounded_relationships_not_full_pairwise()
    {
        // PERF-06 (fase 16): grupo de 10 com teto 3 — sem teto seriam 10*9=90 relações; com
        // teto, cada membro só forma laço com uma janela de 3 vizinhos (ambas direções),
        // O(k x teto) em vez de O(k²). Achado real: workplace de escala permite milhares de
        // presentes simultâneos (ScenarioRunner.ScaleEconomyCatalog), tornando o par-a-par
        // completo impraticável (baseline-timings.md, T5).
        const int groupSize = 10;
        const int cap = 3;
        var rules = RulesWith(cohabitationTrustDelta: 2, maxCohabitationGroupSize: cap);
        var world = BuildWorld(rules);
        MakeWorkplaceWithEmployees(world, count: groupSize);

        TickRelationships(world);

        int fullPairwiseCount = groupSize * (groupSize - 1);
        int expectedCappedCount = groupSize * cap * 2;
        Assert.True(world.Relationships.Count < fullPairwiseCount,
            $"esperava menos que o par-a-par completo ({fullPairwiseCount}), achou {world.Relationships.Count}");
        Assert.Equal(expectedCappedCount, world.Relationships.Count);
    }

    [Fact]
    public void Same_seed_produces_identical_relationship_state_for_capped_group()
    {
        // Determinismo do teto: mesma seed, mesmo grupo -> mesmo conjunto de relações, mesmo
        // valor por relação (sem RNG na janela, só offset por Id ordenado).
        var rules = RulesWith(cohabitationTrustDelta: 2, maxCohabitationGroupSize: 3);

        var worldA = BuildWorld(rules, seed: 7);
        var employeesA = MakeWorkplaceWithEmployees(worldA, count: 8);
        TickRelationships(worldA);

        var worldB = BuildWorld(rules, seed: 7);
        var employeesB = MakeWorkplaceWithEmployees(worldB, count: 8);
        TickRelationships(worldB);

        Assert.Equal(worldA.Relationships.Count, worldB.Relationships.Count);
        for (int i = 0; i < employeesA.Count; i++)
        {
            var keyA = new RelationshipKey(employeesA[i].Id, employeesA[(i + 1) % employeesA.Count].Id);
            var keyB = new RelationshipKey(employeesB[i].Id, employeesB[(i + 1) % employeesB.Count].Id);
            Assert.Equal(worldA.Relationships[keyA].Get(RelationshipAxis.Trust), worldB.Relationships[keyB].Get(RelationshipAxis.Trust));
        }
    }
}
