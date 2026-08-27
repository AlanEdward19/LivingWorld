using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Extraordinary;

public sealed class NpcInstantiationMechanicTests
{
    private static readonly WorldCalendar Calendar = new(24, 30, 12);

    private static readonly Personality DistinctPersonality =
        Personality.Create(90, 10, 80, 20, 70, 30, 60, 40, 55, 45).Value!;

    private static readonly Personality SomePersonality =
        Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    [Fact]
    public void Clone_creates_a_second_npc_with_copied_personality_and_a_distinct_id_on_the_same_tick()
    {
        var (world, carrier, _, _) = WorldWithPower(["npc.clone:1"]);
        carrier.RewritePersonality(DistinctPersonality);
        carrier.GainSkill(new SkillType(3), 40, 100);
        var sink = new RecordingSink();
        var ctx = new TickContext(world, world.Rng, world.Scheduler, sink);
        int before = world.Npcs.Count;
        long tick = ctx.CurrentTick;

        var result = ExtraordinaryInvocationEngine.Invoke(
            world, ctx, new ExtraordinaryInvocation(70, carrier.Id, "test-power", carrier.Id));

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(before + 1, world.Npcs.Count);
        var clone = Assert.Single(world.Npcs, npc => npc.Id != carrier.Id && npc.Id != new NpcId(2));
        Assert.NotEqual(carrier.Id, clone.Id);
        Assert.Equal(DistinctPersonality, clone.Personality);
        Assert.NotSame(carrier.Personality, clone.Personality);
        Assert.NotSame(carrier.Skills, clone.Skills);
        Assert.Equal(40, clone.Skills.Get(new SkillType(3)));
        Assert.Null(clone.Household);
        Assert.Null(clone.MotherId);
        Assert.Null(clone.FatherId);
        Assert.Equal(tick, ctx.CurrentTick);
        Assert.Contains(sink.Events, evt => evt.Kind == WorldEventKind.NpcInstantiated);
        clone.RewritePersonality(SomePersonality);
        Assert.Equal(DistinctPersonality, carrier.Personality);
    }

    [Fact]
    public void Split_on_death_inherits_proportional_skill_fraction_per_new_npc()
    {
        var (world, carrier, _, _) = WorldWithPower(["npc.split-on-death:3"], mode: "Passive");
        var skill = new SkillType(3);
        carrier.GainSkill(skill, 90, 100);
        var sink = new RecordingSink();
        var ctx = new TickContext(world, world.Rng, world.Scheduler, sink);

        NpcDeath.Apply(world, ctx, carrier, WorldEventKind.Death);

        var spawned = world.Npcs.Where(npc => npc.IsAlive && npc.Id != new NpcId(2)).ToList();
        Assert.Equal(3, spawned.Count);
        Assert.All(spawned, child => Assert.Equal(30, child.Skills.Get(skill)));
    }

    [Fact]
    public void InheritSkills_matches_rate_gene_blend_weight_plus_mutation_clamp_shape()
    {
        var source = SkillSet.Empty.WithGain(new SkillType(1), 80, 100);
        var rng = new WorldRng(42);
        var inherited = NpcInstantiationHeredity.InheritSkills(source, weight: 0.5, rng);

        // Mesma fórmula: blended = 80 * 0.5; mutation spread de nível = 0 → 40 exato.
        Assert.Equal(40, inherited.Get(new SkillType(1)));
        Assert.Equal(0, inherited.Get(new SkillType(99)));
    }

    [Fact]
    public void Clone_with_skill_N_and_F_bonds_produces_clone_with_skill_N_and_F_bonds()
    {
        // Independent Test (spec P2 Instanciação / REALISM-26+29).
        var (world, carrier, friend, _) = WorldWithPower(["npc.clone:1"]);
        var skill = new SkillType(3);
        const int skillN = 55;
        carrier.GainSkill(skill, skillN, 100);

        SeedBond(world, carrier.Id, friend.Id, trust: 70, affection: 60);
        int bondCountF = UniquePartnersOf(world, carrier.Id);
        Assert.Equal(1, bondCountF);

        var sink = new RecordingSink();
        var ctx = new TickContext(world, world.Rng, world.Scheduler, sink);
        var result = ExtraordinaryInvocationEngine.Invoke(
            world, ctx, new ExtraordinaryInvocation(70, carrier.Id, "test-power", carrier.Id));

        Assert.True(result.IsSuccess, result.Error);
        var clone = Assert.Single(world.Npcs, npc => npc.Id != carrier.Id && npc.Id != friend.Id);
        Assert.Equal(skillN, clone.Skills.Get(skill));
        Assert.Equal(bondCountF, UniquePartnersOf(world, clone.Id));
        Assert.True(world.Relationships.ContainsKey(new RelationshipKey(clone.Id, friend.Id)));
        Assert.Equal(70, world.Relationships[new RelationshipKey(clone.Id, friend.Id)].Trust);
        Assert.Equal(60, world.Relationships[new RelationshipKey(clone.Id, friend.Id)].Affection);
    }

    [Fact]
    public void Split_preserves_original_bonds_on_each_new_npc()
    {
        var (world, carrier, friend, _) = WorldWithPower(["npc.split-on-death:2"], mode: "Passive");
        SeedBond(world, carrier.Id, friend.Id, trust: 80, affection: 50);
        var sink = new RecordingSink();
        var ctx = new TickContext(world, world.Rng, world.Scheduler, sink);

        NpcDeath.Apply(world, ctx, carrier, WorldEventKind.Death);

        var spawned = world.Npcs.Where(npc => npc.IsAlive && npc.Id != friend.Id).OrderBy(n => n.Id.Value).ToList();
        Assert.Equal(2, spawned.Count);
        Assert.All(spawned, child =>
        {
            Assert.Equal(1, UniquePartnersOf(world, child.Id));
            Assert.Equal(80, world.Relationships[new RelationshipKey(child.Id, friend.Id)].Trust);
        });
    }

    [Fact]
    public void Reincarnate_transfers_no_social_bonds_to_the_newborn()
    {
        var treated = CoupleWorldWithDonor(["npc.reincarnate:50"]);
        SeedBond(treated.World, treated.Donor.Id, treated.Mother.Id, trust: 90, affection: 40);
        int donorBonds = UniquePartnersOf(treated.World, treated.Donor.Id);
        Assert.True(donorBonds >= 1);

        NpcDeath.Apply(treated.World, treated.Ctx, treated.Donor, WorldEventKind.Death);
        var baby = TriggerBirth(treated.World, treated.Ctx, treated.Mother.Id, treated.Father.Id, treated.Home.Id);

        Assert.Equal(0, UniquePartnersOf(treated.World, baby.Id));
        Assert.False(treated.World.Relationships.ContainsKey(new RelationshipKey(baby.Id, treated.Mother.Id)));
    }

    [Fact]
    public void Split_on_death_instantiates_exactly_n_npcs_at_death_never_before_or_after()
    {
        var (world, carrier, _, _) = WorldWithPower(["npc.split-on-death:3"], mode: "Passive");
        var sink = new RecordingSink();
        var ctx = new TickContext(world, world.Rng, world.Scheduler, sink);
        int livingBefore = world.Npcs.Count(npc => npc.IsAlive);

        ExtraordinaryInvocationEngine.Invoke(
            world, ctx, new ExtraordinaryInvocation(
                71, carrier.Id, "test-power", carrier.Id, Origin: ExtraordinaryInvocationOrigin.Authored));
        Assert.Equal(livingBefore, world.Npcs.Count(npc => npc.IsAlive));

        NpcDeath.Apply(world, ctx, carrier, WorldEventKind.Death);

        Assert.False(carrier.IsAlive);
        Assert.Equal(livingBefore - 1 + 3, world.Npcs.Count(npc => npc.IsAlive));
        var spawned = world.Npcs.Where(npc => npc.IsAlive && npc.Id != new NpcId(2)).ToList();
        Assert.Equal(3, spawned.Count);
        Assert.All(spawned, clone =>
        {
            Assert.NotEqual(carrier.Id, clone.Id);
            Assert.Null(clone.Household);
        });
        Assert.Equal(3, sink.Events.Count(evt => evt.Kind == WorldEventKind.NpcInstantiated));
        Assert.Contains(sink.Events, evt => evt.Kind == WorldEventKind.Death);
        int instantiatedIndex = sink.Events.FindIndex(evt => evt.Kind == WorldEventKind.NpcInstantiated);
        int deathIndex = sink.Events.FindIndex(evt => evt.Kind == WorldEventKind.Death);
        Assert.True(instantiatedIndex >= 0 && instantiatedIndex < deathIndex);

        int afterDeath = world.Npcs.Count;
        world.CurrentDate = world.CurrentDate.AddHours(24);
        Assert.Equal(afterDeath, world.Npcs.Count);
    }

    [Fact]
    public void Reincarnate_transfers_the_declared_fraction_to_the_next_natural_birth()
    {
        var treated = CoupleWorldWithDonor(["npc.reincarnate:50"]);
        var control = CoupleWorldWithDonor(effects: null);
        var skill = new SkillType(3);
        treated.Donor.GainSkill(skill, 80, 100);
        treated.Donor.RewritePersonality(DistinctPersonality);
        control.Donor.GainSkill(skill, 80, 100);
        control.Donor.RewritePersonality(DistinctPersonality);

        NpcDeath.Apply(treated.World, treated.Ctx, treated.Donor, WorldEventKind.Death);
        NpcDeath.Apply(control.World, control.Ctx, control.Donor, WorldEventKind.Death);
        Assert.Equal(2, treated.World.Npcs.Count(npc => npc.IsAlive));

        var treatedBaby = TriggerBirth(treated.World, treated.Ctx, treated.Mother.Id, treated.Father.Id, treated.Home.Id);
        var controlBaby = TriggerBirth(control.World, control.Ctx, control.Mother.Id, control.Father.Id, control.Home.Id);

        Assert.Equal(40, treatedBaby.Skills.Get(skill));
        Assert.Equal(0, controlBaby.Skills.Get(skill));
        Assert.Equal(
            NpcInstantiationMechanic.BlendPersonality(DistinctPersonality, controlBaby.Personality, 50),
            treatedBaby.Personality);
        Assert.Contains(
            treated.Sink.Events,
            evt => evt.Kind == WorldEventKind.NpcInstantiated && evt.Payload.EndsWith("|reincarnate", StringComparison.Ordinal));
        Assert.Contains(treated.Sink.Events, evt => evt.Kind == WorldEventKind.Birth);
    }

    private static (WorldState World, Npc Carrier, Npc Target, Household Home) WorldWithPower(
        IReadOnlyList<string> effects, string mode = "Active")
    {
        var descriptor = new PowerDescriptor(
            "test-power", "test-source", effects, mode, [], "Guaranteed", [], [], [], []);
        var state = new ExtraordinaryCarrierState(
            new NpcId(1), [descriptor.Id], true, "active",
            new ExtraordinaryAppearanceState(2.5, "ash", "trail"), null, 1);
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, 42, ScenarioRunner.DefaultMap(42),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules,
            extraordinary: new ExtraordinaryScenarioData(true, [descriptor]), extraordinaryCarriers: [state]);
        var carrier = Npc(new NpcId(1), "carrier", 100, DistinctPersonality);
        var target = Npc(new NpcId(2), "target", 50, SomePersonality);
        var home = new Household(new HouseholdId(1), new CellCoord(0, 0), carrier.Id, [carrier.Id]);
        world.AddNpc(carrier);
        world.AddNpc(target);
        world.AddHousehold(home);
        return (world, carrier, target, home);
    }

    private static void SeedBond(WorldState world, NpcId a, NpcId b, double trust, double affection)
    {
        long now = world.CurrentDate.TotalHours;
        var template = Relationship.FromAxes(trust, affection, respect: 0, debt: 0, now);
        var ab = world.GetOrCreateRelationship(new RelationshipKey(a, b), now);
        ab.CopyAxesFrom(template);
        ab.MarkContact(now);
        var ba = world.GetOrCreateRelationship(new RelationshipKey(b, a), now);
        ba.CopyAxesFrom(template);
        ba.MarkContact(now);
    }

    private static int UniquePartnersOf(WorldState world, NpcId id) =>
        world.Relationships
            .Where(pair => pair.Key.From == id || pair.Key.To == id)
            .Select(pair => pair.Key.From == id ? pair.Key.To : pair.Key.From)
            .Distinct()
            .Count();

    private static Npc Npc(NpcId id, string name, int health, Personality personality) => new(
        id, name, Sex.Male, WorldDate.Epoch(ScenarioRunner.DefaultCalendar),
        ScenarioRunner.DefaultCulture, new CellCoord(0, 0), motherId: null, fatherId: null,
        household: id == new NpcId(1) ? new HouseholdId(1) : null, health: health,
        personality: personality, profession: ProfessionType.None, currentLocation: new CellCoord(0, 0));

    private static CoupleScene CoupleWorldWithDonor(IReadOnlyList<string>? effects)
    {
        var location = new CellCoord(1, 1);
        var mother = new Npc(
            new NpcId(1), "mother", Sex.Female, WorldDate.Epoch(Calendar).AddYears(-20), new CultureId(1), location,
            motherId: null, fatherId: null, household: null, health: 100,
            personality: SomePersonality, profession: ProfessionType.None, currentLocation: location,
            vitality: 60, upbringing: 40);
        var father = new Npc(
            new NpcId(2), "father", Sex.Male, WorldDate.Epoch(Calendar).AddYears(-22), new CultureId(1), location,
            motherId: null, fatherId: null, household: null, health: 100,
            personality: SomePersonality, profession: ProfessionType.None, currentLocation: location,
            vitality: 70, upbringing: 45);
        var donor = new Npc(
            new NpcId(3), "donor", Sex.Male, WorldDate.Epoch(Calendar).AddYears(-30), new CultureId(1), location,
            motherId: null, fatherId: null, household: null, health: 100,
            personality: DistinctPersonality, profession: ProfessionType.None, currentLocation: location);

        mother.Marry(father.Id);
        father.Marry(mother.Id);
        var household = new Household(new HouseholdId(1), location, mother.Id, [mother.Id, father.Id]);
        mother.JoinHousehold(household.Id);
        father.JoinHousehold(household.Id);
        household.Deposit(new ResourceType(1), 100);

        PowerDescriptor[] descriptors = [];
        ExtraordinaryCarrierState[] carriers = [];
        if (effects is not null)
        {
            var descriptor = new PowerDescriptor(
                "reincarnate", "test", effects, "Passive", [], "Guaranteed", [], [], [], []);
            descriptors = [descriptor];
            carriers =
            [
                new ExtraordinaryCarrierState(
                    donor.Id, [descriptor.Id], true, "manifested",
                    new ExtraordinaryAppearanceState(1, "", ""), null, 1),
            ];
        }

        var table = LifeTable.Create(90, [new LifeTableBracket(0, 89, 0.01)]).Value!;
        var conception = PopulationRules.Create(
            table, fertilityMinAge: 16, fertilityMaxAge: 45, annualConceptionChance: 1.0, gestationDays: 10).Value!;
        var world = new WorldState(
            Calendar, 42, ScenarioRunner.DefaultMap(42),
            ScenarioRunner.DefaultPopulationCatalog, conception,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules,
            familyRules: PermissiveFamilyRules(),
            extraordinary: new ExtraordinaryScenarioData(true, descriptors),
            extraordinaryCarriers: carriers);

        world.AddNpc(mother);
        world.AddNpc(father);
        world.AddNpc(donor);
        world.AddHousehold(household);
        world.AdvanceNpcIdTo(4);
        world.AdvanceHouseholdIdTo(2);
        world.CurrentDate = WorldDate.Epoch(Calendar).AddYears(20);
        var sink = new RecordingSink();
        return new CoupleScene(
            world, new TickContext(world, world.Rng, world.Scheduler, sink), mother, father, donor, household, sink);
    }

    private static Npc TriggerBirth(
        WorldState world, TickContext ctx, NpcId motherId, NpcId fatherId, HouseholdId householdId)
    {
        var evt = ctx.ScheduleEvent(
            world.CurrentDate.TotalHours, NatalitySystem.SystemName,
            $"{motherId.Value}|{fatherId.Value}|{householdId.Value}|100");
        new NatalitySystem().HandleEvent(world, ctx, evt);
        return world.Npcs[^1];
    }

    private static FamilyRules PermissiveFamilyRules() =>
        FamilyRules.Create(
            relationshipDeltas: ZeroDeltas(),
            decayPerDay: 0,
            contactLossThresholdDays: 30,
            neutralAxisValue: 50,
            attractionWeights: new Dictionary<AttractionFactor, double>(),
            courtshipThreshold: 0.5,
            courtshipDurationDays: 10,
            marriageInitialStock: new Dictionary<int, long>(),
            conceptionHealthFloor: 0,
            conceptionRelationshipFloor: 0,
            conceptionResourceFloor: new Dictionary<int, long>(),
            maternalDeathRisk: 0,
            infantDeathRisk: 0,
            vitalityMotherWeight: 0.5,
            vitalityFatherWeight: 0.5,
            vitalityMutationStdDev: 0,
            vitalityMortalityWeight: 0,
            upbringingWealthWeight: 0.2,
            environmentalWealthChannelEnabled: false,
            neutralDriftEnabled: false,
            vitalityMortalitySelectionEnabled: true).Value!;

    private static Dictionary<(RelationshipEventType, RelationshipAxis), double> ZeroDeltas()
    {
        var deltas = new Dictionary<(RelationshipEventType, RelationshipAxis), double>();
        foreach (var type in Enum.GetValues<RelationshipEventType>())
            foreach (var axis in Enum.GetValues<RelationshipAxis>())
                deltas[(type, axis)] = 0;
        return deltas;
    }

    private sealed record CoupleScene(
        WorldState World, TickContext Ctx, Npc Mother, Npc Father, Npc Donor, Household Home, RecordingSink Sink);

    private sealed class RecordingSink : IWorldEventSink
    {
        public List<WorldEvent> Events { get; } = [];
        public void Record(WorldEvent evt) => Events.Add(evt);
    }
}
